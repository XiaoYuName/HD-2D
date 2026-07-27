using UnityEngine;
using UnityEngine.EventSystems;
using XFramework;

public partial class PattentSlot : UIBase,IBeginDragHandler,IEndDragHandler,IDragHandler
{
    private PcbSlotData PcbSlotData;
    private ClothingPatternMakingUI ParentUI;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
       
    }


    public void SetData(PcbSlotData pcbSlotData)
    {
        this.PcbSlotData = pcbSlotData;
        image.sprite = LoadAsset<Sprite>(GamePathTools.CombinationPcbIconPath(PcbSlotData.MinIconName));
        image.SetNativeSize();
        ParentUI = UISystem.Instance.GetUI<ClothingPatternMakingUI>("ClothingPatternMakingUI");
    }

    private PcbItemSlot DropItemSlot;

    public void OnBeginDrag(PointerEventData eventData)
    {
        DropItemSlot = ParentUI.SpawnPcbItemSlot(PcbSlotData, eventData.position, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (DropItemSlot != null)
        {
            ParentUI.MovePcbItemSlotToScreenPoint(DropItemSlot, eventData.position, eventData.pressEventCamera);
            if (ParentUI.TryKeepPcbItemSlot(DropItemSlot))
            {
                DropItemSlot.SetBlocksRaycasts(true);
                ParentUI.DeselectPcbItemSlot(DropItemSlot);
                ParentUI.RefreshPcbItemSlotPlacedColor(DropItemSlot);
                DropItemSlot = null;
                ParentUI.RemovePattentSlot(this);
                return;
            }

            DropItemSlot = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DropItemSlot != null)
        {
            ParentUI.MovePcbItemSlotToScreenPoint(DropItemSlot, eventData.position, eventData.pressEventCamera);
            ParentUI.UpdatePcbItemSlotDragColor(DropItemSlot);
        }
    }
}
