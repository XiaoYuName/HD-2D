using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Services;
using XFramework;

public partial class TalkActionController : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        talkBackgroundController.Init();
        talkNameController.Init();
        talkContentController.Init();
    }

    public void SetFrame(ETalkFrame frame)
    {
        talkBackgroundController.SetFrame(frame);
        talkContentController.SetFrame(frame);
        talkNameController.SetFrame(frame);
    }

    public async UniTask ShowLineAsync(DialogueLine line, CancellationToken ct)
    {
        talkNameController.SetName(line);
    }
    
}
