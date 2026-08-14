using Drama.Runtime;
using Drama.Runtime.Services;
using TMPro;
using UnityEngine;
using XFramework;

public partial class TalkNameController : UIBase
{
    /// <summary>
    /// 预制体上配的名字颜色（两套皮肤各一份）。
    ///
    /// <b>要记下来</b>：剧本没指定颜色时得回到它，而不是"不设"——不设的话
    /// 上一句台词的自定义颜色会一直留在标签上。
    /// </summary>
    private Color normalDefaultColor = Color.white;
    private Color hcgDefaultColor = Color.white;

    private TMP_Text normalNameText;
    private TMP_Text hcgNameText;

    public override void Init()
    {
        InitAutoBind();

        // 名字文本和 LocalizeStringEvent 在同一个节点上
        normalNameText = talkNormalName != null ? talkNormalName.GetComponent<TMP_Text>() : null;
        hcgNameText = talkHcgName != null ? talkHcgName.GetComponent<TMP_Text>() : null;

        // 抓原始颜色必须赶在任何一句台词染色之前，所以放 Init
        if (normalNameText != null) normalDefaultColor = normalNameText.color;
        if (hcgNameText != null) hcgDefaultColor = hcgNameText.color;
    }
    
    public void SetFrame(ETalkFrame frame)
    {
       normal.gameObject.SetActive(frame == ETalkFrame.Normal);
       hCG.gameObject.SetActive(frame == ETalkFrame.HCG);
    }

    /// <summary>
    /// 按说话人寻址方式取名字。
    ///
    /// 四路都走 <c>LocalizeStringEvent</c> 绑定（连主角昵称也是——它在多语言表里写成
    /// <c>{global.PlayerName}</c>），这样玩家中途切语言，名字会跟着刷新。
    /// 名字没有打字机，所以直接绑到 text 上没问题。
    ///
    /// 分支本身在 <see cref="DramaSpeakerName"/> 里，和对话历史（Log）共用同一份。
    /// </summary>
    public void SetName(DialogueLine line)
    {
        if (!DramaSpeakerName.TryResolve(line, out string table, out string key))
        {
            // 旁白 / 没配名字的角色：名字栏整个收掉
            Close();
            return;
        }

        SetBoth(table, key);
        ApplyNameColor(line.NameColor);
        Open();
    }

    /// <summary>两套皮肤各有一个名字 Label，两边都要设——切皮肤时不用重新取名字。</summary>
    private void SetBoth(string table, string key)
    {
        talkNormalName.SetText(table, key);
        talkHcgName.SetText(table, key);
    }

    /// <summary>
    /// 上名字颜色。
    ///
    /// <b>白色 = 剧本没指定</b>（<c>TalkAction.NameColor</c> 的默认值就是白），这时候回到
    /// 预制体上配的颜色 —— 本工程的名字标签配的是粉色，无脑写白会把美术的配色冲掉。
    /// 原工程也是这个规则：log 条目里 <c>GetColor(色号) == Color.white</c> 就当成"没填"，
    /// 回退到角色的默认色。
    ///
    /// 注意是"回到默认"而不是"跳过不设"：跳过的话上一句的自定义颜色会留在标签上。
    /// </summary>
    private void ApplyNameColor(Color scriptColor)
    {
        bool unspecified = scriptColor == Color.white;

        if (normalNameText != null)
        {
            normalNameText.color = unspecified ? normalDefaultColor : scriptColor;
        }

        if (hcgNameText != null)
        {
            hcgNameText.color = unspecified ? hcgDefaultColor : scriptColor;
        }
    }
}
