using XFramework;

public partial class BoothGameStartUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
        CharacterManager.Instance.RegisterCharacterBagChange(GameCostTools.MainCharacterID,CharacterBagChange);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        CharacterManager.Instance.UnregisterCharacterBagChange(GameCostTools.MainCharacterID,CharacterBagChange);
    }

    private void PlayerDataChange(PlayerData playerData)
    {
        fenCount.text = $"{playerData.GetProperty(PropertyType.FenCount)}";
    }

    private void CharacterBagChange(CharacterBag characterBag)
    {
        
    }
    

}
