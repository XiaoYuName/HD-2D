using System;
using System.Collections.Generic;
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
            ClothShops = ClothShopDataHelper.GetAll();
        }
    }

    #endregion

    #region ClothShop

    private Action<List<ClothShopData>> onClothShopChange;
    private List<ClothShopData> ClothShops = new List<ClothShopData>();

    public void BindClothShopChange(Action<List<ClothShopData>> ClothShopChanged)
    {
        onClothShopChange += ClothShopChanged;
        
        onClothShopChange?.Invoke(ClothShops);
    }

    public void UnBindClothShopChange(Action<List<ClothShopData>> ClothShopChanged)
    {
        onClothShopChange -=  ClothShopChanged;
    }

    public void SetClothShops(List<ClothShopData> ClothShops)
    {
        this.ClothShops = ClothShops;
        onClothShopChange?.Invoke(ClothShops);
    }


    #endregion

}
