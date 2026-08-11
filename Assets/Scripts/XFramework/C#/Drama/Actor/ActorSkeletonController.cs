using System.Threading;
using Coffee.UIEffects;
using Cysharp.Threading.Tasks;
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
public class ActorSkeletonController : UIBase, IActorView
{
    /// <summary>非说话人微缩到多少。旧工程是靠 mActorScaleSwitch 开关这个效果的。</summary>
    private const float ShrinkScale = 0.92f;

    private SkeletonGraphic skeletonGraphic;
    private SkeletonAnimation skeletonAnimation;

    /// <summary>置灰用。具体是"变灰"还是"压暗"由预制体上 UIEffect 的 colorFilter 决定，这里只推强度。</summary>
    private UIEffect uiEffect;

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
        uiEffect = GetComponentInChildren<UIEffect>();

        skeletonRoot = skeletonGraphic != null
            ? skeletonGraphic.transform
            : skeletonAnimation != null ? skeletonAnimation.transform : null;

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

    public void SetGray(bool gray)
    {
        if (uiEffect == null)
        {
            return;   // 预制体没配 UIEffect 就当不支持置灰，不报错
        }

        uiEffect.colorIntensity = gray ? 1f : 0f;
    }

    public void SetShrink(bool shrink)
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

        skeletonRoot.localScale = shrink ? Vector3.one * ShrinkScale : Vector3.one;
    }

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

        if (loop)
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
}
