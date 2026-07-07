using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class InventoryManager : MonoSingleton<InventoryManager>, IGameInitialized, ISaveable
{
    [SerializeReference] List<ItemInfo> itemList;
    [SerializeField] List<long> unlockedFoodRecipeIds;
    [SerializeField] ItemConfig itemConfigs;
    private List<ItemUnlockSaveData> itemUnlockSaveData;

    public async UniTask Initialized()
    {
        itemConfigs = await AssetsManager.Instance.LoadAssetsUniTask<ItemConfig>(AssetKeys.ItemConfigPath);
    }
    public async UniTask Release()
    {
        AssetsManager.Instance.FreeAsset(AssetKeys.ItemConfigPath);
        await UniTask.CompletedTask;
    }

    #region ISaveable

    public string GUID => "InventoryManager";

    public void Start()
    {
        ((ISaveable)this).RegisterSaveable();
    }
    public void SaveData(GameSaveData data)
    {
        data.itemList = new List<ItemInfo>(itemList);

        // 食物配方解锁保存
        data.unlockedFoodRecipeIds = unlockedFoodRecipeIds;

        data.ItemUnlockSaveDataList = itemUnlockSaveData;
    }

    public void LoadData(GameSaveData data)
    {
        if (data is { itemList: { Count: > 0 } })
        {
            itemList = data.itemList;
        }
        else
        {
            itemList = new List<ItemInfo>();
            foreach (ItemInfo item in GameDataManager.Instance.GameSettingsData.StarItemBagList)
            {
                itemList.Add(ItemInfo.Create(item.Id, item.Count));
            }
        }

        if (data.ItemUnlockSaveDataList != null)
        {
            itemUnlockSaveData = data.ItemUnlockSaveDataList;
        }
        else
        {
            itemUnlockSaveData = new List<ItemUnlockSaveData>();
        }

        TriggerAllItemChange();

        // 食物解锁加载
        unlockedFoodRecipeIds = data.unlockedFoodRecipeIds;
    }

    #endregion

    #region 事件注册
    private Action<List<ItemInfo>> AllItemChange;
    public void RegisterAllItemChange(Action<List<ItemInfo>> action, bool isTrigger = true)
    {
        AllItemChange += action;

        if (isTrigger)
        {
            action?.Invoke(itemList);
        }
    }
    public void UnregisterAllItemChange(Action<List<ItemInfo>> action)
    {
        AllItemChange -= action;
    }

    private readonly Dictionary<long, Action<ItemInfo>> itemChangeCallBack = new();

    /// <summary>
    /// 注册指定物品ID的变化回调
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="callback">回调函数</param>
    /// <param name="isTrigger">是否注册时就触发一次</param>
    public void RegisterItemChangAction(long itemID, Action<ItemInfo> callback, bool isTrigger = true)
    {
        if (callback == null)
            return;

        if (!itemChangeCallBack.ContainsKey(itemID))
        {
            itemChangeCallBack.Add(itemID, callback);
        }
        else
        {
            itemChangeCallBack[itemID] += callback;
        }

        if (isTrigger)
        {
            callback.Invoke(GetItem(itemID));
        }
    }

    /// <summary>
    /// 反注册指定物品ID的变化回调
    /// </summary>
    public void UnregisterItemChangAction(long itemID, Action<ItemInfo> callback)
    {
        if (!itemChangeCallBack.ContainsKey(itemID))
            return;

        itemChangeCallBack[itemID] -= callback;

        if (itemChangeCallBack[itemID] == null)
        {
            itemChangeCallBack.Remove(itemID);
        }
    }

    private Dictionary<Guid, Action<ItemInfo>> ItemIdChangeCallBack = new Dictionary<Guid, Action<ItemInfo>>();

    /// <summary>
    /// 注册指定背包格子的变化回调
    /// </summary>
    public void RegisterItemIdChangeCallBack(Guid guid, Action<ItemInfo> callback, bool isTrigger = true)
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
            callback.Invoke(GetItem(guid));
        }
    }

    /// <summary>
    /// 反注册指定背包格子的变化回调
    /// </summary>
    public void UnregisterItemIdChangeCallBack(Guid guid, Action<ItemInfo> callback)
    {
        if (!ItemIdChangeCallBack.ContainsKey(guid))
            return;

        ItemIdChangeCallBack[guid] -= callback;

        if (ItemIdChangeCallBack[guid] == null)
        {
            ItemIdChangeCallBack.Remove(guid);
        }
    }


    private Dictionary<ItemType,Action<List<ItemInfo>>> itemTypeChangeCallBack = new();

    public void RegisterItemTypeChangeCallBack(ItemType itemType, Action<List<ItemInfo>> callback, bool isTrigger = true)
    {
        if (!itemTypeChangeCallBack.ContainsKey(itemType))
        {
            itemTypeChangeCallBack.Add(itemType,callback);
        }
        else
        {
            itemTypeChangeCallBack[itemType] += callback;
        }
        if(isTrigger)
            callback?.Invoke(GetItemInfoList(itemType));
    }

    public void UnregisterItemTypeChangeCallBack(ItemType itemType, Action<List<ItemInfo>> callback)
    {
        if (itemTypeChangeCallBack.ContainsKey(itemType))
        {
            itemTypeChangeCallBack[itemType] -= callback;
        }
    }




    /// <summary>
    /// 触发整个背包变化回调
    /// </summary>
    private void TriggerAllItemChange()
    {
        AllItemChange?.Invoke(itemList);
    }

    /// <summary>
    /// 触发单个背包格子的变化回调
    /// </summary>
    private void TriggerItemChange(ItemInfo item)
    {
        if (itemChangeCallBack.TryGetValue(item.Id, out Action<ItemInfo> itemChangeAction))
        {
            itemChangeAction?.Invoke(item);
        }

        if (ItemIdChangeCallBack.TryGetValue(item.Guid, out Action<ItemInfo> itemIdChangeAction))
        {
            itemIdChangeAction?.Invoke(item);
        }
    }

    #endregion

    #region 查询相关

    public bool HasItemUnlock(long itemID)
    {
        foreach (var data in itemUnlockSaveData)
        {
            if (data.ItemId == itemID)
            {
                return data.IsUnlocked;
            }

            return false;
        }
        return false;
    }

    public void AddItemUnlock(long itemID)
    {
        itemUnlockSaveData.Add(new ItemUnlockSaveData()
        {
            IsUnlocked = true,
            ItemId = itemID,
            UnlockTimeTicks =  DateTime.Now
        });
    }

    #endregion

    #region 获取Item

    public ItemConfig Config => itemConfigs;

    public ItemData GetItemData(long itemID)
    {
        if (itemConfigs == null)
        {
            Debug.LogError("ItemConfig 未初始化");
            return null;
        }

        return itemConfigs.GetItemData(itemID);
    }

    public List<ItemInfo> GetItemInfoList(ItemType itemType)
    {
        List<ItemInfo> result = new List<ItemInfo>();
        foreach (var itemInfo in itemList)
        {
            if (itemInfo.Type == itemType)
            {
                result.Add(itemInfo);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取指定类型的物品列表，方便按类型获取
    /// </summary>
    public List<ItemInfo> GetItemList(ItemType type)
    {
        List<ItemInfo> result = new();
        for (int i = 0; i < itemList.Count; i++)
        {
            if (itemList[i] != null && itemList[i].Type == type)
                result.Add(itemList[i]);
        }
        return result;
    }

    /// <summary>
    /// 获取指定物品ID的第一个背包格子
    /// </summary>
    public ItemInfo GetItem(long itemID)
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            ItemInfo item = itemList[i];

            if (item != null && item.Id == itemID)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 根据唯一ID获取背包格子
    /// </summary>
    public ItemInfo GetItem(Guid itemGuid)
    {
        for (int i = 0; i < itemList.Count; i++)
        {
            ItemInfo item = itemList[i];

            if (item != null && item.Guid == itemGuid)
            {
                return item;
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

        for (int i = 0; i < itemList.Count; i++)
        {
            ItemInfo item = itemList[i];

            if (item != null && item.Id == itemID)
            {
                count += item.Count;
            }
        }

        return count;
    }

    #endregion

    #region 增加物品背包

    /// <summary>
    /// 增加物品
    /// 如果超过最大堆叠数量，会自动创建新的背包格子
    /// </summary>
    public void AddItem(long itemId, int itemAmount)
    {
        if (itemAmount <= 0)
        {
            Debug.LogWarning($"添加物品数量不合法: {itemAmount}");
            return;
        }

        ItemData itemData = GetItemData(itemId);
        if (itemData == null)
        {
            Debug.LogError($"未找到物品配置，ItemID: {itemId}");
            return;
        }

        int maxCount = itemData.MaxCount > 0 ? itemData.MaxCount : int.MaxValue;

        // 1. 先填充已有的同类未满格子
        int remainingAmount = MergeIntoExistingStacks(itemId, maxCount, itemAmount);

        // 2. 剩余数量创建新的格子
        while (remainingAmount > 0)
        {
            int addAmount = Mathf.Min(maxCount, remainingAmount);

            ItemInfo item = ItemInfo.Create(itemData, addAmount);

            itemList.Add(item);

            remainingAmount -= addAmount;

            TriggerItemChange(item);
        }

        TriggerAllItemChange();
    }
    public void AddItem(ItemStack itemStack)
    {
        AddItem(itemStack.id, itemStack.count);
    }
    /// <summary>
    /// 增加物品（按物品实例，仅取其 Id / Count，普通配置物品）
    /// </summary>
    public void AddItem(ItemInfo info)
    {
        if (info == null)
        {
            Debug.LogError("InventoryManager AddItem: info is null");
            return;
        }

        ItemData data = GetItemData(info.Id);
        if (data == null)
        {
            Debug.LogWarning($"InventoryManager missing item data: {info.Id}");
            return;
        }

        AddItem(info.Id, info.Count);
    }

    /// <summary>
    /// 添加“运行时物品”实例：其 ItemData 不在 ItemConfig 字典中（如工厂合成的生产资料），
    /// 信息（含作为堆叠键的复合 Id）自描述地随实例携带。先并入已有同 Id 堆叠，仍有剩余则把该实例本身作为新堆叠入包。
    /// </summary>
    public void AddRuntimeItem(ItemInfo item)
    {
        if (item == null || item.Count <= 0)
        {
            Debug.LogError("InventoryManager AddRuntimeItem: item null or count <= 0");
            return;
        }

        int remaining = MergeIntoExistingStacks(item.Id, item.MaxCount, item.Count);
        if (remaining > 0)
        {
            if (remaining != item.Count)
                item.SubCount(item.Count - remaining);   // 部分已并入已有堆叠，余量留在本实例
            itemList.Add(item);
            TriggerItemChange(item);
        }

        TriggerAllItemChange();
    }

    // 通用堆叠：把 count 个 Id 物品并入已有的同 Id、未满堆叠，返回未能并入的剩余数量（由调用方据此创建新堆叠）。
    // 仅按实例 Id 比较，普通物品(配置 Id)与运行时物品(复合 Id)同样适用。
    private int MergeIntoExistingStacks(long id, int maxNum, int count)
    {
        int remaining = count;
        for (int i = 0; i < itemList.Count && remaining > 0; i++)
        {
            ItemInfo info = itemList[i];
            if (info == null || info.Id != id || info.Count >= maxNum)
                continue;

            int add = Mathf.Min(maxNum - info.Count, remaining);
            info.AddCount(add);
            remaining -= add;
            TriggerItemChange(info);
        }
        return remaining;
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
        for (int i = itemList.Count - 1; i >= 0; i--)
        {
            ItemInfo item = itemList[i];

            if (item == null)
            {
                itemList.RemoveAt(i);
                continue;
            }

            if (item.Id != itemID)
                continue;

            int consumeAmount = Mathf.Min(item.Count, remainingAmount);

            item.SubCount(consumeAmount);
            remainingAmount -= consumeAmount;

            TriggerItemChange(item);

            if (item.Count <= 0)
            {
                itemList.RemoveAt(i);
                ItemIdChangeCallBack.Remove(item.Guid);
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

        int index = itemList.FindIndex(t => t != null && t.Guid == itemGuid);

        if (index < 0)
        {
            Debug.LogWarning($"未找到指定背包物品，Guid: {itemGuid}");
            return false;
        }

        ItemInfo item = itemList[index];

        if (item.Count < itemAmount)
        {
            Debug.LogWarning($"指定格子物品数量不足，ItemID: {item.Id}, Guid: {itemGuid}, 需要: {itemAmount}, 当前: {item.Count}");
            return false;
        }

        item.SubCount(itemAmount);

        TriggerItemChange(item);

        if (item.Count <= 0)
        {
            itemList.RemoveAt(index);
            ItemIdChangeCallBack.Remove(item.Guid);
        }

        TriggerAllItemChange();

        return true;
    }

    /// <summary>
    /// 消耗指定物品实例（PlayerBag 兼容）：只扣这个实例，扣到 0 自动移除。
    /// </summary>
    public void ConsumeItem(ItemInfo info, int count)
    {
        if (info == null)
        {
            Debug.LogError("InventoryManager ConsumeItem: info is null");
            return;
        }

        info.SubCount(count);

        bool removed = info.Count <= 0;
        if (removed)
            itemList.Remove(info);

        // 先通知监听者（此时 Count 可能已为 0，UI 可据此清空显示），再清理已移除物品的监听
        TriggerItemChange(info);
        if (removed)
            ItemIdChangeCallBack.Remove(info.Guid);

        TriggerAllItemChange();
    }

    /// <summary>能否消耗该物品实例的指定数量（PlayerBag 兼容）。</summary>
    public bool CanConsumeFoodMtItem(ItemInfo info, int count)
    {
        return info != null && info.Count >= count;
    }

    #endregion

    #region 金钱 / 游戏币

    /// <summary>普通金币（PropertyType.Gold）。</summary>
    public int Money => GameDataManager.Instance.GetProperty(PropertyType.Gold).Value;

    public void AddMoney(int value) => GameDataManager.Instance.AddProperty(PropertyType.Gold, value);

    public void SubMoney(int value) => GameDataManager.Instance.RemoveProperty(PropertyType.Gold, value);

    public bool HasMoney(int value) => Money >= value;

    // 赌场小游戏使用的游戏币（PropertyType.GameGold）。与 Money 一样，值与变更事件均归属 GameDataManager：
    // 需要监听变化的界面订阅 GameDataManager.Instance.RegisterPlayerDataChange 即可，这里不再另设重复事件。
    public int GameCoin => GameDataManager.Instance.GetProperty(PropertyType.GameGold).Value;

    public void AddGameCoin(int value) => GameDataManager.Instance.AddProperty(PropertyType.GameGold, value);

    public void SubGameCoin(int value) => GameDataManager.Instance.RemoveProperty(PropertyType.GameGold, value);

    public bool HasGameCoin(int value) => GameCoin >= value;

    #endregion

    #region 配方

    public IReadOnlyList<long> UnlockedRecipeIds => unlockedFoodRecipeIds;

    public bool IsRecipeUnlocked(long recipeItemId) => unlockedFoodRecipeIds.Contains(recipeItemId);

    public void UnlockRecipe(long recipeItemId)
    {
        if (!unlockedFoodRecipeIds.Contains(recipeItemId))
            unlockedFoodRecipeIds.Add(recipeItemId);
    }

    #endregion
}

[Serializable]
public class ItemUnlockSaveData
{
    [LabelText("物品ID")]
    public long ItemId;
    [LabelText("解锁状态")]
    public bool IsUnlocked;
    [LabelText("解锁时间")]
    public DateTime UnlockTimeTicks;
}
