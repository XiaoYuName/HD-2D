using System.Collections;
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
        BindEvents();
        LanguageManager.Instance.AddOnLanguageChanged(OnLanguageChanged);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        StopAllCoroutines();
        UnBindEvents();
        LanguageManager.Instance.RemoveOnLanguageChanged(OnLanguageChanged);
    }

    #region Bind

    private bool isBind;

    private void BindEvents()
    {
        if (!isBind)
        {
            isBind = true;
            GameDataManager.Instance.BindUserChange(UpdateUserUI);
        }
    }

    private void UnBindEvents()
    {
        if (isBind)
        {
            GameDataManager.Instance.UnBindUserChange(UpdateUserUI);
            isBind = false;
        }
    }

    #endregion


    private void UpdateUserUI(User user)
    {
        dayStringEvent.StringReference.SetVar("value",user.Day,true);
        weekStringEvent.StringReference.SetVar("value",user.Day);
        dayTypeImage.gameObject.SetActive(user.EnvironmentMode == EnvironmentMode.Morning || user.EnvironmentMode == EnvironmentMode.Noon);
        nightTypeImage.gameObject.SetActive(user.EnvironmentMode == EnvironmentMode.Evening || user.EnvironmentMode == EnvironmentMode.Midnight);
        valueNumberContent.SetValue(user.ActionPointsValue);
        strengthStringEvent.StringReference.SetVar("value",$"{user.Strength} / {90000}");
        goldNumberStringEvent.StringReference.SetVar("value",$"{user.GoldNumber}");
        var minSceneData = GameDataManager.Instance.MinGameSceneData.GetDataByID(user.minSceneID);
        if (string.IsNullOrEmpty(user.SceneID))
        {
            leftButton.interactable = false;
            sceneNameStringEvent.SetEntry("Empty");
            StartCoroutine(OnPreRender());
            rightButton.interactable = false;
        }
        else
        {
            leftButton.interactable = true;
            sceneNameStringEvent.SetEntry(minSceneData.scene_id);
            StartCoroutine(OnPreRender());
            horizontalLayoutGroup.CalculateLayoutInputHorizontal();
            rightButton.interactable = true;
        }
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

    private void  PreviousL()
    {
        if (string.IsNullOrEmpty(GameDataManager.Instance.CurrentUser.SceneID))
        {
            return;
            
        }
        var data = GameDataManager.Instance.GameSceneData.GetDataByID(GameDataManager.Instance.CurrentUser.SceneID);
        if (data != null)
        {
            int index = data.min_sceneList.FindIndex(x=>x == GameDataManager.Instance.CurrentUser.minSceneID);
            index--;
            if (index < 0)
            {
                index =  data.min_sceneList.Count -1;
            }
            GameDataManager.Instance.EnterGameScene(data.scene_id,data.min_sceneList[index]);
        }
    }

    private void Next()
    {
        if (string.IsNullOrEmpty(GameDataManager.Instance.CurrentUser.SceneID))
        {
            return;
            
        }
        var data = GameDataManager.Instance.GameSceneData.GetDataByID(GameDataManager.Instance.CurrentUser.SceneID);
        if (data != null)
        {
            int index = data.min_sceneList.FindIndex(x=>x == GameDataManager.Instance.CurrentUser.minSceneID);
            index++;
            if (index >= data.min_sceneList.Count)
            {
                index = 0;
            }
            GameDataManager.Instance.EnterGameScene(data.scene_id,data.min_sceneList[index]);
        }
        
        
    }


    public void LoadGameMap()
    {
        if (!string.IsNullOrEmpty(GameDataManager.Instance.CurrentUser.SceneID))
        {
            GameDataManager.Instance.EnterGameScene(string.Empty,string.Empty);
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
        UISystem.Instance.OpenUI<DramaLogUI>("DramaLogUI");
    }

    private void OpenCommonUI()
    {
        UISystem.Instance.OpenUI<CommonUI>("CommonUI");
        Close();
        
    }
    
}
