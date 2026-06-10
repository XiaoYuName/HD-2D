using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using XFramework;

public class GameSettingsUI : UIBase
{
    public const string LabButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/LabelButton/LableButton.prefab";

    private RectTransform PageTweenerRoot;
    private RectTransform LabelButtonParent;
    private RectTransform LabelPageParent;
    
    private Dictionary<GameSettingType,LabelButton> labelBtnDict;
    private Dictionary<GameSettingType,UIBase> labelPageDict;

    private CustomButton ResetButton;
    private CustomButton CloseButton;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        PageTweenerRoot = Get<RectTransform>("UIMask/Panel");
        LabelButtonParent = Get<RectTransform>("UIMask/Panel/ButtonsFarme/Panel");
        LabelPageParent = Get<RectTransform>("UIMask/Panel/GroupContent");
        CloseButton = Get<CustomButton>("UIMask/Panel/CloseButton");
        ResetButton = Get<CustomButton>("UIMask/Panel/ResetButton");
        
        Bind(CloseButton,Close,"");
        Bind(ResetButton,ResetSettingDates,"");
        CreateLabelData();
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        PageTweenerRoot.localScale = Vector3.zero;
        PageTweenerRoot.DOScale(Vector3.one,0.2f).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        PageTweenerRoot.DOScale(Vector3.zero,0.2f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                base.Close();
            });
        
    }

    private void CreateLabelData()
    {
        labelBtnDict = new Dictionary<GameSettingType, LabelButton>();
        labelPageDict = new Dictionary<GameSettingType,UIBase>();
        for (int i = 0; i < GameDataManager.Instance.GameSettingsData.LabelDataList.Count; i++)
        {
            var data = GameDataManager.Instance.GameSettingsData.LabelDataList[i];
           var labelObj =  AssetsManager.Instance.Instantiate(LabButtonPath);
           labelObj.transform.SetParent(LabelButtonParent);
           labelObj.transform.localPosition = Vector3.zero;
           labelObj.transform.localScale = Vector3.one;

           var labelBtn = labelObj.GetComponent<LabelButton>();
           labelBtn.Init();
           labelBtn.SetData(GameDataManager.Instance.GameSettingsData.LabelDataList[i],OnLabelButtonPointerClick);
           labelBtnDict.Add(data.GameSettingType,labelBtn);
           
           var pagePrefab =  AssetsManager.Instance.LoadAssets<GameObject>(GameDataManager.Instance.GameSettingsData.LabelDataList[i].LabelPagePath);
           var pageObj = Instantiate(pagePrefab, LabelPageParent);
           var uiBase = pageObj.GetComponent<UIBase>();
           uiBase.Init();
           uiBase.Close();
           labelPageDict.Add(data.GameSettingType,uiBase);
        }
        OnLabelButtonPointerClick(GameDataManager.Instance.GameSettingsData.LabelDataList[0]);
    }
    
    private void OnLabelButtonPointerClick(LabelData data)
    {
        foreach (var key in labelBtnDict.Keys)
        {
            labelBtnDict[key].SetSelected(false);
            labelPageDict[key].Close();
        }

        if (labelBtnDict.ContainsKey(data.GameSettingType))
        {
            labelBtnDict[data.GameSettingType].SetSelected(true);
            labelPageDict[data.GameSettingType].Open();
        }
    }

    private void ResetSettingDates()
    {
        foreach (var key in labelPageDict.Keys)
        {
            if (labelPageDict[key] is IReset reset)
            {
                reset.ResetData();
            }
        }
    }
}
