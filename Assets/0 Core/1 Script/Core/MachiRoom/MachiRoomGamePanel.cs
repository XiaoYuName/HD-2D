using PrimeTween;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class MachiRoomGamePanel : UIBase
{
    enum State
    {
        None,
        Panel1Select,
        Panel2PaintDraft,
        Panel3Decide,
        Panel4PaintProcess,
        Panel5PaintComplete,
    }

    [FoldoutGroup("Bg"), LabelText("关闭按钮"), SerializeField] Button closeButton;
    [FoldoutGroup("Bg"), LabelText("时间段"), SerializeField] LocalizeStringEvent timePeriodText;
    [FoldoutGroup("Bg"), SerializeField] TextMeshProUGUI spCountText, favorCountText, inspireCountText, presureCountText;
    [FoldoutGroup("Bg"), SerializeField] LocalizeStringEvent leftUpStateText;
    [FoldoutGroup("Bg"), SerializeField] GameObject specialDrawingDraftTip;
    [FoldoutGroup("1"), LabelText("选择面板"), SerializeField] GameObject panel1Select;
    [FoldoutGroup("1"), LabelText("消耗灵感按钮"), SerializeField] Button[] selectButtons;
    [FoldoutGroup("1"), LabelText("消耗灵感文本"), SerializeField] LocalizeStringEvent[] selectButtonTexts;
    [FoldoutGroup("1"), LabelText("灵感不足提示"), SerializeField] CanvasGroup inspirationNotEnoughTip;
    [FoldoutGroup("1"), LabelText("灵感不足提示Text"), SerializeField] LocalizeStringEvent inspirationNotEnoughText;

    [FoldoutGroup("2"), LabelText("绘制草稿面板"), SerializeField] GameObject panel2PaintDraft;
    [FoldoutGroup("2"), SerializeField] MachiRoomScratchTicket scratchTicket;
    [FoldoutGroup("2"), SerializeField] Image paintContentImage;

    [FoldoutGroup("3"), LabelText("稿件决定面板"), SerializeField] GameObject panel3Decide;
    [FoldoutGroup("3"), SerializeField] Image decideArtworkImage;
    [FoldoutGroup("3"), LabelText("画作闪光"), SerializeField] MachiRoomSparkleEffect decideSparkleEffect;
    [FoldoutGroup("3"), SerializeField] TextMeshProUGUI qualityText;
    [FoldoutGroup("3"), LabelText("特殊稿件标记"), SerializeField] GameObject specialTag;
    [FoldoutGroup("3"), SerializeField] Button abandonButton;
    [FoldoutGroup("3"), SerializeField] Button rerollButton;
    [FoldoutGroup("3"), SerializeField] LocalizeStringEvent rerollCostText;
    [FoldoutGroup("3"), SerializeField] Button startPaintingButton;
    [FoldoutGroup("3"), SerializeField] WarnTip panel3DecideDraftWarnTip;

    [FoldoutGroup("4"), LabelText("绘制进度面板"), SerializeField] GameObject panel4PaintProcess;
    [FoldoutGroup("4"), SerializeField] Image processArtworkImage;
    [FoldoutGroup("4"), SerializeField] Image progressBar;
    [FoldoutGroup("4"), SerializeField] TextMeshProUGUI progressText;
    [FoldoutGroup("4"), SerializeField] LocalizeStringEvent scoreText;
    [FoldoutGroup("4"), SerializeField] TextMeshProUGUI gradeText;
    [FoldoutGroup("4"), SerializeField] Button rushButton;
    [FoldoutGroup("4"), SerializeField] LocalizeStringEvent rushButtonText;
    [FoldoutGroup("4"), SerializeField] LocalizeStringEvent rushCostText;
    [FoldoutGroup("4"), LabelText("括号完成提示"), SerializeField] GameObject completeTipText;
    [FoldoutGroup("4"), LabelText("放弃出图按钮"), SerializeField] Button trashButton;
    [FoldoutGroup("4"), LabelText("马吉不在工作室提示"), SerializeField] GameObject machiNotInStudioTip;
    [FoldoutGroup("4"), SerializeField] WarnTip panel4PaintProcessWarnTip;

    [FoldoutGroup("5"), LabelText("绘制完成面板"), SerializeField] MachiRoomPaintResultState paintResult;

    [FoldoutGroup("Runtime"), SerializeField, ReadOnly] State curState;

    Sequence rushSeq;
    Coroutine inspirationTipCt;
    bool isPlayingScratchTicket;
    bool isPlayingRushAnimation;

    const string LocKeyPrefix = "MachiRoom/";
    const string NewCanvas = LocKeyPrefix + nameof(NewCanvas);
    const string DraftProgress = LocKeyPrefix + nameof(DraftProgress);
    const string MachiNotInStudio = LocKeyPrefix + nameof(MachiNotInStudio);
    const string InspirationNotEnough = LocKeyPrefix + nameof(InspirationNotEnough);
    const string MachiBadState = LocKeyPrefix + nameof(MachiBadState);
    const string ActionPointNotEnough = LocKeyPrefix + nameof(ActionPointNotEnough);
    const string SpecialDraft = LocKeyPrefix + nameof(SpecialDraft);
    const string SpecialDraftCreating = LocKeyPrefix + nameof(SpecialDraftCreating);
    const string PaintComplete = LocKeyPrefix + nameof(PaintComplete);
    const string FinishDraft = LocKeyPrefix + nameof(FinishDraft);
    const string UrgeDraft = LocKeyPrefix + nameof(UrgeDraft);
    const string Completed = LocKeyPrefix + nameof(Completed);

    static readonly Dictionary<State, string> stateTextKeyDict = new()
    {
        [State.Panel1Select] = NewCanvas,
        [State.Panel2PaintDraft] = NewCanvas,
        [State.Panel4PaintProcess] = DraftProgress,
        [State.Panel5PaintComplete] = NewCanvas,
    };

    #region LifeCycle
    [Button]
    void Test()
    {
        Init();
        Open();
    }
    public override void Init()
    {
        closeButton.onClick.AddListener(OnCloseButton);
        abandonButton.onClick.AddListener(OnAbandonButton);
        rerollButton.onClick.AddListener(OnRerollButton);
        startPaintingButton.onClick.AddListener(OnStartPaintingButton);
        rushButton.onClick.AddListener(OnRushButton);
        trashButton.onClick.AddListener(OnTrashButton);

        for (int i = 0; i < selectButtons.Length; i++)
        {
            int index = i;
            selectButtonTexts[i].SetVar(
                LocVarSet.Count,
                MachiRoomGameManager.Instance.Config.DraftInspirationCosts[i]);
            selectButtons[i].onClick.AddListener(() => OnSelectButton(index));
        }

        rushCostText.SetVar(
            LocVarSet.Count,
            MachiRoomGameManager.Instance.Config.RushActionPointCost);
        inspirationNotEnoughTip.alpha = 0f;
    }

    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataTimeSlotChange(OnTimePeriodUpdate);
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataUpdate);
        CharacterManager.Instance.RegisterCharacterBagChange(CharaIdSet1.Machi, OnMachiCharacterBagUpdate);
        MachiRoomGameManager.Instance.AddCreationChangedListener(OnCreationChanged);
    }

    public override void Close()
    {
        GameDataManager.Instance.UnregisterPlayerDataTimeSlotChange(OnTimePeriodUpdate);
        GameDataManager.Instance.UnregisterPlayerDataChange(OnPlayerDataUpdate);
        CharacterManager.Instance.UnregisterCharacterBagChange(CharaIdSet1.Machi, OnMachiCharacterBagUpdate);
        MachiRoomGameManager.Instance.RemoveCreationChangedListener(OnCreationChanged);
        scratchTicket.StopScratch();
        isPlayingScratchTicket = false;
        rushSeq.Stop();
        isPlayingRushAnimation = false;
        if (inspirationTipCt != null)
        {
            StopCoroutine(inspirationTipCt);
            inspirationTipCt = null;
        }
        base.Close();
    }

    #endregion

    #region Event

    void OnSelectButton(int index)
    {
        int inspirationCost = MachiRoomGameManager.Instance.Config.DraftInspirationCosts[index];
        isPlayingScratchTicket = true;
        if (!MachiRoomGameManager.Instance.CanStartDraft(
                inspirationCost,
                out MachiRoomDraftActionResult result))
        {
            isPlayingScratchTicket = false;
            ShowDraftFailureTip(result);
            return;
        }

        MachiRoomGameManager.Instance.StartDraft(inspirationCost);
        StartScratchTicket();
    }

    void OnAbandonButton()
    {
        MachiRoomGameManager.Instance.AbandonDraft();
    }

    void OnRerollButton()
    {
        isPlayingScratchTicket = true;
        if (!MachiRoomGameManager.Instance.CanRerollDraft(
                out MachiRoomDraftActionResult result))
        {
            isPlayingScratchTicket = false;
            ShowDraftFailureTipPanel3(result);
            return;
        }

        MachiRoomGameManager.Instance.StartRerollDraft();
        StartScratchTicket();
    }

    void OnStartPaintingButton()
    {
        MachiRoomGameManager.Instance.StartPainting();
    }

    void OnRushButton()
    {
        if (MachiRoomGameManager.Instance.CreationInfo.Progress >= MachiRoomGameManager.Instance.Config.CompleteProgress)
        {
            SwitchState(State.Panel5PaintComplete);
            return;
        }

        if (!MachiRoomGameManager.Instance.CanRushPainting(out MachiRoomDraftActionResult result))
        {
            ShowRushFailureTip(result);
            return;
        }

        float fromProgress = MachiRoomGameManager.Instance.CreationInfo.Progress;
        isPlayingRushAnimation = true;
        MachiRoomGameManager.Instance.StartRushPainting();
        StartRushAnimation(fromProgress, MachiRoomGameManager.Instance.CreationInfo.Progress);
    }

    /// <summary>催稿进程中【放弃出图】：丢弃当前稿件，回到选择草稿面板。</summary>
    void OnTrashButton()
    {
        if (isPlayingRushAnimation)
            return;

        MachiRoomGameManager.Instance.AbandonPainting();
    }

    /// <summary>结算弹窗【收下】：收下画稿后回到空闲态。</summary>
    public void OnResultBack()
    {
        MachiRoomGameManager.Instance.CollectArtwork();
    }

    void OnCloseButton()
    {
        Close();
    }

    void OnTimePeriodUpdate(TimeSlot timeSlot)
    {
        timePeriodText.SetText(LocTableSet.EnumsText, timeSlot.ToString());
        RefreshStudioState();
    }
    void OnPlayerDataUpdate(PlayerData data)
    {
        spCountText.text = GameDataManager.Instance.GetPropertyText(PropertyType.Strength);
        inspireCountText.text = GameDataManager.Instance.GetPropertyText(PropertyType.MachiInspire);
        presureCountText.text = GameDataManager.Instance.GetPropertyText(PropertyType.MachiPressure);
        
        UpdateRushButton();
        // 暂时不如用如下逻辑
        // if (curState == State.Panel4PaintProcess)
        //     rushButton.interactable = MachiRoomGameManager.Instance.CreationInfo.Progress
        //             >= MachiRoomGameManager.Instance.Config.CompleteProgress
        //         || MachiRoomGameManager.Instance.CanRushPainting();
    }

    void OnMachiCharacterBagUpdate(CharacterBag machi)
    {
        favorCountText.text = CharacterManager.Instance.GetFavorText(CharaIdSet1.Machi);
    }

    void OnCreationChanged(MachiRoomCreationInfo creationInfo)
    {
        if (isPlayingScratchTicket || isPlayingRushAnimation)
            return;

        switch (creationInfo.State)
        {
            case MachiRoomCreationState.Idle:
                SwitchState(State.Panel1Select);
                break;
            case MachiRoomCreationState.DraftReady:
                SwitchState(State.Panel3Decide);
                break;
            case MachiRoomCreationState.Painting:
                SwitchState(State.Panel4PaintProcess);
                break;
            case MachiRoomCreationState.Completed:
                if (curState != State.Panel5PaintComplete)
                    SwitchState(State.Panel4PaintProcess);
                break;
        }
    }

    #endregion

    void StartScratchTicket()
    {
        RefreshArtworkImages();
        SwitchState(State.Panel2PaintDraft);
        bool isSpecial = MachiRoomGameManager.Instance.GetManuscriptItemData(
            MachiRoomGameManager.Instance.CreationInfo.ManuscriptItemId).IsSpecial;
        scratchTicket.StartScratch(
            MachiRoomGameManager.Instance.Config,
            isSpecial,
            EndScratchTicket);
    }

    void EndScratchTicket()
    {
        isPlayingScratchTicket = false;
        SwitchState(State.Panel3Decide);
    }

    void StartRushAnimation(float fromProgress, float toProgress)
    {
        float completeProgress = MachiRoomGameManager.Instance.Config.CompleteProgress;
        rushSeq.Stop();
        rushSeq = Sequence.Create(Tween.Custom(
                progressBar,
                fromProgress / completeProgress,
                toProgress / completeProgress,
                MachiRoomGameManager.Instance.Config.RushAnimDura,
                (bar, value) =>
                {
                    bar.fillAmount = value;
                    progressText.text = $"{Mathf.RoundToInt(value * 100f)}%";
                }))
            .ChainCallback(EndRushAnimation);
    }

    void EndRushAnimation()
    {
        isPlayingRushAnimation = false;
        OnCreationChanged(MachiRoomGameManager.Instance.CreationInfo);
    }

    void ShowDraftFailureTip(MachiRoomDraftActionResult result)
    {
        string reasonKey = result switch
        {
            MachiRoomDraftActionResult.MachiNotInStudio => MachiNotInStudio,
            MachiRoomDraftActionResult.InspirationNotEnough => InspirationNotEnough,
            _ => MachiBadState,
        };

        inspirationNotEnoughText.SetText(LocTableSet.MachiRoom, reasonKey);

        if (inspirationTipCt != null)
            StopCoroutine(inspirationTipCt);
        inspirationTipCt = StartCoroutine(ShowInspirationNotEnoughTipIE());
    }
    void ShowDraftFailureTipPanel3(MachiRoomDraftActionResult result)
    {
        string reasonKey = result switch
        {
            MachiRoomDraftActionResult.MachiNotInStudio => MachiNotInStudio,
            MachiRoomDraftActionResult.InspirationNotEnough => InspirationNotEnough,
            _ => MachiBadState,
        };

        panel3DecideDraftWarnTip.Show(LocTableSet.MachiRoom, reasonKey);
        
        if (inspirationTipCt != null)
            StopCoroutine(inspirationTipCt);
        inspirationTipCt = StartCoroutine(ShowInspirationNotEnoughTipIE());
    }

    void ShowRushFailureTip(MachiRoomDraftActionResult result)
    {
        string reasonKey = result switch
        {
            MachiRoomDraftActionResult.MachiNotInStudio => MachiNotInStudio,
            MachiRoomDraftActionResult.InspirationNotEnough => InspirationNotEnough,
            MachiRoomDraftActionResult.ActionPointNotEnough => ActionPointNotEnough,
            _ => MachiBadState,
        };

        panel4PaintProcessWarnTip.Show(LocTableSet.MachiRoom, reasonKey);
    }

    IEnumerator ShowInspirationNotEnoughTipIE()
    {
        inspirationNotEnoughTip.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.5f);
        Tween.Alpha(inspirationNotEnoughTip, 0f, 0.25f, useUnscaledTime: true);
        inspirationTipCt = null;
    }

    void SwitchState(State state)
    {
        curState = state;
        panel1Select.SetActive(state == State.Panel1Select);
        panel2PaintDraft.SetActive(state == State.Panel2PaintDraft);
        panel3Decide.SetActive(state == State.Panel3Decide);
        panel4PaintProcess.SetActive(state == State.Panel4PaintProcess);
        paintResult.gameObject.SetActive(state == State.Panel5PaintComplete);
        specialDrawingDraftTip.SetActive(false);

        if (stateTextKeyDict.TryGetValue(state, out string stateTextKey))
            leftUpStateText.SetText(LocTableSet.MachiRoom, stateTextKey);

        switch (state)
        {
            case State.Panel1Select:
                OnPlayerDataUpdate(GameDataManager.Instance.PlayerData);
                break;
            case State.Panel3Decide:
                RefreshDraftDecision();
                break;
            case State.Panel4PaintProcess:
                RefreshPaintingProcess();
                break;
            case State.Panel5PaintComplete:
                RefreshCompletion();
                break;
        }

        RefreshStudioState();
    }

    /// <summary>女主离开工作室时暂停一切创作操作，底部只保留状态提示。</summary>
    void RefreshStudioState()
    {
        bool inStudio = MachiRoomGameManager.Instance.IsMachiInStudio();
        rushButton.gameObject.SetActive(inStudio);
        startPaintingButton.gameObject.SetActive(inStudio);

        // 马吉不在工作室时不能开新草稿
        for (int i = 0; i < selectButtons.Length; i++)
            selectButtons[i].interactable = inStudio;

        // 催稿进程页用页内提示说明暂停，其余状态走左上角状态文本
        bool isPaintProcess = curState == State.Panel4PaintProcess;
        machiNotInStudioTip.SetActive(!inStudio && isPaintProcess);
        if (!inStudio && !isPaintProcess)
            leftUpStateText.SetText(LocTableSet.MachiRoom, MachiNotInStudio);
    }

    void RefreshDraftDecision()
    {
        ManuscriptItemData manuscriptData = MachiRoomGameManager.Instance.GetManuscriptItemData(
            MachiRoomGameManager.Instance.CreationInfo.ManuscriptItemId);
        RefreshArtworkImages();
        decideSparkleEffect.Play(MachiRoomGameManager.Instance.Config, manuscriptData.IsSpecial);
        qualityText.text = GetQualityText(manuscriptData.BaseScore);
        specialTag.SetActive(manuscriptData.IsSpecial);
        abandonButton.gameObject.SetActive(!manuscriptData.IsSpecial);
        rerollButton.gameObject.SetActive(!manuscriptData.IsSpecial);
        rerollButton.interactable = true;
        rerollCostText.SetVar(
            LocVarSet.Count,
            MachiRoomGameManager.Instance.CreationInfo.DraftInspirationCost);

        specialDrawingDraftTip.SetActive(manuscriptData.IsSpecial);
        if (manuscriptData.IsSpecial)
        {
            leftUpStateText.SetText(LocTableSet.MachiRoom, SpecialDraft);
            specialDrawingDraftTip.GetComponent<LocalizeStringEvent>()
                .SetText(LocTableSet.MachiRoom, SpecialDraftCreating);
        }
        else
        {
            int draftIndex = System.Array.IndexOf(
                MachiRoomGameManager.Instance.Config.DraftInspirationCosts,
                MachiRoomGameManager.Instance.CreationInfo.DraftInspirationCost);
            string draftNameKey = MachiRoomGameManager.Instance.Config.DraftNameKeyList[draftIndex];
            leftUpStateText.SetText(LocTableSet.MachiRoom, draftNameKey);
        }
    }

    void RefreshPaintingProcess()
    {
        RefreshArtworkImages();
        float progressRate = MachiRoomGameManager.Instance.CreationInfo.Progress
            / MachiRoomGameManager.Instance.Config.CompleteProgress;
        progressBar.fillAmount = progressRate;
        progressText.text = $"{Mathf.RoundToInt(progressRate * 100f)}%";
        scoreText.SetVar(LocVarSet.Score, MachiRoomGameManager.Instance.CreationInfo.Score);
        gradeText.text = GetQualityText();

        UpdateRushButton();
    }
    void UpdateRushButton()
    {
        bool isComplete = MachiRoomGameManager.Instance.CreationInfo.Progress
            >= MachiRoomGameManager.Instance.Config.CompleteProgress;
        completeTipText.SetActive(isComplete);
        trashButton.gameObject.SetActive(MachiRoomGameManager.Instance.CanAbandonPainting());
        rushButton.interactable = isComplete || MachiRoomGameManager.Instance.CanRushPainting();
        leftUpStateText.SetText(LocTableSet.MachiRoom, isComplete ? PaintComplete : DraftProgress);
        rushButtonText.SetText(LocTableSet.MachiRoom, isComplete ? FinishDraft : UrgeDraft);
        if (isComplete)
            completeTipText.GetComponent<LocalizeStringEvent>().SetText(LocTableSet.MachiRoom, Completed);
    }
    void RefreshCompletion()
    {
        if (MachiRoomGameManager.Instance.IsRewardEarned)
            paintResult.ShowSuccess();
        else
            paintResult.ShowFail();
    }

    void RefreshArtworkImages()
    {
        Sprite artworkSr = LoadAsset<Sprite>(MachiRoomGameManager.Instance.GetDraftImagePath());
        paintContentImage.sprite = artworkSr;
        paintContentImage.SetNativeSize();
        decideArtworkImage.sprite = artworkSr;
        decideArtworkImage.SetNativeSize();
        processArtworkImage.sprite = artworkSr;
        processArtworkImage.SetNativeSize();
    }
    public string GetQualityText()
    {
        return GetQualityText(MachiRoomGameManager.Instance.CreationInfo.Score);
    }
    string GetQualityText(int baseScore)
    {
        return baseScore switch
        {
            >= 90 => "S",
            >= 80 => "A",
            >= 70 => "R",
            >= 60 => "B",
            _ => "N",
        };
    }
}
