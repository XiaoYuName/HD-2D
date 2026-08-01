using XFramework;

public partial class RacingCarSewingMachinesUI : UIBase
{
    private CharacterBag  characterBag;
    private ClothingBag clothingBag;
    private UIBackground background;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }
    
    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameSceneManager.Instance.EnterMinGameScene(MinGameSceneType.RacingCarSewingMachines,null);
        background = UISystem.Instance.LoadUIBackground<UIBackground>(AssetKeys.RacingCarSewingMachinesBackgroundUIPath);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameSceneManager.Instance.QuitMinGameScene();
        if (background != null)
        {
            UISystem.Instance.ReleaseUIBackground(background);
        }
    }
}
