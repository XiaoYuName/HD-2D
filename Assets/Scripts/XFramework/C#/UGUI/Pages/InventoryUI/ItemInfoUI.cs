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

    private RuntimeItemInfo runtimeItemInfo;
    
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

        itemBagSlot.SetData(item);
        if (InventoryManager.Instance.HasItemData(item))
        {
            itemData = InventoryManager.Instance.GetItemData(item.ID);
            if (itemData != null)
            {
                itemBagSlot.SetSelected(true);
                itemNameStringEvent.StringReference.SetReference(itemData.NameKey.Table,itemData.NameKey.Value);
                itemNameStringEvent.StringReference.RefreshString();
                itemDescriptionStringEvent.StringReference.SetReference(itemData.DescKey.Table,itemData.DescKey.Value);
                itemDescriptionStringEvent.StringReference.RefreshString();
                UseButton.gameObject.SetActive(itemData.ItemType == ItemType.Consumables);
            }
        }
        else
        {
            if (item is RuntimeItemInfo itemInfo)
            {
                runtimeItemInfo = itemInfo;
                itemDescriptionStringEvent.SetText("InventoryItem","RuntimeDesc");
                if (runtimeItemInfo is FactoryComposedItemInfo factoryItem)
                {
                    var itemA = InventoryManager.Instance.GetItemData(factoryItem.FrameItemId);
                    var itemB = InventoryManager.Instance.GetItemData(factoryItem.FrameItemId);
                    string runtimeName =
                        $"{LanguageManager.Instance.GetLocalizedString(itemA.NameKey.Table, itemA.NameKey.Value)}" +
                        $"{LanguageManager.Instance.GetLocalizedString(itemB.NameKey.Table, itemB.NameKey.Value)}";
                    itemNameStringEvent.ClearTextEvent();
                    itemNameStringEvent.SetTextMeshProUGUI(runtimeName);
                    itemDescriptionStringEvent.SetVar("FrameItem",LanguageManager.Instance.GetLocalizedString(itemA.NameKey.Table, itemA.NameKey.Value));
                    itemDescriptionStringEvent.SetVar("PaintingItem",LanguageManager.Instance.GetLocalizedString(itemB.NameKey.Table, itemB.NameKey.Value));
                }
                
                LanguageManager.Instance.RemoveOnLanguageChanged(OnLanguageChanged);
                LanguageManager.Instance.AddOnLanguageChanged(OnLanguageChanged);
                itemBagSlot.SetSelected(true);
                UseButton.gameObject.SetActive(false);
            }
        }
    }

    private void OnLanguageChanged()
    {
        if (runtimeItemInfo is FactoryComposedItemInfo factoryItem)
        {
            var itemA = InventoryManager.Instance.GetItemData(factoryItem.FrameItemId);
            var itemB = InventoryManager.Instance.GetItemData(factoryItem.PaintingItemId);
            string runtimeName =
                $"{LanguageManager.Instance.GetLocalizedString(itemA.NameKey.Table, itemA.NameKey.Value)}" +
                $"{LanguageManager.Instance.GetLocalizedString(itemB.NameKey.Table, itemB.NameKey.Value)}";
            itemNameStringEvent.SetTextMeshProUGUI(runtimeName);
            
            itemDescriptionStringEvent.SetVar("FrameItem",LanguageManager.Instance.GetLocalizedString(itemA.NameKey.Table, itemA.NameKey.Value));
            itemDescriptionStringEvent.SetVar("PaintingItem",LanguageManager.Instance.GetLocalizedString(itemB.NameKey.Table, itemB.NameKey.Value));
        }

        
        
        
       
    }

    public void UseItem()
    {
        InventoryManager.Instance.UseItem(itemData.ID);
    }
}
