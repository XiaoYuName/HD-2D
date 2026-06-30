using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class InventoryManager : MonoSingleton<InventoryManager>, IGameInitialized, ISaveable
{
    private List<ItemBag> PlayerItemBags = new List<ItemBag>();
    private ItemConfig itemConfigs;

    /// <summary>
    /// 初始化脚本函数
    /// </summary>
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

    public void Start()
    {
        ISaveable saveable = this;
        SaveGameManager.Instance.RegisterSaveable(saveable);
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSaveData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.itemBags = PlayerItemBags;
    }

    public void LoadData(GameSaveData data)
    {
        if (data is { itemBags: { Count: > 0 } })
        {
            PlayerItemBags = data.itemBags;
        }
        else
        {
            PlayerItemBags = new List<ItemBag>();
            foreach (ItemBag bag in GameDataManager.Instance.GameSettingsData.StarItemBagList)
            {
                PlayerItemBags.Add(new ItemBag()
                {
                    itemID = bag.itemID,
                    itemAmount =  bag.itemAmount,
                });
            }
        }

        TriggerAllItemChange();
    }

    #endregion

    #region 事件注册

    private Action<List<ItemBag>> AllItemChange;

    /// <summary>
    /// 注册背包内所有物品变化回调
    /// </summary>
    public void RegisterAllItemChange(Action<List<ItemBag>> action, bool isTrigger = true)
    {
        AllItemChange += action;

        if (isTrigger)
        {
            action?.Invoke(PlayerItemBags);
        }
    }

    /// <summary>
    /// 反注册背包内所有物品变化回调
    /// </summary>
    public void UnregisterAllItemChange(Action<List<ItemBag>> action)
    {
        AllItemChange -= action;
    }

    private Dictionary<long, Action<ItemBag>> ItemChangeCallBack = new Dictionary<long, Action<ItemBag>>();

    /// <summary>
    /// 注册指定物品ID的变化回调
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="callback">回调函数</param>
    /// <param name="isTrigger">是否注册时就触发一次</param>
    public void RegisterItemBagChangAction(long itemID, Action<ItemBag> callback, bool isTrigger = true)
    {
        if (callback == null)
            return;

        if (!ItemChangeCallBack.ContainsKey(itemID))
        {
            ItemChangeCallBack.Add(itemID, callback);
        }
        else
        {
            ItemChangeCallBack[itemID] += callback;
        }

        if (isTrigger)
        {
            callback.Invoke(GetFirstItemBag(itemID));
        }
    }

    /// <summary>
    /// 反注册指定物品ID的变化回调
    /// </summary>
    public void UnregisterItemBagChangAction(long itemID, Action<ItemBag> callback)
    {
        if (!ItemChangeCallBack.ContainsKey(itemID))
            return;

        ItemChangeCallBack[itemID] -= callback;

        if (ItemChangeCallBack[itemID] == null)
        {
            ItemChangeCallBack.Remove(itemID);
        }
    }

    private Dictionary<Guid, Action<ItemBag>> ItemIdChangeCallBack = new Dictionary<Guid, Action<ItemBag>>();

    /// <summary>
    /// 注册指定背包格子的变化回调
    /// </summary>
    public void RegisterItemIdChangeCallBack(Guid guid, Action<ItemBag> callback, bool isTrigger = true)
    {
        if (callback == null)
            return;

        if (!ItemIdChangeCallBack.ContainsKey(guid))
        {
            ItemIdChangeCallBack.Add(guid, callback);
        }
        else
        {
            ItemIdChangeCallBack[guid] += callback;
        }

        if (isTrigger)
        {
            callback.Invoke(GetItemBag(guid));
        }
    }

    /// <summary>
    /// 反注册指定背包格子的变化回调
    /// </summary>
    public void UnregisterItemIdChangeCallBack(Guid guid, Action<ItemBag> callback)
    {
        if (!ItemIdChangeCallBack.ContainsKey(guid))
            return;

        ItemIdChangeCallBack[guid] -= callback;

        if (ItemIdChangeCallBack[guid] == null)
        {
            ItemIdChangeCallBack.Remove(guid);
        }
    }

    /// <summary>
    /// 触发整个背包变化回调
    /// </summary>
    private void TriggerAllItemChange()
    {
        AllItemChange?.Invoke(PlayerItemBags);
    }

    /// <summary>
    /// 触发单个背包格子的变化回调
    /// </summary>
    private void TriggerItemBagChange(ItemBag bag)
    {
        if (bag == null)
            return;

        if (ItemChangeCallBack.TryGetValue(bag.itemID, out Action<ItemBag> itemChangeAction))
        {
            itemChangeAction?.Invoke(bag);
        }

        if (ItemIdChangeCallBack.TryGetValue(bag.GetGuid, out Action<ItemBag> itemIdChangeAction))
        {
            itemIdChangeAction?.Invoke(bag);
        }
    }

    #endregion

    #region 获取Item

    public ItemData GetItemData(long itemID)
    {
        if (itemConfigs == null)
        {
            Debug.LogError("ItemConfig 未初始化");
            return null;
        }

        return itemConfigs.GetItemData(itemID);
    }

    /// <summary>
    /// 获取指定物品ID的第一个背包格子
    /// </summary>
    public ItemBag GetFirstItemBag(long itemID)
    {
        for (int i = 0; i < PlayerItemBags.Count; i++)
        {
            ItemBag bag = PlayerItemBags[i];

            if (bag != null && bag.itemID == itemID)
            {
                return bag;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据唯一ID获取背包格子
    /// </summary>
    public ItemBag GetItemBag(Guid itemGuid)
    {
        for (int i = 0; i < PlayerItemBags.Count; i++)
        {
            ItemBag bag = PlayerItemBags[i];

            if (bag != null && bag.GetGuid == itemGuid)
            {
                return bag;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取指定物品ID的总数量
    /// </summary>
    public int GetItemCount(long itemID)
    {
        int count = 0;

        for (int i = 0; i < PlayerItemBags.Count; i++)
        {
            ItemBag bag = PlayerItemBags[i];

            if (bag != null && bag.itemID == itemID)
            {
                count += bag.itemAmount;
            }
        }

        return count;
    }

    /// <summary>
    /// 判断指定物品数量是否足够
    /// </summary>
    public bool HasItem(long itemID, int itemAmount)
    {
        if (itemAmount <= 0)
            return false;

        return GetItemCount(itemID) >= itemAmount;
    }

    #endregion

    #region 增加物品背包

    /// <summary>
    /// 增加物品
    /// 如果超过最大堆叠数量，会自动创建新的背包格子
    /// </summary>
    public void AddItem(long itemID, int itemAmount)
    {
        if (itemAmount <= 0)
        {
            Debug.LogWarning($"添加物品数量不合法: {itemAmount}");
            return;
        }

        ItemData itemData = GetItemData(itemID);
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

        // 1. 先填充已有的同类未满格子
        for (int i = 0; i < PlayerItemBags.Count; i++)
        {
            ItemBag bag = PlayerItemBags[i];

            if (bag == null)
                continue;

            if (bag.itemID != itemID)
                continue;

            if (bag.itemAmount >= maxCount)
                continue;

            int canAddAmount = maxCount - bag.itemAmount;
            int addAmount = Mathf.Min(canAddAmount, remainingAmount);

            bag.itemAmount += addAmount;
            remainingAmount -= addAmount;

            TriggerItemBagChange(bag);

            if (remainingAmount <= 0)
                break;
        }

        // 2. 剩余数量创建新的格子
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

            TriggerItemBagChange(itemBag);
        }

        TriggerAllItemChange();
    }

    #endregion

    #region 修改物品背包

    /// <summary>
    /// 消耗指定 ItemID 的物品
    /// 可以跨多个背包格子扣除
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="itemAmount">消耗数量</param>
    /// <returns>是否消耗成功</returns>
    public bool ConsumeItem(long itemID, int itemAmount)
    {
        if (itemAmount <= 0)
        {
            Debug.LogWarning($"消耗物品数量不合法: {itemAmount}");
            return false;
        }

        int totalAmount = GetItemCount(itemID);

        if (totalAmount < itemAmount)
        {
            Debug.LogWarning($"物品数量不足，ItemID: {itemID}, 需要: {itemAmount}, 当前: {totalAmount}");
            return false;
        }

        int remainingAmount = itemAmount;

        // 从后往前扣，方便删除空格子
        for (int i = PlayerItemBags.Count - 1; i >= 0; i--)
        {
            ItemBag bag = PlayerItemBags[i];

            if (bag == null)
            {
                PlayerItemBags.RemoveAt(i);
                continue;
            }

            if (bag.itemID != itemID)
                continue;

            int consumeAmount = Mathf.Min(bag.itemAmount, remainingAmount);

            bag.itemAmount -= consumeAmount;
            remainingAmount -= consumeAmount;

            TriggerItemBagChange(bag);

            if (bag.itemAmount <= 0)
            {
                PlayerItemBags.RemoveAt(i);
            }

            if (remainingAmount <= 0)
                break;
        }

        TriggerAllItemChange();

        return true;
    }

    /// <summary>
    /// 消耗指定背包格子的物品
    /// 只会扣除这个格子里的数量，不会跨格子扣除
    /// </summary>
    /// <param name="itemGuid">背包格子唯一ID</param>
    /// <param name="itemAmount">消耗数量</param>
    /// <returns>是否消耗成功</returns>
    public bool ConsumeItem(Guid itemGuid, int itemAmount)
    {
        if (itemAmount <= 0)
        {
            Debug.LogWarning($"消耗物品数量不合法: {itemAmount}");
            return false;
        }

        int index = PlayerItemBags.FindIndex(t => t != null && t.GetGuid == itemGuid);

        if (index < 0)
        {
            Debug.LogWarning($"未找到指定背包物品，Guid: {itemGuid}");
            return false;
        }

        ItemBag bag = PlayerItemBags[index];

        if (bag.itemAmount < itemAmount)
        {
            Debug.LogWarning($"指定格子物品数量不足，ItemID: {bag.itemID}, Guid: {itemGuid}, 需要: {itemAmount}, 当前: {bag.itemAmount}");
            return false;
        }

        bag.itemAmount -= itemAmount;

        TriggerItemBagChange(bag);

        if (bag.itemAmount <= 0)
        {
            PlayerItemBags.RemoveAt(index);
        }

        TriggerAllItemChange();

        return true;
    }

    #endregion

    #region GM

    [Button, BoxGroup("测试")]
    public void AddTestFoodMtItems()
    {
        // 蔬菜食材 600000~600019
        for (long id = 600000; id <= 600019; id++)
        {
            AddItem(id, 9);
        }

        // 鱼类食材 610000~610019
        for (long id = 610000; id <= 610019; id++)
        {
            AddItem(id, 9);
        }

        // 调料食材 620000~620009
        for (long id = 620000; id <= 620009; id++)
        {
            AddItem(id, 9);
        }
    }

    [Button, BoxGroup("测试")]
    public void ConsumeTestItem()
    {
        ConsumeItem(600000, 1);
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
