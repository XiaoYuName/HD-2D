using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class FishShopUI : BaseShopUI
{
    /// <summary>
    /// 购买前校验鱼竿的等级解锁要求（FishRodConfig.RequireLevel）；未达标则拦截并弹出提示，不放入购物车。
    /// 非鱼竿商品（RequireLevel 无配置返回0）不受影响。
    /// 直接读配置资产 + GameDataManager 当前等级，不依赖 FishGameManager（商店打开时该常驻管理器不一定已创建）。
    /// </summary>
    protected override void AddBuyItem(ShopGoodsData data)
    {
        if (data != null)
        {
            FishRodConfig rodConfig = AssetsManager.Instance.LoadAssets<FishRodConfig>(AssetKeys.FishRodConfigPath);

            int requireLevel = rodConfig.GetRequireLevel(data.ItemID);

            AssetsManager.Instance.FreeAsset(AssetKeys.FishRodConfigPath);

            if (requireLevel > 0 && GameDataManager.Instance.GetPropertyData(PropertyType.FishLevel).DeftualNumber < requireLevel)
            {
                UIUtility.ShowPopWindow("BuyError_FishLevel");
                return;
            }
        }
        base.AddBuyItem(data);
    }
    /// <summary>
    /// 绑定具体商店的库存变化事件。
    /// 例如布料商店绑定 ClothShop，超市绑定 SuperMarketShop。
    /// </summary>
    protected override void RegisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.RegisterFishShopChange(callback);
    }

    /// <summary>
    /// 解绑具体商店的库存变化事件。
    /// </summary>
    protected override void UnregisterShopChange(Action<List<ShopItemBag>> callback)
    {
        ShopManager.Instance.UnregisterFishShopChange(callback);
    }

    /// <summary>
    /// 结算成功后，把基类计算后的库存写回具体商店。
    /// </summary>
    protected override void SetShopItems(List<ShopItemBag> shopItems)
    {
        ShopManager.Instance.SetFishShops(shopItems);
    }

    /// <summary>
    /// 根据物品 ID 查询当前商店的商品配置。
    /// 不同商店可以来自不同 Luban 表，但返回统一的 ShopGoodsData 给基类使用。
    /// </summary>
    protected override ShopGoodsData GetShopGoodsData(long itemID)
    {
        return ShopManager.Instance.GetFishShopGoodsData(itemID);
    }
}
