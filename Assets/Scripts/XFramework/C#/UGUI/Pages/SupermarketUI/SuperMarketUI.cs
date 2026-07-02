using System;
using System.Collections.Generic;

/// <summary>
/// 超市商店。
/// 复用 BaseShopUI 的购买/出售 UI 流程，只替换为超市的数据源。
/// </summary>
public class SuperMarketUI : BaseShopUI
{
    /// <summary>
    /// 监听超市库存变化，触发基类刷新购买列表。
    /// </summary>
    protected override void RegisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.RegisterSuperMarkShopChange(callback);
    }

    /// <summary>
    /// 关闭 UI 时解绑超市库存变化。
    /// </summary>
    protected override void UnregisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.UnregisterSuperMarkShopChange(callback);
    }

    /// <summary>
    /// 购买结算成功后，把剩余库存写回超市商店。
    /// </summary>
    protected override void SetShopItems(List<ShopItemBag> shopItems)
    {
        ShopManager.Instance.SetSuperMarkShops(shopItems);
    }

    /// <summary>
    /// 从超市配置表读取商品价格、刷新数量和刷新方式。
    /// </summary>
    protected override ShopGoodsData GetShopGoodsData(long itemID)
    {
        return ShopManager.Instance.GetSuperMarketShopGoodsData(itemID);
    }
}
