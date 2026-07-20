using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine.EventSystems;
using XFramework;

public partial class PackController : UIBase,IPointerClickHandler
{
    [LabelText("物品槽位")]
    public List<FlySlot> FlySlots = new List<FlySlot>();

    private int flySlotIndex = 0;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var flySlot in FlySlots)
        {
            flySlot.Init();
            flySlot.transform.localScale = UnityEngine.Vector3.zero;
        }
        flySlotIndex = 0;
    }

    public void SetFlySlotData(FlyItemSlotData flyItemSlotData)
    {
        if (flySlotIndex >= FlySlots.Count) return;
        FlySlots[flySlotIndex].SetData(flyItemSlotData);
        flySlotIndex++;
        FlySlots[flySlotIndex].transform.DOScale(UnityEngine.Vector3.one, 0.25f);
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        
    }
}
