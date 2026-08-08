using System.Threading;
using Cysharp.Threading.Tasks;
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
    
    public async UniTask ShowLineAsync(DialogueLine line, CancellationToken ct)
    {
        
    }
}
