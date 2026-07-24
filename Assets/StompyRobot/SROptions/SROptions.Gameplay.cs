using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using XFramework;
using Object = System.Object;
using Random = UnityEngine.Random;


public partial class SROptions
{
    
    [Category("Save"), DisplayName("存档游戏")]
    public void Save()
    {
        SaveGameManager.Instance.Save();
    }

    [Category("服装制作"),DisplayName("发放服装材料")]
    public void AddGarmentMakingItems()
    {
        foreach (var itemData in LubanManager.Instance.TbItemData.DataList)
        {
            InventoryManager.Instance.AddItem(itemData.ID,Random.Range(5,10));
        }
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

    [Category("GM"), DisplayName("马吉灵感 +10")]
    public void AddMachiInspire10() => GameDataManager.Instance.AddProperty(PropertyType.MachiInspire, 10);

    [Category("GM"), DisplayName("马吉灵感 -10")]
    public void RemoveMachiInspire10() => GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, 10);

    [Category("GM"), DisplayName("马吉压力 +10")]
    public void AddMachiPressure10() => GameDataManager.Instance.AddProperty(PropertyType.MachiPressure, 10);

    [Category("GM"), DisplayName("马吉压力 -10")]
    public void RemoveMachiPressure10() => GameDataManager.Instance.RemoveProperty(PropertyType.MachiPressure, 10);

    [Category("GM"), DisplayName("添加周边物品 1（+10）")]
    public void AddFactoryMerchandiseItem1() => AddFactoryMerchandiseItem(0, 10);

    [Category("GM"), DisplayName("添加周边物品 2（+10）")]
    public void AddFactoryMerchandiseItem2() => AddFactoryMerchandiseItem(1, 10);

    [Category("GM"), DisplayName("添加周边物品 3（+10）")]
    public void AddFactoryMerchandiseItem3() => AddFactoryMerchandiseItem(2, 10);

    private static void AddFactoryMerchandiseItem(int variantIndex, int count)
    {
        InventoryManager bag = InventoryManager.Instance;
        List<ItemData> frames = new();
        List<ItemData> paintings = new();

        foreach (ItemData item in LubanManager.Instance.TbItemData.DataList)
        {
            if (item.ItemType != ItemType.Material)
                continue;

            MaterialItemData materialData = bag.GetMaterialItemData(item.ID);
            if (materialData == null)
                continue;

            if (materialData.MaterialType == ItemMaterialType.FigureModel)
                frames.Add(item);
            else if (materialData.MaterialType == ItemMaterialType.Painting)
                paintings.Add(item);
        }

        if (frames.Count == 0 || paintings.Count == 0)
        {
            Debug.LogWarning("[SROptions] 添加周边物品失败：未找到可用的框架或贴纸配置。");
            return;
        }

        ItemData frame = frames[variantIndex % frames.Count];
        ItemData painting = paintings[variantIndex % paintings.Count];
        long merchandiseId = FactoryComposedItemInfoEt.ComposeMerchandiseId(frame.ID, painting.ID);
        FactoryMerchandiseItemInfo merchandise = new(merchandiseId, count, frame.ID, painting.ID);
        bag.AddRuntimeItem(merchandise);

        Debug.Log($"[SROptions] 已添加周边物品：ID={merchandiseId}，框架={frame.ID}，贴纸={painting.ID}，数量={count}。");
    }

}
