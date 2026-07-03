using System;
using System.Collections.Generic;
using UnityEngine;

public class SexToyStoreUI : BaseShopUI
{
    /// <summary>
    /// 绑定具体商店的库存变化事件。
    /// 例如布料商店绑定 ClothShop，超市绑定 SuperMarketShop。
    /// </summary>
    protected override void RegisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.RegisterSexToShopChange(callback);
    }

    /// <summary>
    /// 解绑具体商店的库存变化事件。
    /// </summary>
    protected override void UnregisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.UnregisterSexToShopChange(callback);
    }

    /// <summary>
    /// 结算成功后，把基类计算后的库存写回具体商店。
    /// </summary>
    protected override void SetShopItems(List<ShopItemBag> shopItems)
    {
       ShopManager.Instance.SetSexToShops(shopItems);
    }

    /// <summary>
    /// 根据物品 ID 查询当前商店的商品配置。
    /// 不同商店可以来自不同 Luban 表，但返回统一的 ShopGoodsData 给基类使用。
    /// </summary>
    protected override ShopGoodsData GetShopGoodsData(long itemID)
    {
        return ShopManager.Instance.GetSexToShopGoodsData(itemID);
    }
}
