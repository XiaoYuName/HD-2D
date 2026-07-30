using XFramework;

public partial class GemSmartSlicerUI : UIBase
{
    private CharacterBag  characterBag;
    private ClothingBag clothingBag;
    private ClothingData clothingData;

    private UIBackground Background;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        gameInfoUI.Init();
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameSceneManager.Instance.EnterMinGameScene(MinGameSceneType.GemSmartSlicerScene,null);
        Background =  UISystem.Instance.LoadUIBackground<UIBackground>(AssetKeys.GemSmartSlicerBackgroundUIPath);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameSceneManager.Instance.QuitMinGameScene();
        if (Background != null)
        {
            UISystem.Instance.ReleaseUIBackground(Background);
        }
    }

    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;
        if (clothingBag != null)
        {
            clothingData = LubanManager.Instance.TbClothingData.Get(clothingBag.clothingID);
            if (clothingData != null)
            {
                gameInfoUI.SetData(clothingData);
            }
        }
    }
}
