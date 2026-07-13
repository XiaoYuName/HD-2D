using UnityEngine;
using XFramework;

public partial class PostingUpdatesPage : UIBase
{
    private long chatMessageID;
    
    
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        RandomChatData();
        Bind(randomButton,RandomChatData,"");
    }

    private void RandomChatData()
    {
        var index = Random.Range(0, LubanManager.Instance.TbChatMessageData.DataList.Count);
        var chatMessageData = LubanManager.Instance.TbChatMessageData.DataList[index];
        chatMessageID = chatMessageData.ID;
        desc.SetText(chatMessageData.Message);
    }
}
