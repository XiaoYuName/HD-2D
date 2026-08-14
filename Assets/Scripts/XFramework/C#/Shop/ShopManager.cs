using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

/// <summary>
/// 商店管理器 - 框架存根版本
/// 根据具体项目需求实现商店逻辑
/// </summary>
public class ShopManager : MonoSingleton<ShopManager>, ISaveable
{
    #region ISaveable

    public string GUID => "ShopManager";

    private void Start()
    {
        ((ISaveable)this).RegisterSaveable();
    }

    public void SaveData(GameSaveData data)
    {
        // 根据项目需求保存商店数据
    }

    public void LoadData(GameSaveData data)
    {
        // 根据项目需求加载商店数据
    }

    #endregion
}

/// <summary>
/// 商店商品数据 - 框架存根版本
/// </summary>
[Serializable]
public class ShopItemBag
{
    public long ItemID;
    public int ItemNumber;
}
