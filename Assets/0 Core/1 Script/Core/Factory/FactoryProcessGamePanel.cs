using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 工厂加工音游面板：开局后按 <see cref="FactoryGameConfig.AssemblyLineNoteSequences"/> 的节奏出货（拍长 NoteInterval，序列循环取用），
/// 产品沿传送带右移，经过包装区域时按对应键处理——A/←/鼠标左键=左品，D/→/鼠标右键=右品，W/↑/鼠标中键=丢残次品（S/↓ 预留未用）。
/// 判定：完美区内 PERFECT、包装区内 GOOD（打包成功，盒装滚出）；按错键闪红+失败包装记失败；
/// 漏掉正常品 MISS 词条直接划过记失败；漏掉残次品卡住机器 3 秒，期间产品锁定无法打包、按键无效。
/// 出货总数 = 基本生产量 + 设备加成；残次品率% = 100 - (基础良品率 + 设备加成)，按此概率把谱面左/右品替换为残次品（谱面「上」本身即残次品）。
/// 产品视图与评价飘字均对象池复用；键鼠输入统一经 <see cref="PlayerInputManager"/> 事件接入（开/关面板订阅/退订）；
/// 鼠标点在暂停/退出按钮上时用 <see cref="RectTransformUtility"/> 判定并跳过游戏判定，避免误触。
/// 本局结束广播 <see cref="OnRoundEnd"/>，结算面板重做后由此接入。
/// </summary>
public class FactoryProcessGamePanel : UIBase
{
    [LabelText("配置")][SerializeField] FactoryGameConfig config;

    [Title("战况")]
    [SerializeField] TextMeshProUGUI curProcessCountText, defectRateText, failCountText, completeCountText;// 本次加工数量 残次品率 失败产品数 完成生产数
    [SerializeField] TextMeshProUGUI comboCountText;    // 连击数
    [LabelText("连击组(0 连击隐藏)")][SerializeField] GameObject comboGroup;
    // 暂时停用倒计时功能
    // [LabelText("倒计时")][SerializeField] TextMeshProUGUI timerText;

    [Title("传送带（包装区域需与产品容器同父级同坐标系，中心锚点）")]
    [LabelText("产品容器(铺满传送带宽)")][SerializeField] RectTransform itemContainer;
    [LabelText("产品模板(隐藏)")][SerializeField] FactoryGamePackItem itemTemplate;
    [LabelText("包装区域")][SerializeField] RectTransform packArea;

    [Title("评价 / 按钮")]
    [LabelText("评价飘字模板(隐藏)")][SerializeField] FactoryProcessEvaluateTip tipTemplate;
    [LabelText("暂停")][SerializeField] Button pauseButton;
    [LabelText("退出")][SerializeField] Button quitButton;
    [LabelText("退出确认面板")][SerializeField] FactoryProcessEndConfirmPanel quitConfirmPanel;

    /// <summary>本局结束：完成生产数、失败产品数（新结算面板接入后由此驱动）。</summary>
    public event Action<int, int> OnRoundEnd;

    const float LockDuration = 3f;   // 漏掉残次品的卡机时长（策划案固定 3 秒）

    class BeltItem
    {
        public FactoryGamePackItem View;
        public FactoryNoteType Type;
        public float Pos;        // 归一化：0=入口 1=出口
        public bool Resolved;    // 已打包 / 已丢弃 / 已判 MISS
    }

    readonly List<BeltItem> items = new ();
    readonly Stack<FactoryGamePackItem> itemPool = new ();
    readonly Stack<FactoryProcessEvaluateTip> tipPool = new ();

    bool playing, paused;
    float zoneCenter, zoneHalf;              // 包装区域（归一化，取自 UI 实际位置 / 宽度）
    IReadOnlyList<FactoryNoteType> chart;    // 本局节奏序列（循环取用）
    int chartIdx;
    float beatTimer;
    int totalToSpawn, spawnedCount, handledCount;
    int defectPercent;
    int completeCount, failCount, combo;
    // float roundRemain;   // 暂时停用倒计时（仅展示；本局以「全部出货且离场」结束）
    float lockRemain;    // 机器卡住剩余时间

    readonly List<FactoryMoldItemInfo> craftBatch = new ();   // 本局加工的生产资料批次（主面板带入），供结算产出用
    Action onClosed;   // 本面板关闭返回时回调（主面板刷新，反映本局已消耗的素材）

    public override void Init()
    {
        itemTemplate.gameObject.SetActive(false);
        tipTemplate.gameObject.SetActive(false);
        pauseButton.onClick.AddListener(() => paused = !paused);
        quitButton.onClick.AddListener(OnQuitButton);
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        StartRound();
        UISystem.Instance.CloseUI(AssetKeys.MainUIPath);
    }

    public override void Close()
    {
        // 进行中不允许关闭（退出走确认弹窗，确认后先停局再 CloseUI）
        if(playing)
            return;

        UISystem.Instance.CloseUI(AssetKeys.MainUIPath);
        base.Close();
        Unsubscribe();
        // 返回时回调一次主面板刷新（反映本局已消耗的素材）；用完即清，避免复用残留旧回调
        Action cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    void Update()
    {
        if(!playing || paused)
            return;

        // roundRemain = Mathf.Max(0f, roundRemain - Time.deltaTime);
        // timerText.text = Mathf.CeilToInt(roundRemain) + "s";

        StepLock(Time.deltaTime);
        StepSpawn(Time.deltaTime);
        StepBelt(Time.deltaTime);

        if(spawnedCount >= totalToSpawn && items.Count == 0)
            EndRound();
    }

    #region 开局 / 结束
    public void StartRound()
    {
        ClearBelt();
        totalToSpawn = config.BaseProductionVolume + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.ProductionVolume);
        defectPercent = 100 - Mathf.Clamp(config.BaseYieldRate + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.Yield), 0, 100);
        chart = config.AssemblyLineNoteSequences[UnityEngine.Random.Range(0, config.AssemblyLineNoteSequences.Count)];
        chartIdx = 0;
        beatTimer = 0f;
        spawnedCount = handledCount = completeCount = failCount = combo = 0;
        lockRemain = 0f;
        paused = false;

        int beats = SpawnBeatCount();
        if(beats == 0)   // 谱面全空拍属配置错误，直接不开局，避免 Update 里永远出不完货
        {
            Debug.LogError("[FactoryProcessGamePanel] 音符序列不含有效音符，无法开局。", config);
            return;
        }

        LayoutZone();
        // roundRemain = beats * config.NoteInterval + 1f / config.BeltSpeed;
        defectRateText.text = defectPercent.ToString();   // 「%」为预制里独立的单位标签
        RefreshStats();
        playing = true;
    }

    void EndRound()
    {
        playing = false;
        OnRoundEnd?.Invoke(completeCount, failCount);
    }

    // 退出：先冻结本局并弹二次确认；确认则放弃本局（什么也不获得）收起面板，取消继续
    void OnQuitButton()
    {
        paused = true;
        quitConfirmPanel.Show(OnQuitConfirmed, () => paused = false);
    }

    void OnQuitConfirmed()
    {
        playing = false;
        ClearBelt();
        UISystem.Instance.CloseUI(uiname);
    }

    // 出完全部货需要的拍数（序列循环取用），用于倒计时展示；序列全空拍返回 0
    int SpawnBeatCount()
    {
        int perLoop = 0;
        foreach(FactoryNoteType n in chart)
            if(n != FactoryNoteType.None)
                perLoop++;
        if(perLoop == 0)
            return 0;

        int steps = 0;
        for(int spawned = 0; spawned < totalToSpawn; steps++)
            if(chart[steps % chart.Count] != FactoryNoteType.None)
                spawned++;
        return steps;
    }

    // 包装区域由策划在预制里摆放，运行时反推其归一化中心 / 半宽，保证「所见即判定」
    void LayoutZone()
    {
        float w = itemContainer.rect.width;
        zoneCenter = packArea.anchoredPosition.x / w + 0.5f;
        zoneHalf = packArea.sizeDelta.x * 0.5f / w;
    }
    #endregion

    #region 传送带推进
    void StepSpawn(float dt)
    {
        if(spawnedCount >= totalToSpawn)
            return;

        beatTimer -= dt;
        if(beatTimer > 0f)
            return;
        beatTimer += config.NoteInterval;

        FactoryNoteType note = chart[chartIdx];
        chartIdx = (chartIdx + 1) % chart.Count;
        if(note == FactoryNoteType.None)
            return;

        // 左/右品按残次品率抽检替换为残次品；谱面「上」本身即残次品
        if(note != FactoryNoteType.Up && UnityEngine.Random.value * 100f < defectPercent)
            note = FactoryNoteType.Up;

        spawnedCount++;
        FactoryGamePackItem view = itemPool.Count > 0 ? itemPool.Pop() : Instantiate(itemTemplate, itemContainer);
        view.gameObject.SetActive(true);
        view.Set(note);
        if(lockRemain > 0f && note != FactoryNoteType.Up)
            view.SetLocked(note, true);
        items.Add(new BeltItem { View = view, Type = note });
    }

    void StepBelt(float dt)
    {
        float move = config.BeltSpeed * dt;
        float w = itemContainer.rect.width;

        for(int i = items.Count - 1; i >= 0; i--)
        {
            BeltItem it = items[i];
            it.Pos += move;
            it.View.Rt.anchoredPosition = new Vector2((it.Pos - 0.5f) * w, 0f);

            // 越过包装区域仍未处理 → 漏件判定
            if(!it.Resolved && it.Pos > zoneCenter + zoneHalf)
            {
                it.Resolved = true;
                handledCount++;
                if(it.Type == FactoryNoteType.Up)
                    JamMachine();
                else
                {
                    ShowTip(FactoryEvaluateType.Miss);   // MISS 词条，商品直接划过滚出
                    Fail();
                }
                RefreshStats();
            }

            if(it.Pos < 1f)
                continue;
            RecycleItem(it.View);
            items.RemoveAt(i);
        }
    }

    // 漏掉残次品：卡住机器一段时间，场上正常品全部锁定无法打包（锁定期漏件照记失败）
    void JamMachine()
    {
        lockRemain = LockDuration;
        foreach(BeltItem it in items)
            if(!it.Resolved && it.Type != FactoryNoteType.Up)
                it.View.SetLocked(it.Type, true);
    }

    void StepLock(float dt)
    {
        if(lockRemain <= 0f)
            return;
        lockRemain -= dt;
        if(lockRemain > 0f)
            return;

        // 卡机结束：场上未处理的正常品恢复可打包外观
        foreach(BeltItem it in items)
            if(!it.Resolved && it.Type != FactoryNoteType.Up)
                it.View.SetLocked(it.Type, false);
    }
    #endregion

    #region 输入订阅与判定
    bool subscribed;
    Camera uiCamera;
    bool uiCameraResolved;

    // 键鼠输入统一走 PlayerInputManager：开面板订阅、关面板退订（Press 内部再按 playing/paused/卡机 拦截）
    void Subscribe()
    {
        if(subscribed)
            return;
        subscribed = true;
        PlayerInputManager mgr = PlayerInputManager.Instance;
        mgr.OnLeft += OnKeyLeft;
        mgr.OnRight += OnKeyRight;
        mgr.OnUp += OnKeyUp;
        mgr.OnClick += OnMouseLeft;
        mgr.OnRightClick += OnMouseRight;
        mgr.OnMiddleClick += OnMouseMiddle;
        // S / ↓（mgr.OnDown）暂无对应玩法，预留不订阅
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        PlayerInputManager mgr = PlayerInputManager.Instance;
        mgr.OnLeft -= OnKeyLeft;
        mgr.OnRight -= OnKeyRight;
        mgr.OnUp -= OnKeyUp;
        mgr.OnClick -= OnMouseLeft;
        mgr.OnRightClick -= OnMouseRight;
        mgr.OnMiddleClick -= OnMouseMiddle;
    }

    // 键盘 A/← D/→ W/↑（对应左品 / 右品 / 丢残次品）
    void OnKeyLeft() => Press(FactoryNoteType.Left);
    void OnKeyRight() => Press(FactoryNoteType.Right);
    void OnKeyUp() => Press(FactoryNoteType.Up);

    // 鼠标左 / 右 / 中键：点在暂停/退出按钮上则不计入游戏判定（原靠 Raycast 屏蔽，现走全局输入后手动判定）
    void OnMouseLeft() => PressByMouse(FactoryNoteType.Left);
    void OnMouseRight() => PressByMouse(FactoryNoteType.Right);
    void OnMouseMiddle() => PressByMouse(FactoryNoteType.Up);

    void PressByMouse(FactoryNoteType key)
    {
        if(PointerOverControlButton())
            return;
        Press(key);
    }

    // 鼠标是否压在暂停/退出按钮上（含 Overlay / Camera 两种 Canvas 渲染模式）
    bool PointerOverControlButton()
    {
        if(Mouse.current == null)
            return false;
        Vector2 p = Mouse.current.position.ReadValue();
        Camera cam = UICamera;
        return Blocks(pauseButton, p, cam) || Blocks(quitButton, p, cam);
    }

    static bool Blocks(Button btn, Vector2 screenPos, Camera cam)
        => btn != null && btn.gameObject.activeInHierarchy && btn.interactable
           && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)btn.transform, screenPos, cam);

    Camera UICamera
    {
        get
        {
            if(!uiCameraResolved)
            {
                uiCameraResolved = true;
                Canvas canvas = GetComponentInParent<Canvas>();
                if(canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    uiCamera = canvas.worldCamera;
            }
            return uiCamera;
        }
    }

    void Press(FactoryNoteType key)
    {
        if(!playing || paused || lockRemain > 0f)   // 卡机期间机器不响应
            return;

        // 取包装区域内离中心最近的未处理产品；区内无产品则空按无惩罚
        BeltItem hit = null;
        float best = float.MaxValue;
        foreach(BeltItem it in items)
        {
            if(it.Resolved)
                continue;
            float d = Mathf.Abs(it.Pos - zoneCenter);
            if(d <= zoneHalf && d < best)
            {
                best = d;
                hit = it;
            }
        }
        if(hit == null)
            return;

        hit.Resolved = true;
        handledCount++;

        if(key != hit.Type)
        {
            // 按错键：残次品保持原样闪红，正常品换失败包装闪红，均随传送带滚出
            if(hit.Type == FactoryNoteType.Up)
                hit.View.FlashFail();
            else
                hit.View.SetPacked(hit.Type, false);
            Fail();
        }
        else if(hit.Type == FactoryNoteType.Up)
        {
            // 丢弃残次品：弹飞渐隐，立即离场
            items.Remove(hit);
            hit.View.FlickAway(() => RecycleItem(hit.View));
            combo++;
        }
        else
        {
            // 打包成功：闪白弹跳 + 评价飘字，盒装滚出
            hit.View.SetPacked(hit.Type, true);
            ShowTip(best <= config.GoodHalfWidth ? FactoryEvaluateType.Perfect : FactoryEvaluateType.Good);
            completeCount++;
            combo++;
        }
        RefreshStats();
    }

    void Fail()
    {
        failCount++;
        combo = 0;
    }
    #endregion

    #region 对象池 / 显示
    void RecycleItem(FactoryGamePackItem view)
    {
        view.gameObject.SetActive(false);
        itemPool.Push(view);
    }

    void ClearBelt()
    {
        foreach(BeltItem it in items)
            RecycleItem(it.View);
        items.Clear();
    }

    void ShowTip(FactoryEvaluateType type)
    {
        FactoryProcessEvaluateTip tip = tipPool.Count > 0 ? tipPool.Pop() : Instantiate(tipTemplate, tipTemplate.transform.parent);
        tip.gameObject.SetActive(true);
        tip.Show(type, () =>
        {
            tip.gameObject.SetActive(false);
            tipPool.Push(tip);
        });
    }

    void RefreshStats()
    {
        curProcessCountText.text = handledCount.ToString("D3");
        failCountText.text = failCount.ToString("D2");
        completeCountText.text = completeCount.ToString("D2");
        comboCountText.text = combo.ToString();
        comboGroup.SetActive(combo > 0);
    }
    #endregion

    #region 本局批次
    /// <summary>由主面板在「开始加工」时带入本局加工的生产资料批次并立即消耗（开始即扣，提前退出不返还），产出发放待新结算面板接入。</summary>
    public void SetCraftBatch(IReadOnlyList<FactoryMoldItemInfo> materials)
    {
        craftBatch.Clear();
        craftBatch.AddRange(materials);
        foreach(FactoryMoldItemInfo material in craftBatch)
            InventoryManager.Instance.ConsumeItem(material, 1);
    }

    /// <summary>由主面板设置：本面板关闭返回时回调一次（主面板据此刷新，反映本局已消耗的素材）。</summary>
    public void SetOnClosed(Action callback) => onClosed = callback;
    #endregion
}
