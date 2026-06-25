using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XFramework;
using Random = UnityEngine.Random;

public class SetDatingTargetUI : UIBase
{
    private CustomButton CloseButton;
    private CustomButton StartButton;
    private RawImage backgroundImage;
    private OptionUI optionUI;
    private Texture2D CurrentTexture2D;

    [LabelText("约会地点"),ValueDropdown("GetMinSceneItemID")]
    public List<string> DatingScenes;

    private CharacterData characterData;
    private ShowingData showingData;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        CloseButton = Get<CustomButton>("UIMask/Background/CloseButton");
        StartButton = Get<CustomButton>("UIMask/Background/StartButton");
        backgroundImage = Get<RawImage>("UIMask/Background/SpriteFarme/Raw");
        optionUI = Get<OptionUI>("UIMask/Background/SceneFarme/OptionUI");
        optionUI.Init();
        optionUI.ShowingSceneOptions(DatingScenes,OptionSelected);
        Bind(CloseButton,Close,"");
        Bind(StartButton,StarDatingScene,"");
    }

    public void SetData(CharacterData characterData, ShowingData showingData)
    {
        this.characterData = characterData;
        this.showingData = showingData;
    }

    public void OptionSelected(string sceneID)
    {
        var minSceneData = GameDataManager.Instance.MinGameSceneData.GetDataByID(sceneID);
        CurrentTexture2D = AssetsManager.Instance.LoadAssets<Texture2D>(minSceneData.SceneTexturePath);
        backgroundImage.texture = CurrentTexture2D;
    }

    public void StarDatingScene()
    {
        string sceneID = optionUI.SelectedOption;
        if(string.IsNullOrEmpty(sceneID))return;
        Debug.Log("开始进入: "+sceneID + "的约会流程");
        
        var data = characterData.DatingDramaList.Find(temp => temp.SceneID == sceneID);
        if (data != null)
        {
            UISystem.Instance.OpenUI<DramaUI>("DramaUI").StartDrama(data.DramaList[Random.Range(0, data.DramaList.Count)]);
        }
        Close();
    }



    public IEnumerable GetMinSceneItemID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        
        return MinGameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.SceneID))
            .Select(temp => new ValueDropdownItem(temp.scene_description,temp.SceneID));
    }
}
