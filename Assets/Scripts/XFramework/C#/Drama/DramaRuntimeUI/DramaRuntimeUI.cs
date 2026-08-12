using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;
using Drama.UI;
using UnityEngine;
using XFramework;

public partial class DramaRuntimeUI : UIBase,IDialogueView,IChoiceView
{
    public  UIDramaBackground BackgroundController { get; private set; }

    public ScreenActionController ScreenActionController => screenActionController;

    public ActorController ActorController => actorController;

    public override void Init()
    {
        InitAutoBind();
        // 翻页点击走这个盖满全屏的 Button，不走 PlayerInputManager ——
        // 全局输入连玩家点选项 / 点菜单都会算作翻页。
        // 它是 TalkActionController 的兄弟节点而不是子节点：对话框被玩家收起来之后
        // 还得有个东西接管点击，才能再点一次把框叫回来。
        // Init 只跑一次，所以这里订阅一次就够，不用在 Open/Close 里配对。
        onClikc.onClick.RemoveAllListeners();
        onClikc.onClick.AddListener(talkActionController.HandleClick);
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        BackgroundController  = UISystem.Instance.LoadUIBackground<UIDramaBackground>(AssetKeys.DramaBackgroundPath);

        // AUTO / SKIP 是跨剧本保持的，进来时得按当前模式把选中态画对。
        // 这一句只能放在这儿：关的是本面板，子控制器的 isOpen 一直是 true，
        // 它自己的 Open() 第二次进剧情不会再走
        talkActionController.RefreshPlaybackButtons();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();

        // 对话框要跟着收掉。关本面板不会级联到子控制器，它的 isOpen 会一直是 true，
        // 下次进剧情就顶着上一段最后一句话显示出来了 —— 而"显示"本该是台词指令的副作用，
        // 第一条 TalkAction 走 ShowLineAsync 时会自己 Open，这里收干净不影响下次
        talkActionController.Close();

        // 选项面板同理。正常选完 PickAsync 自己会收，这里是给"选到一半被退出"兜底 ——
        // 不收的话按钮会漏在池外面，下次进剧情还挂着上一轮的选项
        optionController.ClearImmediate();

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
    public UniTask ShowLineAsync(DialogueLine line, EDramaPlaybackMode mode, CancellationToken ct)
        => talkActionController.ShowLineAsync(line, mode, ct);

    /// <summary>等玩家点击翻页。三态判断在 TalkActionController.OnClick 里。</summary>
    public UniTask WaitForAdvanceAsync(CancellationToken ct)
        => talkActionController.WaitForAdvanceAsync(ct);

    /// <summary>对话框整体显隐。<see cref="TalkShowAction"/> 用。</summary>
    public UniTask SetVisibleAsync(bool visible, CancellationToken ct)
        => talkActionController.SetVisibleAsync(visible, ct);

    /// <summary>切换对话框皮肤。<see cref="SetTalkFrameAction"/> 用。</summary>
    public void SetFrame(ETalkFrame frame)
    {
        talkActionController.SetFrame(frame);
    }

    /// <summary>弹出选项并等玩家选，返回选中的下标。收的是多语言引用，选项期间切语言会跟着变。</summary>
    public async UniTask<int> PickAsync(LocalizedRef[] options, CancellationToken ct)
    {
        int picked = await optionController.PickAsync(options, ct);

        // 选完就把跳过关掉，和多数 AVG 一致：玩家刚做完一个决定，通常想看看结果，
        // 继续跳过等于把自己选出来的那段直接冲掉。
        //
        // 自动【不】关 —— 自动只是替玩家翻页，不吞内容。
        //
        // 放在这一层而不是包的 ChoiceActionHandler 里，是因为 AUTO / SKIP 的按钮态
        // 归 TalkActionController 管：包那边只改得到 ctx.Mode，改完按钮还亮着，对不上
        if (DramaManager.IsInitialized &&
            DramaManager.Instance.PlaybackMode == EDramaPlaybackMode.Skip)
        {
            talkActionController.StopAutoAndSkip();
        }

        return picked;
    }
}
