using System;
using UnityEngine;

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

    // 供子类（如运行时合成的 FactoryProductionMaterialsData）填充基类字段：基类字段为私有，
    // 子类无法直接用对象初始化器赋值，故开放此受保护入口。普通物品仍走 Create。
    protected void SetBaseData(long id, string remark, string name, string desc, ItemType type,
        int maxCount, int shop, int currencyType, int value, int[] purchaseRestriction, string iconPath, int quality)
    {
        this.id = id; this.remark = remark; this.name = name; this.desc = desc; this.type = type;
        this.maxCount = maxCount; this.shop = shop; this.currencyType = currencyType; this.value = value;
        this.purchaseRestriction = purchaseRestriction; this.iconPath = iconPath; this.quality = quality;
    }
    #endregion
}
