using Drama.Runtime;
using XFramework;

public partial class TalkBackgroundController : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }
    
    public void SetFrame(ETalkFrame frame)
    {
        talkNormalBackgroud.gameObject.SetActive(frame == ETalkFrame.Normal);
        talkHCGBackgroud.gameObject.SetActive(frame == ETalkFrame.HCG);
    }
}
