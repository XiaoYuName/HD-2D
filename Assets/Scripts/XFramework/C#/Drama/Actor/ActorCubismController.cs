using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Services;
using Live2D.Cubism.Rendering;
using UnityEngine;
using XFramework;

/// <summary>
/// Live2D 立绘。挂在世界空间的 Cubism 模型预制体根节点上。
///
/// <b>为什么它在世界空间，而另两种立绘在 Canvas 里</b>：
/// Cubism 模型是每个 Drawable 一个 MeshRenderer，没有 Graphic/CanvasRenderer 实现
/// （Spine 能待在 Canvas 里是因为官方专门做了 SkeletonGraphic，Live2D 没有对应物）。
/// 硬塞进 Canvas 的后果是 CanvasGroup 管不到它、Mask 裁不了它。
/// 本工程走的是<b>相机分层</b>：背景（主相机）→ Live2D（Cubism 相机）→ UI（UI 相机）。
///
/// <b>但剧本指令是按 UI 口径写的</b>（锚点 + 像素偏移），世界空间没有锚点。
/// 所以 <see cref="Root"/> 返回的是 Canvas 里的一个<b>替身 RectTransform</b>（proxy）：
/// 位移 / 缩放 / 抖动 / 换方向锚点全部照常动替身，本组件每帧把替身的位姿换算到世界空间。
/// 这样分辨率一变，替身作为真正的 UI 元素会被 CanvasScaler 和锚点重新摆好，模型自动跟上 ——
/// 不需要在这里复刻一遍 CanvasScaler 的逻辑（Match 混合模式手工复现很难对）。
/// </summary>
public class ActorCubismController : GameBase, IDramaActorView
{
    private CubismRenderController renderController;
    private Animator animator;

    private readonly ActorViewWarnings warnings = new ActorViewWarnings();

    /// <summary>替身 → 世界的换算。和 Live2D CG 共用同一套，见 <see cref="CubismProxySync"/>。</summary>
    private readonly CubismProxySync sync = new CubismProxySync();

    /// <summary>讲话人微缩的倍率。和剧本的缩放相乘，不能抢同一个值。</summary>
    private float shrink = 1f;

    public int ActorId { get; private set; }

    /// <summary>
    /// 剧本指令的地盘 —— 返回<b>替身</b>而不是模型自己的 Transform。
    ///
    /// 直接返回世界 Transform 的话，剧本里「位置 (0,-50)」这个 UI 像素值会被当成世界坐标用，
    /// 差几十倍；而且换方向锚点（SetDirection 改父节点）在世界空间没有意义。
    /// </summary>
    public Transform Root => sync.Proxy;

    /// <summary>
    /// 由舞台在实例化之后调一次。<b>不是 override</b> —— GameBase 没有 Init 这个约定，
    /// 那是 UIBase 那条线的东西，Live2D 立绘在世界空间，走的是 GameBase。
    /// </summary>
    public void Init()
    {
        renderController = GetComponent<CubismRenderController>()
                           ?? GetComponentInChildren<CubismRenderController>();
        animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

        if (renderController == null)
        {
            Debug.LogError($"[Drama] Live2D 立绘上没有 CubismRenderController，显隐和压暗都做不了：{name}");
        }
    }

    /// <summary>
    /// 入场时由舞台调一次，把替身和两台相机交过来。
    ///
    /// Live2D 是<b>一角色一预制体</b>（角色表里存的是模型预制体路径），
    /// 不像另两种是"共用模板 + 换资源"，所以这里不需要换任何资产。
    /// </summary>
    public void Bind(int actorId, RectTransform proxyRect, Camera canvasCamera, Camera modelCamera)
    {
        ActorId = actorId;

        sync.Bind(transform, proxyRect, canvasCamera, modelCamera);

        if (sync.CalibrationFailed)
        {
            Debug.LogWarning($"[Drama] 角色 {actorId} 的 Live2D 尺寸标定失败（相机没找着 或 预制体缩放是 0），按 1 处理");
        }
    }

    /// <summary>
    /// 跟随替身。
    ///
    /// <b>必须是 LateUpdate</b>：DOTween 默认在 Update 跑、Canvas 布局在那之后才重建，
    /// 放 Update 里会慢一帧 —— 抖动这类高频指令能明显看出来。
    /// </summary>
    private void LateUpdate()
    {
        sync.Apply(transform, shrink);
    }

    public void SetAlpha(float alpha)
    {
        if (renderController != null)
        {
            renderController.Opacity = Mathf.Clamp01(alpha);
        }
    }

    /// <summary>
    /// 压暗到指定亮度，1 = 原样。走模型的整体乘算颜色。
    ///
    /// <b>不做 0.2 秒过渡</b>，和另两种立绘不同：那两个是 DOTween 动 Graphic 的 color，
    /// 这里动的是 Cubism 的 ModelMultiplyColor，每帧写会和 Cubism 自己的
    /// 颜色处理管线打架。压暗是瞬时的，视觉差异很小。
    /// </summary>
    public void SetDim(float brightness)
    {
        if (renderController == null)
        {
            return;
        }

        float b = Mathf.Clamp01(brightness);
        renderController.ModelMultiplyColor = new Color(b, b, b, 1f);
    }

    /// <summary>
    /// 缩到指定倍率，1 = 原样。
    ///
    /// 存成倍率而不是直接写 localScale：<see cref="SyncFromProxy"/> 每帧都会按替身重算缩放，
    /// 直接写会在下一帧被冲掉。
    /// </summary>
    public void SetShrink(float scale)
    {
        shrink = scale <= 0f ? 1f : scale;
    }

    /// <summary>Live2D 的压暗是瞬时的，没有过渡要收。</summary>
    public void CompleteHighlightTweens()
    {
    }

    /// <summary>
    /// 关的是<b>模型自己</b>，不是替身。
    /// 替身要留着 —— 它挂在方向锚点下，位置信息还得用；而且关了 UI 元素
    /// 反而会让下次显示时布局要重算一帧。
    /// </summary>
    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    /// <summary>
    /// 替身不在模型这棵树上（它在 Canvas 里），舞台销毁模型时收不到它，得自己来。
    /// </summary>
    public void ReleaseView()
    {
        if (sync.Proxy != null)
        {
            Destroy(sync.Proxy.gameObject);
        }
    }

    // ============================================================ 动画

    /// <summary>
    /// 播 Animator 里的一个状态。
    ///
    /// <b>循环与否是运行时问出来的</b>，不是剧本填的 —— <c>Animator.Play</c> 压根没有
    /// loop 参数，循环是 clip 的导入设置。所以这里读 <c>stateInfo.loop</c>：
    /// 循环就立刻返回（否则永远等不到结束，剧本死在这条指令上），单次就等它播完。
    /// <paramref name="track"/> 当 Animator 的 layer 用。
    /// </summary>
    public async UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale,
                                            CancellationToken ct)
    {
        if (animator == null || string.IsNullOrEmpty(animationName))
        {
            warnings.Once("播动画", ActorId, "Live2D");
            return;
        }

        int layer = Mathf.Max(0, track);

        if (!animator.HasState(layer, Animator.StringToHash(animationName)))
        {
            Debug.LogWarning($"[Drama] 角色 {ActorId} 的 Animator 第 {layer} 层没有状态「{animationName}」，已跳过");
            return;
        }

        animator.speed = timeScale <= 0f ? 1f : timeScale;
        animator.Play(animationName, layer, 0f);

        // 跳过 / 读档恢复：状态照切（那是结果），但不等它播完
        if (ActorPlayback.IsInstant)
        {
            return;
        }

        // Play 是下一次 Animator 求值才真正切状态，当帧读 stateInfo 拿到的还是旧状态
        await UniTask.NextFrame(ct);

        if (animator == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
        if (state.loop)
        {
            return;
        }

        float seconds = state.length / Mathf.Max(0.01f, animator.speed);
        await UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.DeltaTime,
                            PlayerLoopTiming.Update, ct);
    }

    public void SetAnimatorBool(string parameterName, bool value)
    {
        if (EnsureAnimator(parameterName)) animator.SetBool(parameterName, value);
    }

    public void SetAnimatorInt(string parameterName, int value)
    {
        if (EnsureAnimator(parameterName)) animator.SetInteger(parameterName, value);
    }

    public void SetAnimatorFloat(string parameterName, float value)
    {
        if (EnsureAnimator(parameterName)) animator.SetFloat(parameterName, value);
    }

    public void SetAnimatorTrigger(string parameterName, bool reset)
    {
        if (!EnsureAnimator(parameterName))
        {
            return;
        }

        if (reset) animator.ResetTrigger(parameterName);
        else animator.SetTrigger(parameterName);
    }

    private bool EnsureAnimator(string parameterName)
    {
        if (animator == null)
        {
            warnings.Once("Animator 参数", ActorId, "Live2D");
            return false;
        }

        return !string.IsNullOrEmpty(parameterName);
    }

    /// <summary>Live2D 没有 Spine 那种皮肤概念，换装走的是别的机制。</summary>
    public void SetSkin(string skinName) => warnings.Once("换皮肤", ActorId, "Live2D");
}
