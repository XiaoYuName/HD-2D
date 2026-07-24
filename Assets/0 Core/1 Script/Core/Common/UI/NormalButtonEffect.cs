using UnityEngine;
using UnityEngine.EventSystems;
using PrimeTween;

/// <summary>
/// 通用按钮点击效果：按下缩小、抬起回弹，适用于 AGV 游戏内各类按钮
/// 挂在带 Graphic(可接收射线) 的按钮节点上即可，无需额外引用
/// </summary>
public class NormalButtonEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    const float pressedScale = 0.9f;    // 按下时的缩放倍数
    const float pressDuration = 0.08f;  // 按下动画时长 
    const float releaseDuration = 0.18f;// 抬起回弹动画时长
    public static readonly Vector3 orScale = Vector3.one;
    Tween tween;
    bool pressed;

    void Awake()
    {
        if(transform.localScale != Vector3.one)
            Debug.LogError("UI需要保持 Scale 为1 ", gameObject);
    }

    void OnDisable()
    {
        tween.Stop();
        pressed = false;
        transform.localScale = orScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        tween.Stop();
        tween = Tween.Scale(transform, orScale * pressedScale, pressDuration, Ease.OutQuad, useUnscaledTime: true);
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
        tween = Tween.Scale(transform, orScale, releaseDuration, Ease.OutBack, useUnscaledTime: true);
    }
}
