using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class PaintTubeMoldSlot : UIBase
{
    [LabelText("模具类型")]
    public ClothingPaintTubeMoldType Type;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        
    }
}
