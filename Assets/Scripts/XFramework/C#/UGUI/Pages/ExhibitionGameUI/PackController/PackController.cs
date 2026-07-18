using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.EventSystems;
using XFramework;

public partial class PackController : UIBase,IPointerClickHandler
{
    [LabelText("物品槽位")]
    public List<FlySlot> FlySlots = new List<FlySlot>();
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var flySlot in FlySlots)
        {
            flySlot.Init();
        }
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        
    }
}
