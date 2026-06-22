using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// 爆点冲刺小游戏界面：顶部数值栏（下注/预期获得/倍率上限）、中心超大倍率、倍率进度条、下注滑条、开始 / 收手 / 返回。
/// 逻辑全部由 <see cref="CrashSprintGameManager"/> 处理，本类只负责 UI 与输入。
/// </summary>
[RequireComponent(typeof(CrashSprintGameManager))]
public class CrashSprintPanel : UIBase
{
    [SerializeField] CrashSprintGameManager manager;

    [Title("数值栏")]
    [LabelText("下注金额数值")][SerializeField] TMP_Text betValueText;
    [LabelText("预期获得数值")][SerializeField] TMP_Text expectValueText;
    [LabelText("倍率上限数值")][SerializeField] TMP_Text capValueText;

    [Title("中心倍率")]
    [LabelText("超大倍率文本")][SerializeField] TMP_Text bigMultiplierText;
    [LabelText("倍率进度条填充")][SerializeField] Image progressFill;

    [Title("下注")]
    [SerializeField] Slider betSlider;
    [LabelText("下注区间提示")][SerializeField] LocalizeStringEvent betRangeText;   // "最低金额{MinBet} 最高金额{MaxBet}"

    [Title("提示 / 结算")]
    [LabelText("余额不足提示")][SerializeField] WarnTip warnTip;
    [LabelText("结算面板头像")][SerializeField] Sprite settleAvatar;

    [Title("按钮")]
    [SerializeField] Button startButton;     // 开始
    [SerializeField] Button cashOutButton;   // 收手（止盈）
    [SerializeField] Button closeButton;     // 返回

    // 倍率越高，中心数字与「收手」按钮颜色越深（压迫感）
    static readonly Color BigLight = new Color(1f, 0.45f, 0.75f);
    static readonly Color BigDeep = new Color(0.86f, 0.04f, 0.34f);
    static readonly Color BtnLight = new Color(0.95f, 0.45f, 0.70f);
    static readonly Color BtnDeep = new Color(0.70f, 0.04f, 0.30f);

    bool subscribed;

    #region 生命周期
    public override void Init()
    {
        if(manager == null)
            manager = GetComponent<CrashSprintGameManager>();

        startButton.onClick.AddListener(OnStartButton);
        cashOutButton.onClick.AddListener(OnCashOutButton);
        closeButton.onClick.AddListener(OnCloseButton);
        betSlider.onValueChanged.AddListener(OnSliderChanged);

        SetupSlider();
        Subscribe();
        RefreshBetRange();
        capValueText.text = manager.Config.MaxMultiplier.ToString("0.00") + "X";
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        manager.ResetToBetting();
        EnterBettingView();
    }

    public override void Close()
    {
        // 进行中不允许关闭（避免吞掉已下注金币、让倍率在隐藏状态停滞）
        if(manager.State == CrashSprintGameManager.GameState.Playing)
            return;

        base.Close();
        Unsubscribe();
    }

    // 倍率上涨阶段每帧刷新中心数字、进度条与预期收益
    void Update()
    {
        if(manager.State != CrashSprintGameManager.GameState.Playing)
            return;

        SetMultiplierDisplay(manager.CurrentMultiplier);
        expectValueText.text = manager.ExpectedPayout.ToString();
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
        manager.OnRoundEnd += OnRoundEnd;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        manager.OnStateChanged -= OnStateChanged;
        manager.OnBetChanged -= OnBetChanged;
        manager.OnRoundStart -= OnRoundStart;
        manager.OnRoundEnd -= OnRoundEnd;
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

    void OnSliderChanged(float value) => manager.SetBet(Mathf.RoundToInt(value));

    void RefreshBetRange()
    {
        betRangeText.SetVar(LocalizeVarSet.CrashSprint.MinBet, manager.Config.MinBet, false);
        betRangeText.SetVar(LocalizeVarSet.CrashSprint.MaxBet, manager.Config.MaxBet);
    }
    #endregion

    #region 视图切换
    // 下注阶段：滑条可用，开始按钮可见，中心倍率归零
    void EnterBettingView()
    {
        betSlider.interactable = true;
        startButton.gameObject.SetActive(true);
        cashOutButton.gameObject.SetActive(false);
        warnTip.Close();

        OnBetChanged(manager.Bet);
        SetMultiplierDisplay(0f);
        expectValueText.text = "0";
    }

    // 进行中：滑条锁定，隐藏开始，显示常驻可点的「收手」
    void EnterPlayingView()
    {
        betSlider.interactable = false;
        startButton.gameObject.SetActive(false);
        cashOutButton.gameObject.SetActive(true);
        cashOutButton.interactable = true;
    }
    #endregion

    #region 按钮
    // 开始前先校验：钱不足 / 体力不足各弹对应提示，满足才由管理器扣费并开局
    void OnStartButton()
    {
        switch(manager.StartRound())
        {
            case CrashSprintGameManager.StartCondition.NotEnoughMoney:
                warnTip.ShowTip(LocalizeTableSet.CasinoGame, LocalizeVarSet.MiniGame.NotEnoughGameCoin);
                break;
            case CrashSprintGameManager.StartCondition.NotEnoughStamina:
                warnTip.ShowTip(LocalizeTableSet.CasinoGame, LocalizeVarSet.MiniGame.NotEnoughStamina);
                break;
        }
    }

    void OnCashOutButton() => manager.CashOut();

    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion

    #region 管理器事件
    void OnStateChanged(CrashSprintGameManager.GameState s)
    {
        switch(s)
        {
            case CrashSprintGameManager.GameState.Betting:
                EnterBettingView();
                break;
            case CrashSprintGameManager.GameState.Playing:
                EnterPlayingView();
                break;
        }
    }

    void OnBetChanged(int bet)
    {
        if(!Mathf.Approximately(betSlider.value, bet))
            betSlider.SetValueWithoutNotify(bet);
        betValueText.text = bet.ToString();
    }

    void OnRoundStart()
    {
        SetMultiplierDisplay(0f);
        expectValueText.text = "0";
    }

    void OnRoundEnd(bool win, float stopMul, float crash, int payout)
    {
        // 锁定显示：成功停在止盈倍率，失败停在爆点
        SetMultiplierDisplay(win ? stopMul : crash);
        expectValueText.text = (win ? payout : 0).ToString();

        cashOutButton.interactable = false;
        cashOutButton.gameObject.SetActive(false);
        startButton.gameObject.SetActive(true);
        betSlider.interactable = true;

        ShowSettlePanel(win, stopMul, crash, payout);
    }
    #endregion

    #region 显示
    // 中心倍率文本 + 进度条 + 颜色（倍率越高越深）
    void SetMultiplierDisplay(float m)
    {
        float max = manager.Config.MaxMultiplier;
        float t = max > 0f ? Mathf.Clamp01(m / max) : 0f;

        bigMultiplierText.text = m.ToString("0.00") + "X";
        bigMultiplierText.color = Color.Lerp(BigLight, BigDeep, t);

        progressFill.fillAmount = t;

        if(cashOutButton.targetGraphic is Image img)
            img.color = Color.Lerp(BtnLight, BtnDeep, t);
    }
    #endregion

    #region 结算
    // 弹出通用「本局结算」面板：胜负各用一套台词/内容 Key 与占位符（爆点结算时公开）
    void ShowSettlePanel(bool win, float stopMul, float crash, int payout)
    {
        GameSettlePanel.Data data = new ()
        {
            Avatar = settleAvatar,
            Table = LocalizeTableSet.CasinoGame,
            TitleKey = "SettleTitle",
            SpeechKey = win ? "CrashSprintSettleWinSpeech" : "CrashSprintSettleLoseSpeech",
            ContentKey = win ? "CrashSprintSettleWinContent" : "CrashSprintSettleLoseContent",
            ContentVars = new (string, object)[]
            {
                (LocalizeVarSet.CrashSprint.Bet, manager.Bet),
                (LocalizeVarSet.CrashSprint.Multiplier, (win ? stopMul : 0f).ToString("0.00")),
                (LocalizeVarSet.CrashSprint.Crash, crash.ToString("0.00")),
                (LocalizeVarSet.CrashSprint.Payout, payout),
            },
            ItemHintKey = null,
            PlayAgainSpCost = manager.Config.PlayAgainSpCost,
            // 再来一局只是回到下注阶段（免费）：不在此校验体力，扣费与校验统一交给「开始」按钮
            PlayAgainCondition = null,
            PlayAgainFailTipKey = null,
            OnPlayAgain = OnSettlePlayAgain,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<GameSettlePanel>(UIPanelIdSet.GameSettlePanel).Show(data);
    }

    // 再来一局：不直接开局，关闭结算面板并回到下注阶段（开始按钮可见、下注可调），由「开始」重新校验扣费
    void OnSettlePlayAgain()
    {
        UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
        manager.ResetToBetting();
    }

    void OnSettleBack() => UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
    #endregion

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("根据规则生成窗口、数值栏（下注/预期获得/倍率上限）、中心超大倍率、倍率进度条、下注滑条、开始/收手/返回按钮，并自动赋值所有引用与多语言 Key（表：CasinoGame）。可重复点击，旧的生成内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        if(manager == null)
            manager = GetComponent<CrashSprintGameManager>();

        // 清空旧的生成内容（保留根上的脚本与全屏底图）
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // 窗口
        Image window = MakeImage("Window", transform, new Color(0.07f, 0.05f, 0.08f, 0.96f));
        Center(window.rectTransform, 1040, 640, 0, 0);

        // 顶部数值栏：下注金额 / 预期获得 / 倍率上限
        RectTransform statsBar = MakeNode("StatsBar", window.transform);
        Anchor(statsBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), 960, 110, 0, -24);
        HorizontalLayoutGroup hlg = statsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        betValueText = MakeStatBox(statsBar, "BetBox", "CrashSprintBetLabel", "0", new Color(1f, 0.78f, 0.25f));
        expectValueText = MakeStatBox(statsBar, "ExpectBox", "CrashSprintExpectLabel", "0", new Color(0.55f, 0.7f, 1f));
        capValueText = MakeStatBox(statsBar, "CapBox", "CrashSprintCapLabel", "5.00X", new Color(0.85f, 0.6f, 1f));

        // 中心超大倍率
        bigMultiplierText = MakePlainText("BigMultiplier", window.transform, "0.00X", 150, BigLight, TextAlignmentOptions.Center);
        Center(bigMultiplierText.rectTransform, 900, 240, 0, 70);
        bigMultiplierText.fontStyle = FontStyles.Bold;

        // 倍率进度条
        progressFill = MakeProgressBar("ProgressBar", window.transform);
        Center((RectTransform)progressFill.transform.parent, 900, 26, 0, -70);

        // 下注滑条 + 区间提示
        LocalizeStringEvent betLabel = MakeLocalizedText("BetSliderLabel", window.transform, "CrashSprintBetLabel", 24, Color.white, TextAlignmentOptions.Left);
        Anchor(betLabel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 900, 32, 0, -118);

        betSlider = MakeSlider("BetSlider", window.transform);
        Anchor((RectTransform)betSlider.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 900, 22, 0, -150);

        betRangeText = MakeLocalizedText("BetRangeText", window.transform, "CrashSprintBetRange", 22, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center, LocalizeVarSet.CrashSprint.MinBet, LocalizeVarSet.CrashSprint.MaxBet);
        Anchor(betRangeText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 700, 28, 0, -182);

        // 按钮：开始 / 收手
        startButton = MakeButton("StartButton", window.transform, "CrashSprintStart", new Color(0.95f, 0.95f, 0.95f), Color.black);
        Center((RectTransform)startButton.transform, 260, 76, -150, -250);

        cashOutButton = MakeButton("CashOutButton", window.transform, "CrashSprintCashOut", BtnLight, Color.white);
        Center((RectTransform)cashOutButton.transform, 260, 76, 150, -250);
        cashOutButton.gameObject.SetActive(false);

        // 返回（右下角）
        closeButton = MakeButton("CloseButton", window.transform, "CrashSprintBack", new Color(0.9f, 0.9f, 0.9f), Color.black);
        Anchor((RectTransform)closeButton.transform, new Vector2(1, 0), new Vector2(1, 0), 170, 62, -30, 34);

        EditorUtility.SetDirty(this);
        Debug.Log("[CrashSprintPanel] 界面已生成", this);
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

    // 纯文本（数值/中心倍率，运行时直接赋 text，无需多语言）
    TMP_Text MakePlainText(string name, Transform parent, string text, int fontSize, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = MakeNode(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // 多语言文本（标签/按钮/区间提示）
    LocalizeStringEvent MakeLocalizedText(string name, Transform parent, string key, int fontSize, Color color, TextAlignmentOptions align, params string[] vars)
    {
        TextMeshProUGUI tmp = (TextMeshProUGUI)MakePlainText(name, parent, string.Empty, fontSize, color, align);

        LocalizeStringEvent lse = tmp.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.SetReference(LocalizeTableSet.CasinoGame, key);
        UnityAction<string> setText = tmp.SetText;
        UnityEventTools.AddPersistentListener(lse.OnUpdateString, setText);

        // 预置占位符默认值并序列化进场景：否则编辑器/首帧 OnEnable 时 SmartFormat 找不到 {变量} 会抛 FormattingException
        foreach(string v in vars)
            lse.SetVar(v, 0, false);
        return lse;
    }

    // 数值框：上方多语言标签 + 下方纯文本数值，返回数值文本
    TMP_Text MakeStatBox(Transform parent, string name, string labelKey, string initialValue, Color valueColor)
    {
        Image box = MakeImage(name, parent, new Color(0.16f, 0.13f, 0.18f, 0.95f));
        VerticalLayoutGroup vlg = box.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = true;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 2;

        MakeLocalizedText(name + "Label", box.transform, labelKey, 22, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Center);
        return MakePlainText(name + "Value", box.transform, initialValue, 36, valueColor, TextAlignmentOptions.Center);
    }

    Button MakeButton(string name, Transform parent, string labelKey, Color bgColor, Color textColor)
    {
        Image img = MakeImage(name, parent, bgColor);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        LocalizeStringEvent label = MakeLocalizedText(name + "Text", img.transform, labelKey, 30, textColor, TextAlignmentOptions.Center);
        Stretch(label.GetComponent<RectTransform>());
        return btn;
    }

    Slider MakeSlider(string name, Transform parent)
    {
        RectTransform rt = MakeNode(name, parent);
        Slider slider = rt.gameObject.AddComponent<Slider>();

        Image bg = MakeImage("Background", rt, new Color(0.3f, 0.27f, 0.3f));
        Stretch(bg.rectTransform);

        RectTransform fillArea = MakeNode("Fill Area", rt);
        Stretch(fillArea);
        Image fill = MakeImage("Fill", fillArea, new Color(0.95f, 0.9f, 0.45f));
        Stretch(fill.rectTransform);

        RectTransform handleArea = MakeNode("Handle Slide Area", rt);
        Stretch(handleArea);
        Image handle = MakeImage("Handle", handleArea, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(22, 0);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    // 倍率进度条：底图 + 横向 Filled 填充，返回填充 Image（运行时设 fillAmount）
    Image MakeProgressBar(string name, Transform parent)
    {
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        Image bg = MakeImage(name, parent, new Color(0.22f, 0.18f, 0.24f, 0.95f));
        bg.sprite = uiSprite;
        bg.type = Image.Type.Sliced;

        Image fill = MakeImage("Fill", bg.transform, new Color(0.95f, 0.45f, 0.7f));
        Stretch(fill.rectTransform);
        fill.sprite = uiSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        return fill;
    }

    // ===== 布局辅助 =====
    // 锚点定位：anchorMin/Max + 尺寸 + 相对锚点的偏移
    static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, float w, float h, float x, float y)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = aMin;
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
