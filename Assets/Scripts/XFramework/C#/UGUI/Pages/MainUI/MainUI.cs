using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class MainUI : UIBase
{
    private CustomButton GameMapButton;
    private CustomButton GameTaskButton;
    private CustomButton GamePhoneButton;
    private CustomButton InventoryButton;
    private CustomButton RememberButton;

    private LocalizeStringEvent dayStringEvent;
    private LocalizeStringEvent weekStringEvent;
    private Image dayTypeImage;
    private Image nightTypeImage;
    private ValueNumberContent valueNumberContent;
    private LocalizeStringEvent strengthStringEvent;
    private LocalizeStringEvent goldNumberStringEvent;

    private HorizontalLayoutGroup horizontalLayoutGroup;
    private ContentSizeFitter _contentSizeFitter;
    private CustomButton leftButton;
    private CustomButton rightButton;
    private LocalizeStringEvent sceneNameStringEvent;

    private CustomButton sleepButton;

    private SelectedButton autoDramaButton;
    private SelectedButton skipDramaButton;
    private SelectedButton loadSaveButton;
    private SelectedButton saveButton;
    private SelectedButton dramaLogButton;
    private SelectedButton homeButton;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        GameMapButton = Get<CustomButton>("UIMask/DownButtons/WordMapButton");
        GameTaskButton = Get<CustomButton>("UIMask/DownButtons/TaskButton");
        GamePhoneButton = Get<CustomButton>("UIMask/DownButtons/PhoneButton");
        InventoryButton = Get<CustomButton>("UIMask/DownButtons/BagButton");
        RememberButton = Get<CustomButton>("UIMask/DownButtons/RememberButton");

        sleepButton = Get<CustomButton>("UIMask/UserInfoPanel/StrengthFarme/SleepButton");
        
        Bind(GameMapButton,LoadGameMap,"");
        Bind(GameTaskButton,ShowingGameTaskUI,"");
        Bind(GamePhoneButton,ShowingPhoneUI,"");
        Bind(InventoryButton,ShowingInventoryUI,"");
        Bind(RememberButton,ShowRememberUI,"");

        dayStringEvent = Get<LocalizeStringEvent>("UIMask/UserInfoPanel/TopFarme/Top/DayTex");
        weekStringEvent = Get<LocalizeStringEvent>("UIMask/UserInfoPanel/TopFarme/Top/WeekTex");
        dayTypeImage = Get<Image>("UIMask/UserInfoPanel/TopFarme/Top/EnvironmentMode/day");
        nightTypeImage = Get<Image>("UIMask/UserInfoPanel/TopFarme/Top/EnvironmentMode/Night");
        valueNumberContent = Get<ValueNumberContent>("UIMask/UserInfoPanel/ActionPointsFarme/StarContent");
        strengthStringEvent = Get<LocalizeStringEvent>("UIMask/UserInfoPanel/StrengthFarme/StrengthTex");
        goldNumberStringEvent = Get<LocalizeStringEvent>("UIMask/UserInfoPanel/GoldNumberFarme/Text (TMP)");

        horizontalLayoutGroup = Get<HorizontalLayoutGroup>("UIMask/OptionMinSceneFarme");
        _contentSizeFitter = Get<ContentSizeFitter>("UIMask/OptionMinSceneFarme");
        leftButton = Get<CustomButton>("UIMask/OptionMinSceneFarme/LeftButton");
        rightButton = Get<CustomButton>("UIMask/OptionMinSceneFarme/RightButton");
        sceneNameStringEvent = Get<LocalizeStringEvent>("UIMask/OptionMinSceneFarme/ScenenNameTex");
        Bind(leftButton,PreviousL,"");
        Bind(rightButton,Next,"");
        Bind(sleepButton,Sleep,"");

        autoDramaButton = Get<SelectedButton>("UIMask/MeumButtons/AutoDramaBtn");
        skipDramaButton = Get<SelectedButton>("UIMask/MeumButtons/SkipDramaBtn");
        loadSaveButton = Get<SelectedButton>("UIMask/MeumButtons/LoadSaveBtn");
        saveButton = Get<SelectedButton>("UIMask/MeumButtons/SaveBtn");
        dramaLogButton = Get<SelectedButton>("UIMask/MeumButtons/DramaLogBtn");
        homeButton = Get<SelectedButton>("UIMask/MeumButtons/QuitBtn");
        
        
        Bind(autoDramaButton,AutoDrama,"");
        Bind(skipDramaButton,SkipDrama,"");
        Bind(loadSaveButton,OpenLoadSaveUI,"");
        Bind(saveButton,OpenSaveGameButton,"");
        Bind(dramaLogButton,OpenDramaLogUI,"");
        Bind(homeButton,OpenCommonUI,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        RegisterEvents();
        LanguageManager.Instance.AddOnLanguageChanged(OnLanguageChanged);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        StopAllCoroutines();
        UnregisterEvents();
        LanguageManager.Instance.RemoveOnLanguageChanged(OnLanguageChanged);
    }

    #region Bind

    private bool isBind;

    private void RegisterEvents()
    {
        if (!isBind)
        {
            isBind = true;
            GameDataManager.Instance.RegisterPlayerDataChange(UpdatePlayerUI);
            CharacterManager.Instance.RegisterAllCharacterBagChange(UpdateCharacter);
            InventoryManager.Instance.RegisterAllItemChange(UpdateItem);
            GameSceneManager.Instance.RegisterSceneChange(UpdateScene);
        }
    }

    private void UnregisterEvents()
    {
        if (isBind)
        {
            GameDataManager.Instance.UnregisterPlayerDataChange(UpdatePlayerUI);
            CharacterManager.Instance.UnregisterAllCharacterBagChange(UpdateCharacter);
            InventoryManager.Instance.UnregisterAllItemChange(UpdateItem);
            GameSceneManager.Instance.UnregisterSceneChange(UpdateScene);
            isBind = false;
        }
    }

    #endregion


    private void UpdatePlayerUI(PlayerData user)
    {
        dayStringEvent.StringReference.SetVar("value",user.Day,true);
        weekStringEvent.StringReference.SetVar("value",user.Week);
        dayTypeImage.gameObject.SetActive(user.EnvironmentMode == EnvironmentMode.Morning || user.EnvironmentMode == EnvironmentMode.Noon);
        nightTypeImage.gameObject.SetActive(user.EnvironmentMode == EnvironmentMode.Evening || user.EnvironmentMode == EnvironmentMode.Midnight);
        valueNumberContent.SetValue(user.GetProperty(PropertyType.ActionPointsValue));
        strengthStringEvent.StringReference.SetVar("value",$"{user.GetProperty(PropertyType.Strength)} / {GameDataManager.Instance.GameSettingsData.StrengthLimit}");
        goldNumberStringEvent.StringReference.SetVar("value",$"{user.GetProperty(PropertyType.Gold)}");
       
    }

    private void UpdateScene(SceneData sceneData)
    {
        if (!GameSceneManager.Instance.ContainsWordMapScene(sceneData.WordMapSceneID))
        {
            leftButton.interactable = false;
            sceneNameStringEvent.SetText("WordScene","MianSceneName");
            StartCoroutine(OnPreRender());
            rightButton.interactable = false;
        }
        else
        {
            var gameSceneData = GameSceneManager.Instance.GetGameSceneData(sceneData.SceneID);
            sceneNameStringEvent.SetText(gameSceneData.SceneName.Table,gameSceneData.SceneName.Value);
            StartCoroutine(OnPreRender());
            horizontalLayoutGroup.CalculateLayoutInputHorizontal();
            leftButton.interactable = CheckOption(sceneData.WordMapSceneID);
            rightButton.interactable = CheckOption(sceneData.WordMapSceneID);
        }
        
    }

    private void UpdateCharacter(List<CharacterBag> characterBags)
    {
        
    }

    private void UpdateItem(List<ItemBag> itemBags)
    {
        
    }
    


    private void OnLanguageChanged()
    {
        StartCoroutine(OnPreRender());
    }

    private IEnumerator OnPreRender()
    {
        yield return new WaitForEndOfFrame();
        horizontalLayoutGroup.CalculateLayoutInputHorizontal();
        _contentSizeFitter.SetLayoutHorizontal();
    }

    #region 切换场景

    private bool CheckOption(long SceneID)
    {
        WordMapSceneData wordMapSceneData = GameSceneManager.Instance.GetWordMapSceneData(SceneID);
        if (wordMapSceneData == null || wordMapSceneData.SubScenes.Count <= 1)
        {
            return false;
        }
        return true;
    }

    private void  PreviousL()
    {
        var data = GameSceneManager.Instance.GetWordMapSceneData(GameSceneManager.Instance.GameSceneData.WordMapSceneID);
        if (data != null)
        {
            int index = data.SubScenes.FindIndex(x=>x == GameSceneManager.Instance.GameSceneData.SceneID);
            index--;
            if (index < 0)
            {
                index =  data.SubScenes.Count -1;
            }
            while (!CheckOption(data.ID))
            {
                index--;
                if (index < 0)
                {
                    index =  data.SubScenes.Count -1;
                }
            }
            GameSceneManager.Instance.OptionGameScene(data.SubScenes[index]);
        }
    }

    private void Next()
    {
        var data = GameSceneManager.Instance.GetWordMapSceneData(GameSceneManager.Instance.GameSceneData.WordMapSceneID);
        if (data != null)
        {
            int index = data.SubScenes.FindIndex(x=>x == GameSceneManager.Instance.GameSceneData.SceneID);
            index++;
            if (index >= data.SubScenes.Count)
            {
                index = 0;
            }
            while (!CheckOption(data.ID))
            {
                index++;
                if (index >= data.SubScenes.Count)
                {
                    index = 0;
                }
            }
            GameSceneManager.Instance.OptionGameScene(data.SubScenes[index]);
        }
    }

    #endregion




    public void LoadGameMap()
    {
        if (GameSceneManager.Instance.ContainsWordMapScene(GameSceneManager.Instance.GameSceneData.WordMapSceneID))
        {
            UISystem.Instance.CloseUI("CharacterFunctionUI");
            GameSceneManager.Instance.QuitGameScene();
        }
    }

    private void ShowingGameTaskUI()
    {
        
    }

    private void ShowingPhoneUI()
    {
        
    }

    private void ShowingInventoryUI()
    {
        UISystem.Instance.OpenUI<InventoryUI>("InventoryUI");
    }

    private void ShowRememberUI()
    {
        
    }

    private void Sleep()
    {
        GameDataManager.Instance.Sleep();
    }

    private void AutoDrama()
    {
        autoDramaButton.SetSelected(!autoDramaButton.isSelected);
        DramaManager.Instance.isAutoDrama = autoDramaButton.isSelected;
    }

    private void SkipDrama()
    {
        
    }

    private void OpenLoadSaveUI()
    {
        UISystem.Instance.OpenUI<LoadSaveGameUI>("LoadSaveGameUI");
    }

    private void OpenSaveGameButton()
    {
        UISystem.Instance.OpenUI<SaveGameUI>("SaveGameUI");
    }

    private void OpenDramaLogUI()
    {
        DramaManager.Instance.ShowDramaLogUI();
    }

    private void OpenCommonUI()
    {
        UISystem.Instance.OpenUI<CommonUI>("CommonUI");
        Close();
        
    }
    
}
