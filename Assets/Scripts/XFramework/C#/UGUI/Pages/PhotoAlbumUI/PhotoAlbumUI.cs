using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class PhotoAlbumUI : UIBase
{
    public const string LabButtonPath =
        "Assets/AddressableAssets/Remote/Prefabs/LabelButton/LableButton.prefab";

    private RectTransform PageTweenerRoot;
    private RectTransform LabelButtonParent;
    private RectTransform LabelPageParent;
    
    private Dictionary<PhotoLabelType,LabelButton> labelBtnDict;
    private Dictionary<PhotoLabelType,UIBase> labelPageDict;
    
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
        
        Bind(CloseButton,Close,"");
        CreateLabelData();
    }
    
    private void CreateLabelData()
    {
        labelBtnDict = new Dictionary<PhotoLabelType, LabelButton>();
        labelPageDict = new Dictionary<PhotoLabelType,UIBase>();
        for (int i = 0; i < GameDataManager.Instance.PhotoAlbumData.LabelDataList.Count; i++)
        {
            var data = GameDataManager.Instance.PhotoAlbumData.LabelDataList[i];
            var labelObj =  AssetsManager.Instance.Instantiate(LabButtonPath);
            labelObj.transform.SetParent(LabelButtonParent);
            labelObj.transform.localPosition = Vector3.zero;
            labelObj.transform.localScale = Vector3.one;

            var labelBtn = labelObj.GetComponent<LabelButton>();
            labelBtn.Init();
            labelBtn.SetData(GameDataManager.Instance.PhotoAlbumData.LabelDataList[i],OnLabelButtonPointerClick);
            labelBtnDict.Add(data.PhotoLabelType,labelBtn);
           
            var pagePrefab =  AssetsManager.Instance.LoadAssets<GameObject>(GameDataManager.Instance.PhotoAlbumData.LabelDataList[i].LabelPagePath);
            var pageObj = Instantiate(pagePrefab, LabelPageParent);
            var uiBase = pageObj.GetComponent<UIBase>();
            uiBase.Init();
            uiBase.Close();
            labelPageDict.Add(data.PhotoLabelType,uiBase);
        }
        OnLabelButtonPointerClick(GameDataManager.Instance.PhotoAlbumData.LabelDataList[0]);
    }
    
    private void OnLabelButtonPointerClick(PhotoLabelData data)
    {
        foreach (var key in labelBtnDict.Keys)
        {
            labelBtnDict[key].SetSelected(false);
            labelPageDict[key].Close();
        }

        if (labelBtnDict.ContainsKey(data.PhotoLabelType))
        {
            labelBtnDict[data.PhotoLabelType].SetSelected(true);
            labelPageDict[data.PhotoLabelType].Open();
        }
    }
}
