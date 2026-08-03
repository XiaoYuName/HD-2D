using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class BodyAccessoriesSlot : UIBase
{
    [LabelText("配件ID")]
    public long AccessoriesID;

    [LabelText("轮廓材质")]
    [Tooltip("未装配时只显示轮廓所用的材质,装配后会换回默认 UI 材质")]
    public Material outlineMaterial;

    [LabelText("吸附半径")]
    [Tooltip("拖拽落点离本部件多近算命中,0 表示必须落在部件范围内")]
    public float snapRadius = 80f;

    private const float BlinkDuration = 0.45f;
    private const float BlinkMinAlpha = 0.2f;

    /// <summary>
    /// 静止态的透明度: 未装配时完全隐藏,只有闪烁提示或装配之后才显示
    /// </summary>
    private float RestAlpha => IsEquipped ? 1f : 0f;

    public RectTransform Rect { get; private set; }

    /// <summary>
    /// 是否已经装配。装配后正常显示图片,不再只显示轮廓,也不再参与闪烁
    /// </summary>
    public bool IsEquipped { get; private set; }

    private Image image;
    private Tween blinkTween;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        image = GetComponent<Image>();
        Rect = GetComponent<RectTransform>();
        SetEquipped(false);
    }

    /// <summary>
    /// 未装配: 挂轮廓材质,只显示边缘轮廓; 已装配: 换回默认材质,正常显示图片
    /// </summary>
    public void SetEquipped(bool equipped)
    {
        IsEquipped = equipped;
        image.material = equipped ? null : outlineMaterial;
        // SetBlink 会按 RestAlpha 收尾: 装配后完全不透明,未装配则隐藏
        SetBlink(false);
    }

    /// <summary>
    /// 闪烁提示可装配的位置,已装配的部件不会闪
    /// </summary>
    public void SetBlink(bool blink)
    {
        blinkTween?.Kill();
        blinkTween = null;

        if (!blink || IsEquipped)
        {
            SetAlpha(RestAlpha);
            return;
        }

        // 闪烁期间才把隐藏的轮廓显示出来
        SetAlpha(1f);
        blinkTween = image.DOFade(BlinkMinAlpha, BlinkDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 世界坐标点到本部件矩形的距离,落在矩形内为 0
    /// </summary>
    public float GetDropDistance(Vector3 worldPoint)
    {
        var localPoint = (Vector2)Rect.InverseTransformPoint(worldPoint);
        var rect = Rect.rect;
        var dx = Mathf.Max(rect.xMin - localPoint.x, 0f, localPoint.x - rect.xMax);
        var dy = Mathf.Max(rect.yMin - localPoint.y, 0f, localPoint.y - rect.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 落点是否在吸附范围内(部件范围内,或离边缘不超过 snapRadius)
    /// </summary>
    public bool IsInDropRange(Vector3 worldPoint)
    {
        return GetDropDistance(worldPoint) <= Mathf.Max(snapRadius, 0f);
    }

    public override void Release()
    {
        blinkTween?.Kill();
        blinkTween = null;
        base.Release();
    }

    private void SetAlpha(float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
