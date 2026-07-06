using System;
using UnityEngine;
using XFramework;

public partial class ClawMachineUI : UIBase
{
    private ClawMachineController minGameController;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(guideButton,OpenClawMachineGuideUI,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        UISystem.Instance.CloseUI("MainUI");
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
        GuideManager.Instance.RegisterClawMachineGameDataChange(ClawMachineGameDataChange);
        EnterClawMachineScene();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        UISystem.Instance.OpenUI("MainUI");
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        GuideManager.Instance.UnregisterClawMachineGameDataChange(ClawMachineGameDataChange);
        ExitClawMachineScene();
    }

    private void EnterClawMachineScene()
    {
        GameSceneManager.Instance.EnterMinGameScene(MinGameSceneType.ClawMachineScene, () =>
        {
            minGameController = FindAnyObjectByType<ClawMachineController>();
            minGameController.Initialized();
        });
        
    }

    private void ExitClawMachineScene()
    {
        GameSceneManager.Instance.QuitMinGameScene();
    }


    private void PlayerDataChange(PlayerData playerData)
    {
        int gameValue = playerData.GetProperty(PropertyType.ClawMachineValue);
        clawNumberTex.text = gameValue.ToString();
    }

    private void ClawMachineGameDataChange(ClawMachineGameData clawMachineGameData)
    {
        clawNumberTex.text = $"{clawMachineGameData.DollNumber}";
        resetNumberTex.text = $"{clawMachineGameData.ResetNumber}";
    }

    private void OpenClawMachineGuideUI()
    {
        UISystem.Instance.OpenUI("ClawMachineGuideUI");
    }

}
