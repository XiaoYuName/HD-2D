using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 一枚贴纸在制作画布上的位置 / 变换状态。随模板保存（见 <see cref="FactoryMoldMgPanel"/> 的每模板 placements），
/// 由 <see cref="FactoryMoldStickerView"/> 在拖拽/缩放/镜像时就地写回，模板切换时据此重建画布。
/// scale.x 取负表示水平镜像；图层(前后)顺序由 placements 列表顺序（= 子物体 SiblingIndex）表示。
/// </summary>
[Serializable]
public class StickerPlacement
{
    public long itemId;          // 来源贴纸(Painting)物品 Id（完成制作时按此消耗）
    public string spriteKey;     // 画布精灵 AA Key（创建时解析好，重建无需再查表/背包）
    public int value;            // 贴纸售价（创建时取自 FactoryMoldMgConfig 贴纸售价字典，用于预估售价累加）
    public Vector2 anchoredPos;
    public Vector2 scale = Vector2.one;   // x 为负=水平镜像
    public float rotation;

    public StickerPlacement(long itemId, string spriteKey, Vector2 anchoredPos)
    {
        this.itemId = itemId;
        this.spriteKey = spriteKey;
        this.anchoredPos = anchoredPos;
    }
}

/// <summary>
/// 制作画布上的<b>单枚贴纸实例</b>：<b>按住</b>贴纸可拖拽移动，<b>按住并滚滑轮</b>可等比缩放（宽高同比）；另支持镜像翻转、图层上下、删除。
/// 选中时同时显示：虚线方框(<see cref="dashedBox"/>) + 沿精灵边缘的 Shader 描边(换 <see cref="outlineMaterial"/>) + 面板功能框(由面板弹出)。
/// 由 <see cref="FactoryMoldMgPanel"/> 从隐藏模板实例化；点击/开始拖拽即回调面板选中。
/// 模板结构（手动搭模板时）：根物体挂本组件 + bodyImage(Image，作贴纸图与射线目标)；子物体 dashedBox(虚线框图，选中时显示，铺满贴纸)。
/// 滚轮缩放直接读 <c>Mouse.current.scroll</c>（与 PhotoStudioFocusGame 一致），无需改 InputActions。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FactoryMoldStickerView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image bodyImage;
    [LabelText("描边材质(UI/FactoryStickerOutline)")][SerializeField] Material outlineMaterial;
    [LabelText("虚线方框(选中时显示，铺满贴纸)")][SerializeField] GameObject dashedBox;
    [LabelText("滚轮每格缩放步长")][SerializeField] float scaleStep = 0.08f;
    [LabelText("缩放下限 / 上限")][SerializeField] Vector2 scaleRange = new (0.2f, 5f);

    RectTransform rt;
    RectTransform parentRect;   // 贴纸层容器：拖拽/缩放坐标换算的参考系
    Material defaultMaterial;   // bodyImage 原始材质（取消选中时还原）
    StickerPlacement placement;
    Action<FactoryMoldStickerView> onSelected;
    bool pressed;    // 指针是否正按在本贴纸上
    bool hovering;   // 指针是否悬停在本贴纸上（按住或悬停期间滚轮缩放）
    Vector2 dragOffset;   // 按下点与贴纸锚点的偏移，保证拖拽时贴纸跟随光标不跳变

    public StickerPlacement Placement => placement;
    public long ItemId => placement.itemId;

    /// <summary>初始化：绑定数据、加载精灵、按 placement 应用变换。</summary>
    public void Setup(StickerPlacement p, Action<FactoryMoldStickerView> onSelected)
    {
        rt = (RectTransform)transform;
        parentRect = rt.parent as RectTransform;
        defaultMaterial = bodyImage.material;   // 记下原始材质，取消选中时还原
        placement = p;
        this.onSelected = onSelected;
        bodyImage.SetIcon(p.spriteKey);
        ApplyTransform();
        SetSelected(false);
    }

    void ApplyTransform()
    {
        rt.anchoredPosition = placement.anchoredPos;
        rt.localScale = new Vector3(placement.scale.x, placement.scale.y, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
    }

    // 选中：虚线框 + 描边材质（沿精灵边缘）；取消：隐藏虚线框 + 还原默认材质
    public void SetSelected(bool on)
    {
        bodyImage.material = on ? outlineMaterial : defaultMaterial;
        dashedBox.SetActive(on);
    }

    // 按住或悬停在贴纸上时，用滚轮等比缩放（实时）
    void Update()
    {
        if((!pressed && !hovering) || Mouse.current == null)
            return;
        float sy = Mouse.current.scroll.ReadValue().y;
        if(Mathf.Abs(sy) > 0.01f)
            ScaleBy(Mathf.Sign(sy) * scaleStep);   // 用符号+固定步长，跨平台滚轮量级差异无关
    }

    // 等比缩放（保留镜像符号），并写回 placement
    void ScaleBy(float delta)
    {
        float sign = placement.scale.x >= 0f ? 1f : -1f;
        float cur = Mathf.Abs(placement.scale.x);
        float next = Mathf.Clamp(cur + delta, scaleRange.x, scaleRange.y);
        placement.scale = new Vector2(sign * next, next);
        rt.localScale = new Vector3(placement.scale.x, placement.scale.y, 1f);
    }

    #region 功能（由面板功能框按钮调用）
    public void Mirror()
    {
        placement.scale.x = -placement.scale.x;
        rt.localScale = new Vector3(placement.scale.x, placement.scale.y, 1f);
    }

    public void LayerUp() => rt.SetSiblingIndex(rt.GetSiblingIndex() + 1);
    public void LayerDown() => rt.SetSiblingIndex(Mathf.Max(0, rt.GetSiblingIndex() - 1));
    #endregion

    #region 指针（选中 / 悬停 / 拖拽移动）
    public void OnPointerEnter(PointerEventData e) => hovering = true;
    public void OnPointerExit(PointerEventData e) => hovering = false;

    public void OnPointerDown(PointerEventData e)
    {
        pressed = true;
        onSelected?.Invoke(this);
    }

    public void OnPointerUp(PointerEventData e) => pressed = false;

    public void OnBeginDrag(PointerEventData e)
    {
        onSelected?.Invoke(this);
        // 记录“光标当前所在的容器本地坐标”与贴纸锚点的差值，拖拽时用它保持相对位置，避免贴纸瞬移到光标点
        if(parentRect != null &&
           RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, e.pressEventCamera, out Vector2 p))
            dragOffset = rt.anchoredPosition - p;
        else
            dragOffset = Vector2.zero;
    }

    // 直接把光标屏幕坐标换算成容器本地坐标，实时贴合光标（兼容画布缩放 / Screen Space-Camera）
    public void OnDrag(PointerEventData e)
    {
        if(parentRect == null)
            return;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, e.position, e.pressEventCamera, out Vector2 p))
        {
            rt.anchoredPosition = p + dragOffset;
            placement.anchoredPos = rt.anchoredPosition;
        }
    }
    #endregion
}
