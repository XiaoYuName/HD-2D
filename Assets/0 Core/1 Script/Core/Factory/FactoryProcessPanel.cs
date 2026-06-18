using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
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
    [LabelText("倒计时数值")][SerializeField] TMP_Text timerValueText;

    [Title("传送带")]
    [LabelText("产品容器(铺满传送带宽)")][SerializeField] RectTransform itemContainer;
    [LabelText("产品模板(隐藏)")][SerializeField] FactoryItemView itemTemplate;
    [LabelText("下压区标记")][SerializeField] RectTransform pressZoneRtf;
    [LabelText("下压器(锤)")][SerializeField] RectTransform stampRtf;
    [LabelText("灯带(成功/失败闪烁)")][SerializeField] Image lightStrip;

    [Title("反馈 / 提示")]
    [LabelText("下压判定飘字(多语言)")][SerializeField] LocalizeStringEvent feedbackLse;
    [LabelText("下压判定飘字(本体)")][SerializeField] TMP_Text feedbackText;
    [LabelText("体力不足提示")][SerializeField] WarnTip notEnoughStaminaTip;
    [LabelText("结算面板头像")][SerializeField] Sprite settleAvatar;

    [Title("按钮")]
    [LabelText("结束本局")][SerializeField] Button endRoundButton;
    [LabelText("返回")][SerializeField] Button closeButton;

    static readonly Color GoodColor = new (0.2f, 0.75f, 0.35f);
    static readonly Color OkColor = new (0.85f, 0.65f, 0.2f);
    static readonly Color BadColor = new (0.85f, 0.2f, 0.2f);

    readonly Dictionary<int, FactoryItemView> activeViews = new ();
    readonly Stack<FactoryItemView> viewPool = new ();
    readonly HashSet<int> liveIds = new ();
    readonly List<int> goneIds = new ();
    Vector2 stampHomePos;
    Coroutine stampCt;
    Coroutine feedbackCt;
    Coroutine lightCt;
    bool subscribed;

    #region 生命周期
    public override void Init()
    {
        if(manager == null)
            manager = GetComponent<FactoryProcessGameManager>();

        endRoundButton.onClick.AddListener(OnEndRoundButton);
        closeButton.onClick.AddListener(OnCloseButton);

        itemTemplate.gameObject.SetActive(false);
        stampHomePos = stampRtf.anchoredPosition;
        feedbackText.gameObject.SetActive(false);
        Subscribe();
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        LayoutBelt();

        if(!manager.StartRound())
            notEnoughStaminaTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Process.NotEnoughStamina);
    }

    public override void Close()
    {
        // 进行中不允许关闭（避免吞掉已扣体力、让倒计时在隐藏状态停滞）
        if(manager.State == FactoryProcessGameManager.GameState.Playing)
            return;

        base.Close();
        Unsubscribe();
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
        manager.OnTimeChanged += RefreshTimer;
        manager.OnRoundEnd += OnRoundEnd;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        manager.OnStateChanged -= OnStateChanged;
        manager.OnScoreChanged -= RefreshStats;
        manager.OnTimeChanged -= RefreshTimer;
        manager.OnRoundEnd -= OnRoundEnd;
    }
    #endregion

    #region 传送带布局与产品同步
    // 按配置把下压区标记与下压器对齐到归一化中心 / 宽度（容器实际宽度需运行时取）
    void LayoutBelt()
    {
        float w = itemContainer.rect.width;
        float centerX = (manager.Config.PressCenter - 0.5f) * w;
        pressZoneRtf.anchoredPosition = new Vector2(centerX, pressZoneRtf.anchoredPosition.y);
        pressZoneRtf.sizeDelta = new Vector2(manager.Config.PressHalfWidth * 2f * w, pressZoneRtf.sizeDelta.y);
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
                view = SpawnView(it);
                activeViews.Add(it.Id, view);
            }
            view.rtf.anchoredPosition = new Vector2((it.Pos - 0.5f) * w, 0f);
            if(it.Resolved)
                view.SetResolved();
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

    FactoryItemView SpawnView(FactoryProcessGameManager.Item it)
    {
        FactoryItemView view = viewPool.Count > 0
            ? viewPool.Pop()
            : Instantiate(itemTemplate, itemContainer);
        view.gameObject.SetActive(true);
        view.SetData(it.Qualified);
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
            case FactoryProcessGameManager.PressResult.Good:
                ShowFeedback(FactoryLocKeySet.Process.Good, GoodColor);
                FlashLight(GoodColor);
                break;
            case FactoryProcessGameManager.PressResult.Ok:
                ShowFeedback(FactoryLocKeySet.Process.Ok, OkColor);
                FlashLight(OkColor);
                break;
            case FactoryProcessGameManager.PressResult.Bad:
                ShowFeedback(FactoryLocKeySet.Process.Bad, BadColor);
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

    // 下压器快速下探再回位
    IEnumerator PlayStampIE()
    {
        const float downDist = 60f;
        const float dur = 0.08f;
        Vector2 down = stampHomePos + Vector2.down * downDist;

        for(float t = 0; t < dur; t += Time.deltaTime)
        {
            stampRtf.anchoredPosition = Vector2.Lerp(stampHomePos, down, t / dur);
            yield return null;
        }
        for(float t = 0; t < dur; t += Time.deltaTime)
        {
            stampRtf.anchoredPosition = Vector2.Lerp(down, stampHomePos, t / dur);
            yield return null;
        }
        stampRtf.anchoredPosition = stampHomePos;
    }

    void ShowFeedback(string key, Color color)
    {
        if(feedbackCt != null)
            StopCoroutine(feedbackCt);
        feedbackText.gameObject.SetActive(true);
        feedbackLse.SetText(LocalizeTableSet.Factory, key);
        feedbackText.color = color;
        feedbackCt = StartCoroutine(HideFeedbackIE());
    }

    IEnumerator HideFeedbackIE()
    {
        yield return new WaitForSeconds(0.5f);
        feedbackText.gameObject.SetActive(false);
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

    void RefreshTimer(float time) => timerValueText.text = Mathf.CeilToInt(time) + "s";
    #endregion

    #region 管理器事件
    void OnStateChanged(FactoryProcessGameManager.GameState s)
    {
        if(s == FactoryProcessGameManager.GameState.Playing)
        {
            ClearViews();
            stampRtf.anchoredPosition = stampHomePos;
            feedbackText.gameObject.SetActive(false);
            RefreshStats();
        }
    }

    void OnRoundEnd(int score, int success, int fail, float completion, int reward)
    {
        ClearViews();
        ShowSettlePanel(score, success, fail, completion, reward);
    }
    #endregion

    #region 按钮
    void OnEndRoundButton() => manager.EndRound();
    void OnCloseButton() => UISystem.Instance.CloseUI(uiname);
    #endregion

    #region 结算
    void ShowSettlePanel(int score, int success, int fail, float completion, int reward)
    {
        GameSettlePanel.Data data = new ()
        {
            Avatar = settleAvatar,
            Table = LocalizeTableSet.Factory,
            TitleKey = FactoryLocKeySet.Process.SettleTitle,
            SpeechKey = FactoryLocKeySet.Process.SettleSpeech,
            ContentKey = FactoryLocKeySet.Process.SettleContent,
            ContentVars = new (string, object)[]
            {
                (LocalizeVarSet.FactoryProcess.Score, score),
                (LocalizeVarSet.FactoryProcess.Success, success),
                (LocalizeVarSet.FactoryProcess.Fail, fail),
                (LocalizeVarSet.FactoryProcess.Completion, Mathf.RoundToInt(completion * 100f)),
                (LocalizeVarSet.FactoryProcess.Reward, reward),
            },
            ItemHintKey = null,
            PlayAgainSpCost = manager.Config.StartSpCost,
            PlayAgainCondition = manager.CanStartRound,
            PlayAgainFailTipKey = FactoryLocKeySet.Process.NotEnoughStamina,
            OnPlayAgain = OnSettlePlayAgain,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<GameSettlePanel>(UIPanelIdSet.GameSettlePanel).Show(data);
    }

    // 再来一局：条件已由结算面板校验，扣体力由 StartRound 内部处理
    void OnSettlePlayAgain()
    {
        UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
        manager.ResetToReady();
        manager.StartRound();
    }

    void OnSettleBack() => UISystem.Instance.CloseUI(UIPanelIdSet.GameSettlePanel);
    #endregion
}
