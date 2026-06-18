using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using XFramework;
using Random = System.Random;

public class InventoryManager : MonoSingleton<InventoryManager>,IGameInitialized,ISaveable
{
    private List<ItemBag> PlayerItemBags = new List<ItemBag>();
    private ItemConfig itemConfigs;
    /// <summary>
    /// 初始化脚本函数
    /// </summary>
    /// <returns></returns>
    public async UniTask Initialized()
    {
        itemConfigs = await AssetsManager.Instance.LoadAssetsUniTask<ItemConfig>(AssetKeys.ItemConfigPath);
    }

    /// <summary>
    /// 释放脚本函数
    /// </summary>
    public async UniTask Release()
    {
         AssetsManager.Instance.FreeAsset(AssetKeys.ItemConfigPath);
         await UniTask.CompletedTask;
    }

    #region ISaveable

    public string GUID => "InventoryManager";

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public GameSaveData GenerateSaveData()
    {
        GameSaveData gameSaveData = new GameSaveData();
        gameSaveData.itemBags = PlayerItemBags;
        return gameSaveData;
    }

    public void RestoreData(GameSaveData GameSave)
    {
        if (GameSave is { itemBags: { Count: > 0 } })
        {
            PlayerItemBags = GameSave.itemBags;
        }
        else
        {
            PlayerItemBags = new List<ItemBag>();
        }
    }
    

    #endregion
    
    #region 事件注册

    private Action<List<ItemBag>> AllItemChange;

    /// <summary>
    /// 注册背包内所有物品变化回调
    /// </summary>
    /// <param name="action"></param>
    public void RegisterAllItemChange(Action<List<ItemBag>> action,bool isTrigger = true)
    {
        AllItemChange += action;
        if (isTrigger)
        {
            AllItemChange?.Invoke(PlayerItemBags);
        }
    }

    /// <summary>
    /// 反注册背包内所有物品变化回调
    /// </summary>
    /// <param name="action"></param>
    public void UnregisterAllItemChange(Action<List<ItemBag>> action)
    {
        AllItemChange -= action;
    }

    private Dictionary<long, Action<ItemBag>> ItemChangeCallBack = new Dictionary<long, Action<ItemBag>>();
    
    /// <summary>
    /// 注册游戏背包变化回调
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="CallBack">回调函数</param>
    /// <param name="isTrigger">是否注册时就触发一次</param>
    public void  RegisterItemBagChangAction(long itemID, Action<ItemBag> CallBack,bool isTrigger=true)
    {
        if (!ItemChangeCallBack.ContainsKey(itemID))
        {
            ItemChangeCallBack.Add(itemID,CallBack);
        }
        else
        {
            ItemChangeCallBack[itemID] += CallBack;
        }

        // if (isTrigger)
        // {
        //     CallBack?.Invoke(GetItemBag(itemID));
        // }
    }
    
    /// <summary>
    /// 反注册游戏背包变化回调
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="callback">回调函数</param>
    public void  UnregisterItemBagChangAction(long itemID, Action<ItemBag> callback)
    {
        if (ItemChangeCallBack.ContainsKey(itemID))
        {
            ItemChangeCallBack[itemID] -= callback;
        }
    }
    
    
    
    private Dictionary<Guid,Action<ItemBag>> ItemIdChangeCallBack = new Dictionary<Guid,Action<ItemBag>>();

    public void RegisterItemIdChangeCallBack(Guid guid, Action<ItemBag> callback)
    {
        if (!ItemIdChangeCallBack.ContainsKey(guid))
        {
            ItemIdChangeCallBack.Add(guid,callback);
        }
        else
        {
            ItemIdChangeCallBack[guid] += callback;
        }
    }

    public void UnregisterItemIdChangeCallBack(Guid guid, Action<ItemBag> callback)
    {
        if (ItemIdChangeCallBack.ContainsKey(guid))
        {
            ItemIdChangeCallBack[guid] -= callback;
        }
    }
    

    #endregion

    #region 获取Item

    public ItemData GetItemData(long itemID)
    {
        return itemConfigs.GetItemData(itemID);
    }

    #endregion

    #region 增加物品背包

    public void AddItem(long itemID, int itemAmount)
    {
        if (itemAmount <= 0)
        {
            Debug.LogWarning($"添加物品数量不合法: {itemAmount}");
            return;
        }

        ItemData itemData = itemConfigs.GetItemData(itemID);
        if (itemData == null)
        {
            Debug.LogError($"未找到物品配置，ItemID: {itemID}");
            return;
        }

        int maxCount = itemData.MaxCount;

        if (maxCount <= 0)
        {
            Debug.LogError($"物品最大堆叠数量配置错误，ItemID: {itemID}, MaxCount: {maxCount}");
            return;
        }

        int remainingAmount = itemAmount;

        // 1. 先尝试填充已有的同类物品格子
        foreach (ItemBag bag in PlayerItemBags)
        {
            if (bag.itemID != itemID)
                continue;

            // 当前格子已经满了
            if (bag.itemAmount >= maxCount)
                continue;

            int canAddAmount = maxCount - bag.itemAmount;
            int addAmount = Mathf.Min(canAddAmount, remainingAmount);

            bag.itemAmount += addAmount;
            remainingAmount -= addAmount;
            if (ItemChangeCallBack.ContainsKey(bag.itemID))
            {
                ItemChangeCallBack[bag.itemID].Invoke(bag);
            }

            if (ItemIdChangeCallBack.ContainsKey(bag.GetGuid))
            {
                ItemIdChangeCallBack[bag.GetGuid].Invoke(bag);
            }

            // 已经全部添加完成
            if (remainingAmount <= 0)
            {
                break;
            }
        }

        // 2. 剩余数量继续创建新的格子
        while (remainingAmount > 0)
        {
            int addAmount = Mathf.Min(maxCount, remainingAmount);

            ItemBag itemBag = new ItemBag
            {
                itemID = itemID,
                itemAmount = addAmount
            };

            PlayerItemBags.Add(itemBag);

            remainingAmount -= addAmount;
        }
        AllItemChange?.Invoke(PlayerItemBags);
    }

    #endregion

    #region 修改物品背包
    

    #endregion

    #region GM
    [Button,BoxGroup("测试")]
    public void AddTestFoodMtItems()
    {
        // 蔬菜食材 600000~600019
        for(long id = 600000; id <= 600019; id++)
            AddItem(id, 9);
        // 鱼类食材 610000~610019
        for(long id = 610000; id <= 610019; id++)
            AddItem(id, 9);
        // 调料食材 620000~620009
        for(long id = 620000; id <= 620009; id++)
            AddItem(id, 9);
    }

    #endregion

}

[System.Serializable]
public class ItemBag
{
    [HorizontalGroup("物品"),LabelText("物品ID")]
    public long itemID;
    [HorizontalGroup("物品"),LabelText("物品数量")]
    public int itemAmount;
    
    /// <summary>
    /// 背包道具的唯一标识符
    /// </summary>
    public Guid GetGuid { get; private set; } = Guid.NewGuid();
    
    [LabelText("获取时间")]
    public DateTime CreateTime { get; private set; } = DateTime.Now;
}
