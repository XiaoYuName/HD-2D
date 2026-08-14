using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Services;
using Spine;
using Spine.Unity;
using UnityEngine;
using XFramework;
using Animation = Spine.Animation;

/// <summary>
/// 一个在台上的立绘。<see cref="IActorView"/> 的实现。
///
/// <b>Spine 4.3 的 UI 路线是两个组件并存</b>，不是二选一：
///   <see cref="SkeletonGraphic"/>   → MaskableGraphic，负责在 Canvas 里渲染、管颜色/透明度
///   <see cref="SkeletonAnimation"/> → 负责 AnimationState、播动画、换皮肤
/// （见 <c>SkeletonGraphic.NewSkeletonGraphicGameObject</c> 的返回类型）
///
/// <b>预制体约定</b>：本组件挂在根节点上，Skeleton 那两个组件挂在<b>子节点</b>。
/// 因为 <see cref="Root"/>（也就是根节点）的 position/scale/rotation 归剧本指令用，
/// 「微缩」要动的是另一个节点，不能跟剧本抢同一个 localScale。
/// </summary>
public class ActorSkeletonController : UIBase, IDramaActorView
{
    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    /// <summary>没有额外资源要收 —— 舞台销毁本节点就够了。</summary>
    public void ReleaseView()
    {
    }

    /// <summary>
    /// 压暗 / 微缩的过渡时长。照旧工程的 0.2s linear。
    ///
    /// 强度（压到多少亮度、缩到多少倍）已经不写死了 —— 由剧本在「讲话人缩放」节点上配，
    /// 走 <see cref="SetDim"/> / <see cref="SetShrink"/> 传进来。
    /// </summary>
    private const float HighlightSeconds = 0.2f;

    private SkeletonGraphic skeletonGraphic;
    private SkeletonAnimation skeletonAnimation;

    /// <summary>Skeleton 所在的节点。微缩动它，不动 <see cref="Root"/>。</summary>
    private Transform skeletonRoot;

    private bool shrinkUnavailableLogged;

    public int ActorId { get; private set; }

    /// <summary>位移 / 缩放 / 旋转 / 抖动都是 DOTween 直接动这个（剧本指令的地盘）。</summary>
    public Transform Root => transform;

    public override void Init()
    {
        skeletonGraphic = GetComponentInChildren<SkeletonGraphic>();
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();

        skeletonRoot = skeletonGraphic != null
            ? skeletonGraphic.transform
            : skeletonAnimation != null ? skeletonAnimation.transform : null;

        // 每个实例两个独立的 Tween id，压暗和微缩各一个
        highlightColorId = new object();
        highlightScaleId = new object();

        if (skeletonGraphic == null)
        {
            Debug.LogError($"[Drama] 立绘预制体上没有 SkeletonGraphic，UI 里渲染不出来：{name}");
        }
    }

    /// <summary>
    /// 入场时由舞台调一次：记住自己是谁，并把这个角色的 Spine 数据换进来。
    ///
    /// 预制体是所有角色<b>共用一个模板</b>，靠这里换 <see cref="SkeletonDataAsset"/> 变成具体角色，
    /// 而不是每个角色出一个 Prefab（角色表里 illustPath 存的就是 SkeletonData 资产路径）。
    /// </summary>
    public void Bind(int actorId, SkeletonDataAsset skeletonData)
    {
        ActorId = actorId;

        if (skeletonData == null || skeletonGraphic == null)
        {
            return;
        }

        // 4.3 里 skeletonDataAsset 只有一份存储（在渲染组件上），
        // SkeletonAnimationBase.skeletonDataAsset 是代理到 Renderer 的，所以赋值一次就够
        skeletonGraphic.skeletonDataAsset = skeletonData;

        // overwrite: true 强制按新数据重建 skeleton 和 mesh
        skeletonGraphic.Initialize(true);

        // 渲染重建完还要让动画组件重建 AnimationState，否则播动画时拿的还是旧 skeleton
        skeletonAnimation?.Initialize(true);
    }

    public void SetAlpha(float alpha)
    {
        if (skeletonGraphic == null)
        {
            return;
        }

        Color c = skeletonGraphic.color;
        c.a = Mathf.Clamp01(alpha);
        skeletonGraphic.color = c;
    }

    /// <summary>
    /// 压暗到指定亮度，1 = 原样。0.2 秒过渡，对齐旧工程的
    /// <c>FadeRawImageColor(GRAY, 0.2f, linear)</c>。
    ///
    /// 走 SkeletonGraphic 的 color 而不是 UIEffect：<c>skeleton.SetColor(color)</c> 是整体
    /// 顶点色相乘，正好就是"压到 N% 亮度"这个效果，不需要额外组件。
    /// </summary>
    public void SetDim(float brightness)
    {
        if (skeletonGraphic == null)
        {
            return;
        }

        float b = Mathf.Clamp01(brightness);

        // 别动 alpha —— 那是显隐动画的地盘
        Color target = new Color(b, b, b, skeletonGraphic.color.a);

        DOTween.Kill(highlightColorId);
        DOTween.To(() => skeletonGraphic.color,
                   c => skeletonGraphic.color = c,
                   target, HighlightSeconds)
               .SetEase(Ease.Linear)
               .SetId(highlightColorId);
    }

    /// <summary>缩到指定倍率，1 = 原样。</summary>
    public void SetShrink(float scale)
    {
        // 这里只能动 skeletonRoot：Root 的 localScale 是 ActorScaleAction 的，
        // 两边抢同一个值的话，剧本一缩放就把微缩状态冲掉了
        if (skeletonRoot == null || skeletonRoot == transform)
        {
            if (!shrinkUnavailableLogged)
            {
                shrinkUnavailableLogged = true;
                Debug.LogWarning($"[Drama] {name} 的 Skeleton 直接挂在根节点上，微缩会和剧本的缩放指令冲突，已跳过。" +
                                 "把 Skeleton 移到子节点即可。");
            }

            return;
        }

        DOTween.Kill(highlightScaleId);
        skeletonRoot.DOScale(scale, HighlightSeconds)
                    .SetEase(Ease.Linear)
                    .SetId(highlightScaleId);
    }

    /// <summary>
    /// 把压暗 / 微缩的过渡立刻推到终点。
    ///
    /// 这两个 Tween 的 target 一个是闭包、一个是 skeleton 子节点，
    /// 舞台那句 <c>DOTween.Complete(Root)</c> 都收不到，所以单独给个出口。
    /// </summary>
    public void CompleteHighlightTweens()
    {
        DOTween.Complete(highlightColorId, withCallbacks: true);
        DOTween.Complete(highlightScaleId, withCallbacks: true);
    }

    /// <summary>
    /// Tween 的 id。用实例自己当 id，这样同一个立绘的压暗动画会互相顶掉，
    /// 不同立绘之间互不干扰。
    /// </summary>
    private object highlightColorId;
    private object highlightScaleId;

    public void SetSkin(string skinName)
    {
        Skeleton skeleton = Skeleton;
        if (skeleton == null || string.IsNullOrEmpty(skinName))
        {
            return;
        }

        if (skeleton.Data.FindSkin(skinName) == null)
        {
            Debug.LogWarning($"[Drama] 角色 {ActorId} 没有皮肤「{skinName}」，已跳过");
            return;
        }

        skeleton.SetSkin(skinName);
        skeleton.SetupPoseSlots();          // 4.3 里这个方法叫 SetupPoseSlots（老版本是 SetSlotsToSetupPose）
        skeletonAnimation?.AnimationState.Apply(skeleton);
    }

    /// <summary>
    /// 播动画。<paramref name="loop"/> 为 true 时<b>立刻返回</b>——
    /// 循环动画永远等不到结束，等它剧本就死在这条指令上了。
    /// </summary>
    public UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale, CancellationToken ct)
    {
        if (skeletonAnimation == null || string.IsNullOrEmpty(animationName))
        {
            return UniTask.CompletedTask;
        }

        // SetAnimation 传不存在的名字会抛 ArgumentException，先查一遍
        Animation animation = skeletonAnimation.Skeleton?.Data.FindAnimation(animationName);
        if (animation == null)
        {
            Debug.LogWarning($"[Drama] 角色 {ActorId} 没有动画「{animationName}」，已跳过");
            return UniTask.CompletedTask;
        }

        TrackEntry entry = skeletonAnimation.AnimationState.SetAnimation(track, animation, loop);
        if (entry == null)
        {
            return UniTask.CompletedTask;
        }

        entry.TimeScale = timeScale <= 0f ? 1f : timeScale;

        // 循环动画永远等不到结束；跳过 / 读档恢复时非循环的也不等 ——
        // 动画照切（那是结果），但不能让剧本在这条指令上卡满一整个动画长度
        if (loop || ActorPlayback.IsInstant)
        {
            return UniTask.CompletedTask;
        }

        UniTaskCompletionSource done = new UniTaskCompletionSource();

        void Release(TrackEntry _) => done.TrySetResult();

        // Complete 是正常播完；End / Dispose 是被别的动画顶掉或轨道被清空 ——
        // 后两种也必须放行，否则这条 await 永远挂着
        entry.Complete += Release;
        entry.End += Release;
        entry.Dispose += Release;

        return done.Task.AttachExternalCancellation(ct);
    }

    /// <summary>渲染组件和动画组件各有一份 Skeleton 引用，取到哪个都行。</summary>
    private Skeleton Skeleton =>
        skeletonAnimation != null ? skeletonAnimation.Skeleton : skeletonGraphic?.Skeleton;

    // ============================================================ Animator（Spine 用不上）

    private readonly ActorViewWarnings warnings = new ActorViewWarnings();

    // 骨骼立绘没有 Unity Animator —— 动画走 Spine 的 AnimationState（见 PlayAnimationAsync）。
    // 这四条是「立绘Animator」那组节点的落点，挂到骨骼立绘上属于配置错误，
    // 警告一次后跳过：不抛，是因为不该让一条配错的指令把整段剧情打断
    public void SetAnimatorBool(string parameterName, bool value) => WarnNoAnimator();
    public void SetAnimatorInt(string parameterName, int value) => WarnNoAnimator();
    public void SetAnimatorFloat(string parameterName, float value) => WarnNoAnimator();
    public void SetAnimatorTrigger(string parameterName, bool reset) => WarnNoAnimator();

    private void WarnNoAnimator() => warnings.Once("Animator 参数", ActorId, "骨骼");
}
