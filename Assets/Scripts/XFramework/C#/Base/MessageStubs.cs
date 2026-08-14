using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// 消息数据 - 框架存根版本
/// 如果项目不需要消息系统，可以删除此文件
/// </summary>
[Serializable]
public class MessageData
{
    [LabelText("消息ID")]
    public long MessageID;

    [LabelText("消息内容")]
    public string Content;

    [LabelText("发送时间")]
    public DateTime SendTime;
}

/// <summary>
/// 私信背包 - 框架存根版本
/// </summary>
[Serializable]
public class PriavateMessageBag
{
    [LabelText("角色ID")]
    public long CharacterID;

    [LabelText("消息列表")]
    public List<MessageData> Messages = new List<MessageData>();
}

/// <summary>
/// 展会宣发背包 - 框架存根版本
/// </summary>
[Serializable]
public class ExhibitionPromotionBag
{
    [LabelText("宣发ID")]
    public long PromotionID;

    [LabelText("宣发内容")]
    public string Content;
}
