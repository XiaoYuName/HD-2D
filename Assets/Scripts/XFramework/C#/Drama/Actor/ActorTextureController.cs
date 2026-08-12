using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime.Services;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 图片立绘。<see cref="IActorView"/> 三种实现里最简单的一种：一张 <see cref="Image"/>。
///
/// <b>预制体约定和骨骼立绘一致</b>：本组件挂在根节点，Image 挂在<b>子节点</b>。
/// 因为 <see cref="Root"/>（根节点）的 position/scale 归剧本指令用，
/// 「微缩」要动的是另一个节点，不能跟剧本抢同一个 localScale。
/// </summary>
public class ActorTextureController : UIBase, IDramaActorView
{
    private Image image;

    /// <summary>Image 所在的节点。微缩动它，不动 <see cref="Root"/>。</summary>
    private Transform imageRoot;

    private readonly ActorHighlightTween highlight = new ActorHighlightTween();
    private readonly ActorViewWarnings warnings = new ActorViewWarnings();

    private bool shrinkUnavailableLogged;

    public int ActorId { get; private set; }

    /// <summary>位移 / 缩放 / 旋转 / 抖动都是 DOTween 直接动这个（剧本指令的地盘）。</summary>
    public Transform Root => transform;

    public override void Init()
    {
        image = GetComponentInChildren<Image>();
        imageRoot = image != null ? image.transform : null;

        if (image == null)
        {
            Debug.LogError($"[Drama] 图片立绘预制体上没有 Image，渲染不出来：{name}");
        }
    }

    /// <summary>
    /// 入场时由舞台调一次。预制体是所有角色<b>共用一个模板</b>，
    /// 靠这里换 Sprite 变成具体角色（角色表里存的是立绘图路径）。
    /// </summary>
    public void Bind(int actorId, Sprite sprite)
    {
        ActorId = actorId;

        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;

        // 模板的尺寸是随便摆的，换图之后按图的原始尺寸走，
        // 否则不同角色的立绘会被拉成同一个比例
        image.SetNativeSize();
    }

    public void SetAlpha(float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    /// <summary>压暗到指定亮度，1 = 原样。顶点色整体相乘，和骨骼立绘同一套做法。</summary>
    public void SetDim(float brightness)
    {
        if (image == null)
        {
            return;
        }

        float b = Mathf.Clamp01(brightness);

        // 别动 alpha —— 那是显隐动画的地盘
        Color target = new Color(b, b, b, image.color.a);

        highlight.KillColor();
        DOTween.To(() => image.color, c => image.color = c, target, ActorHighlightTween.Seconds)
               .SetEase(Ease.Linear)
               .SetId(highlight.ColorId);
    }

    /// <summary>缩到指定倍率，1 = 原样。</summary>
    public void SetShrink(float scale)
    {
        // 只能动 imageRoot：Root 的 localScale 是 ActorScaleAction 的地盘，
        // 两边抢同一个值的话，剧本一缩放就把微缩状态冲掉了
        if (imageRoot == null || imageRoot == transform)
        {
            if (!shrinkUnavailableLogged)
            {
                shrinkUnavailableLogged = true;
                Debug.LogWarning($"[Drama] {name} 的 Image 直接挂在根节点上，微缩会和剧本的缩放指令冲突，已跳过。" +
                                 "把 Image 移到子节点即可。");
            }

            return;
        }

        highlight.KillScale();
        imageRoot.DOScale(scale, ActorHighlightTween.Seconds)
                 .SetEase(Ease.Linear)
                 .SetId(highlight.ScaleId);
    }

    /// <inheritdoc cref="ActorHighlightTween.CompleteAll"/>
    public void CompleteHighlightTweens() => highlight.CompleteAll();

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    /// <summary>没有额外资源要收 —— 舞台销毁本节点就够了。</summary>
    public void ReleaseView()
    {
    }

    // ============================================================ 用不上的能力

    public void SetSkin(string skinName) => warnings.Once("换皮肤", ActorId, "图片");

    /// <summary>
    /// 图片立绘没有动画。真要做序列帧的话在这里换 Sprite 序列，
    /// 但那属于新增能力，不是"骨骼动画的图片版"，别硬套 Spine 的轨道语义。
    /// </summary>
    public UniTask PlayAnimationAsync(string animationName, int track, bool loop, float timeScale, CancellationToken ct)
    {
        warnings.Once("播动画", ActorId, "图片");
        return UniTask.CompletedTask;
    }

    public void SetAnimatorBool(string parameterName, bool value) => WarnNoAnimator();
    public void SetAnimatorInt(string parameterName, int value) => WarnNoAnimator();
    public void SetAnimatorFloat(string parameterName, float value) => WarnNoAnimator();
    public void SetAnimatorTrigger(string parameterName, bool reset) => WarnNoAnimator();

    private void WarnNoAnimator() => warnings.Once("Animator 参数", ActorId, "图片");
}
