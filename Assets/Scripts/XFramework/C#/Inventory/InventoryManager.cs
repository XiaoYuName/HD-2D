using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class InventoryManager : MonoSingleton<InventoryManager>, IGameInitialized, ISaveable
    {
        
        #region 玩家背包数据
        
        [TitleGroup("玩家背包")]
        [LabelText("玩家背包数据"),SerializeReference,ShowInInspector]
        private List<ItemStack> PlayerStack = new List<ItemStack>();
        
        [ReadOnly,LabelText("玩家解锁道具列表"),ShowInInspector]
        private List<ItemUnlockSaveData> itemUnlockSaveData;
        
        [SerializeField] 
        private List<long> unlockedFoodRecipeIds;
        
        #endregion
        
        public async UniTask Initialized()
        {
           await  UniTask.CompletedTask;
        }

        public async UniTask Release()
        {
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
            data.PlayerStack = new List<ItemStack>(PlayerStack);

            // 食物配方解锁保存
            data.unlockedFoodRecipeIds = unlockedFoodRecipeIds;

            data.ItemUnlockSaveDataList = itemUnlockSaveData;
        }

        public void LoadData(GameSaveData data)
        {
            if (data is { PlayerStack: { Count: > 0 } })
            {
                PlayerStack = data.PlayerStack;
            }
            else
            {
                PlayerStack = new List<ItemStack>();
                foreach (ItemStack item in GameDataManager.Instance.GameSettingsData.StarItemBagList)
                {
                    switch (item.ItemType)
                    {
                        case ItemType.Material:
                            PlayerStack.Add(new ItemStack(item.ID, item.Count, item.ItemType));
                            break;
                        case ItemType.Consumables:
                            PlayerStack.Add(new ItemStack(item.ID, item.Count, item.ItemType));
                            break;
                    }
                    
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

            //TODO: 食材加载解锁
            //unlockedFoodRecipeIds = data.unlockedFoodRecipeIds;
        }

        #endregion

        #region 事件注册

        private Action<List<ItemStack>> AllItemChange;

        public void RegisterAllItemChange(Action<List<ItemStack>> action, bool isTrigger = true)
        {
            AllItemChange += action;

            if (isTrigger)
            {
                action?.Invoke(PlayerStack);
            }
        }

        public void UnregisterAllItemChange(Action<List<ItemStack>> action)
        {
            AllItemChange -= action;
        }

        private readonly Dictionary<long, Action<ItemStack>> itemChangeCallBack = new();

        /// <summary>
        /// 注册指定物品ID的变化回调
        /// </summary>
        /// <param name="itemID">物品ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="isTrigger">是否注册时就触发一次</param>
        public void RegisterItemChangAction(long itemID, Action<ItemStack> callback, bool isTrigger = true)
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
        public void UnregisterItemChangAction(long itemID, Action<ItemStack> callback)
        {
            if (!itemChangeCallBack.ContainsKey(itemID))
                return;

            itemChangeCallBack[itemID] -= callback;

            if (itemChangeCallBack[itemID] == null)
            {
                itemChangeCallBack.Remove(itemID);
            }
        }

        private Dictionary<Guid, Action<ItemStack>> ItemIdChangeCallBack = new Dictionary<Guid, Action<ItemStack>>();

        /// <summary>
        /// 注册指定背包格子的变化回调
        /// </summary>
        public void RegisterItemIdChangeCallBack(Guid guid, Action<ItemStack> callback, bool isTrigger = true)
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
        public void UnregisterItemIdChangeCallBack(Guid guid, Action<ItemStack> callback)
        {
            if (!ItemIdChangeCallBack.ContainsKey(guid))
                return;

            ItemIdChangeCallBack[guid] -= callback;

            if (ItemIdChangeCallBack[guid] == null)
            {
                ItemIdChangeCallBack.Remove(guid);
            }
        }


        private Dictionary<ItemType, Action<List<ItemStack>>> itemTypeChangeCallBack = new();

        public void RegisterItemTypeChangeCallBack(ItemType itemType, Action<List<ItemStack>> callback,
            bool isTrigger = true)
        {
            if (!itemTypeChangeCallBack.ContainsKey(itemType))
            {
                itemTypeChangeCallBack.Add(itemType, callback);
            }
            else
            {
                itemTypeChangeCallBack[itemType] += callback;
            }

            if (isTrigger)
                callback?.Invoke(GetItemStackList(itemType));
        }

        public void UnregisterItemTypeChangeCallBack(ItemType itemType, Action<List<ItemStack>> callback)
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
            AllItemChange?.Invoke(PlayerStack);
        }

        /// <summary>
        /// 触发单个背包格子的变化回调
        /// </summary>
        private void TriggerItemChange(ItemStack item)
        {
            if (itemChangeCallBack.TryGetValue(item.ID, out Action<ItemStack> itemChangeAction))
            {
                itemChangeAction?.Invoke(item);
            }

            if (ItemIdChangeCallBack.TryGetValue(item.Guid, out Action<ItemStack> itemIdChangeAction))
            {
                itemIdChangeAction?.Invoke(item);
            }


            if (itemTypeChangeCallBack.TryGetValue(item.ItemType, out Action<List<ItemStack>> itemTypeChangeAction))
            {
                itemTypeChangeAction?.Invoke(GetItemStackList(item.ItemType));
            }
        }

        private void TriggerItemChange(long itemID)
        {
            ItemData itemData = GetItemData(itemID);
            if (itemData != null)
            {
                if (itemChangeCallBack.TryGetValue(itemData.ID, out Action<ItemStack> itemChangeAction))
                {
                    itemChangeAction?.Invoke(GetItem(itemData.ID));
                }

                if (itemTypeChangeCallBack.TryGetValue(itemData.ItemType, out Action<List<ItemStack>> itemTypeChangeAction))
                {
                    itemTypeChangeAction?.Invoke(GetItemStackList(itemData.ItemType));
                }
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

        public void UlockItem(long itemID)
        {
            itemUnlockSaveData.Add(new ItemUnlockSaveData()
            {
                IsUnlocked = true,
                ItemId = itemID,
                UnlockTimeTicks = DateTime.Now
            });

            var itemData = GetItemData(itemID);
            if (itemData != null)
            {
                TriggerItemChange(itemData.ID);
            }

            TriggerAllItemChange();
        }


        #endregion

        #region 获取Item

        /// <summary>
        /// 获取基本的物品表定义字段
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public ItemData GetItemData(long itemID)
        {
            try
            {
               var itemData =  LubanManager.Instance.TbItemData.Get(itemID);
               return itemData;
            }
            catch (Exception e)
            {
                Debug.LogError("没有找到对应的物品~~~~~~~~~~~~~~~~~~");
            }
            return null;
        }

        /// <summary>
        /// 获取消耗品物品表定义字段
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public ConsumablesItemData GetConsumablesItemData(long itemID)
        {
            try
            {
                var itemData =  LubanManager.Instance.TbConsumablesItemData.Get(itemID);
                return itemData;
            }
            catch (Exception e)
            {
                Debug.LogError("没有找到对应的物品~~~~~~~~~~~~~~~~~~");
            }
            return null;
        }
        
        
        /// <summary>
        /// 获取指定的物品类型列表背包
        /// </summary>
        /// <param name="itemType"></param>
        /// <returns></returns>
        public List<ItemStack> GetItemStackList(ItemType itemType)
        {
            List<ItemStack> result = new List<ItemStack>();
            foreach (var itemInfo in PlayerStack)
            {
                if (itemInfo.ItemType == itemType)
                {
                    result.Add(itemInfo);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定物品ID的第一个背包格子
        /// </summary>
        public ItemStack GetItem(long itemID)
        {
            for (int i = 0; i < PlayerStack.Count; i++)
            {
                ItemStack item = PlayerStack[i];

                if (item != null && item.ID == itemID)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// 根据唯一ID获取背包格子
        /// </summary>
        public ItemStack GetItem(Guid itemGuid)
        {
            for (int i = 0; i < PlayerStack.Count; i++)
            {
                ItemStack item = PlayerStack[i];

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

            for (int i = 0; i < PlayerStack.Count; i++)
            {
                ItemStack item = PlayerStack[i];

                if (item != null && item.ID == itemID)
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

            int maxCount = itemData.MaxNum > 0 ? itemData.MaxNum : int.MaxValue;

            // 1. 先填充已有的同类未满格子
            int remainingAmount = MergeIntoExistingStacks(itemId, maxCount, itemAmount);

            // 2. 剩余数量创建新的格子
            while (remainingAmount > 0)
            {
                int addAmount = Mathf.Min(maxCount, remainingAmount);
                ItemStack item = null;
                switch (itemData.ItemType)
                {
                    case ItemType.Material or ItemType.Consumables:
                        item = new ItemStack(itemId, itemAmount, itemData.ItemType);
                        break;
                }
                remainingAmount -= addAmount;

                PlayerStack.Add(item);
                TriggerItemChange(item);
            }

            TriggerAllItemChange();
        }

        // 通用堆叠：把 count 个 Id 物品并入已有的同 Id、未满堆叠，返回未能并入的剩余数量（由调用方据此创建新堆叠）。
        // 仅按实例 Id 比较，普通物品(配置 Id)与运行时物品(复合 Id)同样适用。
        private int MergeIntoExistingStacks(long id, int maxNum, int count)
        {
            int remaining = count;
            for (int i = 0; i < PlayerStack.Count && remaining > 0; i++)
            {
                ItemStack info = PlayerStack[i];
                if (info == null || info.ID != id || info.Count >= maxNum)
                    continue;

                int add = Mathf.Min(maxNum - info.Count, remaining);
                info.Count += add;
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
            for (int i = PlayerStack.Count - 1; i >= 0; i--)
            {
                ItemStack item = PlayerStack[i];

                if (item == null)
                {
                    PlayerStack.RemoveAt(i);
                    continue;
                }

                if (item.ID != itemID)
                    continue;

                int consumeAmount = Mathf.Min(item.Count, remainingAmount);
                item.Count -= consumeAmount;
                remainingAmount -= consumeAmount;

                TriggerItemChange(item);

                if (item.Count <= 0)
                {
                    PlayerStack.RemoveAt(i);
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

            int index = PlayerStack.FindIndex(t => t != null && t.Guid == itemGuid);

            if (index < 0)
            {
                Debug.LogWarning($"未找到指定背包物品，Guid: {itemGuid}");
                return false;
            }

            ItemStack item = PlayerStack[index];

            if (item.Count < itemAmount)
            {
                Debug.LogWarning($"指定格子物品数量不足，ItemID: {item.ID}, Guid: {itemGuid}, 需要: {itemAmount}, 当前: {item.Count}");
                return false;
            }

            item.Count -= itemAmount;

            TriggerItemChange(item);

            if (item.Count <= 0)
            {
                PlayerStack.RemoveAt(index);
                ItemIdChangeCallBack.Remove(item.Guid);
            }

            TriggerAllItemChange();

            return true;
        }

        /// <summary>
        /// 消耗指定物品实例（PlayerBag 兼容）：只扣这个实例，扣到 0 自动移除。
        /// </summary>
        public void ConsumeItem(ItemStack info, int count)
        {
            if (info == null)
            {
                Debug.LogError("InventoryManager ConsumeItem: info is null");
                return;
            }

            info.Count -= count;

            bool removed = info.Count <= 0;
            if (removed)
                PlayerStack.Remove(info);

            // 先通知监听者（此时 Count 可能已为 0，UI 可据此清空显示），再清理已移除物品的监听
            TriggerItemChange(info);
            if (removed)
                ItemIdChangeCallBack.Remove(info.Guid);

            TriggerAllItemChange();
        }

        /// <summary>能否消耗该物品实例的指定数量（PlayerBag 兼容）。</summary>
        public bool CanConsumeFoodMtItem(ItemStack info, int count)
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

    /// <summary>
    /// 物品背包数据
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        [HorizontalGroup("标识ID"), LabelText("唯一ID")]
        public Guid Guid { get; private set; }

        [HorizontalGroup("标识ID"), LabelText("物品ID"),ShowInInspector]
        public long ID { get; set; }

        [LabelText("物品类型"),ShowInInspector] 
        public ItemType ItemType { get; set; }

        [LabelText("物品数量"),ShowInInspector] 
        public int Count { get; set; }
        
        [LabelText("获取时间")] 
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// 必须附带无参构造函数，保证序列化/反序列化无异常
        /// </summary>
        public ItemStack()
        {
            
        }

        public ItemStack(long id, int count, ItemType itemType)
        {
            Guid = System.Guid.NewGuid();
            ID = id;
            this.Count = count;
            this.ItemType = itemType;
            CreationTime = DateTime.Now;
        }
        
        public ItemStack(Guid guid, int count, ItemType itemType)
        {
            this.ItemType = itemType;
            Guid = guid;
            Count = count;
        }
    }

    [Serializable]
    public class FactoryComposedItemStack : ItemStack
    {
        public long FarmeItemID { get; set; }
        public long PaintingItemID { get; set; }

        /// <summary>
        /// 必须附带无参构造函数，保证序列化/反序列化无异常
        /// </summary>
        public FactoryComposedItemStack()
        {
            
        }

        public FactoryComposedItemStack(Guid guid, long frameItemId, long paintingItemId, int Count, ItemType ItemType)
            : base(guid, Count, ItemType)
        {
            ID = -1;
            this.FarmeItemID = frameItemId;
            this.PaintingItemID = paintingItemId;
        }
    }
    

}

