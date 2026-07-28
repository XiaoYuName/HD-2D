using Coffee.UIEffects;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public class PaintTubeColorSlot : UIBase,IPointerClickHandler
{
    [LabelText("颜色管道")]
    public PaintTubeColorType Type;

    /// <summary>被点击时抛出,由 MedicinalSolutionUI 统一处理选中/取消</summary>
    public UnityEvent<PaintTubeColorSlot> OnSelect = new UnityEvent<PaintTubeColorSlot>();

    public bool IsSelected { get; private set; }

    private UIEffect uiEffect;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        uiEffect = GetComponent<UIEffect>();
        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
        uiEffect.edgeMode = isSelected ? EdgeMode.Plain : EdgeMode.None;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSelect?.Invoke(this);
    }
}
