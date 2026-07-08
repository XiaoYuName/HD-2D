using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 购物车中的单个待购买商品槽位。
/// ItemBag.ItemNumber 表示本次选择购买的数量，不是配置表里的刷新库存。
/// </summary>
public class ClothBuyItemSlot : UIBase
{
    /// <summary>
    /// 购物车数据，ItemNumber 是当前购物车内的购买数量。
    /// </summary>
    public ShopItemBag ItemBag { get; private set; }

    /// <summary>
    /// 当前商店的商品配置，主要用于计算单价。
    /// </summary>
    public ShopGoodsData ShopItemData { get; private set; }

    /// <summary>
    /// 物品基础配置，用于显示名称和图标。
    /// </summary>
    public ItemData ItemData { get; private set; }

    // 购物车槽位 UI 引用。
    private Image iconImg;
    private LocalizeStringEvent  itemNameString;
    private LocalizeStringEvent  itemPriceString;
    private TextMeshProUGUI  itemNumberString;
    private Button AddNumberBtn;
    private Button RemoveNumberBtn;

    private BaseShopUI ParentUI;
    
    
    /// <summary>
    /// 初始化槽位 UI 引用和按钮事件，一般不需要手动调用。
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
        ItemBag = null;
        ShopItemData = null;
    }
    
    /// <summary>
    /// 点击加号，继续增加该商品购买数量。
    /// </summary>
    private void AddNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.AddBuyItem(this);
        }
    }

    /// <summary>
    /// 点击减号，减少该商品购买数量。
    /// </summary>
    private void RemoveNumberOnClick()
    {
        if (ParentUI != null)
        {
            ParentUI.RemoveBuyItem(this);
        }
    }

    /// <summary>
    /// 设置购物车商品数据。
    /// 商品基础信息来自 InventoryManager，商店价格等信息通过父级 BaseShopUI 查询。
    /// </summary>
    public void SetData(ShopItemBag shopData,BaseShopUI clothShopUI)
    {
        Release();
        ParentUI = clothShopUI;
        ItemBag = shopData;
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
                itemNumberString.text = shopData.ItemNumber.ToString();
            }
        }
    }
}
