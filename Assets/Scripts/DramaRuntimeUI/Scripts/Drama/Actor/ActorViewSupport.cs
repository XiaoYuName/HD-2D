using System.Collections.Generic;
using DG.Tweening;
using Drama.Runtime.Services;
using UnityEngine;

/// <summary>
/// 本工程的立绘视图。在包的 <see cref="IActorView"/> 之上补三件<b>只有舞台需要</b>的事。
///
/// 不往包的接口上加：这三件都是"宿主怎么实现"的细节 ——
/// 压暗有没有过渡、显隐关的是哪个 GameObject、Live2D 还有个替身要一起收，
/// 换个工程实现方式就变了，包不该知道。
/// </summary>
public interface IDramaActorView : IActorView
{
    /// <summary>把压暗 / 微缩的过渡立刻推到终点。跳过模式和剧本收尾时用。</summary>
    void CompleteHighlightTweens();

    /// <summary>
    /// 整体显隐。<b>不要让舞台直接去 SetActive(Root.gameObject)</b> ——
    /// Live2D 的 Root 是 Canvas 里的替身，关掉替身模型照样在屏幕上。
    /// </summary>
    void SetVisible(bool visible);

    /// <summary>
    /// 下台前的自清理。舞台会销毁本组件所在的 GameObject，
    /// 但 Live2D 在 Canvas 里还有个替身不在那棵树上，得自己收。
    /// </summary>
    void ReleaseView();
}

/// <summary>
/// 三种立绘（Spine / 图片 / Live2D）共用的零碎逻辑。
///
/// <b>为什么不是共享基类</b>：<c>UIBase : MonoBehaviour</c>、<c>GameBase : SerializedMonoBehaviour</c>
/// 是两条不同的继承链，而 Live2D 那份立绘挂在世界空间的模型上（GameBase），
/// 另两份在 Canvas 里（UIBase）—— 强行统一基类得动框架，代价比抽两个工具类大。
/// </summary>
public sealed class ActorHighlightTween
{
    /// <summary>压暗 / 微缩的过渡时长。照旧工程的 0.2s linear。</summary>
    public const float Seconds = 0.2f;

    /// <summary>
    /// Tween 的 id。用实例自己当 id，这样同一个立绘的压暗动画会互相顶掉，
    /// 不同立绘之间互不干扰。
    /// </summary>
    public readonly object ColorId = new object();

    public readonly object ScaleId = new object();

    /// <summary>
    /// 把压暗 / 微缩的过渡立刻推到终点。
    ///
    /// 这两个 Tween 的 target 一个是闭包、一个是子节点，
    /// 舞台那句 <c>DOTween.Complete(Root)</c> 都收不到，所以单独给个出口。
    /// </summary>
    public void CompleteAll()
    {
        DOTween.Complete(ColorId, withCallbacks: true);
        DOTween.Complete(ScaleId, withCallbacks: true);
    }

    public void KillColor() => DOTween.Kill(ColorId);
    public void KillScale() => DOTween.Kill(ScaleId);
}

/// <summary>立绘实现共用的播放模式判断。</summary>
public static class ActorPlayback
{
    /// <summary>
    /// 现在是不是"一切从速"的模式（跳过 / 读档恢复）。
    ///
    /// <b>播动画的地方必须问一句</b>：跳过时播一条 2 秒的非循环动画，
    /// 如果照常 await 它播完，剧本就在这条指令上卡 2 秒 —— 玩家点了跳过却还在等，
    /// 表现和"没生效"一模一样。
    ///
    /// 正确的家其实是包里的 <c>ActorPlayAnimationActionHandler</c>（那儿拿得到 ctx.Mode），
    /// 放在这里是为了不为一个三行的改动再发一次包。等下次发版可以挪过去。
    /// </summary>
    public static bool IsInstant =>
        DramaManager.IsInitialized &&
        Drama.Runtime.Flow.DramaWait.IsInstant(DramaManager.Instance.PlaybackMode);
}

/// <summary>
/// "这种立绘不支持这个能力"的提示。
///
/// <b>只报一次</b>：一段剧本里可能有几十条同类指令，每条都报会把真正的错因冲掉。
/// <b>而且只是警告不抛异常</b>：策划在静态图片上挂了个 Animator 指令是配置错误，
/// 不该升级成"整段剧情播不下去"。
/// </summary>
public sealed class ActorViewWarnings
{
    private readonly HashSet<string> reported = new HashSet<string>();

    public void Once(string feature, int actorId, string viewKind)
    {
        if (!reported.Add(feature))
        {
            return;
        }

        Debug.LogWarning($"[Drama] 角色 {actorId} 是{viewKind}立绘，不支持「{feature}」，本段剧本里这类指令都会被跳过");
    }
}
