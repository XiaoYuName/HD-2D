using XFramework;

public partial class PrivateMessagePage : UIBase
{
    private enum ShowModel { Home,Message}
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(startReplyButton,ShowDialogue,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        
    }

    private void PlayerDataChange(PlayerData playerData)
    {
        if (playerData.GetProperty(PropertyType.ActionPointsValue) >= 1)
        {
            startReplyButton.interactable = true;
        }
        else
        {
            startReplyButton.interactable = false;
        }
    }

    private void Option(ShowModel showModel)
    {
        messageInfo.gameObject.SetActive(showModel == ShowModel.Home);
        message.gameObject.SetActive(showModel == ShowModel.Message);
    }
    
    private void ShowDialogue()
    {
        UIUtility.ShowPopDialogue("StartReplyMessageContent",confirmAction:StartReply,cancelAction:null);
    }

    private void StartReply()
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,1);
        Option(ShowModel.Message);
    }
}
