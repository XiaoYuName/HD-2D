using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;
using XFramework;

public class CommonUI : UIBase
{
    private CommonButton LoadGameButton;
    private CommonButton StartGameButton;
    private CommonButton PhotoButton;
    private CommonButton GameSettingsButton;
    private CommonButton QuitButton;
    
    public override void Init()
    {
        UISystem.Instance.AddUI("CommonUI",this);
        LoadGameButton = Get<CommonButton>("UIMask/MenuButtonController/LoadGameButton");
        StartGameButton = Get<CommonButton>("UIMask/MenuButtonController/StartGameButton");
        PhotoButton = Get<CommonButton>("UIMask/MenuButtonController/PhotoButton");
        GameSettingsButton = Get<CommonButton>("UIMask/MenuButtonController/GameSettingsButton");
        QuitButton = Get<CommonButton>("UIMask/MenuButtonController/QuitButton");

        LoadGameButton.gameObject.SetActive(SaveGameManager.Instance.Users.Count > 0);


        BindAGVClick(LoadGameButton,LoadGameOnClick,"");
        BindAGVClick(StartGameButton,StartGameOnClick,"");
        BindAGVClick(PhotoButton,PhotoOnClick,"");
        BindAGVClick(GameSettingsButton,GameSettingOnClick,"");
        BindAGVClick(QuitButton,QuitButtonOnClick,"");
    }

    private void LoadGameOnClick()
    {
        UISystem.Instance.OpenUI<LoadSaveGameUI>("LoadSaveGameUI");
    }

    private void StartGameOnClick()
    {
        UISystem.Instance.OpenUI<SetUserNameUI>("SetUserNameUI");
        UISystem.Instance.CloseUI("CommonUI");
    }

    private void PhotoOnClick()
    {
        
    }

    private void GameSettingOnClick()
    {
        UISystem.Instance.OpenUI<GameSettingsUI>("GameSettingsUI");
    }

    private void QuitButtonOnClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }
}
