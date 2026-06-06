using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class CommonUI : UIBase
{
    private Button StartButton;
    
    public override void Init()
    {
        Debug.Log("CommonUI init");
        UISystem.Instance.AddUI("CommonUI",this);
        StartButton = Get<Button>("UIMask/Button");
        Bind(StartButton,StartGame,"");
    }

    private void StartGame()
    {
        GameDataManager.Instance.EnterGameScene(GameDataManager.Instance.CurrentUser.SceneID,
            GameDataManager.Instance.CurrentUser.minSceneID);
        UISystem.Instance.OpenUI<MainUI>("MainUI");
        UISystem.Instance.CloseUI("CommonUI");
    }
}
