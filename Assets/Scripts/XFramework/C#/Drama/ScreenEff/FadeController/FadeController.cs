using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class FadeController : UIBase
{
    private Image image;
    private CanvasGroup canvasGroup;
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        image = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public override void Release()
    {
        base.Release();
        canvasGroup.DOKill();
        canvasGroup.alpha = 0;
        ResetWipe();
    }

    public async UniTask FadeIn(float seconds, Color color, float alpha, Ease ease, CancellationToken ct)
    {
        ResetWipe();
        image.color = color;
        canvasGroup.blocksRaycasts = true;
        await canvasGroup.DOFade(alpha, seconds).SetEase(ease)
            .SetLink(gameObject)
            .ToUniTask(TweenCancelBehaviour.CancelAwait, ct);
    }

    public async UniTask FadeOut(float seconds, Ease ease, CancellationToken ct)
    {
        await canvasGroup.DOFade(0, seconds).SetEase(ease)
            .SetLink(gameObject)
            .ToUniTask(TweenCancelBehaviour.CancelAwait, ct);
        canvasGroup.blocksRaycasts = false;
        ResetWipe();
    }

    // ==================================================== 竖条 / 百叶窗

    // 这两种过场复用的就是上面那张遮罩 Image，只是把材质换成条纹 Shader ——
    // 所以预制体一个节点都不用加。条纹的形状和时序全在 Shader 里，
    // 这边只负责把 0→1 的进度推过去。数值口径见 DramaScreenWipe.shader 的注释。
    private const string WipeShaderPath = "Drama/DramaScreenWipe";

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");

    private Material wipeMaterial;
    private bool wipeShaderMissing;
    private float wipeProgress;

    /// <summary>盖上（竖条 / 百叶窗）。跑完停在全覆盖状态，和 <see cref="FadeIn"/> 一样不自动还原。</summary>
    public async UniTask WipeIn(EScreenTransitionKind kind, float seconds, Color color, float alpha, Ease ease,
        CancellationToken ct)
    {
        Material mat = EnsureWipeMaterial();
        if (mat == null)
        {
            // 拿不到 Shader 就退回淡入。宁可换个效果，也别让「盖上」这一步失效 ——
            // 剧本指望它挡着，不挡就是换背景换立绘的过程直接穿帮给玩家看
            await FadeIn(seconds, color, alpha, ease, ct);
            return;
        }

        Color c = color;
        c.a = alpha;
        BeginWipe(mat, kind, c, reveal: false);

        await PlayWipeAsync(mat, seconds, ease, ct);
    }

    /// <summary>揭开（竖条 / 百叶窗）。跑完遮罩完全透明且不吃点击。</summary>
    public async UniTask WipeOut(EScreenTransitionKind kind, float seconds, Ease ease, CancellationToken ct)
    {
        Material mat = EnsureWipeMaterial();
        if (mat == null)
        {
            await FadeOut(seconds, ease, ct);
            return;
        }

        // 从"当前盖着什么"接着揭。两种来路：
        //   上一步就是条纹盖上的 → 颜色和不透明度都在材质里，原样接着用
        //   上一步是 FadeIn 盖上的（剧本允许盖和揭用不同样式）→ 颜色在 image.color 上、
        //   不透明度在 canvasGroup 上，得先收拢到材质里
        Color c;
        if (image.material == mat)
        {
            c = mat.GetColor(ColorId);
        }
        else
        {
            c = image.color;
            c.a = canvasGroup.alpha;
        }

        BeginWipe(mat, kind, c, reveal: true);

        await PlayWipeAsync(mat, seconds, ease, ct);

        // ★ 揭完必须把 alpha 归 0。条纹是靠材质里的覆盖率变没的，CanvasGroup 一直是 1，
        // 不在这儿归零的话，摘掉材质的瞬间整张遮罩会变成一块不透明的纯色糊在屏幕上
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        ResetWipe();
    }

    /// <summary>不放动画，立刻把遮罩清干净。剧本结束 / 跳转 / 被打断时调。</summary>
    public void ClearImmediate()
    {
        canvasGroup.DOKill();
        DOTween.Kill(this);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        ResetWipe();
    }

    private void BeginWipe(Material mat, EScreenTransitionKind kind, Color color, bool reveal)
    {
        mat.SetColor(ColorId, color);
        mat.SetFloat(ModeId, kind == EScreenTransitionKind.Comb ? 1f : 0f);
        mat.SetFloat(RevealId, reveal ? 1f : 0f);

        // 颜色和不透明度都交给材质，顶点色置白，免得被乘两次
        image.color = Color.white;
        image.material = mat;
        canvasGroup.DOKill();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    private async UniTask PlayWipeAsync(Material mat, float seconds, Ease ease, CancellationToken ct)
    {
        mat.SetFloat(ProgressId, 0f);

        // Skip 时 Handler 已经把时长缩成 0 了，直接落终点，别起一条 0 秒的 Tween
        if (seconds <= 0f)
        {
            mat.SetFloat(ProgressId, 1f);
            return;
        }

        wipeProgress = 0f;
        await DOTween.To(() => wipeProgress, x =>
                     {
                         wipeProgress = x;
                         mat.SetFloat(ProgressId, x);
                     }, 1f, seconds)
                     .SetEase(ease)
                     .SetTarget(this)          // ClearImmediate 要能掐掉它
                     .SetLink(gameObject)
                     .ToUniTask(TweenCancelBehaviour.CancelAwait, ct);
    }

    /// <summary>把 Image 还原成普通的纯色遮罩。材质留着复用，只是不再挂在 Image 上。</summary>
    private void ResetWipe()
    {
        if (image == null)
        {
            return;
        }

        if (image.material != null && image.material == wipeMaterial)
        {
            image.material = null;
        }

        wipeMaterial?.SetFloat(ProgressId, 0f);
    }

    private Material EnsureWipeMaterial()
    {
        if (wipeMaterial != null || wipeShaderMissing)
        {
            return wipeMaterial;
        }

        Shader shader = Resources.Load<Shader>(WipeShaderPath);
        if (shader == null)
        {
            // 只报一次：这条要是每帧刷，真正的错因反而被冲走了
            wipeShaderMissing = true;
            Debug.LogError($"[Drama] 找不到过场 Shader「Resources/{WipeShaderPath}」，竖条 / 百叶窗会退回淡入");
            return null;
        }

        wipeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return wipeMaterial;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (wipeMaterial != null)
        {
            Destroy(wipeMaterial);
            wipeMaterial = null;
        }
    }
}
