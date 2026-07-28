using Coffee.UIEffects;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class PaintTubeColorSlot : UIBase
{
    [LabelText("颜色管道")]
    public PaintTubeColorType Type;

    private UIEffect uiEffect;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        uiEffect = GetComponent<UIEffect>();
    }

    public void SetSelected(bool isSelected)
    {
        uiEffect.edgeMode = isSelected ? EdgeMode.Plain : EdgeMode.None;
    }
}
