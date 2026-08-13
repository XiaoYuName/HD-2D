using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;
using XFramework;

public partial class TalkNameController : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
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
        Open();
    }

    /// <summary>两套皮肤各有一个名字 Label，两边都要设——切皮肤时不用重新取名字。</summary>
    private void SetBoth(string table, string key)
    {
        talkNormalName.SetText(table, key);
        talkHcgName.SetText(table, key);
    }
}
