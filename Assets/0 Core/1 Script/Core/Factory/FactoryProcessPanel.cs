using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// 工厂加工（传送带下压）小游戏界面：左侧战况栏（积分 / 完成率 / 成功 / 失败）、顶部倒计时、
/// 中央传送带与下压区、空格下压。逻辑全部由 <see cref="FactoryProcessGameManager"/> 处理，本类只负责 UI、输入与表现。
/// </summary>
[RequireComponent(typeof(FactoryProcessGameManager))]
public class FactoryProcessPanel : UIBase
{
    [SerializeField] FactoryProcessGameManager manager;

    [Title("战况栏")]
    [LabelText("积分数值")][SerializeField] TMP_Text scoreValueText;
    [LabelText("完成率数值")][SerializeField] TMP_Text completionValueText;
    [LabelText("成功数值")][SerializeField] TMP_Text successValueText;
    [LabelText("失败数值")][SerializeField] TMP_Text failValueText;

    [Title("传送带")]
    [LabelText("产品容器(铺满传送带宽)")][SerializeField] RectTransform itemContainer;
    [LabelText("产品模板(隐藏)")][SerializeField] FactoryItemView itemTemplate;
    [LabelText("下压区标记")][SerializeField] RectTransform pressZoneRtf;
    [LabelText("下压器(锤)")][SerializeField] RectTransform stampRtf;
    [LabelText("灯带(成功/失败闪烁)")][SerializeField] Image lightStrip;

    [Title("反馈 / 提示")]
    [SerializeField] float pressScale = 1.3f;
    [SerializeField] float dur = 0.08f;
    [LabelText("下压判定评价图标(碾压机旁)")][SerializeField] Image feedbackIcon;
    [LabelText("体力不足提示")][SerializeField] WarnTip notEnoughStaminaTip;
    [LabelText("结算面板头像")][SerializeField] Sprite settleAvatar;

    [Title("按钮")]
    [LabelText("结束本局")][SerializeField] Button endRoundButton;
    [LabelText("返回")][SerializeField] Button closeButton;
    [LabelText("提前结束确认弹窗(本面板下隐藏子物体)")][SerializeField] FactoryProcessEndConfirmPanel endConfirmPanel;

    static readonly Color GoodColor = new (0.2f, 0.75f, 0.35f);
    static readonly Color OkColor = new (0.85f, 0.65f, 0.2f);
    static readonly Color BadColor = new (0.85f, 0.2f, 0.2f);

    readonly Dictionary<int, FactoryItemView> activeViews = new ();
    readonly Stack<FactoryItemView> viewPool = new ();
    readonly HashSet<int> liveIds = new ();
    readonly List<int> goneIds = new ();
    readonly List<FactoryMoldItemInfo> craftBatch = new ();   // 本局加工的生产资料批次（由主面板带入），仅用于结算展示
    Vector2 stampHomePos;
    Vector3 stampHomeScale;
    Coroutine stampCt;
    Coroutine feedbackCt;
    Coroutine lightCt;
    bool subscribed;
    System.Action onClosed;   // 本面板关闭返回时回调（主面板用于刷新，反映本局已消耗的素材）

    #region LifeCycle
    public override void Init()
    {
        endRoundButton.onClick.AddListener(OnEndRoundButton);
        closeButton.onClick.AddListener(OnCloseButton);

        itemTemplate.gameObject.SetActive(false);
        stampHomePos = stampRtf.anchoredPosition;
        stampHomeScale = stampRtf.localScale;
        feedbackIcon.gameObject.SetActive(false);
        Subscribe();
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        LayoutBelt();

        if(!manager.StartRound())
            notEnoughStaminaTip.ShowTip(LocTableSet.Factory, FactoryLocKeySet.Process.NotEnoughStamina);
    }

    // 开局消耗本局选定的模具：每个各扣 1 个（craftBatch 里是背包中的物品实例引用，扣到 0 由背包自动移除）。
    // 在此消耗保证「开始即扣」，即便随后提前结束也不返还。
    // 注意：由 SetCraftBatch 触发（OpenUI 会先跑 Open→StartRound，之后调用方才 SetCraftBatch 传入批次，故消耗放在拿到批次后）。
    void ConsumeCraftMaterials()
    {
        InventoryManager bag = InventoryManager.Instance;
        if(bag == null)
            return;

        foreach(FactoryMoldItemInfo material in craftBatch)
            if(material != null && material.Count > 0)
                bag.ConsumeItem(material, 1);
    }

    public override void Close()
    {
        // 进行中不允许关闭（避免吞掉已扣体力、让倒计时在隐藏状态停滞）
        if(manager.State == FactoryProcessGameManager.GameState.Playing)
            return;

        base.Close();
        Unsubscribe();

        // 返回时回调一次主面板刷新（结算返回 / 直接返回都经此关闭）；用完即清，避免下次复用残留旧回调
        System.Action cb = onClosed;
        onClosed = null;
        cb?.Invoke();
    }

    void Update()
    {
        if(manager.State != FactoryProcessGameManager.GameState.Playing)
            return;

        SyncItems();

        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            StartPress();
    }
    #endregion

    #region 订阅
    void Subscribe()
    {
        if(subscribed)
            return;
        subscribed = true;
        manager.OnStateChanged += OnStateChanged;
        manager.OnScoreChanged += RefreshStats;
        manager.OnRoundEnd += OnRoundEnd;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        manager.OnStateChanged -= OnStateChanged;
        manager.OnScoreChanged -= RefreshStats;
        manager.OnRoundEnd -= OnRoundEnd;
    }
    #endregion

    #region 传送带布局与产品同步
    // 凹槽 UI（pressZoneRtf）由策划在预制里摆放，运行时反推其归一化中心 / 半宽交给逻辑，保证「所见即判定」；
    // 下压器对齐到凹槽中心。容器实际宽度需运行时取。
    void LayoutBelt()
    {
        float w = itemContainer.rect.width;
        if(w <= 0f)
            return;
        float centerX = pressZoneRtf.anchoredPosition.x;          // 容器中心为 0，与产品视图同坐标系
        float centerNorm = centerX / w + 0.5f;                    // 0=入口 1=出口
        float halfWidthNorm = pressZoneRtf.sizeDelta.x * 0.5f / w;
        manager.SetPressZone(centerNorm, halfWidthNorm);
        stampRtf.anchoredPosition = stampHomePos = new Vector2(centerX, stampHomePos.y);
    }

    // 逐帧把管理器逻辑产品同步到界面视图：新产品取池生成、移动、灰化、离场回收
    void SyncItems()
    {
        float w = itemContainer.rect.width;
        liveIds.Clear();

        foreach(FactoryProcessGameManager.Item it in manager.Items)
        {
            liveIds.Add(it.Id);
            if(!activeViews.TryGetValue(it.Id, out FactoryItemView view))
            {
                view = SpawnView();
                activeViews.Add(it.Id, view);
            }
            view.Rt.anchoredPosition = new Vector2((it.Pos - 0.5f) * w, 0f);
            if(it.Resolved)
                view.SetResolved(it.Qualified ? manager.Config.QualifiedBoxPrefab : manager.Config.DefectiveBoxPrefab);
        }

        // 回收已离场（管理器中已移除）的视图
        goneIds.Clear();
        foreach(int id in activeViews.Keys)
            if(!liveIds.Contains(id))
                goneIds.Add(id);
        foreach(int id in goneIds)
        {
            RecycleView(activeViews[id]);
            activeViews.Remove(id);
        }
    }

    FactoryItemView SpawnView()
    {
        FactoryItemView view = viewPool.Count > 0
            ? viewPool.Pop()
            : Instantiate(itemTemplate, itemContainer);
        view.gameObject.SetActive(true);
        view.SetData();
        return view;
    }

    void RecycleView(FactoryItemView view)
    {
        view.gameObject.SetActive(false);
        viewPool.Push(view);
    }

    void ClearViews()
    {
        foreach(FactoryItemView view in activeViews.Values)
            RecycleView(view);
        activeViews.Clear();
    }
    #endregion

    #region 下压与表现
    void StartPress()
    {
        FactoryProcessGameManager.PressResult r = manager.PressStamp();
        PlayStamp();

        switch(r)
        {
            // Good / Ok 同为合格品，展示「合格品」评价图标；Bad 为次品
            case FactoryProcessGameManager.PressResult.Good:
                ShowFeedback(manager.Config.QualifiedEvalIcon);
                FlashLight(GoodColor);
                break;
            case FactoryProcessGameManager.PressResult.Ok:
                ShowFeedback(manager.Config.QualifiedEvalIcon);
                FlashLight(OkColor);
                break;
            case FactoryProcessGameManager.PressResult.Bad:
                ShowFeedback(manager.Config.DefectiveEvalIcon);
                FlashLight(BadColor);
                break;
            // Empty：空压不反馈，仅落锤
        }
    }

    void PlayStamp()
    {
        if(stampCt != null)
            StopCoroutine(stampCt);
        stampCt = StartCoroutine(PlayStampIE());
    }

    // 下压器快速拉伸再回弹（仅缩放 Y，模拟砸下冲击，不改位置）
    IEnumerator PlayStampIE()
    {
        Vector3 pressed = new (stampHomeScale.x, stampHomeScale.y * pressScale, stampHomeScale.z);

        for(float t = 0; t < dur; t += Time.deltaTime)
        {
            stampRtf.localScale = Vector3.Lerp(stampHomeScale, pressed, t / dur);
            yield return null;
        }
        for(float t = 0; t < dur; t += Time.deltaTime)
        {
            stampRtf.localScale = Vector3.Lerp(pressed, stampHomeScale, t / dur);
            yield return null;
        }
        stampRtf.localScale = stampHomeScale;
    }

    // 在碾压机旁弹出评价图标（合格品 / 次品）
    void ShowFeedback(Sprite icon)
    {
        if(feedbackCt != null)
            StopCoroutine(feedbackCt);
        feedbackIcon.sprite = icon;
        feedbackIcon.gameObject.SetActive(true);
        feedbackCt = StartCoroutine(HideFeedbackIE());
    }

    IEnumerator HideFeedbackIE()
    {
        yield return new WaitForSeconds(0.5f);
        feedbackIcon.gameObject.SetActive(false);
    }

    void FlashLight(Color color)
    {
        if(lightCt != null)
            StopCoroutine(lightCt);
        lightCt = StartCoroutine(FlashLightIE(color));
    }

    IEnumerator FlashLightIE(Color color)
    {
        Color dim = new (color.r, color.g, color.b, 0.25f);
        lightStrip.color = color;
        for(float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            lightStrip.color = Color.Lerp(color, dim, t / 0.3f);
            yield return null;
        }
        lightStrip.color = dim;
    }
    #endregion

    #region 刷新显示
    void RefreshStats()
    {
        scoreValueText.text = manager.Score.ToString();
        completionValueText.text = Mathf.RoundToInt(manager.Completion * 100f) + "%";
        successValueText.text = "X" + manager.SuccessCount;
        failValueText.text = "X" + manager.FailCount;
    }
    #endregion

    #region 管理器事件
    void OnStateChanged(FactoryProcessGameManager.GameState s)
    {
        if(s == FactoryProcessGameManager.GameState.Playing)
        {
            ClearViews();
            stampRtf.localScale = stampHomeScale;
            feedbackIcon.gameObject.SetActive(false);
            RefreshStats();
        }
    }

    void OnRoundEnd(int score, int success, int fail, float completion, int reward)
    {
        ClearViews();
        List<FactoryMerchandiseItemInfo> granted = GrantProducts(completion);
        ShowSettlePanel(score, success, completion, granted);
    }

    // 把本局加工的周边商品(Merchandise)发放进背包（对应结算面板「道具已自动发放进背包」提示）。
    // 周边商品是运行时自描述物品(FactoryMerchandiseItemInfo)，不再查/写 ItemConfig。
    // 产出总数 = 本局「制作成功」数（实际压中做出来的件数，非生产量上限；生产量只决定传送带出多少个）。
    // 再按完成率拆为合格品 / 次品：合格品数 = 四舍五入(成功数 × 完成率)，其余记为次品（售价减半）。完成率越低次品越多。
    List<FactoryMerchandiseItemInfo> GrantProducts(float completion)
    {
        List<FactoryMerchandiseItemInfo> granted = new ();
        InventoryManager bag = InventoryManager.Instance;
        if(bag == null)
            return granted;

        int craftCount = manager.SuccessCount;   // 实际做出来的件数 = 制作成功数
        if(craftCount <= 0)
            return granted;

        float rate = Mathf.Clamp01(completion);
        int qualified = Mathf.Clamp(Mathf.RoundToInt(craftCount * rate), 0, craftCount);
        int defective = craftCount - qualified;

        foreach(FactoryMoldItemInfo material in craftBatch)
        {
            if(material == null)
                continue;

            if(qualified > 0)
            {
                FactoryMerchandiseItemInfo item = FactoryMerchandiseItemInfo.Create(material, FactoryMerchandiseItemInfo.QualityGrade.Qualified, qualified);
                bag.AddRuntimeItem(item);
                granted.Add(item);
            }
            if(defective > 0)
            {
                FactoryMerchandiseItemInfo item = FactoryMerchandiseItemInfo.Create(material, FactoryMerchandiseItemInfo.QualityGrade.Defective, defective);
                bag.AddRuntimeItem(item);
                granted.Add(item);
            }
        }
        return granted;
    }
    #endregion

    #region 按钮
    // 结束本局：先暂停本局并弹出确认面板（提前结束将什么也不获得）；确认则放弃本局回主界面，取消则继续。
    void OnEndRoundButton()
    {
        if(manager.State != FactoryProcessGameManager.GameState.Playing)
            return;

        manager.SetPaused(true);
        endConfirmPanel.Show(OnEndConfirmed, OnEndCancelled);
    }

    // 确认提前结束：放弃本局（不结算、不产出），关闭小游戏返回主界面
    void OnEndConfirmed()
    {
        manager.AbortRound();
        UISystem.Instance.CloseUI(uiname);
    }

    // 取消：恢复本局继续进行
    void OnEndCancelled() => manager.SetPaused(false);

    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion

    #region 本局批次
    /// <summary>由主面板在「开始加工」时带入本局加工的生产资料批次，用于产出周边商品与结算展示。</summary>
    public void SetCraftBatch(IReadOnlyList<FactoryMoldItemInfo> materials)
    {
        craftBatch.Clear();
        craftBatch.AddRange(materials);
        ConsumeCraftMaterials();   // 拿到本局批次即消耗选定模具（此时 Open→StartRound 已执行，等同「开始即扣」）
    }

    /// <summary>由主面板设置：本面板关闭返回时回调一次（主面板据此刷新，反映本局已消耗的素材）。</summary>
    public void SetOnClosed(System.Action callback) => onClosed = callback;
    #endregion

    #region 结算
    // 售价倍率暂为占位（X2.0），待策划数值确定（这一块后续可能调整/删除）
    const float SettleSaleMultiplier = 2f;

    void ShowSettlePanel(int score, int success, float completion, List<FactoryMerchandiseItemInfo> products)
    {
        FactorySettlePanel.Data data = new ()
        {
            Score = score,
            SuccessCount = success,
            Completion = completion,
            SaleMultiplier = SettleSaleMultiplier,
            Products = products,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<FactorySettlePanel>(UIPanelIdSet.FactorySettlePanel).Show(data);
    }

    // 返回：关结算 + 关本局小游戏，回到加工厂主界面
    void OnSettleBack()
    {
        UISystem.Instance.CloseUI(UIPanelIdSet.FactorySettlePanel);
        UISystem.Instance.CloseUI(uiname);
    }
    #endregion
}
