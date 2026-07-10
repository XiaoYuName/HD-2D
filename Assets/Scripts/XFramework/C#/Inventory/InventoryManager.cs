using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<ItemInfo> PlayerStack = new List<ItemInfo>();
        
        [ReadOnly,LabelText("玩家解锁道具列表"),ShowInInspector]
        private List<ItemUnlockSaveData> itemUnlockSaveData;
        
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
            data.PlayerStack = new List<ItemInfo>(PlayerStack);
            
            data.ItemUnlockSaveDataList = itemUnlockSaveData;
        }

        public void LoadData(GameSaveData data)
        {
            if (data?.PlayerStack == null)
            {
                PlayerStack = new List<ItemInfo>();
                foreach (ItemInfo item in GameDataManager.Instance.GameSettingsData.StarItemBagList)
                {
                    ItemData itemData = GetItemData(item.ID);
                    if (itemData == null) continue;
                    switch (itemData.ItemType)
                    {
                        case ItemType.Material:
                            PlayerStack.Add(new ItemInfo(item.ID, item.Count));
                            break;
                        case ItemType.Consumables:
                            PlayerStack.Add(new ItemInfo(item.ID, item.Count));
                            break;
                    }
                    
                }
            }
            else
            {
                PlayerStack = new List<ItemInfo>(data.PlayerStack);
            }

            if (data?.ItemUnlockSaveDataList != null)
            {
                itemUnlockSaveData = new List<ItemUnlockSaveData>(data.ItemUnlockSaveDataList);
            }
            else
            {
                itemUnlockSaveData = new List<ItemUnlockSaveData>();
            }


            if (data?.PlayerStack != null)
            {
                foreach (var itemInfo in data.PlayerStack)
                {
                    TriggerItemChange(itemInfo.ID);
                }
            }
            TriggerAllItemChange();
        }

        #endregion

        #region 事件注册

        private Action<List<ItemInfo>> AllItemChange;

        public void RegisterAllItemChange(Action<List<ItemInfo>> action, bool isTrigger = true)
        {
            AllItemChange += action;

            if (isTrigger)
            {
                action?.Invoke(PlayerStack);
            }
        }

        public void UnregisterAllItemChange(Action<List<ItemInfo>> action)
        {
            AllItemChange -= action;
        }
        
        private Dictionary<ItemMaterialType,Action<List<ItemInfo>>> itemMaterialTypeChangeCallBack = new();

        /// <summary>
        /// 注册指定材料类型变化回调
        /// </summary>
        /// <param name="itemMaterialType">材料类型</param>
        /// <param name="callback">回调函数</param>
        /// <param name="isTrigger">触发器</param>
        public void RegisterMaterialTypeChangeCallBack(ItemMaterialType itemMaterialType,
            Action<List<ItemInfo>> callback, bool isTrigger = true)
        {
            if (!itemMaterialTypeChangeCallBack.ContainsKey(itemMaterialType))
            {
                itemMaterialTypeChangeCallBack.Add(itemMaterialType, callback);
            }
            else
            {
                itemMaterialTypeChangeCallBack[itemMaterialType] += callback;
            }

            if (isTrigger)
                callback?.Invoke(GetMaterialList(itemMaterialType));
        }

        /// <summary>
        /// 反注册指定材料类型变化回调
        /// </summary>
        /// <param name="itemMaterialType"></param>
        /// <param name="callback"></param>
        public void UnregisterMaterialTypeChangeCallBack(ItemMaterialType itemMaterialType,
            Action<List<ItemInfo>> callback)
        {
            if (itemMaterialTypeChangeCallBack.ContainsKey(itemMaterialType))
            {
                itemMaterialTypeChangeCallBack[itemMaterialType] -= callback;
            }
        }
        
        
        private Dictionary<ItemConsumType, Action<List<ItemInfo>>> itemConsumTypeChangeCallBack = new();

        /// <summary>
        /// 注册消耗品类型变化回调
        /// </summary>
        /// <param name="itemConsumablesType"></param>
        /// <param name="callback"></param>
        /// <param name="isTrigger"></param>
        public void RegisterItemConsumablesTypeChangeCallBack(ItemConsumType itemConsumablesType, Action<List<ItemInfo>> callback,
            bool isTrigger = true)
        {
            if (!itemConsumTypeChangeCallBack.ContainsKey(itemConsumablesType))
            {
                itemConsumTypeChangeCallBack.Add(itemConsumablesType, callback);
            }
            else
            {
                itemConsumTypeChangeCallBack[itemConsumablesType] += callback;
            }

            if (isTrigger)
            {
                callback?.Invoke(GetConsumableList(itemConsumablesType));
            }
        }

        /// <summary>
        /// 反注册消耗品类型回调
        /// </summary>
        /// <param name="itemConsumablesType"></param>
        /// <param name="callback"></param>
        public void UnregisterItemConsumablesTypeChangeCallBack(ItemConsumType itemConsumablesType,
            Action<List<ItemInfo>> callback)
        {
            if (itemConsumTypeChangeCallBack.ContainsKey(itemConsumablesType))
            {
                itemConsumTypeChangeCallBack[itemConsumablesType] -= callback;
            }
        }


        private Dictionary<long, Action<List<ItemInfo>>> ItemIDChangeCallBack = new Dictionary<long, Action<List<ItemInfo>>>();

        public void RegisterItemIDChangeCallBack(long itemID, Action<List<ItemInfo>> callback, bool isTrigger = true)
        {
            if (!ItemIDChangeCallBack.ContainsKey(itemID))
            {
                ItemIDChangeCallBack.Add(itemID,callback);
            }
            else
            {
                ItemIDChangeCallBack[itemID] += callback;
            }

            if (isTrigger)
            {
                callback?.Invoke(GetItem(itemID));
            }
        }

        public void UnregisterItemIDChangeCallBack(long itemID, Action<List<ItemInfo>> callback)
        {
            if (ItemIDChangeCallBack.ContainsKey(itemID))
            {
                ItemIDChangeCallBack[itemID] -= callback;
            }
        }
        
        
        private Dictionary<Guid,Action<ItemInfo>> ItemGuidChangeCallBack = new Dictionary<Guid,Action<ItemInfo>>();

        public void RegisterItemGuidChangeCallBack(Guid itemGuid, Action<ItemInfo> callback, bool isTrigger = true)
        {
            if (!ItemGuidChangeCallBack.ContainsKey(itemGuid))
            {
                ItemGuidChangeCallBack.Add(itemGuid, callback);
            }
            else
            {
                ItemGuidChangeCallBack[itemGuid] += callback;
            }

            if (isTrigger)
            {
                callback?.Invoke(GetItem(itemGuid));
            }
        }

        public void UnregisterItemGuidChangeCallBack(Guid itemGuid, Action<ItemInfo> callback)
        {
            if (ItemGuidChangeCallBack.ContainsKey(itemGuid))
            {
                ItemGuidChangeCallBack[itemGuid] -= callback;
            }
        }


        /// <summary>
        /// 触发整个背包变化回调
        /// </summary>
        private void TriggerAllItemChange()
        {
            AllItemChange?.Invoke(PlayerStack);
        }

        private void TriggerItemChange(long itemID)
        {
            ItemData itemData = GetItemData(itemID);
            if (itemData != null)
            {
                if (ItemIDChangeCallBack.ContainsKey(itemID))
                {
                    ItemIDChangeCallBack[itemID]?.Invoke(GetItem(itemID));
                }
                switch (itemData.ItemType)
                {
                    case ItemType.Material:
                        MaterialItemData materialItemData = GetMaterialItemData(itemData.ID);
                        if (materialItemData != null)
                        {
                            if (itemMaterialTypeChangeCallBack.ContainsKey(materialItemData.MaterialType))
                            {
                                itemMaterialTypeChangeCallBack[materialItemData.MaterialType]?.Invoke(GetMaterialList(materialItemData.MaterialType));
                            }
                        }
                        break;
                    case ItemType.Consumables:
                        ConsumablesItemData consumablesItemData = GetConsumablesItemData(itemData.ID);
                        if (consumablesItemData != null)
                        {
                            if (itemConsumTypeChangeCallBack.ContainsKey(consumablesItemData.ConsumType))
                            {
                                itemConsumTypeChangeCallBack[consumablesItemData.ConsumType]?.Invoke(GetConsumableList(consumablesItemData.ConsumType));
                            }
                        }

                        break;
                }

                var list = GetItem(itemID);
                if (list != null)
                {
                    foreach (var itemInfo in list )
                    {
                        if (ItemGuidChangeCallBack.ContainsKey(itemInfo.Guid))
                        {
                            ItemGuidChangeCallBack[itemInfo.Guid]?.Invoke(itemInfo);
                        }
                    }
                }
            }
           
        }

        #endregion

        #region 解锁相关

        /// <summary>
        /// 判断物品是否已解锁
        /// </summary>
        /// <param name="itemID">物品ID</param>
        /// <returns>已解锁返回True,否则返回False</returns>
        public bool HasItemUnlock(long itemID)
        {
            foreach (var data in itemUnlockSaveData)
            {
                if (data.ItemId == itemID)
                {
                    return data.IsUnlocked;
                }
            }

            return false;
        }

        /// <summary>
        /// 解锁物品
        /// </summary>
        /// <param name="itemID">物品ID</param>
        public void UlockItem(long itemID)
        {
            if (itemUnlockSaveData.All(temp => temp.ItemId != itemID))
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
        }

        /// <summary>
        /// 设置物品的解锁状态
        /// </summary>
        /// <param name="itemID">物品ID</param>
        /// <param name="unlock">是否解锁</param>
        public void SetUlockItem(long itemID, bool unlock)
        {
            if (itemUnlockSaveData.All(temp => temp.ItemId != itemID))
            {
                itemUnlockSaveData.Add(new ItemUnlockSaveData()
                {
                    ItemId = itemID,
                    IsUnlocked = unlock,
                    UnlockTimeTicks = DateTime.Now
                });
            }
            else
            {
                int index = itemUnlockSaveData.FindIndex(temp => temp.ItemId == itemID);
                itemUnlockSaveData[index].IsUnlocked = unlock;
            }
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
                Debug.LogError("没有找到对应的物品~~~" + e.Message);
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
        /// 获取材料物品定义数据
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public MaterialItemData GetMaterialItemData(long itemID)
        {
            try
            {
                var itemData =  LubanManager.Instance.TbMaterialItemData.Get(itemID);
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
        public List<ItemInfo> GetItemList(ItemType itemType)
        {
            List<ItemInfo> result = new();
            foreach (var itemInfo in PlayerStack)
            {
                ItemData itemData = GetItemData(itemInfo.ID);
                if(itemData == null)continue;
                
                if (itemData.ItemType == itemType)
                {
                    result.Add(itemInfo);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定材料类型的背包物品
        /// </summary>
        /// <param name="itemMaterialType"></param>
        /// <returns></returns>
        public List<ItemInfo> GetMaterialList(ItemMaterialType itemMaterialType)
        {
            List<ItemInfo> result = new();
            foreach (var itemInfo in PlayerStack)
            {
                ItemData itemData = GetItemData(itemInfo.ID);
                if(itemData.ItemType != ItemType.Material)continue;
                MaterialItemData materialItemData = GetMaterialItemData(itemData.ID);
                if (materialItemData == null)continue;
                if(materialItemData.MaterialType != itemMaterialType)continue;
                result.Add(itemInfo);
            }
            return result;
        }

        /// <summary>
        /// 获取指定消耗品类型的背包物品
        /// </summary>
        /// <param name="itemConsumableType"></param>
        /// <returns></returns>
        public List<ItemInfo> GetConsumableList(ItemConsumType itemConsumableType)
        {
            List<ItemInfo> result = new();
            foreach (var itemInfo in PlayerStack)
            {
                ItemData itemData = GetItemData(itemInfo.ID);
                if(itemData.ItemType != ItemType.Material)continue;
                ConsumablesItemData consumablesItemData = GetConsumablesItemData(itemData.ID);
                if (consumablesItemData == null)continue;
                if(consumablesItemData.ConsumType != itemConsumableType)continue;
                result.Add(itemInfo);
            }
            return result;
        }


        /// <summary>
        /// 获取指定物品ID的第一个背包格子
        /// </summary>
        public List<ItemInfo> GetItem(long itemID)
        {
            List<ItemInfo> result = new();
            for (int i = 0; i < PlayerStack.Count; i++)
            {
                ItemInfo item = PlayerStack[i];

                if (item != null && item.ID == itemID)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// 根据唯一ID获取背包格子
        /// </summary>
        public ItemInfo GetItem(Guid itemGuid)
        {
            for (int i = 0; i < PlayerStack.Count; i++)
            {
                ItemInfo item = PlayerStack[i];

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
                ItemInfo item = PlayerStack[i];

                if (item != null && item.ID == itemID)
                {
                    count += item.Count;
                }
            }

            return count;
        }

        #endregion

        #region 工厂创建方法

        public ItemInfo NewItem(long itemID, int Count)
        {
            ItemData itemData = GetItemData(itemID);
            if (itemData == null)
            {
                Debug.LogError("表中没有对应的物品定义~~~~~~");
                return null;
            }

            ItemInfo itemInfo = new ItemInfo(itemID,Count);
            return itemInfo;
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
                ItemInfo item = null;
                switch (itemData.ItemType)
                {
                    case ItemType.Material or ItemType.Consumables:
                        item = new ItemInfo(itemId, addAmount);
                        break;
                    default:
                        item = new ItemInfo(itemId, addAmount);
                        break;
                }
                remainingAmount -= addAmount;

                PlayerStack.Add(item);
                TriggerItemChange(item.ID);
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
                ItemInfo info = PlayerStack[i];
                if (info == null || info.ID != id || info.Count >= maxNum)
                    continue;

                int add = Mathf.Min(maxNum - info.Count, remaining);
                info.Count += add;
                remaining -= add;
            }

            return remaining;
        }
        /// <summary>
        /// 加入一个已构建好的运行时自描述物品实例(如 FactoryMoldItemInfo / FactoryMerchandiseItemInfo)。
        /// 按实例 ID 并入已有同 ID 堆叠，剩余量以传入实例作为新堆叠加入(保留其多态子类型/字段)。
        /// </summary>
        public void AddRuntimeItem(ItemInfo item)
        {
            if (item == null)
            {
                Debug.LogError("InventoryManager AddRuntimeItem: item is null");
                return;
            }

            const int runtimeMaxNum = 999; // 运行时自描述物品堆叠上限(沿用旧 FactoryComposedItemInfo.MaxCount)
            int remaining = MergeIntoExistingStacks(item.ID, runtimeMaxNum, item.Count);
            if (remaining > 0)
            {
                item.Count = remaining;
                PlayerStack.Add(item);
                TriggerItemChange(item.ID);
            }
            TriggerAllItemChange();
        }

        #endregion

        #region 配方解锁（TODO：待接入 Luban RecipeItemData 后改为读写存档）

        // TODO(配方系统未接入)：目前配方解锁仅内存态，不持久化、不读取 RecipeItemData 表。
        // 接入烹饪配方后需改为基于存档 + Luban 表的实现。
        private readonly HashSet<long> unlockedRecipeIds = new HashSet<long>();

        /// <summary>配方是否已解锁。</summary>
        public bool IsRecipeUnlocked(long recipeItemId) => unlockedRecipeIds.Contains(recipeItemId);

        /// <summary>解锁配方。</summary>
        public void UnlockRecipe(long recipeItemId) => unlockedRecipeIds.Add(recipeItemId);

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
                ItemInfo item = PlayerStack[i];

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

                
                TriggerItemChange(item.ID);
                if (item.Count <= 0)
                {
                    PlayerStack.RemoveAt(i);
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

            ItemInfo item = PlayerStack[index];

            if (item.Count < itemAmount)
            {
                Debug.LogWarning($"指定格子物品数量不足，ItemID: {item.ID}, Guid: {itemGuid}, 需要: {itemAmount}, 当前: {item.Count}");
                return false;
            }

            item.Count -= itemAmount;
            
            if (item.Count <= 0)
            {
                PlayerStack.RemoveAt(index);
            }
            TriggerItemChange(item.ID);

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

            info.Count -= count;

            bool removed = info.Count <= 0;
            if (removed)
                PlayerStack.Remove(info);

            // 先通知监听者（此时 Count 可能已为 0，UI 可据此清空显示），再清理已移除物品的监听
            TriggerItemChange(info.ID);
            
            TriggerAllItemChange();
        }

        /// <summary>能否消耗该物品实例的指定数量（PlayerBag 兼容）。</summary>
        public bool CanConsumeFoodMtItem(ItemInfo info, int count)
        {
            return info != null && info.Count >= count;
        }

        #endregion

        #region 使用Item

        /// <summary>
        /// 使用道具
        /// </summary>
        /// <param name="itemID"></param>
        public void UseItem(long itemID)
        {
            var itemData = GetItemData(itemID);
            if (itemData == null) return;
            if (itemData.ItemType == ItemType.Consumables && ConsumeItem(itemID, 1))
            {
                ConsumablesItemData consumablesItemData = GetConsumablesItemData(itemData.ID);
                if (consumablesItemData != null)
                {
                    //奖励物品
                    if (consumablesItemData.RewardItem != null)
                    {
                        foreach (TbRewardItemData rewardItemData in consumablesItemData.RewardItem)
                        {
                            AddItem(rewardItemData.ItemID,rewardItemData.Count);
                        }
                    }

                    //奖励玩家属性
                    if (consumablesItemData.RewardProp != null)
                    {
                        foreach (TbRewardPropData propData  in consumablesItemData.RewardProp)
                        {
                            GameDataManager.Instance.AddProperty(propData.PropType,propData.Value);
                        }
                    }
                }
            }

            
        }

        #endregion
    }

    /// <summary>
    /// 存档解锁数据
    /// </summary>
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
    public class ItemInfo
    {
        [HorizontalGroup("标识ID"), LabelText("唯一ID")]
        public Guid Guid { get; private set; }

        [HorizontalGroup("标识ID"), LabelText("物品ID"),ShowInInspector]
        public long ID { get; set; }

        [LabelText("物品数量"),ShowInInspector] 
        public int Count { get; set; }
        
        [LabelText("获取时间")] 
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// 必须附带无参构造函数，保证序列化/反序列化无异常
        /// </summary>
        public ItemInfo()
        {
            
        }

        public ItemInfo(long id, int count)
        {
            Guid = System.Guid.NewGuid();
            ID = id;
            this.Count = count;
            CreationTime = DateTime.Now;
        }
        
        public ItemInfo(Guid guid, int count)
        {
            Guid = guid;
            Count = count;
        }
        
    }
    


}

