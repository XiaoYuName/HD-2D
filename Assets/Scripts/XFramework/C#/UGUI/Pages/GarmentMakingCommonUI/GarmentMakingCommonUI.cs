using XFramework;

public partial class GarmentMakingCommonUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(onlineGameButton,OpenOnLineGameUI,"");
        Bind(garmentMakingButton,OpenGarmentMaking,"");
        Bind(sleepButton,Sleep,"");
    }

    private void OpenOnLineGameUI()
    {
        UISystem.Instance.OpenUI<OnLineGameUI>("OnLineGameUI");
    }

    private void OpenGarmentMaking()
    {
        
    }

    private void Sleep()
    {
        GameDataManager.Instance.Sleep();
    }
}
