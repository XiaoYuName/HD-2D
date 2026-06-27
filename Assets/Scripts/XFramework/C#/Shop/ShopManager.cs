using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework;

/// <summary>
/// 商店管理器。记录商店状态,保存,读取商店信息
/// </summary>
public class ShopManager : MonoSingleton<ShopManager>,ISaveable
{
    #region ISaveable implementation
    public string GUID => "ShopManager";
    private void Start()
    {
        ISaveable saveable = this;
        SaveGameManager.Instance.RegisterSaveable(saveable);
        GameManager.Instance.OnEnterGame += BindEvents;
        GameManager.Instance.OnExitGame += UnBindEvents;
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public GameSaveData GenerateSaveData()
    {
        GameSaveData data = new GameSaveData();
        data.ClothShops = ClothShops; 
        data.SuperMarketShops = SuperMarkShops;
        return data;
        
        
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="GameSave"></param>
    public void RestoreData(GameSaveData GameSave)
    {
        if (GameSave.ClothShops is { Count: > 0 })
        {
            ClothShops = GameSave.ClothShops;
        }
        else
        {
            ClothShops = CreateClothShopItems();
        }

        if (GameSave.SuperMarketShops is { Count: > 0 })
        {
            SuperMarkShops = GameSave.SuperMarketShops;
        }
        else
        {
            SuperMarkShops = CreateSuperMarketShopItems();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameDataManager.IsInitialized)
        {
            GameManager.Instance.OnEnterGame -= BindEvents;
            GameManager.Instance.OnExitGame -= UnBindEvents;
        }
    }

    #endregion

    #region Event

    private bool isBind;

    private void BindEvents()
    {
        if (!isBind)
        {
            GameDataManager.Instance.BindPlayerDataDayChange(OnPlayerDayChange);
            GameDataManager.Instance.BindPlayerDataWeekChange(OnPlayerWeekChange);
            isBind = true;
        }
    }

    private void UnBindEvents()
    {
        if (isBind)
        {
            GameDataManager.Instance.UnBindPlayerDataDayChange(OnPlayerDayChange);
            GameDataManager.Instance.UnBindPlayerDataWeekChange(OnPlayerWeekChange);
            isBind = false;
        }
    }

    private void OnPlayerDayChange(PlayerData user)
    {
        RefreshShopItems(ClothShops, GetClothShopGoodsData, ShopUpdateType.Day);
        onClothShopChange?.Invoke(ClothShops);
        RefreshShopItems(SuperMarkShops, GetSuperMarketShopGoodsData, ShopUpdateType.Day);
        onSuperMarkShopChange?.Invoke(SuperMarkShops);
    }

    private void OnPlayerWeekChange(PlayerData user)
    {
        RefreshShopItems(ClothShops, GetClothShopGoodsData, ShopUpdateType.Week);
        onClothShopChange?.Invoke(ClothShops);
        RefreshShopItems(SuperMarkShops, GetSuperMarketShopGoodsData, ShopUpdateType.Week);
        onSuperMarkShopChange?.Invoke(SuperMarkShops);
    }

    #endregion


    #region ClothShop

    private Action<List<ShopItemBag>> onClothShopChange;
    private List<ShopItemBag> ClothShops = new List<ShopItemBag>();

    public void BindClothShopChange(Action<List<ShopItemBag>> ClothShopChanged)
    {
        onClothShopChange += ClothShopChanged;
        
        onClothShopChange?.Invoke(ClothShops);
    }

    public void UnBindClothShopChange(Action<List<ShopItemBag>> ClothShopChanged)
    {
        onClothShopChange -=  ClothShopChanged;
    }

    public void SetClothShops(List<ShopItemBag> ClothShops)
    {
        this.ClothShops = ClothShops;
        onClothShopChange?.Invoke(ClothShops);
    }


    #endregion

    #region SupermarkShop 超市货品
    private Action<List<ShopItemBag>> onSuperMarkShopChange;
    
    private List<ShopItemBag> SuperMarkShops = new List<ShopItemBag>();

    public void BindSuperMarkShopChange(Action<List<ShopItemBag>> SuperMarkShopChanged)
    {
        onSuperMarkShopChange += SuperMarkShopChanged;
        onSuperMarkShopChange?.Invoke(SuperMarkShops);
    }

    public void UnBindSuperMarkShopChange(Action<List<ShopItemBag>> SuperMarkShopChanged)
    {
        onSuperMarkShopChange -= SuperMarkShopChanged;
    }
    
    public void SetSuperMarkShops(List<ShopItemBag> superMarkShops)
    {
        this.SuperMarkShops = superMarkShops;
        onSuperMarkShopChange?.Invoke(SuperMarkShops);
    }


    #endregion

    #region GetClothShops

    public List<ClothShopData> GetClothShops()
    {
        return LubanManager.Instance.TbClothShopData.DataList.ToList();
    }

    public ClothShopData GetClothShopData(long itemID)
    {
        return LubanManager.Instance.TbClothShopData.Get(itemID);
    }

    public ShopGoodsData GetClothShopGoodsData(long itemID)
    {
        var data = LubanManager.Instance.TbClothShopData.GetOrDefault(itemID);
        return data == null ? null : new ShopGoodsData(data.ItemID, data.ItemNumber, data.Price, data.UpdateMode);
    }

    public ShopGoodsData GetSuperMarketShopGoodsData(long itemID)
    {
        var data = LubanManager.Instance.TbSuperMarketShopData.GetOrDefault(itemID);
        return data == null ? null : new ShopGoodsData(data.ItemID, data.ItemNumber, data.Price, data.UpdateMode);
    }

    private static List<ShopItemBag> CreateClothShopItems()
    {
        return LubanManager.Instance.TbClothShopData.DataList
            .Select(data => new ShopItemBag
            {
                ItemID = data.ItemID,
                ItemNumber = data.ItemNumber,
            })
            .ToList();
    }

    private static List<ShopItemBag> CreateSuperMarketShopItems()
    {
        return LubanManager.Instance.TbSuperMarketShopData.DataList
            .Select(data => new ShopItemBag
            {
                ItemID = data.ItemID,
                ItemNumber = data.ItemNumber,
            })
            .ToList();
    }

    private static void RefreshShopItems(
        List<ShopItemBag> shopItems,
        Func<long, ShopGoodsData> getGoodsData,
        ShopUpdateType updateMode)
    {
        foreach (var shopItem in shopItems)
        {
            var goodsData = getGoodsData(shopItem.ItemID);
            if (goodsData != null && goodsData.UpdateMode == updateMode)
            {
                shopItem.ItemNumber = goodsData.ItemNumber;
            }
        }
    }

    #endregion

}

public class ShopGoodsData
{
    public ShopGoodsData(long itemID, int itemNumber, int price, ShopUpdateType updateMode)
    {
        ItemID = itemID;
        ItemNumber = itemNumber;
        Price = price;
        UpdateMode = updateMode;
    }

    public long ItemID { get; }
    public int ItemNumber { get; }
    public int Price { get; }
    public ShopUpdateType UpdateMode { get; }
}

/// <summary>
/// 商品背包
/// </summary>
[Serializable]
public class ShopItemBag
{
    public long ItemID;
    public int ItemNumber;
}
