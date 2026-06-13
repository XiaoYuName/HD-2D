using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class WordMapInfoUI : UIBase
{
    private const string prefabPath = "Assets/AddressableAssets/Remote/Prefabs/UGUI/WordMapInfoUI/WordItemSlot.prefab";
    
    private RectTransform ButtonContent;
    private CustomButton CloseButton;
    private RawImage TextureRawImage;
    private LocalizeStringEvent localizeStringEvent;
    private CustomButton EnterButton;

    private List<WordItemSlot> WordItemSlotList;
    private MinSceneData minSceneData;
    private GameSceneData gameSceneItemData;

    [LabelText("颜色列表")]
    public Color[] LabelColors;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        ButtonContent = Get<RectTransform>("UIMask/Page/ButtonContent");
        CloseButton = Get<CustomButton>("UIMask/Page/CloseButton");
        TextureRawImage = Get<RawImage>("UIMask/Page/ImageFarme/RawImage");
        localizeStringEvent = Get<LocalizeStringEvent>("UIMask/Page/Panel/Text (TMP)");
        WordItemSlotList = new List<WordItemSlot>();
        EnterButton = Get<CustomButton>("UIMask/Page/EnterButton");
        Bind(CloseButton,Close,"");
        Bind(EnterButton,OnEnterButtonClick,"");
        
    }

    public void ShowData(GameSceneData gameSceneData)
    {
        this.gameSceneItemData = gameSceneData;
        
        for (int i = 0; i < gameSceneData.min_sceneList.Count; i++)
        {
            var Data =  GameDataManager.Instance.MinGameSceneData.GetDataByID(gameSceneData.min_sceneList[i]);
            
            var obj = AssetsManager.Instance.Instantiate(prefabPath);
            obj.transform.SetParent(ButtonContent);
            obj.transform.localScale = Vector3.one;
            Color color = LabelColors[0];
            if (i < LabelColors.Length)
            {
                color = LabelColors[i];
            }
            var slot = obj.GetComponent<WordItemSlot>();
            slot.Init();
            slot.SetData(color, Data,ShowingInfo);
            WordItemSlotList.Add(slot);
        }
        
        var deftual =  GameDataManager.Instance.MinGameSceneData.GetDataByID(gameSceneData.min_sceneList[0]);
        ShowingInfo(deftual);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var VARIABLE in WordItemSlotList)
        {
            AssetsManager.Instance.FreeGameObject(VARIABLE.gameObject);
        }
        
    }

    private void ShowingInfo(MinSceneData minSceneData)
    {
        this.minSceneData = minSceneData;
        TextureRawImage.texture = AssetsManager.Instance.LoadAssets<Texture2D>(minSceneData.SceneTexturePath);
        localizeStringEvent.SetEntry(minSceneData.scene_description);
    }
    
    private void OnEnterButtonClick()
    {
        GameDataManager.Instance.EnterGameScene(gameSceneItemData.scene_id,minSceneData.scene_id);
        UISystem.Instance.CloseUI("WordMapInfoUI");
    }
}
