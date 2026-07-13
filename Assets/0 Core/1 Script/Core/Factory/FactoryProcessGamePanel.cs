using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 工厂加工音游面板：开局按生产量预生成整条徽章队列（50% 重/左、50% 轻/右，再按不良率把「生产量×不良率」件随机刷成不良品），
/// 依 <see cref="FactoryGameConfig.AssemblyLineNoteSequences"/> 的节奏依次入场（拍长 NoteInterval，0=空拍、非 0=出一件，序列循环取用）。
/// 按键：重=A/←/鼠标左键，轻=D/→/鼠标右键，不良品=W/↑/鼠标中键（左上右对应鼠标左滚轮右）。
/// 判定：徽章「中心进入绿色区域」时按对应键才算盖章成功，否则该件失败——轻/重失误或漏盖失败计数+1；
/// 不良品点错 / 漏掉则卡住机器 <see cref="FactoryGameConfig.JamDuration"/> 秒（可配置），期间无法操作；
/// 卡机时已在场的不良品漏过不追责（玩家无法操作，不重复卡机，避免链式卡机）。
/// 每次按键 / 点击有 <see cref="FactoryGameConfig.PressCooldown"/> 秒 CD（协程驱动，防连打），CD 剩余以填充条展示。
/// 全部徽章走过绿色区域后本局结束，广播 <see cref="OnRoundEnd"/>（获得数 = 生产数 − 不良品数 − 失败计数），
/// 短暂展示后自动发放产出并弹出结算面板 <see cref="FactorySettlePanel"/>，点击任意位置关结算 + 关本面板回主界面。
/// 音频：面板自建两个 AudioSource——BGM 开局播放、暂停/退出确认时挂起（恢复续播）、关面板停止；成功/失败逐件播对应音效，
/// 结束时有产出播胜利音效（音效为临时生成的占位 WAV，待正式资源替换，见 Remote/Audio/Factory）。
/// 产品视图与评价飘字均对象池复用；键鼠输入统一经 <see cref="PlayerInputManager"/> 事件接入（开/关面板订阅/退订）；
/// 鼠标点在暂停/退出按钮上时用 <see cref="RectTransformUtility"/> 判定并跳过游戏判定，避免误触。
/// </summary>
public class FactoryProcessGamePanel : UIBase
{
    [LabelText("配置")][SerializeField] FactoryGameConfig config;

    [Title("战况")]
    [SerializeField] TextMeshProUGUI curProcessCountText, defectRateText, failCountText, completeCountText;// 本次加工数量 残次品率 失败产品数 完成生产数
    [SerializeField] TextMeshProUGUI comboCountText;    // 连击数
    [LabelText("连击组(0 连击隐藏)")][SerializeField] GameObject comboGroup;
    [Title("传送带（包装区域需与产品容器同父级同坐标系，中心锚点）")]
    [LabelText("产品容器(铺满传送带宽)")][SerializeField] RectTransform itemContainer;
    [LabelText("产品模板(隐藏)")][SerializeField] FactoryGamePackItem itemTemplate;
    [LabelText("包装区域")][SerializeField] RectTransform packArea;

    [Title("评价 / 按钮")]
    [LabelText("评价飘字模板(隐藏)")][SerializeField] FactoryProcessEvaluateTip tipTemplate;
    [LabelText("暂停")][SerializeField] Button pauseButton;
    [LabelText("退出")][SerializeField] Button quitButton;
    [LabelText("退出确认面板")][SerializeField] FactoryProcessEndConfirmPanel quitConfirmPanel;
    [SerializeField] Image pasueImage;

    [Title("按键 CD")]
    [LabelText("CD 组(无 CD 隐藏)")][SerializeField] GameObject cdGroup;
    [LabelText("CD 填充(剩余比例)")][SerializeField] Image cdFillImage;

    [Title("音频（占位资源，待正式音效替换）")]
    const string FactoryFailSound = nameof(FactoryFailSound);
    const string FactorySuccessSound = nameof(FactorySuccessSound);
    const string FactoryGameBgm = nameof(FactoryGameBgm);
    const string FactoryVictoryClipSound = nameof(FactoryVictoryClipSound);

    /// <summary>本局结束：获得数量(生产数−不良品−失败计数，下限 0)、失败计数。结算面板已由本面板内部弹出，此事件供外部系统监听。</summary>
    public event Action<int, int> OnRoundEnd;

    const float EndExitDelay = 1.5f;   // 结束后停留展示时长，随后自动关闭面板

    class BeltItem
    {
        public FactoryGamePackItem View;
        public FactoryNoteType Type;
        public float Pos;        // 归一化：0=入口 1=出口
        public bool Resolved;    // 已打包 / 已丢弃 / 已判 MISS
        public bool JamExempt;   // 卡机时已在场的不良品：漏过不追责（不重复卡机）
    }

    readonly List<BeltItem> items = new ();
    readonly Stack<FactoryGamePackItem> itemPool = new ();
    readonly Stack<FactoryProcessEvaluateTip> tipPool = new ();

    bool playing, paused;
    float zoneCenter, zoneHalf;              // 包装区域（归一化，取自 UI 实际位置 / 宽度）；徽章中心进区即可盖章
    float itemHalf;                          // 徽章半宽（归一化，取自模板 UI 实际宽度），用于「有接触」范围
    IReadOnlyList<FactoryNoteType> chart;    // 本局出货节奏序列（0=空拍 非0=出一件，循环取用）
    int chartIdx;
    float beatTimer;
    int totalToSpawn, spawnedCount, handledCount;
    int defectPercent, defectCount;
    readonly List<FactoryNoteType> spawnQueue = new ();   // 开局预生成的整条徽章队列（类型按规则排定，与节奏序列解耦）
    int completeCount, failCount, combo;
    // float roundRemain;   // 暂时停用倒计时（仅展示；本局以「全部徽章走过绿色区域」结束）
    float lockRemain;      // 机器卡住剩余时间
    Coroutine cdRoutine;   // 按键 CD 协程（非空 = CD 中）
    float endExitRemain;   // 结束后自动关闭倒计时
    AudioSource bgmSource;   // 本面板自管的播放器：BGM 可随暂停挂起/恢复（AudioManager 无暂停接口），音效走 PlayOneShot

    readonly List<FactoryMoldItemInfo> craftBatch = new ();   // 本局加工的生产资料批次（主面板带入），供结算产出用
    Action onClosed;   // 本面板关闭返回时回调（主面板刷新，反映本局已消耗的素材）

    public override void Init()
    {
        itemTemplate.gameObject.SetActive(false);
        tipTemplate.gameObject.SetActive(false);
        cdGroup.SetActive(false);
        pauseButton.onClick.AddListener(Toggle);
        quitButton.onClick.AddListener(OnQuitButton);
    }
    void Toggle()
    {
        paused = !paused;
        pasueImage.gameObject.SetActive(paused);
        PauseBgm(paused);
    }
    public override void Open()
    {
        base.Open();
        Subscribe();
        StartRound();
        PlayBgm();
        UISystem.Instance.CloseUI(AssetKeys.MainUIPath);
    }

    public override void Close()
    {
        // 进行中不允许关闭（退出走确认弹窗，确认后先停局再 CloseUI）
        if(playing)
            return;

        StopBgm();
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
        if(paused)
            return;

        // 结束展示期：传送带继续把剩余盒子送出，倒计时归零后自动弹出结算面板
        if(!playing)
        {
            if(endExitRemain <= 0f)
                return;
            StepBelt(Time.deltaTime);
            endExitRemain -= Time.deltaTime;
            if(endExitRemain <= 0f)
                ShowSettlePanel();
            return;
        }

        StepLock(Time.deltaTime);
        StepSpawn(Time.deltaTime);
        StepBelt(Time.deltaTime);

        // 全部徽章都已走过绿色区域（逐件在盖章 / 漏件时计入 handledCount）→ 本局结束
        if(handledCount >= totalToSpawn)
            EndRound();
    }

    #region 开局 / 结束
    public void StartRound()
    {
        ClearBelt();
        totalToSpawn = config.BaseProductionVolume + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.ProductionVolume);
        defectPercent = 100 - Mathf.Clamp(config.BaseYieldRate + FactoryEquipManager.St.SumBonus(FactoryEquipBonusType.Yield), 0, 100);
        defectCount = Mathf.Clamp(Mathf.RoundToInt(totalToSpawn * defectPercent / 100f), 0, totalToSpawn);
        BuildSpawnQueue();
        chart = config.AssemblyLineNoteSequences[UnityEngine.Random.Range(0, config.AssemblyLineNoteSequences.Count)];
        chartIdx = 0;
        beatTimer = 0f;
        spawnedCount = handledCount = completeCount = failCount = combo = 0;
        lockRemain = endExitRemain = 0f;
        paused = false;
        pasueImage.gameObject.SetActive(false);   // 上局若在暂停中被确认退出，遮罩会残留到复用的面板上，这里一并复位
        StopCd();

        int beats = SpawnBeatCount();
        if(beats == 0)   // 节奏序列全空拍属配置错误，直接不开局，避免 Update 里永远出不完货
        {
            Debug.LogError("[FactoryProcessGamePanel] 出货节奏序列不含有效出货拍，无法开局。", config);
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
        StopCd();
        endExitRemain = EndExitDelay;   // 短暂展示后由 Update 自动弹出结算面板
        // 获得数量 = 生产数量 − 不良品 − 失败计数（下限 0）
        int gained = Mathf.Max(0, totalToSpawn - defectCount - failCount);
        // 有产出即算本局胜利，播胜利音效（颗粒无收不播，避免误导）

        AudioManager.Instance.PlayAudio(FactoryVictoryClipSound);

        OnRoundEnd?.Invoke(gained, failCount);
    }

    // 结算：把产出（周边商品，运行时自描述物品）按获得数量发放进背包，并弹出结算面板展示
    void ShowSettlePanel()
    {
        int gained = Mathf.Max(0, totalToSpawn - defectCount - failCount);

        List<FactoryMerchandiseItemInfo> granted = new ();
        InventoryManager bag = InventoryManager.Instance;
        if(bag != null && gained > 0)
            foreach(FactoryMoldItemInfo material in craftBatch)
            {
                if(material == null)
                    continue;
                FactoryMerchandiseItemInfo item = material.CreateMerchandiseItem(gained);
                bag.AddRuntimeItem(item);
                granted.Add(item);
            }

        FactorySettlePanel.Data data = new ()
        {
            CraftCount = totalToSpawn,
            DefectRate = defectPercent / 100f,
            FailCount = failCount,
            DoneCount = gained,
            Products = granted,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<FactorySettlePanel>(UIPanelIdSet.FactorySettlePanel).Show(data);
    }

    // 结算面板点击任意位置返回：关结算 + 关本局小游戏，回到加工厂主界面
    void OnSettleBack()
    {
        UISystem.Instance.CloseUI(UIPanelIdSet.FactorySettlePanel);
        UISystem.Instance.CloseUI(uiname);
    }

    // 预生成整条徽章队列：50% 重(左)/50% 轻(右)，再把「生产量×不良率」件随机位置刷成不良品
    void BuildSpawnQueue()
    {
        spawnQueue.Clear();
        for(int i = 0; i < totalToSpawn; i++)
            spawnQueue.Add(i < defectCount ? FactoryNoteType.Up
                : UnityEngine.Random.value < 0.5f ? FactoryNoteType.Left : FactoryNoteType.Right);
        for(int i = spawnQueue.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (spawnQueue[i], spawnQueue[j]) = (spawnQueue[j], spawnQueue[i]);
        }
    }

    // 退出：先冻结本局并弹二次确认（BGM 一并挂起）；确认则放弃本局（什么也不获得）收起面板，
    // 取消继续——此前可能是从暂停状态点的退出，故取消时一并收起暂停遮罩再恢复
    void OnQuitButton()
    {
        paused = true;
        PauseBgm(true);
        quitConfirmPanel.Show(OnQuitConfirmed, () =>
        {
            paused = false;
            pasueImage.gameObject.SetActive(false);
            PauseBgm(false);
        });
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

    // 包装区域由策划在预制里摆放，运行时反推其归一化中心 / 半宽，保证「所见即判定」；
    // 成功窗口 = 区域半宽（徽章中心进入绿色区域内即可盖章），徽章半宽仅用于「有接触」范围
    void LayoutZone()
    {
        float w = itemContainer.rect.width;
        zoneCenter = packArea.anchoredPosition.x / w + 0.5f;
        zoneHalf = packArea.sizeDelta.x * 0.5f / w;
        itemHalf = itemTemplate.Rt.rect.width * 0.5f / w;
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

        // 节奏序列只决定「何时出货」（0=空拍 非0=出一件），徽章类型取开局预生成的队列
        FactoryNoteType beat = chart[chartIdx];
        chartIdx = (chartIdx + 1) % chart.Count;
        if(beat == FactoryNoteType.None)
            return;

        FactoryNoteType note = spawnQueue[spawnedCount];
        spawnedCount++;
        FactoryGamePackItem view = itemPool.Count > 0 ? itemPool.Pop() : Instantiate(itemTemplate, itemContainer);
        view.gameObject.SetActive(true);
        view.Set(note);
        if(lockRemain > 0f)
            view.SetLocked(note, true);
        BeltItem item = new() { View = view, Type = note };
        // 卡机期间新出的不良品玩家同样无法处理，直接豁免，避免其漏过时再次触发链式卡机
        if(lockRemain > 0f && note == FactoryNoteType.Up)
            item.JamExempt = true;
        items.Add(item);
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

            // 越过包装区域仍未盖章 → 漏件判定：轻/重记失败，不良品卡住机器
            if(!it.Resolved && it.Pos > zoneCenter + zoneHalf)
            {
                it.Resolved = true;
                handledCount++;
                PlayRoundSfx(false);
                if(it.Type == FactoryNoteType.Up)
                {
                    // 卡机时已在场的不良品玩家本就无法处理，漏过不追责（豁免见 JamMachine），避免链式卡机
                    if(!it.JamExempt)
                        JamMachine();
                }
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

    // 不良品处理失败（点错 / 漏掉）：卡住机器一段时间（时长可配置），场上所有产品（含不良品）全部锁定无法操作（锁定期漏件照记失败）；
    // 已在场的不良品同时打上豁免标记——卡机期间玩家无法处理，它们漏过不再触发新卡机
    void JamMachine()
    {
        lockRemain = config.JamDuration;
        foreach(BeltItem it in items)
        {
            if(it.Resolved)
                continue;
            if(it.Type == FactoryNoteType.Up)
                it.JamExempt = true;
            it.View.SetLocked(it.Type, true);
        }
    }

    void StepLock(float dt)
    {
        if(lockRemain <= 0f)
            return;
        lockRemain -= dt;
        if(lockRemain > 0f)
            return;

        // 卡机结束：场上未处理的产品（含不良品）恢复可操作外观
        foreach(BeltItem it in items)
            if(!it.Resolved)
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
        if(!playing || paused || lockRemain > 0f || cdRoutine != null)
        {
            string reason = !playing ? "本局未开始或已结束" : paused ? "暂停中"
                : lockRemain > 0f ? $"卡机中(剩 {lockRemain:F2}s)" : "按键 CD 中";
            Debug.Log($"[FactoryProcessGamePanel] 按键未响应：{reason}");
            return;
        }
        StartCd();

        // 取与绿色区域有接触且离中心最近的未处理徽章；完全无接触则空按无惩罚（仅进 CD）
        BeltItem hit = null;
        float best = float.MaxValue;
        foreach(BeltItem it in items)
        {
            if(it.Resolved)
                continue;
            float d = Mathf.Abs(it.Pos - zoneCenter);
            if(d <= zoneHalf + itemHalf && d < best)
            {
                best = d;
                hit = it;
            }
        }
        if(hit == null)
        {
            Debug.Log("[FactoryProcessGamePanel] 空按：判定区附近没有徽章（无惩罚，仅进 CD）");
            return;
        }

        hit.Resolved = true;
        handledCount++;

        // 徽章中心进入绿色区域内按下对应键即成功，时机不对或按错键都算本次失败
        if(best > zoneHalf || key != hit.Type)
        {
            PlayRoundSfx(false);
            if(hit.Type == FactoryNoteType.Up)
            {
                // 不良品点击失败：闪红随带滚出，并卡住机器
                hit.View.FlashFail();
                JamMachine();
            }
            else
            {
                // 轻/重盖章失误：换失败包装闪红随带滚出，失败计数+1
                hit.View.SetPacked(hit.Type, false);
                Fail();
            }
        }
        else if(hit.Type == FactoryNoteType.Up)
        {
            // 丢弃不良品：弹飞渐隐，立即离场
            PlayRoundSfx(true);
            items.Remove(hit);
            hit.View.FlickAway(() => RecycleItem(hit.View));
            combo++;
        }
        else
        {
            // 打包成功：闪白弹跳 + 评价飘字，盒装滚出
            PlayRoundSfx(true);
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

    // 按键 CD 协程：期间输入无效（cdRoutine 非空即 CD 中），剩余比例以填充条展示；面板隐藏时协程自动终止，StartRound 里 StopCd 兜底清引用
    void StartCd()
    {
        if(config.PressCooldown <= 0f)
            return;
        StopCd();
        cdRoutine = StartCoroutine(CdRoutine());
    }

    void StopCd()
    {
        if(cdRoutine != null)
            StopCoroutine(cdRoutine);
        cdRoutine = null;
        cdGroup.SetActive(false);
    }

    IEnumerator CdRoutine()
    {
        cdGroup.SetActive(true);
        for(float remain = config.PressCooldown; remain > 0f; remain -= Time.deltaTime)
        {
            cdFillImage.fillAmount = Mathf.Clamp01(remain / config.PressCooldown);
            yield return null;
        }
        cdGroup.SetActive(false);
        cdRoutine = null;
    }

    #endregion

    #region 音频
    void PlayBgm()
    {
        // AudioManager.Instance.PlayAudio(FactoryGameBgm);  
    }

    void StopBgm()
    {
        // AudioManager.Instance.StopAudio(FactoryGameBgm);  
    }
    // 暂停挂起 / 恢复续播（保留播放进度）；暂停按钮与退出确认弹窗共用
    void PauseBgm(bool pause)
    {
        if(pause)
            bgmSource.Pause();
        else
            bgmSource.UnPause();
    }

    // 逐件判定音效：成功盖章 / 丢弃不良品播成功音，失误 / 遗漏播失败音
    void PlayRoundSfx(bool success)
    {
        AudioManager.Instance.PlayAudio(success ?  FactorySuccessSound : FactoryFailSound);  
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
    public void SetCraftBatch(IReadOnlyList<FactoryMoldItemInfo> items)
    {
        craftBatch.Clear();
        craftBatch.AddRange(items);
        craftBatch.RemoveAll(x => x == null);
        foreach(FactoryMoldItemInfo material in craftBatch)
            InventoryManager.Instance.ConsumeItem(material, 1);
    }

    /// <summary>由主面板设置：本面板关闭返回时回调一次（主面板据此刷新，反映本局已消耗的素材）。</summary>
    public void SetOnClosed(Action callback) => onClosed = callback;
    #endregion
}
