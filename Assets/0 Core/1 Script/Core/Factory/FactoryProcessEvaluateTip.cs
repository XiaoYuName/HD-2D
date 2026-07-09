using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 打包评价飘字（PERFECT / GOOD / MISS 图标）：突然放大弹出，同时上飘，随后渐隐。
/// 实例由 <see cref="FactoryProcessGamePanel"/> 对象池管理，播完经回调回收。
/// </summary>
public class FactoryProcessEvaluateTip : MonoBehaviour
{
    [SerializeField] FactoryGameConfig config;
    [SerializeField] RectTransform rt;
    [SerializeField] Image icon;

    const float PopDuration = 0.18f;
    const float RiseDistance = 70f;
    const float RiseDuration = 0.5f;
    const float FadeDuration = 0.25f;

    Vector2 homePos;   // 模板摆放的初始位置，每次播放从这里起飘
    Sequence seq;

    void Awake() => homePos = rt.anchoredPosition;

    /// <summary>播放一次评价，动画完回调 <paramref name="onDone"/>（供回收进池）。</summary>
    public void Show(FactoryEvaluateType type, Action onDone)
    {
        icon.sprite = config.EvalIcons[(int)type];
        icon.color = Color.white;
        rt.anchoredPosition = homePos;
        rt.localScale = Vector3.zero;

        seq.Stop();
        seq = Sequence.Create()
            .Group(Tween.Scale(rt, 1f, PopDuration, Ease.OutBack))
            .Group(Tween.UIAnchoredPositionY(rt, homePos.y + RiseDistance, RiseDuration, Ease.OutCubic))
            .Chain(Tween.Alpha(icon, 0f, FadeDuration, Ease.InQuad))
            .ChainCallback(onDone);
    }
}
