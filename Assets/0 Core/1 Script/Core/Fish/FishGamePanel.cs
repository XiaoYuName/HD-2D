using UnityEngine;
using XFramework;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using System.Collections.Generic;
using System.Collections;
using Sirenix.OdinInspector;

public class FishGamePanel : UIBase
{
    enum State
    {
        None,   // 未开始。未进入界面状态
        SePos,  // 等待玩家点击选择下勾位置，下勾会检验玩家的鱼饵、行动力，如果不够，则提示。
        WaitFish,   // 等待鱼移动上钩
        WaitCatchFish,  // 等待玩家点击鼠标左键确认上钩
        CatchFish,  // 抓鱼阶段，玩家要通过鼠标左键的按住和松开，调整升降控制条的升降位置。
        End,    // 结束，弹出，保持现状，等待玩家点击继续钓鱼后，将状态重置为WaitFish
    }

    [SerializeField] TimeSlotConfig envModeConfig;
    [SerializeField] FishConfig config;
    [SerializeField] Button closeButton, leftClickButton, hookButton;
    [SerializeField] TextMeshProUGUI fishLvText, beltCountText, apText;// timeLeftText
    [SerializeField] Image timePeriodIcon;
    [SerializeField] LocalizeStringEvent timePeriodText;
    [SerializeField] Image chargeProgressBar;

    [SerializeField] FishLogCellUI fishLogCell;
    [SerializeField] Transform fishLogCellContainer;
    [SerializeField] List<FishLogCellUI> fishLogCellList;
    [SerializeField] LocalizeStringEvent tipText;
    [SerializeField] WarnTip warnTip;
    [LabelText("鱼钩物体")] [SerializeField] CanvasGroup hookCg;
    [LabelText("玩家点击下勾位置区域")][SerializeField] RectTransform clickAreaRt;
    [LabelText("抓鱼的升降控制条背景区域Rt")][SerializeField] RectTransform catchCtrlBarBgRt;   // 代表限制范围

    [LabelText("抓鱼的升降控制条")][SerializeField] Image catchCtrlBar; // 
    Coroutine catchCtrlBarMoveCt;
    [LabelText("抓鱼进度条")][SerializeField] Image catchProgressBar;
    
    [SerializeField] List<long> curPossibleFishIds;
    [SerializeField] float targetCatchPoint;  // 目标抓鱼点数，难度
    [SerializeField] float curCatchPoint;   // 当前抓鱼点数
    [SerializeField] State curState;
    #region Get
    float FishCatchCtrlBarMoveSpeed => config.FishCatchCtrlBarMoveSpeed;
    float FishProgressBarSpeed => config.FishProgressBarSpeed;
    #endregion
    #region Lifecycle
    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
    }

    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataDayChange(OnTimePerChange);
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChange);


        // 按住左键控制力度，右键收钩
        PlayerInputManager.Instance.OnLeftMouseDown += OnLeftMouseDown;
        PlayerInputManager.Instance.OnLeftMouseUp += OnLeftMouseUp;

        // 根据当前的鱼竿、当前时间段，获取可能调的所有鱼，然后再加上所有垃圾物

    }
    public override void Close()
    {
        base.Close();
        
        timePeriodIcon.ClearIcon();
        GameDataManager.Instance.UnregisterPlayerDataDayChange(OnTimePerChange);

        PlayerInputManager.Instance.OnLeftMouseDown -= OnLeftMouseDown;
        PlayerInputManager.Instance.OnLeftMouseUp -= OnLeftMouseUp;

        foreach (var item in fishLogCellList)
        {
            Destroy(item.gameObject);
        }
        fishLogCellList.Clear();
    }
    #endregion
    void OnTimePerChange(PlayerData playerData)
    {
        timePeriodIcon.SetIcon(envModeConfig.GetIconPath(GameDataManager.Instance.CurTimeSlot));
        timePeriodText.SetText(LocTableSet.MainUI, envModeConfig.GetNameKey(GameDataManager.Instance.CurTimeSlot));
    }
    void OnPlayerDataChange(PlayerData playerData)
    {
        apText.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);
    }
    // 尝试下勾
    void TryStartFish()
    {
        // 检查鱼饵是否足够
        if(InventoryManager.Instance.GetItemCount(ItemIdSet.Bait) == 0)
        {
            // 鱼饵不足
            warnTip.Show(LocTableSet.Fish, LocVarSet.FishGame.NotEnoughBait);
            return;
        }
        // 检查行动力是否足够
        if(GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value == 0)
        {
            // 行动力不足
            warnTip.Show(LocTableSet.GameEnterPanel, LocVarSet.MiniGame.NotEnoughAp);
            return;
        }
        // 扣除一个鱼饵和一个行动力
        InventoryManager.Instance.ConsumeItem(ItemIdSet.Bait, 1);
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue, 1);
        // 切换到Wait状态，等待鱼上钩
        // ...
    }

    void SwitchState(State targetState)
    {
        curState = targetState;
    }

    void OnLeftMouseDown()
    {
        // 按住时，往上移动 catchCtrlBar
        // catchCtrlBar.rectTransform
        if(catchCtrlBarMoveCt != null)
            StopCoroutine(catchCtrlBarMoveCt);
        catchCtrlBarMoveCt = StartCoroutine(CatchCtrlBarMove(true));
    }
    void OnLeftMouseUp()
    {
        // 松开时，控制条往下移动
        if(catchCtrlBarMoveCt != null)
            StopCoroutine(catchCtrlBarMoveCt);
       catchCtrlBarMoveCt = StartCoroutine(CatchCtrlBarMove(false));
    }

    IEnumerator CatchCtrlBarMove(bool isUp = true)
    {
        while(true)
        {
            catchCtrlBar.rectTransform.anchoredPosition = new Vector2(catchCtrlBar.rectTransform.anchoredPosition.x, catchCtrlBar.rectTransform.anchoredPosition.y + 1);
            yield return null;
            // 如果控制条在特定区域（中间区域），则进度条增加
            curCatchPoint += Time.deltaTime * FishProgressBarSpeed;
        }
    }


    // 增添渔获日志
    void AddLog(ItemInfo fishItem)
    {
        FishLogCellUI fishLogCellUI = Instantiate(fishLogCell, fishLogCellContainer);
        fishLogCellUI.Set(fishItem, 0f, 0f);
        fishLogCellList.Add(fishLogCellUI);
    }

}
