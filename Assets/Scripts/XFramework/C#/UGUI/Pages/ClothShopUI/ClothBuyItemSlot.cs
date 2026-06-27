using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class ClothBuyItemSlot : UIBase
{
    public ShopItemBag ItemBag { get; private set; }
    public ClothShopData ClothShopData { get; private set; }
    public ItemData ItemData { get; private set; }

    private Image iconImg;
    private LocalizeStringEvent  itemNameString;
    private LocalizeStringEvent  itemPriceString;
    private TextMeshProUGUI  itemNumberString;
    private Button AddNumberBtn;
    private Button RemoveNumberBtn;

    private BaseShopUI ParentUI;
    
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        iconImg = Get<Image>("itemFarme/iconImg");
        itemNameString = Get<LocalizeStringEvent>("itemNameString");
        itemPriceString = Get<LocalizeStringEvent>("itemPriceString");
        itemNumberString = Get<TextMeshProUGUI>("BuyFarme/itemNumberString");
        AddNumberBtn = Get<Button>("BuyFarme/AddNumberBtn");
        RemoveNumberBtn = Get<Button>("BuyFarme/RemoveNumberBtn");
        Bind(AddNumberBtn,AddNumberOnClick,"");
        Bind(RemoveNumberBtn,RemoveNumberOnClick,"");
    }

    public void Release()
    {
        if (ItemData != null)
        {
            iconImg.sprite = null;
            AssetsManager.Instance.FreeAsset(ItemData.IconPath);
            ItemData = null;
        }
        ItemBag = null;
        ClothShopData = null;
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

    public void SetData(ShopItemBag shopData,BaseShopUI clothShopUI)
    {
        Release();
        ParentUI = clothShopUI;
        ItemBag = shopData;
        ClothShopData = ShopManager.Instance.GetClothShopData(ItemBag.ItemID);
        if (shopData != null)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(shopData.ItemID);
            if (itemData != null)
            {
                ItemData = itemData;
                iconImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(itemData.IconPath);
                itemNameString.SetText("InventoryItem",itemData.Name);
                itemPriceString.SetVar("value",ClothShopData.Price);
                itemNumberString.text = shopData.ItemNumber.ToString();
            }
        }
    }
}
