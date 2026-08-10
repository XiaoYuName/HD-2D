using System;
using Drama.Runtime;
using Drama.Runtime.Services;
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

    public void SetName(DialogueLine line)
    {
        Open();
        switch (line.Speaker)
        {
            case ESpeakerKind.Aside:
                Close();
                break;
            case ESpeakerKind.Hero:
                talkNormalName.SetText("UIText","Character/Hero_Name");
                talkHcgName.SetText("UIText","Character/Hero_Hero_Name");
                break;
            case ESpeakerKind.Custom:
                talkHcgName.SetText(line.SpeakerNameRef.Table, line.SpeakerNameRef.Key);
                talkHcgName.SetText(line.SpeakerNameRef.Table,line.SpeakerNameRef.Key);
                break;
            case ESpeakerKind.Actor:
                NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(line.ActorId);
                if (npcData == null)
                {
                    Close();
                    return;
                }
                talkNormalName.SetText(npcData.Name);
                talkHcgName.SetText(npcData.Name);

                break;
            
        }
    }
}
