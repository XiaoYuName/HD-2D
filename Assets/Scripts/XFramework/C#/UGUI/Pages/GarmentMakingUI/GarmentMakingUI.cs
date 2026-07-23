using XFramework;

public partial class GarmentMakingUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        commonTopUI.Init();
        commonTopUI.SetTitle(uiPageData.PageID,"Title");
        commonTopUI.SetClose(Close);
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        commonTopUI.Open();
        CharacterManager.Instance.RegisterCharacterBagChange(GameCostTools.MainCharacterID,SelectedCharacterBagChange);
    }


    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        commonTopUI.Close();
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,SelectedCharacterBagChange);
    }


    private void SelectedCharacterBagChange(CharacterBag characterBag)
    {
       CharacterData characterData =  CharacterManager.Instance.GetCharacterDataByID(characterBag.CharacterID);
       foreach (var ID in characterData.ClothingList)
       {
           
       }
       
    }
}
