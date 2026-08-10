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

public partial class DramaRuntimeUI : UIBase,IDialogueView,IChoiceView,IActorStage
{
    public  UIDramaBackground BackgroundController { get; private set; }

    public ScreenActionController ScreenActionController => screenActionController;

    public override void Init()
    {
        InitAutoBind();
        talkActionController.Init();
        screenActionController.Init();

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

    /// <summary>弹出选项并等玩家选，返回选中的下标。</summary>
    public async UniTask<int> PickAsync(string[] options, CancellationToken ct)
    {
        await UniTask.CompletedTask;
        return -1;
    }

    /// <summary>拿到（必要时加载并入场）指定角色的立绘。</summary>
    public async UniTask<IActorView> AcquireAsync(int actorId, CancellationToken ct)
    {
        return null;
    }

    /// <summary>找已经在台上的立绘；不在台上返回 null。</summary>
    public IActorView Find(int actorId)
    {
        return null;
    }

    /// <summary>
    /// 显隐。<paramref name="duration"/> 为 0 就是瞬间切换。
    ///
    /// 实现里产生的 Tween <b>必须登记到舞台自己名下</b>，
    /// 这样 <see cref="CompleteAllTweens"/> 才收得住 —— 见
    /// <see cref="ActorShowAction.WaitForCompletion"/> 为 false 的情况。
    /// </summary>
    public async UniTask SetVisibleAsync(IActorView actor, bool visible, float duration, Ease ease, CancellationToken ct)
    {
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 把所有还在跑的立绘动画立刻推到终点。
    /// 剧本结束 / 跳转 / 切到 Skip 时调，防止游离动画漏到下一段剧情里。
    /// </summary>
    public void CompleteAllTweens()
    {
       
    }

    /// <summary>清空舞台并释放立绘资源。</summary>
    public void ReleaseAll()
    {
        
    }
}
