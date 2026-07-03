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
        
    }

    public void SetData(ItemBag itemBag)
    {
        if (itemBag == null)
        {
            itemMask.gameObject.SetActive(true);
            return;
        }
        itemMask.gameObject.SetActive(false);

        itemData = InventoryManager.Instance.GetItemData(itemBag.itemID);
        if (itemData != null)
        {
            itemBagSlot.SetData(itemBag,null);
            itemBagSlot.SetSelected(true);
            itemNameStringEvent.StringReference.SetReference("InventoryItem",itemData.NameKey);
            itemNameStringEvent.StringReference.RefreshString();
            itemDescriptionStringEvent.StringReference.SetReference("InventoryItem",itemData.DescKey);
            itemDescriptionStringEvent.StringReference.RefreshString();
            UseButton.gameObject.SetActive(!(itemData.Type is ItemType.Ingredient or ItemType.Recipe));
           
        }
        
    }
}
