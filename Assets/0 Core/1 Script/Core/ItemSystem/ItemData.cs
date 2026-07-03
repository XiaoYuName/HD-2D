using System;
using UnityEngine;

[Serializable]
public class ItemData
{
    [SerializeField] long id;
    [SerializeField] string remark;
    [SerializeField] string nameKey;
    [SerializeField] string descKey;
    [SerializeField] ItemType type;
    [SerializeField] int maxCount;
    [SerializeField] int shop;  // 出售商店类型
    [SerializeField] int currencyType;
    [SerializeField] int value; // 价值
    [SerializeField] int[] purchaseRestriction;
    [SerializeField] string iconPath;
    [SerializeField] int quality;

    #region Get
    public long Id => id;
    public string Remark => remark;
    public string NameKey => nameKey;
    public string DescKey => descKey;
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
    public int Quality => quality;
    #endregion
    #region Create
    public static ItemData Create(long id, string remark, string name, string desc, ItemType type,
        int maxCount, int shop, int currencyType, int value, int[] purchaseRestriction, string iconPath, int quality)
    {
        return new ItemData
        {
            id = id, remark = remark, nameKey = name, descKey = desc, type = type,
            maxCount = maxCount, shop = shop, currencyType = currencyType, value = value,
            purchaseRestriction = purchaseRestriction, iconPath = iconPath, quality = quality
        };
    }
    #endregion
}
