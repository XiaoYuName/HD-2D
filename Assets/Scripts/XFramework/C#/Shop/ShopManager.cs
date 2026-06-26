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
            ClothShops = new List<ShopItemBag>();
            foreach (var clothShopData in LubanManager.Instance.TbClothShopData.DataList)
            {
                ClothShops.Add(new ShopItemBag()
                {
                    ItemID = clothShopData.ItemID,
                    ItemNumber = clothShopData.ItemNumber,
                });
            }
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
        if (!isBind)
        {
            GameDataManager.Instance.UnBindPlayerDataDayChange(OnPlayerDayChange);
            GameDataManager.Instance.UnBindPlayerDataWeekChange(OnPlayerWeekChange);
            isBind = false;
        }
    }

    private void OnPlayerDayChange(PlayerData user)
    {
        foreach (var clothShopData in ClothShops)
        {
            
            if (LubanManager.Instance.TbClothShopData.Get(clothShopData.ItemID).UpdateMode == ShopUpdateType.Day)
            {
                var data = LubanManager.Instance.TbClothShopData.Get(clothShopData.ItemID);
                if (data != null)
                {
                    clothShopData.ItemNumber = data.ItemNumber;
                }
            }
        }
        onClothShopChange?.Invoke(ClothShops);
    }

    private void OnPlayerWeekChange(PlayerData user)
    {
        foreach (var clothShopData in ClothShops)
        {
            if (LubanManager.Instance.TbClothShopData.Get(clothShopData.ItemID).UpdateMode  == ShopUpdateType.Week)
            {
                clothShopData.ItemNumber = LubanManager.Instance.TbClothShopData.Get(clothShopData.ItemID).ItemNumber;
            }
        }
        onClothShopChange?.Invoke(ClothShops);
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

    #region GetClothShops

    public List<ClothShopData> GetClothShops()
    {
        return LubanManager.Instance.TbClothShopData.DataList.ToList();
    }

    public ClothShopData GetClothShopData(long itemID)
    {
        return LubanManager.Instance.TbClothShopData.Get(itemID);
    }

    #endregion

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
