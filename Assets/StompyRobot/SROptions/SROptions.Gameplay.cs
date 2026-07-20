using System;
using System.ComponentModel;
using UnityEngine;
using XFramework;
using Object = System.Object;


public partial class SROptions
{
    
    [Category("Save"), DisplayName("存档游戏")]
    public void Save()
    {
        SaveGameManager.Instance.Save();
    }

    [Category("GM"), DisplayName("添加工厂测试道具")]
    public void AddFactoryTestItems()
    {
        InventoryManager bag = InventoryManager.Instance;
        int frameKinds = 0, stickerKinds = 0;
        foreach (ItemData item in LubanManager.Instance.TbItemData.DataList)
        {
            if (item.ItemType != ItemType.Material)
                continue;
            MaterialItemData materialData = bag.GetMaterialItemData(item.ID);
            if (materialData == null)
                continue;

            if (materialData.MaterialType == ItemMaterialType.FigureModel) { bag.AddItem(item.ID, 1); frameKinds++; }      // 框架不消耗，1 个够测
            else if (materialData.MaterialType == ItemMaterialType.Painting) { bag.AddItem(item.ID, 5); stickerKinds++; }   // 贴纸会被消耗，多给几个
        }
        Debug.Log($"[SROptions] 工厂测试道具：框架 {frameKinds} 种、贴纸 {stickerKinds} 种");
    }

    [Category("GM"), DisplayName("添加厨房测试道具")]
    public void AddKitchenTestItems()
    {
        InventoryManager bag = InventoryManager.Instance;
        // 蛋炒饭(110001)
        bag.AddItem(100000, 2); // 米饭
        bag.AddItem(100006, 2); // 鸡蛋
        // 八宝菜(110002)
        bag.AddItem(100007, 2); // 猪肉
        bag.AddItem(100021, 2); // 鱿鱼
        bag.AddItem(100013, 2); // 萝卜
        bag.AddItem(100012, 2); // 香菇
        // 香煎鱼(110007)
        bag.AddItem(100036, 2); // 青花鱼
        // 味增汤(110018)
        bag.AddItem(100001, 2); // 豆腐
        bag.AddItem(100015, 2); // 海苔
        // 调料
        bag.AddItem(100040, 5); // 食用盐
        bag.AddItem(100043, 3); // 高汤
        Debug.Log("[SROptions] 厨房测试食材已发放");
    }

    [Category("GM"), DisplayName("鱼饵 +10")]
    public void AddBait() => InventoryManager.Instance.AddItem(ItemIdSet.Bait, 10);

    [Category("GM"), DisplayName("添加竹鱼竿")]
    public void AddBambooRod() => InventoryManager.Instance.AddItem(140000, 1);

    [Category("GM"), DisplayName("添加玻璃纤维竿")]
    public void AddFiberglassRod() => InventoryManager.Instance.AddItem(140001, 1);

    [Category("GM"), DisplayName("添加铱金鱼竿")]
    public void AddIridiumRod() => InventoryManager.Instance.AddItem(140002, 1);

    [Category("GM"), DisplayName("添加高级铱金鱼竿")]
    public void AddAdvancedIridiumRod() => InventoryManager.Instance.AddItem(140003, 1);

    [Category("GM"), DisplayName("金币 +1000")]
    public void AddMoney1000() => GameDataManager.Instance.AddProperty(PropertyType.Coin, 1000);

    [Category("GM"), DisplayName("金币 +10000")]
    public void AddMoney10000() => GameDataManager.Instance.AddProperty(PropertyType.Coin, 10000);

    [Category("GM"), DisplayName("金币 +100000")]
    public void AddMoney100000() => GameDataManager.Instance.AddProperty(PropertyType.Coin, 100000);

    [Category("GM"), DisplayName("游戏币 +1000")]
    public void AddGameCoin1000() => GameDataManager.Instance.AddProperty(PropertyType.GameCoin, 1000);

    [Category("GM"), DisplayName("游戏币 +10000")]
    public void AddGameCoin10000() => GameDataManager.Instance.AddProperty(PropertyType.GameCoin, 10000);

    [Category("GM"), DisplayName("游戏币 +100000")]
    public void AddGameCoin100000() => GameDataManager.Instance.AddProperty(PropertyType.GameCoin, 100000);

}
