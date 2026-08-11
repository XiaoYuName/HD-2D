using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Handlers;
using Drama.Runtime.Services;
using XFramework;

/// <summary>
/// 对话框。<see cref="IDialogueView"/> 的实际干活的地方（DramaRuntimeUI 只是转发）。
///
/// <b>整个对话框只有一个点击入口</b>（<see cref="OnClick"/>），三态：
///   框被玩家收起 → 这次点击只重新打开
///   打字机在跑   → 这次点击只把字一次性显示完
///   都不是       → 才真正翻页
/// 这套结构照的是旧工程 UI_Drama.NextStepClick 的语义。
/// </summary>
public partial class TalkActionController : UIBase
{
    /// <summary>气泡抖动的振幅 / 时长。旧工程写死的是 SetShakeDuration(0f, 5f)，时长 0 兜底成 0.2s。</summary>
    private const float BalloonShakeAmplitude = 5f;
    private const float BalloonShakeSeconds = 0.2f;

    /// <summary>当前皮肤。<b>要存成状态</b>：藏了再显出来得知道显哪一套。</summary>
    private ETalkFrame currentFrame = ETalkFrame.Normal;

    /// <summary>玩家自己把框收起来了（想看画面）。这是临时态，跟剧情状态无关。</summary>
    private bool isHiddenByPlayer;

    /// <summary><see cref="WaitForAdvanceAsync"/> 在等的信号，点击三态机的第三态触发。</summary>
    private UniTaskCompletionSource advance;

    public override void Init()
    {
        InitAutoBind();

        talkBackgroundController.Init();
        talkNameController.Init();
        talkContentController.Init();

        SetFrame(currentFrame);
    }

    public override void Close()
    {
        // 别把还在 await 的一方永久挂住。
        // 注意是 AbortWaiting 而不是 talkContentController.Close() —— 后者会把子节点
        // SetActive(false)，而 base.Open() 只开自己开不到子节点，下次就再也显示不出来了
        advance?.TrySetCanceled();
        advance = null;

        talkContentController.AbortWaiting();
        base.Close();
    }

    // ============================================================ IDialogueView

    /// <summary>
    /// 显示一句台词并等打字机跑完。
    ///
    /// <b>开头自己把框显出来</b>：显示是台词指令的副作用，不该要求剧本先来一条「对话框显示」。
    /// 旧工程也是这么做的（每条 Talk 开头硬调 TalkShow(true)）。
    /// </summary>
    public async UniTask ShowLineAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct)
    {
        if (!isOpen)
        {
            Open();
        }

        isHiddenByPlayer = false;
        ApplyFrame();

        talkNameController.SetName(line);

        await talkContentController.ShowTextAsync(line, mode, ct);

        // 气泡抖动放在 ShowLineAsync 里面，所以"抖完才准翻页"是天然的 ——
        // Handler 要等本方法返回才会去 await WaitForAdvanceAsync，不需要额外加锁。
        // 旧工程是靠 SetTalkBallon 返回 true 时不 UnLock 来实现同一件事的。
        if (line.Balloon != EBalloonKind.Normal && mode != EDramaPlaybackMode.Skip)
        {
            await DramaShake.HardAsync(transform, EShakeAxis.PositionXY,
                                       BalloonShakeAmplitude, BalloonShakeSeconds,
                                       restoreOnEnd: true, ct);
        }
    }

    /// <summary>等玩家点击翻页。真正的判断在 <see cref="OnClick"/> 里。</summary>
    public async UniTask WaitForAdvanceAsync(CancellationToken ct)
    {
        advance = new UniTaskCompletionSource();
        try
        {
            await advance.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            // ★ 必须清掉。留着的话下一句会被这次残留的 tcs 立刻满足，台词直接跳过去
            advance = null;
        }
    }

    /// <summary>剧本显式控制的显隐（TalkShowAction）。玩家临时收框不走这里。</summary>
    public UniTask SetVisibleAsync(bool visible, CancellationToken ct)
    {
        if (visible)
        {
            Open();
            ApplyFrame();
        }
        else
        {
            Close();
        }

        return UniTask.CompletedTask;
    }

    public void SetFrame(ETalkFrame frame)
    {
        currentFrame = frame;
        ApplyFrame();
    }

    // ============================================================ 点击

    /// <summary>
    /// 唯一的点击入口，三态见类注释。
    ///
    /// <b>由 DramaRuntimeUI 那个盖满全屏的 Button 驱动，不是 PlayerInputManager。</b>
    /// 走全局输入的话，玩家点选项、点菜单这些真按钮时也会顺带翻页。
    /// </summary>
    public void HandleClick()
    {
        // ① 玩家收起了框 → 这次点击只用来重新打开，不推进剧情
        if (isHiddenByPlayer)
        {
            ReopenByPlayer();
            return;
        }

        // ② 打字机还在跑 → 只把字显示完。SkipTypewriter 会触发 onTextShowed，
        //    ShowLineAsync 自然返回，走的和"自然跑完"同一条出口
        if (talkContentController.IsShowingText)
        {
            talkContentController.SkipTypewriter();
            return;
        }

        // ③ 都不是 → 真正翻页
        advance?.TrySetResult();
    }

    /// <summary>
    /// 玩家临时收起对话框（想看立绘 / 背景）。
    ///
    /// <b>这不经过 IDialogueView</b> —— 从 DramaPlayer 的视角看就是"这句还没翻页"，
    /// 它本来就卡在 WaitForAdvanceAsync 上，天然停住，流程层什么都不用改。
    /// 触发口（右键 / 盖满全屏的透明按钮）还没接，接的时候调这个就行。
    /// </summary>
    public void HideByPlayer()
    {
        if (isHiddenByPlayer)
        {
            return;
        }

        isHiddenByPlayer = true;
        base.Close();
    }

    private void ReopenByPlayer()
    {
        isHiddenByPlayer = false;
        base.Open();
        ApplyFrame();
    }

    // ============================================================ 内部

    /// <summary>
    /// 按当前皮肤刷一遍三个子控制器。别默认回普通框。
    ///
    /// 顺手保证背景和正文是激活的：<see cref="UIBase.Open"/> 只管自己这一个 GameObject，
    /// 子节点要是被谁 SetActive(false) 过（手动关的、或者早先版本的代码关的），
    /// 光开父节点是显示不出来的。名字栏不在这儿管 —— 它按说话人逐句开关，归 SetName。
    /// </summary>
    private void ApplyFrame()
    {
        if (!talkBackgroundController.isOpen)
        {
            talkBackgroundController.Open();
        }

        if (!talkContentController.isOpen)
        {
            talkContentController.Open();
        }

        talkBackgroundController.SetFrame(currentFrame);
        talkContentController.SetFrame(currentFrame);
        talkNameController.SetFrame(currentFrame);
    }
}
