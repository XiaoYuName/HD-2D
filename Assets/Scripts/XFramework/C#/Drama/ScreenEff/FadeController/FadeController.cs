using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        
    }

    public async UniTask FadeIn(float seconds, Color color, float alpha, Ease ease, CancellationToken ct)
    {
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
    }
}
