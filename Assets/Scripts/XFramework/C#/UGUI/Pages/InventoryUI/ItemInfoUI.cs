using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class ItemInfoUI : UIBase
{
    private LocalizeStringEvent itemNameStringEvent;
    
    private LocalizeStringEvent itemDescriptionStringEvent;
    
    private ItemBagSlot itemBagSlot;

    private Button UseButton;

    private RectTransform itemMask;
    
    private ItemData itemData;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        itemNameStringEvent = Get<LocalizeStringEvent>("Row1/itemName");
        itemDescriptionStringEvent = Get<LocalizeStringEvent>("BackFarme/itemDescription");
        itemBagSlot = Get<ItemBagSlot>("itemBagSlot");
        itemBagSlot.Init();
        itemMask = Get<RectTransform>("itemMask");
        UseButton = Get<Button>("UseButton");
        Bind(UseButton,UseItem,"");
    }

    public void SetData(ItemInfo item)
    {
        if (item == null)
        {
            itemMask.gameObject.SetActive(true);
            return;
        }
        itemMask.gameObject.SetActive(false);

        itemData = InventoryManager.Instance.GetItemData(item.ID);
        if (itemData != null)
        {
            itemBagSlot.SetData(item,null);
            itemBagSlot.SetSelected(true);
            itemNameStringEvent.StringReference.SetReference(itemData.NameKey.Table,itemData.NameKey.Value);
            itemNameStringEvent.StringReference.RefreshString();
            itemDescriptionStringEvent.StringReference.SetReference(itemData.DescKey.Table,itemData.DescKey.Value);
            itemDescriptionStringEvent.StringReference.RefreshString();
            UseButton.gameObject.SetActive(itemData.ItemType == ItemType.Consumables);
        }
        
    }

    public void UseItem()
    {
        InventoryManager.Instance.UseItem(itemData.ID);
    }
}
