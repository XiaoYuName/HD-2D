using System;
using UnityEngine;
using XFramework;

[Serializable]
public class ItemData
{
    [SerializeField] long id;
    [SerializeField] string remark;
    [SerializeField] string name;
    [SerializeField] string desc;
    [SerializeField] ItemType type;
    [SerializeField] int maxCount;
    [SerializeField] int shop;
    [SerializeField] int currencyType;
    [SerializeField] int value;
    [SerializeField] int[] purchaseRestriction;
    [SerializeField] string iconPath;
    [SerializeField] int quality;

    #region Get
    public long Id => id;
    public string Remark => remark;
    public string Name => name;
    public string Desc => desc;
    public ItemType Type => type;
    public int MaxCount => maxCount;
    public int Shop => shop;
    public int CurrencyType => currencyType;
    public int Value => value;
    public bool CanSell => value > 0;
    public int SellCurrencyType => currencyType;
    public int SellAmount => value;
    public int[] PurchaseRestriction => purchaseRestriction;
    public string IconPath => iconPath;
    // 通过 AA 包动态加载图标，配置本身不直接引用 Sprite，实现资源分离
    public Sprite Icon => string.IsNullOrEmpty(iconPath) ? null : AssetsManager.Instance.LoadAssets<Sprite>(iconPath);
    public int Quality => quality;
    #endregion
    #region Create
    public static ItemData Create(long id, string remark, string name, string desc, ItemType type,
        int maxCount, int shop, int currencyType, int value, int[] purchaseRestriction, string iconPath, int quality)
    {
        return new ItemData
        {
            id = id, remark = remark, name = name, desc = desc, type = type,
            maxCount = maxCount, shop = shop, currencyType = currencyType, value = value,
            purchaseRestriction = purchaseRestriction, iconPath = iconPath, quality = quality
        };
    }
    #endregion
}
