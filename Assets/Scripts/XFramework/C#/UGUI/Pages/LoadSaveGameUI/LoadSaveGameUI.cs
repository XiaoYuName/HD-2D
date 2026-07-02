using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class LoadSaveGameUI : UIBase
{
    private Tweener _tweener;
    private RectTransform PageTweener;

    private CustomButton LoadButton;
    private CustomButton CloseButton;

    public SaveGameSlot AutoSaveGameSlot;
    public List<SaveGameSlot> SaveGameSlots;
    private SaveGameSlot SelectedSaveGameSlot;
    
    [LabelText("标题")]
    public LocalSelectedData TipsLocalSelectedData;
    [LabelText("内容")]
    public LocalSelectedData ContentLocalSelectedData;
    [LabelText("取消文本")]
    public LocalSelectedData CancelLocalSelectedData;
    [LabelText("确定文本")]
    public LocalSelectedData ActionLocalSelectedData;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        PageTweener = Get<RectTransform>("UIMask/Background");
        LoadButton = Get<CustomButton>("UIMask/Background/LoadButton");
        CloseButton = Get<CustomButton>("UIMask/Background/QuitButton");
        Bind(CloseButton,Close,"");
        AutoSaveGameSlot.Init();
        AutoSaveGameSlot.BindClick(SetSelectedSaveGameSlot);
        for (int i = 0; i < SaveGameSlots.Count; i++)
        {
            SaveGameSlots[i].Init();
            SaveGameSlots[i].BindClick(SetSelectedSaveGameSlot);
        }
        Bind(LoadButton,LoadSaveOnClick,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        AutoSaveGameSlot.SetEmpty();
        for (int i = 0; i < SaveGameSlots.Count; i++)
        {
            SaveGameSlots[i].SetEmpty();
        }

        SelectedSaveGameSlot = null;
        LoadButton.interactable = false;
        
        
        _tweener?.Kill();
        PageTweener.transform.localScale = Vector3.zero;
        PageTweener.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        SaveGameManager.Instance.RegionUsersChange(UpdateUsers);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        _tweener?.Kill();
        if (SaveGameManager.IsInitialized)
        {
            SaveGameManager.Instance.URegionUsersChange(UpdateUsers);
        }
        PageTweener.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                base.Close();
            });
    }

    private void UpdateUsers(List<UserSaveSummary> users)
    {
        AutoSaveGameSlot.SetEmpty();
        for (int i = 0; i < SaveGameSlots.Count; i++)
        {
            SaveGameSlots[i].SetEmpty();
        }
        for (int i = 0; i < users.Count; i++)
        {
            int idx = users[i].UserID;
            if (idx == 0)
            {
                AutoSaveGameSlot.SetData(users[i]);
            }
            else
            {
                SaveGameSlots[idx -1].SetData(users[i]);
            }
        }
    }
    
    private void SetSelectedSaveGameSlot(SaveGameSlot slot)
    {
        if (slot.UserSaveSummaryData != null)
        {
            if (SelectedSaveGameSlot == slot)
            {
                SelectedSaveGameSlot = null;
                LoadButton.interactable = false;
            }
            else
            {
                SelectedSaveGameSlot = slot;
                LoadButton.interactable = true;
            }
        }
    }

    private void LoadSaveOnClick()
    {
        UIUtility.PopDialogue(title: TipsLocalSelectedData,content: ContentLocalSelectedData,cancelData:
            CancelLocalSelectedData,ActionLocalSelectedData, () => { }, () =>
            {
                if(SelectedSaveGameSlot == null)
                    return;
                SaveGameManager.Instance.Load(SelectedSaveGameSlot.UserSaveSummaryData);
                GameManager.Instance.EnterGame(SaveGameManager.Instance.CurUserSaveSummary);
                UISystem.Instance.CloseUI("LoadSaveGameUI");
            });
       
    }
}
