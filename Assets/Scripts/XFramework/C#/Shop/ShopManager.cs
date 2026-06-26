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
        GameManager.Instance.OnEnterGame += BindEvents;
        GameManager.Instance.OnExitGame += UnBindEvents;
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.ClothShops = ClothShops; 
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="data"></param>
    public void LoadData(GameSaveData data)
    {
        if (data.ClothShops is { Count: > 0 })
        {
            ClothShops = data.ClothShops;
        }
        else
        {
            ClothShops = ClothShopDataHelper.GetAll();
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
            if ((ShopUpdateType)clothShopData.UpdateModes == ShopUpdateType.Day)
            {
                var data = ClothShopDataHelper.GetOneByCondition(temp => temp.ItemID == clothShopData.ItemID);
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
            if ((ShopUpdateType)clothShopData.UpdateModes == ShopUpdateType.Week)
            {
                var data = ClothShopDataHelper.GetOneByCondition(temp => temp.ItemID == clothShopData.ItemID);
                if (data != null)
                {
                    clothShopData.ItemNumber = data.ItemNumber;
                }
            }
        }
        onClothShopChange?.Invoke(ClothShops);
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
