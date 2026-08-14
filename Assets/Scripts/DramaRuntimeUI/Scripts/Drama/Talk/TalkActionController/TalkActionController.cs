using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Handlers;
using Drama.Runtime.Services;
using UnityEngine;
using XFramework;

/// <summary>
/// 对话框。<see cref="IDialogueView"/> 的实际干活的地方（DramaRuntimeUI 只是转发）。
///
/// <b>整个对话框只有一个点击入口</b>（<see cref="HandleClick"/>），四态：
///   跳过模式中   → 这次点击是刹车，退回正常模式，不翻页
///   框被玩家收起 → 这次点击只重新打开
///   打字机在跑   → 这次点击只把字一次性显示完
///   都不是       → 才真正翻页
/// 后三态照的是旧工程 UI_Drama.NextStepClick 的语义；
/// 第一态是本工程加的，旧工程在跳过态下点击是完全无效的（见 HandleClick 的注释）。
/// </summary>
public partial class TalkActionController : UIBase
{
    /// <summary>气泡抖动的振幅 / 时长。旧工程写死的是 SetShakeDuration(0f, 5f)，时长 0 兜底成 0.2s。</summary>
    private const float BalloonShakeAmplitude = 5f;
    private const float BalloonShakeSeconds = 0.2f;

    /// <summary>自动播放的等待时长 = 每字 × 字数 + 尾巴。数值取自旧工程 DramaData.AutoPlaySpeed。</summary>
    private const float AutoSecondsPerChar = 0.075f;
    private const float AutoSecondsTail = 0.3f;

    /// <summary>跳过模式每句的固定间隔，旧工程同样写死 0.1。</summary>
    private const float SkipAdvanceSeconds = 0.1f;

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

        talkBackgroundController.BindPlaybackButtons(OnAutoClick, OnSkipClick, OnLogClick);
        RefreshPlaybackButtons();

        SetFrame(currentFrame);
    }

    public override void Close()
    {
        // 别把还在 await 的一方永久挂住。
        // 注意是 AbortWaiting 而不是 talkContentController.Close() —— 后者会把子节点
        // SetActive(false)，而 base.Open() 只开自己开不到子节点，下次就再也显示不出来了
        advance?.TrySetCanceled();
        advance = null;

        // 框都收了，"玩家临时收起"这个临时态没有意义了。留着的话下次进剧情
        // 第一次点击会被当成"重新打开"吃掉，而不是翻页
        isHiddenByPlayer = false;

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

        // 跳过模式的节奏只能压在这儿：TalkActionHandler 在 Skip 下显示完台词就直接返回，
        // 根本不会走 WaitForAdvanceAsync，那条计时器管不到。不压的话每句只占一帧，
        // 整段剧情一两秒就冲完了，玩家连自己跳过了什么都看不见。0.1 秒是原工程的值
        if (mode == EDramaPlaybackMode.Skip)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(SkipAdvanceSeconds),
                                DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
        }
    }

    /// <summary>
    /// 把 AUTO / SKIP 的选中态刷成当前播放模式。
    ///
    /// <b>不能只在 <see cref="Init"/> 和 <see cref="Open"/> 里刷。</b>
    /// Init 一个 UI 实例只跑一次；而对话框关的是 <c>DramaRuntimeUI</c>，
    /// 本控制器的 <c>isOpen</c> 跨剧本一直是 true，第二次进剧情 <c>Open()</c> 根本不会再走 ——
    /// 所以 <c>DramaRuntimeUI.Open()</c> 每次都要显式调一下这个。
    /// </summary>
    public void RefreshPlaybackButtons()
    {
        // MonoSingleton.Instance 是裸字段不会自建，切场景 / 退出时可能已经没了
        if (talkBackgroundController != null && DramaManager.IsInitialized)
        {
            talkBackgroundController.RefreshPlaybackButtons(DramaManager.Instance.PlaybackMode);
        }
    }

    public override void Open()
    {
        base.Open();
        RefreshPlaybackButtons();
    }

    /// <summary>
    /// 等玩家点击翻页。真正的判断在 <see cref="HandleClick"/> 里。
    ///
    /// 自动 / 跳过模式下，<see cref="AutoAdvanceAsync"/> 到点会替玩家"点一下" ——
    /// 走的是同一个 tcs，所以这两条路天然是赛跑关系，谁先到听谁的，不用额外合并。
    /// </summary>
    public async UniTask WaitForAdvanceAsync(CancellationToken ct)
    {
        UniTaskCompletionSource tcs = new UniTaskCompletionSource();
        advance = tcs;

        // 输的那一方要停下来。不掐的话计时循环会一直跑进下一句，
        // 攒出来的时间会把下一句直接冲过去
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            AutoAdvanceAsync(tcs, linked.Token).Forget();
            await tcs.Task.AttachExternalCancellation(ct);
        }
        finally
        {
            linked.Cancel();

            // ★ 必须清掉。留着的话下一句会被这次残留的 tcs 立刻满足，台词直接跳过去
            if (advance == tcs)
            {
                advance = null;
            }
        }
    }

    /// <summary>
    /// 自动 / 跳过的推进计时，到点就替玩家点一下。
    ///
    /// 手感照抄原工程 <c>UI_Drama.Update</c>：
    ///   自动 —— 等 <c>0.075 × 本句字数 + 0.3</c> 秒；<b>语音还在念就不计时</b>（开自动的人是想听完的）
    ///   跳过 —— 固定 0.1 秒，不理语音
    /// 字数进公式是因为长句短句念完的时间差很多，固定间隔要么长句看不完、要么短句干等。
    ///
    /// <b>每帧现读模式而不是进来时读一次</b>：玩家在这句等待期间才点开自动，也要立刻算数。
    /// 反过来中途关掉就把计时清零 —— 不清的话再开时攒着的时间会一下把台词冲过去。
    /// </summary>
    private async UniTaskVoid AutoAdvanceAsync(UniTaskCompletionSource target, CancellationToken ct)
    {
        float elapsed = 0f;

        try
        {
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                // 这一句已经翻过页了（被点击或被取消），本次计时作废
                if (advance != target)
                {
                    return;
                }

                EDramaPlaybackMode mode = DramaManager.Instance.PlaybackMode;

                if (mode == EDramaPlaybackMode.Skip)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed >= SkipAdvanceSeconds)
                    {
                        break;
                    }

                    continue;
                }

                if (mode != EDramaPlaybackMode.Auto)
                {
                    elapsed = 0f;
                    continue;
                }

                // 语音直接问 AudioManager 而不是走 IDramaAudio：剧情语音独占人声轨，
                // 这条轨在响就说明这句还没念完。包的接口上没有"在不在播"，也不该有 ——
                // 那是宿主的音频系统才知道的事
                if (AudioManager.Instance.IsHumanPlaying)
                {
                    continue;
                }

                elapsed += Time.deltaTime;
                if (elapsed >= AutoAdvanceSeconds())
                {
                    break;
                }
            }

            target.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            // 翻页了 / 剧情结束了，正常路径
        }
    }

    private float AutoAdvanceSeconds() =>
        AutoSecondsPerChar * talkContentController.CurrentTextLength + AutoSecondsTail;

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
        // ⓪ 跳过模式下点一下 = 踩刹车：回正常模式，并且【不】翻页。
        //    玩家是看到想看的东西才伸手的，顺手再走一句正好把它冲掉。
        //
        //    这一条和原工程不同：原工程 NextStepClick 开头是 if (mIsSkipDrama) return，
        //    跳过态下点击完全无效，也退不出跳过，只能再点一次 SKIP 按钮。
        //
        //    自动模式故意没有这条 —— 那边点击是"提前翻页"，翻完继续自动，
        //    和原工程一致；自动本来就不吞内容，不需要刹车。
        if (DramaManager.IsInitialized &&
            DramaManager.Instance.PlaybackMode == EDramaPlaybackMode.Skip)
        {
            StopAutoAndSkip();
            return;
        }

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

    // ============================================================ AUTO / SKIP

    private void OnAutoClick() => TogglePlaybackMode(EDramaPlaybackMode.Auto);

    private void OnSkipClick() => TogglePlaybackMode(EDramaPlaybackMode.Skip);

    /// <summary>
    /// 点 LOG：翻看之前的台词。
    ///
    /// <b>先关掉自动 / 跳过再开界面</b>，和原工程一致（<c>OpenLogClick</c> 里
    /// <c>OpenLog(); StopAutoAndSkip();</c>）—— 玩家要回头看，剧情还在背后自己往下播
    /// 等于一边看一边把内容冲掉。
    ///
    /// 顺序是先停再开：反过来的话开界面那一帧自动计时还在跑，可能正好翻掉一句。
    /// </summary>
    private void OnLogClick()
    {
        StopAutoAndSkip();
        DramaManager.Instance.ShowDramaLogUI();
    }

    /// <summary>
    /// 点 AUTO / SKIP：已经是这个模式就关掉（回正常），否则切过去。
    /// 两者互斥是白送的 —— 模式本来就是一个枚举，开一个另一个自然灭。
    ///
    /// <b>从「正常」切出去时先替玩家走一步</b>，不然点完还要干等当前这句走完。
    /// 这个条件是照抄原工程的：它写的是 <c>if (!mIsAutoDrama) NextStepClick()</c>，
    /// 而跳过态下 <c>GoSpeed</c> 会把 auto 也一起置 true，所以那个判断实际就等于
    /// "当前是正常模式"。自动↔跳过互切、以及关掉回正常，都不走这一步。
    ///
    /// 走 <see cref="HandleClick"/> 而不是直接翻页，是为了让"打字机在跑就只把字打完"
    /// 这条三态语义继续成立 —— 玩家点 AUTO 不该把没看完的半句直接吞掉。
    /// </summary>
    private void TogglePlaybackMode(EDramaPlaybackMode mode)
    {
        EDramaPlaybackMode previous = DramaManager.Instance.PlaybackMode;
        EDramaPlaybackMode next = previous == mode ? EDramaPlaybackMode.Normal : mode;

        // ★ 必须先走这一步、后翻开关（原工程 AutoSwitchClick 也是这个顺序）。
        // 反过来的话，点 SKIP 时模式已经是 Skip 了，HandleClick 的刹车分支会
        // 立刻把它踩回正常 —— 按钮按下去等于没反应
        if (previous == EDramaPlaybackMode.Normal)
        {
            HandleClick();
        }

        DramaManager.Instance.SetPlaybackMode(next);
        talkBackgroundController.RefreshPlaybackButtons(next);
    }

    /// <summary>
    /// 回正常模式并刷按钮。玩家收框、开 Log、选完选项这类"我要自己看"的操作都该调它。
    ///
    /// <b>模式和按钮态必须一起改</b>：只调 <c>DramaManager.SetPlaybackMode</c> 的话
    /// 按钮还亮着，和实际模式对不上；AUTO / SKIP 的选中态在本控制器手里。
    /// </summary>
    public void StopAutoAndSkip()
    {
        if (DramaManager.Instance.PlaybackMode == EDramaPlaybackMode.Normal)
        {
            return;
        }

        DramaManager.Instance.SetPlaybackMode(EDramaPlaybackMode.Normal);
        talkBackgroundController.RefreshPlaybackButtons(EDramaPlaybackMode.Normal);
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

        // 玩家主动收框是"我要自己看画面"，这时候还在自动/跳过推进就等于把剧情偷偷播完了。
        // 原工程 OnHideTalk 里同样调 StopAutoAndSkip
        StopAutoAndSkip();

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
