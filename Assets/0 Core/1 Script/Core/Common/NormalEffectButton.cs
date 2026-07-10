using UnityEngine;
using UnityEngine.EventSystems;
using PrimeTween;

/// <summary>
/// 通用按钮点击效果：按下缩小、抬起回弹，适用于 AGV 游戏内各类按钮
/// 挂在带 Graphic(可接收射线) 的按钮节点上即可，无需额外引用
/// </summary>
public class NormalEffectButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Tooltip("按下时的缩放倍数")] float pressedScale = 0.9f;
    [SerializeField, Tooltip("按下动画时长")] float pressDuration = 0.08f;
    [SerializeField, Tooltip("抬起回弹动画时长")] float releaseDuration = 0.18f;

    Vector3 originalScale;
    Tween tween;
    bool pressed;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    void OnDisable()
    {
        tween.Stop();
        pressed = false;
        transform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        tween.Stop();
        tween = Tween.Scale(transform, originalScale * pressedScale, pressDuration, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    // 按住移出按钮范围时也复原，避免卡在按下状态
    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    void Release()
    {
        if (!pressed) return;
        pressed = false;
        tween.Stop();
        tween = Tween.Scale(transform, originalScale, releaseDuration, Ease.OutBack);
    }
}
