using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 商店购买列表中的单个商品槽位。
/// 虽然类名沿用了 ClothShop，但实际通过 BaseShopUI 查询配置，可被不同商店复用。
/// </summary>
public class ClothShopItemSlot : UIBase
{
    // 商品展示相关 UI。
    private Image iconImg;
    private LocalizeStringEvent itemNameString;
    private LocalizeStringEvent itemPriceString;
    private LocalizeStringEvent itemDescriptionString;
    private LocalizeStringEvent itemNumberString;
    private LocalizeStringEvent itemMyNumberString;
    private Button AddNumberButton;
    private Button RemoveNumberButton;

    /// <summary>
    /// 当前商店的商品配置，包含价格、默认库存和刷新方式。
    /// </summary>
    public ShopGoodsData ShopItemData { get; private set; }

    /// <summary>
    /// 当前商店库存数据，ItemNumber 表示当前剩余库存。
    /// </summary>
    public ShopItemBag ShopItemBag { get; private set; }

    /// <summary>
    /// 物品基础配置，用于显示名称、描述、图标和类型。
    /// </summary>
    public ItemData ItemData { get; private set; }
    
    // 持有父级商店 UI，点击加减时把操作交回 BaseShopUI 统一处理。
    private BaseShopUI ParentUI;
    

    /// <summary>
    /// 初始化槽位 UI 引用和按钮事件，一般不需要手动调用。
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


    /// <summary>
    /// 设置购买列表商品数据。
    /// 商品基础信息来自 InventoryManager，商店价格等信息通过父级 BaseShopUI 查询。
    /// </summary>
    public void SetData(ShopItemBag shopData,BaseShopUI parentUI)
    {
        Release();
        ParentUI  = parentUI;
        ShopItemBag = shopData;
        if (shopData != null && ParentUI != null)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(shopData.ItemID);
            if (itemData != null)
            {
                ItemData = itemData;
                ShopItemData = ParentUI.GetGoodsData(ItemData.ID);
                if (ShopItemData == null)
                {
                    Debug.LogWarning($"未找到商店商品配置，ItemID: {ItemData.ID}");
                    return;
                }
                
                iconImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(itemData.IconName);
                itemNameString.SetText(itemData.NameKey.Table,itemData.NameKey.Value);
                itemPriceString.SetVar("value",ShopItemData.Price);
                itemDescriptionString.SetText(itemData.DescKey.Table,itemData.DescKey.Value);
                itemNumberString.SetVar("value",shopData.ItemNumber);
                itemMyNumberString.SetVar("value",InventoryManager.Instance.GetItemCount(itemData.ID)); 
            }
        }
    }

    /// <summary>
    /// 点击加号，把该商品加入购物车。
    /// </summary>
    private void AddNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.AddBuyItem(this);
        }
        
    }

    /// <summary>
    /// 点击减号，从购物车减少该商品数量。
    /// </summary>
    private void RemoveNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.RemoveBuyItem(this);
        }
    }

    /// <summary>
    /// 释放槽位引用的图标资源，并清空缓存数据，便于复用或回收。
    /// </summary>
    public void Release()
    {
        if (ItemData != null)
        {
            iconImg.sprite = null;
            AssetsManager.Instance.FreeAsset(ItemData.IconName);
            ItemData = null;
        }
        ShopItemData = null;
        ShopItemBag = null;
    }

}
