using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using XFramework;
using Object = System.Object;
using Random = UnityEngine.Random;


/// <summary>
/// SROptions 游戏GM指令 - 框架精简版本
/// 根据具体项目需求添加GM指令
/// </summary>
public partial class SROptions
{
    private PropertyType propertyType;
    public int propertyAmount;

    [Category("属性"),DisplayName("类型")]
    public PropertyType PropertyType
    {
        get { return propertyType; }
        set { propertyType = value; }
    }

    [Category("属性"),DisplayName("数量")]
    public int PropertyAmount
    {
        get { return propertyAmount; }
        set { propertyAmount = value; }
    }

    [Category("属性"),DisplayName("增加数量")]
    public void AddProperty()
    {
        if (GameDataManager.IsInitialized)
        {
            GameDataManager.Instance.AddProperty(propertyType, propertyAmount);
        }
    }

    [Category("Save"), DisplayName("存档游戏")]
    public void Save()
    {
        SaveGameManager.Instance.Save();
    }

    [Category("GM"), DisplayName("添加所有物品（测试）")]
    public void AddAllItems()
    {
        InventoryManager bag = InventoryManager.Instance;
        foreach (var itemData in LubanManager.Instance.TbItemData.DataList)
        {
            bag.AddItem(itemData.ID, Random.Range(1, 5));
        }
        Debug.Log("[SROptions] 已添加所有物品");
    }

    // 游戏特定的GM指令已移除，根据项目需求自行添加
}
