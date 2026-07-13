using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using TMPro;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// 女巫毒药小游戏界面：下注滑条、药瓶棋盘、倍率/收益显示、见好就收。
/// 逻辑全部由 <see cref="WitchPotionGameManager"/> 处理，本类只负责 UI 与输入。
/// </summary>
[RequireComponent(typeof(WitchPotionGameManager))]
public class WitchPoisonPanel : UIBase
{
    [SerializeField] WitchPotionGameManager manager;

    [Title("棋盘")]
    [LabelText("药瓶父级")][SerializeField] RectTransform gridContainer;
    [SerializeField] GridLayoutGroup glg;
    [LabelText("药瓶预制体")][SerializeField] WitchPotionBottleUI bottlePrefab;

    [Title("下注")]
    [SerializeField] Slider betSlider;
    [LabelText("下注金额区间提示")][SerializeField] LocalizeStringEvent betRangeText;   // "最低金额{MinBet} 最高金额{MaxBet}"

    [Title("数值显示")]
    [SerializeField] LocalizeStringEvent betValueText;        // 下注金额
    [SerializeField] LocalizeStringEvent remainValueText;     // "{Opened}/{Total}"
    [SerializeField] LocalizeStringEvent multiplierValueText; // "{Multiplier}x"
    [SerializeField] LocalizeStringEvent payoutValueText;     // 收益
    [SerializeField] LocalizeStringEvent goldText;            // "现有金币：{Gold}"

    [Title("祝福 / 提示")]
    [LabelText("祝福提示（女巫祝福 倍率翻倍）")][SerializeField] GameObject blessingTip;
    [LabelText("游戏币不足提示")][SerializeField] WarnTip warnTip;

    [Title("本局结算")]
    [LabelText("结算面板头像")][SerializeField] Sprite settleAvatar;

    [Title("按钮")]
    [SerializeField] Button startButton;     // 开始 / 再来一局
    [SerializeField] Button cashOutButton;   // 见好就收
    [SerializeField] Button closeButton;     // 返回

    readonly List<WitchPotionBottleUI> bottles = new List<WitchPotionBottleUI>();
    bool subscribed;

    #region 生命周期
    public override void Init()
    {
        startButton.onClick.AddListener(OnStartButton);
        cashOutButton.onClick.AddListener(OnCashOutButton);
        closeButton.onClick.AddListener(OnCloseButton);
        betSlider.onValueChanged.AddListener(OnSliderChanged);

        SetupSlider();
        BuildGrid();
        Subscribe();
        RefreshBetRange();
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        manager.ResetToBetting();
        EnterBettingView();
        RefreshGold();
    }

    public override void Close()
    {
        // 进行中不允许直接关闭（避免吞掉已下注金币）；如需强制关闭可去掉此判断
        if(manager.State == WitchPotionGameManager.GameState.Playing)
            return;

        base.Close();
        Unsubscribe();
    }
    #endregion

    #region 订阅
    void Subscribe()
    {
        if(subscribed)
            return;
        subscribed = true;
        manager.OnStateChanged += OnStateChanged;
        manager.OnBetChanged += OnBetChanged;
        manager.OnRoundStart += OnRoundStart;
        manager.OnBottleOpened += OnBottleOpened;
        manager.OnCashOutUnlocked += OnCashOutUnlocked;
        manager.OnPayoutChanged += OnPayoutChanged;
        manager.OnRoundEnd += OnRoundEnd;
        manager.OnBlessingChanged += OnBlessingChanged;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        manager.OnStateChanged -= OnStateChanged;
        manager.OnBetChanged -= OnBetChanged;
        manager.OnRoundStart -= OnRoundStart;
        manager.OnBottleOpened -= OnBottleOpened;
        manager.OnCashOutUnlocked -= OnCashOutUnlocked;
        manager.OnPayoutChanged -= OnPayoutChanged;
        manager.OnRoundEnd -= OnRoundEnd;
        manager.OnBlessingChanged -= OnBlessingChanged;
    }
    #endregion

    #region 棋盘构建
    void BuildGrid()
    {
        int total = manager.Config.TotalCount;
        if(bottles.Count == total)
            return;

        for(int i = gridContainer.childCount - 1; i >= 0; i--)
            Destroy(gridContainer.GetChild(i).gameObject);
        bottles.Clear();

        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = manager.Config.Cols;

        for(int i = 0; i < total; i++)
        {
            WitchPotionBottleUI cell = Instantiate(bottlePrefab, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Init(i);
            cell.OnClick += OnBottleClicked;
            bottles.Add(cell);
        }
    }

    void ResetGridVisual()
    {
        foreach(WitchPotionBottleUI cell in bottles)
            cell.SetClosed();
    }

    void SetGridInteractable(bool value)
    {
        foreach(WitchPotionBottleUI cell in bottles)
            cell.SetInteractable(value);
    }
    #endregion

    #region 下注滑条
    void SetupSlider()
    {
        betSlider.wholeNumbers = true;
        betSlider.minValue = manager.Config.MinBet;
        betSlider.maxValue = manager.Config.MaxBet;
        betSlider.SetValueWithoutNotify(manager.Bet);
    }

    void OnSliderChanged(float value)
    {
        manager.SetBet(Mathf.RoundToInt(value));
    }

    void RefreshBetRange()
    {
        betRangeText.SetVar(LocVarSet.WitchPotion.MinBet, manager.Config.MinBet, false);
        betRangeText.SetVar(LocVarSet.WitchPotion.MaxBet, manager.Config.MaxBet);
    }
    #endregion

    #region 视图切换
    // 下注阶段：滑条可用，开始按钮可见，棋盘锁定并复位
    void EnterBettingView()
    {
        ResetGridVisual();
        SetGridInteractable(false);

        betSlider.interactable = true;
        startButton.gameObject.SetActive(true);
        cashOutButton.gameObject.SetActive(false);
        warnTip.Close();

        OnBetChanged(manager.Bet);
        RefreshStats();
    }

    // 进行中：滑条锁定，隐藏开始按钮，显示见好就收（默认不可点，开够安全瓶才解锁）
    void EnterPlayingView()
    {
        SetGridInteractable(true);
        betSlider.interactable = false;
        startButton.gameObject.SetActive(false);
        cashOutButton.gameObject.SetActive(true);
        cashOutButton.interactable = false;
    }
    #endregion

    #region 按钮
    void OnStartButton()
    {
        // 开局失败=金币不足
        if(!manager.StartRound())
        {
            warnTip.ShowTip(LocTableSet.CasinoGame, LocVarSet.MiniGame.NotEnoughGameCoin);
        }
    }

    void OnCashOutButton() => manager.CashOut();

    void OnCloseButton() => Close();

    void OnBottleClicked(int index) => manager.OpenBottle(index);
    #endregion

    #region 管理器事件
    void OnStateChanged(WitchPotionGameManager.GameState s)
    {
        switch(s)
        {
            case WitchPotionGameManager.GameState.Betting:
                EnterBettingView();
                break;
            case WitchPotionGameManager.GameState.Playing:
                EnterPlayingView();
                break;
        }
    }

    void OnBetChanged(int bet)
    {
        if(!Mathf.Approximately(betSlider.value, bet))
            betSlider.SetValueWithoutNotify(bet);
        betValueText.SetVar(LocVarSet.WitchPotion.Bet, bet);
    }

    void OnRoundStart(int total)
    {
        ResetGridVisual();
        RefreshStats();
        RefreshGold();   // 下注已扣除
    }

    void OnBottleOpened(int index, bool poison, int safeOpened, float multiplier, int payout)
    {
        bottles[index].Reveal(poison);
        RefreshRemain();
    }

    void OnCashOutUnlocked() => cashOutButton.interactable = true;

    void OnPayoutChanged(float multiplier, int payout)
    {
        multiplierValueText.SetVar(LocVarSet.WitchPotion.Multiplier, multiplier);
        payoutValueText.SetVar(LocVarSet.WitchPotion.Payout, payout);
    }

    void OnRoundEnd(bool win, int payout, bool blessed)
    {
        SetGridInteractable(false);
        cashOutButton.interactable = false;

        // 结束后可再次下注：放开滑条并显示开始按钮（保留翻开的棋盘供查看）
        betSlider.interactable = true;
        startButton.gameObject.SetActive(true);
        cashOutButton.gameObject.SetActive(false);

        RefreshGold();   // 收益已发放

        ShowSettlePanel(win, payout);
    }

    void OnBlessingChanged(bool active) => blessingTip.SetActive(active);

    // 弹出通用「本局结算」面板：胜负各用一套台词/内容 Key 与占位符
    void ShowSettlePanel(bool win, int payout)
    {
        GameSettlePanel.Data data = new ()
        {
            Avatar = settleAvatar,
            Table = LocTableSet.CasinoGame,
            TitleKey =  "SettleTitle",
            SpeechKey = win ? "WitchPotionSettleWinSpeech" : "WitchPotionSettleLoseSpeech",
            ContentKey = win ? "WitchPotionSettleWinContent" : "WitchPotionSettleLoseContent",
            ContentVars = win
                ? new (string, object)[]
                {
                    (LocVarSet.WitchPotion.Bet, manager.Bet),
                    (LocVarSet.WitchPotion.Opened, manager.SafeOpened),
                    (LocVarSet.WitchPotion.Multiplier, manager.CurrentMultiplier),
                    (LocVarSet.WitchPotion.Payout, payout),
                }
                : new (string, object)[]
                {
                    (LocVarSet.WitchPotion.Bet, manager.Bet),
                    (LocVarSet.WitchPotion.Opened, manager.SafeOpened),
                },
            ItemHintKey = null,   // 女巫毒药奖励为金币（已在内容中体现），无道具提示
            PlayAgainSpCost = manager.Config.PlayAgainSpCost,
            PlayAgainCondition = CanPlayAgain,
            PlayAgainFailTipKey = "SettleNotEnoughStamina",
            OnPlayAgain = OnSettlePlayAgain,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<GameSettlePanel>(UIPanelIdSet.GameSettlePanel).Show(data);
    }

    // 再来一局条件：体力足够（无消耗则恒为 true）。不满足时由结算面板弹 WarnTip
    bool CanPlayAgain()
    {
        return GameDataManager.Instance.GetProperty(PropertyType.Strength).Value >= manager.Config.PlayAgainSpCost;
    }

    // 再来一局：条件已由结算面板校验通过，扣体力后用当前下注重新开局
    void OnSettlePlayAgain()
    {
        UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
        manager.ResetToBetting();
        OnStartButton();
    }

    void OnSettleBack() => UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
    #endregion

    #region 刷新
    void RefreshStats()
    {
        OnBetChanged(manager.Bet);
        RefreshRemain();
        OnPayoutChanged(manager.CurrentMultiplier, manager.CurrentPayout);
    }

    void RefreshRemain()
    {
        remainValueText.SetVar(LocVarSet.WitchPotion.Opened, manager.SafeOpened, false);
        remainValueText.SetVar(LocVarSet.WitchPotion.Total, manager.Config.TotalCount);
    }

    // 刷新"现有金币"显示（金币走 InventoryManager.Instance.GameCoin，需在扣/加后主动刷新）
    void RefreshGold() => goldText.SetVar(LocVarSet.WitchPotion.GameCoin, GameDataManager.Instance.GetProperty(PropertyType.GameCoin).Value);
    #endregion

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("根据配置生成窗口、数值栏、药瓶棋盘、下注滑条、按钮与提示，并自动赋值所有引用与多语言 Key（表：CasinoGame）。可重复点击，旧的生成内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        if(manager == null)
            manager = GetComponent<WitchPotionGameManager>();

        // 清空旧的生成内容（保留根上的脚本与全屏底图）
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        bottles.Clear();

        int cols = manager != null && manager.Config != null ? manager.Config.Cols : 4;
        int rows = manager != null && manager.Config != null ? manager.Config.Rows : 3;

        // 窗口
        Image window = MakeImage("Window", transform, new Color(0.06f, 0.04f, 0.07f, 0.96f));
        Center(window.rectTransform, 1000, 660, 0, 0);

        // 现有金币
        goldText = MakeText("GoldText", window.transform, "WitchPotionGold", 28, Color.white, TextAlignmentOptions.Left, LocVarSet.WitchPotion.GameCoin);
        Anchor(goldText.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), 440, 44, 30, -16);

        // 数值栏：下注金额 / 剩余箱子 / 倍率 / 收益
        RectTransform statsBar = MakeNode("StatsBar", window.transform);
        Anchor(statsBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), 920, 96, 0, -78);
        HorizontalLayoutGroup hlg = statsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        betValueText = MakeStatBox(statsBar, "BetBox", "WitchPotionBetLabel", "WitchPotionBetValue", LocVarSet.WitchPotion.Bet);
        remainValueText = MakeStatBox(statsBar, "RemainBox", "WitchPotionRemainLabel", "WitchPotionRemainValue", LocVarSet.WitchPotion.Opened, LocVarSet.WitchPotion.Total);
        multiplierValueText = MakeStatBox(statsBar, "MultiplierBox", "WitchPotionMultiplierLabel", "WitchPotionMultiplierValue", LocVarSet.WitchPotion.Multiplier);
        payoutValueText = MakeStatBox(statsBar, "PayoutBox", "WitchPotionPayoutLabel", "WitchPotionPayoutValue", LocVarSet.WitchPotion.Payout);

        // 棋盘
        Image gridBg = MakeImage("GridContainer", window.transform, new Color(0.02f, 0.01f, 0.03f, 1f));
        Center(gridBg.rectTransform, cols * 160 + 24, rows * 116 + 24, 0, 26);
        gridContainer = gridBg.rectTransform;
        GridLayoutGroup grid = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 108);
        grid.spacing = new Vector2(8, 8);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        // 药瓶模板（隐藏，运行时克隆）
        bottlePrefab = MakeBottleTemplate(window.transform);

        // 下注滑条 + 区间提示
        LocalizeStringEvent betLabel = MakeText("BetLabel", window.transform, "WitchPotionBetLabel", 26, Color.white, TextAlignmentOptions.Left);
        Anchor(betLabel.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), 300, 36, 60, 184);

        betSlider = MakeSlider("BetSlider", window.transform);
        Anchor((RectTransform)betSlider.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), 880, 24, 0, 168);

        betRangeText = MakeText("BetRangeText", window.transform, "WitchPotionBetRange", 22, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center, LocVarSet.WitchPotion.MinBet, LocVarSet.WitchPotion.MaxBet);
        Anchor(betRangeText.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), 600, 30, 0, 138);

        // 按钮
        startButton = MakeButton("StartButton", window.transform, "WitchPotionStartGame", new Color(0.93f, 0.2f, 0.45f));
        Anchor((RectTransform)startButton.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), 280, 72, 0, 50);

        cashOutButton = MakeButton("CashOutButton", window.transform, "WitchPotionCashOut", new Color(0.93f, 0.2f, 0.45f));
        Anchor((RectTransform)cashOutButton.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), 280, 72, 0, 50);
        cashOutButton.gameObject.SetActive(false);

        closeButton = MakeButton("CloseButton", window.transform, "WitchPotionBack", new Color(0.9f, 0.9f, 0.9f, 1f));
        Anchor((RectTransform)closeButton.transform, new Vector2(1, 0), new Vector2(1, 0), 180, 64, -30, 36);

        // 提示
        LocalizeStringEvent blessing = MakeText("BlessingTip", window.transform, "WitchPotionBlessing", 26, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center);
        Anchor(blessing.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), 600, 40, 0, -150);
        blessingTip = blessing.gameObject;
        blessingTip.SetActive(false);

        EditorUtility.SetDirty(this);
        Debug.Log("[WitchPoisonPanel] 界面已生成，请按需调整样式/位置。", this);
    }

    // ===== 生成辅助 =====
    static RectTransform MakeNode(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image MakeImage(string name, Transform parent, Color color)
    {
        RectTransform rt = MakeNode(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    LocalizeStringEvent MakeText(string name, Transform parent, string key, int fontSize, Color color, TextAlignmentOptions align, params string[] vars)
    {
        RectTransform rt = MakeNode(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        LocalizeStringEvent lse = rt.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.SetReference(LocTableSet.CasinoGame, key);
        UnityAction<string> setText = tmp.SetText;
        UnityEventTools.AddPersistentListener(lse.OnUpdateString, setText);

        // 预置占位符默认值并序列化进场景：否则编辑器/首帧 OnEnable 时 SmartFormat 找不到 {变量} 会抛 FormattingException
        foreach(string v in vars)
            lse.SetVar(v, 0, false);
        return lse;
    }

    // 数值框：上方标签 + 下方数值，返回数值文本的 LocalizeStringEvent
    LocalizeStringEvent MakeStatBox(Transform parent, string name, string labelKey, string valueKey, params string[] valueVars)
    {
        Image box = MakeImage(name, parent, new Color(0.16f, 0.13f, 0.18f, 1f));
        VerticalLayoutGroup vlg = box.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = true;
        vlg.padding = new RectOffset(6, 6, 6, 6);

        MakeText(name + "Label", box.transform, labelKey, 22, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);
        return MakeText(name + "Value", box.transform, valueKey, 30, Color.white, TextAlignmentOptions.Center, valueVars);
    }

    Button MakeButton(string name, Transform parent, string labelKey, Color color)
    {
        Image img = MakeImage(name, parent, color);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        LocalizeStringEvent label = MakeText(name + "Text", img.transform, labelKey, 28, Color.white, TextAlignmentOptions.Center);
        Stretch(label.GetComponent<RectTransform>());
        return btn;
    }

    Slider MakeSlider(string name, Transform parent)
    {
        RectTransform rt = MakeNode(name, parent);
        Slider slider = rt.gameObject.AddComponent<Slider>();

        Image bg = MakeImage("Background", rt, new Color(0.3f, 0.25f, 0.3f));
        Stretch(bg.rectTransform);

        RectTransform fillArea = MakeNode("Fill Area", rt);
        Stretch(fillArea);
        Image fill = MakeImage("Fill", fillArea, new Color(0.95f, 0.9f, 0.45f));
        Stretch(fill.rectTransform);

        RectTransform handleArea = MakeNode("Handle Slide Area", rt);
        Stretch(handleArea);
        Image handle = MakeImage("Handle", handleArea, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(20, 0);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    WitchPotionBottleUI MakeBottleTemplate(Transform parent)
    {
        Image bg = MakeImage("BottleTemplate", parent, new Color(0.85f, 0.85f, 0.85f));
        Button btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        WitchPotionBottleUI cell = bg.gameObject.AddComponent<WitchPotionBottleUI>();

        Image icon = MakeImage("Icon", bg.transform, Color.white);
        Stretch(icon.rectTransform);
        icon.enabled = false;

        // 通过 SerializedObject 赋值 WitchPotionBottleUI 的私有 [SerializeField]
        SerializedObject so = new SerializedObject(cell);
        so.FindProperty("button").objectReferenceValue = btn;
        so.FindProperty("bg").objectReferenceValue = bg;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.ApplyModifiedProperties();

        bg.gameObject.SetActive(false);   // 模板隐藏，运行时克隆并激活
        return cell;
    }

    // ===== 布局辅助 =====
    // 锚点定位：anchorMin/Max + 尺寸 + 相对锚点的偏移
    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, float w, float h, float x, float y)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = aMin;   // 以锚点为枢轴，偏移直观
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    // 居中定位
    static void Center(RectTransform rt, float w, float h, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    // 全拉伸填满父级
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
    #endregion
#endif
}
