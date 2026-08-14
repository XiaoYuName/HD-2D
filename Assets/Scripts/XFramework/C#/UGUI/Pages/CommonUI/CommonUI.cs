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
    }
    

    private void QuitButtonOnClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }
}
