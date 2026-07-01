using System;
using System.Collections.Generic;

/// <summary>
/// 布料商店。
/// 只负责把 BaseShopUI 的通用流程接到布料商店数据源。
/// </summary>
public class ClothShopUI : BaseShopUI
{
    /// <summary>
    /// 监听布料商店库存变化，触发基类刷新购买列表。
    /// </summary>
    protected override void RegisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.RegisterClothShopChange(callback);
    }

    /// <summary>
    /// 关闭 UI 时解绑布料商店库存变化。
    /// </summary>
    protected override void UnregisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.UnregisterClothShopChange(callback);
    }

    /// <summary>
    /// 购买结算成功后，把剩余库存写回布料商店。
    /// </summary>
    protected override void SetShopItems(List<ShopItemBag> shopItems)
    {
        ShopManager.Instance.SetClothShops(shopItems);
    }

    /// <summary>
    /// 从布料商店配置表读取商品价格、刷新数量和刷新方式。
    /// </summary>
    protected override ShopGoodsData GetShopGoodsData(long itemID)
    {
        return ShopManager.Instance.GetClothShopGoodsData(itemID);
    }
}
