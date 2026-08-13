using UnityEngine;
using Live2D.Cubism.Rendering;
using XFramework;

/// <summary>
/// 一张 CG。挂在世界空间的 CG 模型预制体根节点上（本工程的 CG 是全屏 Live2D）。
///
/// 和 <see cref="ActorCubismController"/> 是同级的东西 —— 都是"每个模型一份"的组件，
/// 都靠 Canvas 里的替身定位（原因见 <see cref="CubismProxySync"/>）。
/// 区别只在于：CG 没有角色ID、没有讲话人突出（进 CG 时立绘整层都藏了，没人可压）。
///
/// <b>实现 <see cref="Drama.Runtime.Services.IDramaCG"/> 的不是本类</b>，是
/// <see cref="DramaCGStage"/> —— 那一层要在"还没有任何 CG"的时候就存在。
/// </summary>
public class CGController : GameBase
{
    private CubismRenderController renderController;
    private Animator animator;

    private readonly CubismProxySync sync = new CubismProxySync();

    private bool animatorMissingLogged;

    /// <summary>剧本指令的地盘 —— Canvas 里的替身，不是模型自己的 Transform。</summary>
    public Transform Root => sync.Proxy;

    /// <summary>由 <see cref="DramaCGStage"/> 在实例化之后调一次。</summary>
    public void Init()
    {
        renderController = GetComponent<CubismRenderController>()
                           ?? GetComponentInChildren<CubismRenderController>();
        animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

        if (renderController == null)
        {
            Debug.LogError($"[Drama] CG 上没有 CubismRenderController，淡入淡出做不了：{name}");
        }
    }

    /// <summary>入场时把替身和两台相机交过来。</summary>
    public void Bind(long cgId, RectTransform proxy, Camera canvasCamera, Camera modelCamera)
    {
        sync.Bind(transform, proxy, canvasCamera, modelCamera);

        if (sync.CalibrationFailed)
        {
            Debug.LogWarning($"[Drama] CG {cgId} 的尺寸标定失败（相机没找着 或 预制体缩放是 0），按 1 处理");
        }
    }

    /// <summary>跟随替身。放 LateUpdate 的理由见 <see cref="CubismProxySync.Apply"/>。</summary>
    private void LateUpdate()
    {
        sync.Apply(transform, 1f);
    }

    /// <summary>整体不透明度。淡入淡出用。</summary>
    public void SetAlpha(float alpha)
    {
        if (renderController != null)
        {
            renderController.Opacity = Mathf.Clamp01(alpha);
        }
    }

    /// <summary>替身不在模型这棵树上（它在 Canvas 里），销毁模型时收不到它，得自己来。</summary>
    public void DestroyProxy()
    {
        if (sync.Proxy != null)
        {
            Destroy(sync.Proxy.gameObject);
        }
    }

    // ==================================================== Animator

    public void SetAnimatorBool(string parameterName, bool value)
    {
        if (Ensure(parameterName)) animator.SetBool(parameterName, value);
    }

    public void SetAnimatorInt(string parameterName, int value)
    {
        if (Ensure(parameterName)) animator.SetInteger(parameterName, value);
    }

    public void SetAnimatorFloat(string parameterName, float value)
    {
        if (Ensure(parameterName)) animator.SetFloat(parameterName, value);
    }

    public void SetAnimatorTrigger(string parameterName, bool reset)
    {
        if (!Ensure(parameterName))
        {
            return;
        }

        if (reset) animator.ResetTrigger(parameterName);
        else animator.SetTrigger(parameterName);
    }

    /// <summary>
    /// 没有 Animator 就警告一次后跳过，不抛 —— 策划在一张没做状态机的 CG 上挂了
    /// Animator 指令是配置错误，不该升级成"整段剧情播不下去"。
    /// </summary>
    private bool Ensure(string parameterName)
    {
        if (animator == null)
        {
            if (!animatorMissingLogged)
            {
                animatorMissingLogged = true;
                Debug.LogWarning($"[Drama] CG「{name}」上没有 Animator，本段剧本里的 CG Animator 指令都会被跳过");
            }

            return false;
        }

        return !string.IsNullOrEmpty(parameterName);
    }
}
