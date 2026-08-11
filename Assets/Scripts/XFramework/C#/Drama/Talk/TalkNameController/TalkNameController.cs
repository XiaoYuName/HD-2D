using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;
using XFramework;

public partial class TalkNameController : UIBase
{
    /// <summary>
    /// 主角名在多语言表里的位置。表里的值写成 <c>{global.PlayerName}</c>，
    /// 由 <c>GameDataManager.SetGlobalVariablesSource("global", "PlayerName", ...)</c> 灌进去。
    ///
    /// 这样主角名也能跟着语言变（日文版可以写成「{global.PlayerName}さん」），
    /// 而且这里少一个"字面量而不是引用"的特例分支。
    /// </summary>
    private const string HeroNameTable = "UIText";
    private const string HeroNameKey = "Character/Hero_Name";

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
    /// </summary>
    public void SetName(DialogueLine line)
    {
        switch (line.Speaker)
        {
            case ESpeakerKind.Aside:
                // 旁白不显示名字
                Close();
                return;

            case ESpeakerKind.Hero:
                SetBoth(HeroNameTable, HeroNameKey);
                break;

            case ESpeakerKind.Custom:
                SetBoth(line.SpeakerNameRef.Table, line.SpeakerNameRef.Key);
                break;

            case ESpeakerKind.Actor:
                NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(line.ActorId);
                if (npcData?.Name == null)
                {
                    Debug.LogWarning($"[Drama] 角色 {line.ActorId} 没有名字配置，名字栏不显示");
                    Close();
                    return;
                }

                SetBoth(npcData.Name.Table, npcData.Name.Value);
                break;
        }

        Open();
    }

    /// <summary>两套皮肤各有一个名字 Label，两边都要设——切皮肤时不用重新取名字。</summary>
    private void SetBoth(string table, string key)
    {
        talkNormalName.SetText(table, key);
        talkHcgName.SetText(table, key);
    }
}
