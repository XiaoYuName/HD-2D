using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class ClothShopItemSlot : UIBase
{
    private Image iconImg;
    private LocalizeStringEvent itemNameString;
    private LocalizeStringEvent itemPriceString;
    private LocalizeStringEvent itemDescriptionString;
    private LocalizeStringEvent itemNumberString;
    private LocalizeStringEvent itemMyNumberString;
    private Button AddNumberButton;
    private Button RemoveNumberButton;


    public ClothShopData ShopItemData { get; private set; }
    public ShopItemBag ShopItemBag { get; private set; }
    public ItemData ItemData { get; private set; }
    
    private BaseShopUI ParentUI;
    

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        iconImg = Get<Image>("ItemSlot/Icon");
        itemNameString = Get<LocalizeStringEvent>("ItemNameText");
        itemDescriptionString = Get<LocalizeStringEvent>("ItemDescriptionText");
        itemPriceString = Get<LocalizeStringEvent>("ItemPriceText");
        itemNumberString = Get<LocalizeStringEvent>("BuyPanel/Farme/ShopNumberTex");
        itemMyNumberString  = Get<LocalizeStringEvent>("BuyPanel/Farme/MyCountText");
        AddNumberButton = Get<Button>("BuyPanel/Farme/BuyFarme/AddNumberButton");
        RemoveNumberButton = Get<Button>("BuyPanel/Farme/BuyFarme/RemoveNumberButton");
        
        Bind(AddNumberButton,AddNumberOnClick,"");
        Bind(RemoveNumberButton,RemoveNumberOnClick,"");
    }


    public void SetData(ShopItemBag shopData,BaseShopUI parentUI)
    {
        ParentUI  = parentUI;
        ShopItemBag = shopData;
        if (shopData != null)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(shopData.ItemID);
            if (itemData != null)
            {
                ItemData = itemData;
                ShopItemData =  ShopManager.Instance.GetClothShopData(ItemData.Id);
                
                iconImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(itemData.IconPath);
                itemNameString.SetText("InventoryItem",itemData.Name);
                itemPriceString.SetVar("value",ShopItemData.Price);
                itemDescriptionString.SetText("InventoryItem",itemData.Desc);
                itemNumberString.SetVar("value",shopData.ItemNumber);
                itemMyNumberString.SetVar("value",InventoryManager.Instance.GetItemCount(itemData.Id)); 
            }
        }
    }

    private void AddNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.AddBuyItem(this);
        }
        
    }

    private void RemoveNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.RemoveBuyItem(this);
        }
    }

    public void Release()
    {
        if (ItemData != null)
        {
            AssetsManager.Instance.FreeAsset(ItemData.IconPath);
            ItemData = null;
        }
    }

}
