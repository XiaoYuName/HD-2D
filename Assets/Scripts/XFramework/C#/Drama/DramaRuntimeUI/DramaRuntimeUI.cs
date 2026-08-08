using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;
using XFramework;

public partial class DramaRuntimeUI : UIBase,IDialogueView,IChoiceView
{
    public  UIBackground BackgroundController { get; private set; }

    public override void Init()
    {
        InitAutoBind();
        talkActionController.Init();
        
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        BackgroundController  = UISystem.Instance.LoadUIBackground<UIBackground>(AssetKeys.DramaBackgroundPath);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        if (BackgroundController != null)
        {
            BackgroundController.Close();
            UISystem.Instance.ReleaseUIBackground(BackgroundController);
        }
    }

    /// <summary>
    /// 显示一句台词并放完打字机效果。
    ///
    /// 实现要点：打字机进行中玩家点一下应当立刻全文显示，
    /// 此时本方法返回，然后才轮到 <see cref="WaitForAdvanceAsync"/> 等真正的翻页。
    /// <paramref name="mode"/> 是 Skip / FastForward 时不要放动画。
    /// </summary>
    public async UniTask ShowLineAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct)
    {
        await talkActionController.ShowLineAsync(line, ct);
        await UniTask.CompletedTask;
    }

    /// <summary>等玩家点击翻页。实现就是一个 UniTaskCompletionSource，点击回调里 TrySetResult。</summary>
    public async UniTask WaitForAdvanceAsync(CancellationToken ct)
    {
        await UniTask.CompletedTask;
    }

    /// <summary>对话框整体显隐。<see cref="TalkShowAction"/> 用。</summary>
    public async UniTask SetVisibleAsync(bool visible, CancellationToken ct)
    {
        await UniTask.CompletedTask;
    }

    /// <summary>切换对话框皮肤。<see cref="SetTalkFrameAction"/> 用。</summary>
    public void SetFrame(ETalkFrame frame)
    {
        
    }

    /// <summary>弹出选项并等玩家选，返回选中的下标。</summary>
    public async UniTask<int> PickAsync(string[] options, CancellationToken ct)
    {
        await UniTask.CompletedTask;
        return -1;
    }
}
