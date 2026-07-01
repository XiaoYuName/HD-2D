using XFramework;

public partial class PopClawMachineTipUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(addGameNumberBtn,AddClawMachineNumber,"");
        Bind(startGameButton,StartGameButton,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
        PlayerInputManager.Instance.OnRightClick += Close;
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        PlayerInputManager.Instance.OnRightClick -= Close;
    }

    private void AddClawMachineNumber()
    {
        if (GameDataManager.Instance.GetProperty(PropertyType.HeartCoins).Value > 0)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.HeartCoins,1);
            GameDataManager.Instance.AddProperty(PropertyType.ClawMachineValue,1);
        }
        else
        {
            UIUtility.ShowPopWindow("HearCoinsRemoveTip");
        }
    }


    private void PlayerDataChange(PlayerData playerData)
    {
        if (playerData == null) return;
        number.text = playerData.GetProperty(PropertyType.ClawMachineValue).ToString();
        
    }

    private void StartGameButton()
    {
        if (GameDataManager.Instance.GetProperty(PropertyType.ClawMachineValue).Value > 0)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.ClawMachineValue,1);
            Close();
            UISystem.Instance.OpenUI<ClawMachineUI>("ClawMachineUI");
        }
        else
        {
            UIUtility.ShowPopWindow("ClawMachineRemoveTip");
        }
    }

}
