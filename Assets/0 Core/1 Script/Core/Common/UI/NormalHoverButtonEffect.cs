using UnityEngine;
using UnityEngine.EventSystems;
using PrimeTween;

public class NormalHoverButtonEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    const float hoverScale = 1.1f;      // 悬停时的放大倍数
    const float pressedScale = 0.9f;    // 按下时(相对悬停状态)的缩放倍数
    const float enterDuration = 0.15f;  // 放大动画时长
    const float exitDuration = 0.18f;   // 回弹动画时长
    const float pressDuration = 0.08f;  // 按下动画时长

    Vector3 orScale;
    Tween tween;
    bool hovering;
    bool pressed;

    void Awake()
    {
        orScale = transform.localScale;
    }

    void OnDisable()
    {
        tween.Stop();
        hovering = false;
        pressed = false;
        transform.localScale = orScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        Apply(enterDuration, Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressed = false;
        Apply(exitDuration, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        Apply(pressDuration, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        Apply(exitDuration, Ease.OutBack);
    }

    // 按当前 悬停/按下 状态叠加计算目标缩放
    void Apply(float duration, Ease ease)
    {
        float factor = 1f;
        if (hovering) factor *= hoverScale;
        if (pressed) factor *= pressedScale;

        tween.Stop();
        tween = Tween.Scale(transform, orScale * factor, duration, ease, useUnscaledTime: true);
    }
}
