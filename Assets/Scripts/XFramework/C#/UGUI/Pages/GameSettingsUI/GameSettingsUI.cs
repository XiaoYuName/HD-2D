using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class GameSettingsUI : UIBase
{
    public const string LabButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/GameSettingsUI/LableButton.prefab";
    
    private RectTransform LabelButtonParent;
    private RectTransform LabelPageParent;
    
    private Dictionary<GameSettingType,LabelButton> labelBtnDict;
    private Dictionary<GameSettingType,UIBase> labelPageDict;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        LabelButtonParent = Get<RectTransform>("UIMask/Panel/ButtonsFarme/Panel");
        LabelPageParent = Get<RectTransform>("UIMask/Panel/GroupContent");
        
        CreateLabelData();
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
}
