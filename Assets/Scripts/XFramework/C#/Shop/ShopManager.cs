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
        data.FruitShops = FruitShops;
        data.SexToShops = SexToShops;
        data.FishShops = FishShops;
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

        if (GameSave.FruitShops is { Count: > 0 })
        {
            FruitShops = GameSave.FruitShops;
        }
        else
        {
            FruitShops = CreateFruitShopItems();
        }

        if (GameSave.SexToShops is { Count: > 0 })
        {
            SexToShops = GameSave.SexToShops;
        }
        else
        {
            SexToShops = CreatSexToShopItems();
        }

        if (GameSave.FishShops is { Count: > 0 })
        {
            FishShops = GameSave.FishShops;
        }
        else
        {
            FishShops = CreatFishShopItems();
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


    #region 布料商店

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
        SuperMarkShopChanged?.Invoke(SuperMarkShops);
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

    #region 果蔬店商品

    private Action<List<ShopItemBag>> onFruitShopChange;
    
    private List<ShopItemBag> FruitShops = new List<ShopItemBag>();

    public void BindFruitShopChange(Action<List<ShopItemBag>> FruitShopChanged)
    {
        onFruitShopChange += FruitShopChanged;
        FruitShopChanged?.Invoke(FruitShops);
    }

    public void UnBindFruitShopChange(Action<List<ShopItemBag>> FruitShopChanged)
    {
        onFruitShopChange -= FruitShopChanged;
    }

    public void SetFruitShops(List<ShopItemBag> FruitShops)
    {
        SuperMarkShops = FruitShops;
        onFruitShopChange?.Invoke(FruitShops);
    }



    #endregion

    #region 情趣用品店
    private Action<List<ShopItemBag>> onSexToShopChange;
    
    private List<ShopItemBag> SexToShops = new List<ShopItemBag>();

    public void BindSexToShopChange(Action<List<ShopItemBag>> SexToShopChange)
    {
        onSexToShopChange += SexToShopChange;
        SexToShopChange?.Invoke(FruitShops);
    }

    public void UnBindSexToShopChange(Action<List<ShopItemBag>> SexToShopChange)
    {
        onSexToShopChange -= SexToShopChange;
    }

    public void SetSexToShops(List<ShopItemBag> FruitShops)
    {
        SuperMarkShops = FruitShops;
        onFruitShopChange?.Invoke(FruitShops);
    }

    #endregion

    #region 钓鱼商店
    private Action<List<ShopItemBag>> onFishShopChange;
    
    private List<ShopItemBag> FishShops = new List<ShopItemBag>();

    public void BindFishShopChange(Action<List<ShopItemBag>> FishShopChange)
    {
        onFishShopChange += FishShopChange;
        FishShopChange?.Invoke(FishShops);
    }

    public void UnBindFishShopChange(Action<List<ShopItemBag>> FishShopChange)
    {
        onFishShopChange -= FishShopChange;
    }

    public void SetFishShops(List<ShopItemBag> FruitShops)
    {
        FishShops = FruitShops;
        onFishShopChange?.Invoke(FruitShops);
    }

    #endregion

    #region GetClothShops

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

    public ShopGoodsData GetFruitShopGoodsData(long itemID)
    {
        var data = LubanManager.Instance.TbFruitShopData.GetOrDefault(itemID);
        return data == null ? null : new ShopGoodsData(data.ItemID, data.ItemNumber, data.Price, data.UpdateMode);
    }

    public ShopGoodsData GetSexToShopGoodsData(long itemID)
    {
        var data = LubanManager.Instance.TbSexToShopData.GetOrDefault(itemID);
        return data == null ? null : new ShopGoodsData(data.ItemID, data.ItemNumber, data.Price, data.UpdateMode);
    }
    
    public ShopGoodsData GetFishShopGoodsData(long itemID)
    {
        var data = LubanManager.Instance.TbFishShopData.GetOrDefault(itemID);
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

    private static List<ShopItemBag> CreateFruitShopItems()
    {
        return LubanManager.Instance.TbFruitShopData.DataList .Select(data => new ShopItemBag
            {
                ItemID = data.ItemID,
                ItemNumber = data.ItemNumber,
            })
            .ToList();
    }

    private static List<ShopItemBag> CreatSexToShopItems()
    {
        return LubanManager.Instance.TbSexToShopData.DataList
            .Select(data => new ShopItemBag
            {
                ItemID = data.ItemID,
                ItemNumber = data.ItemNumber,
            })
            .ToList();
    }

    private static List<ShopItemBag> CreatFishShopItems()
    {
        return LubanManager.Instance.TbFishShopData.DataList
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
