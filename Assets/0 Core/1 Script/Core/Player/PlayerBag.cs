using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System;
using XFramework;

public class PlayerBag : MonoBehaviour, ISaveable
{
    [SerializeField] List<ItemInfo> itemList;
    [LabelText("已解锁配方ID")][SerializeField] List<long> unlockedRecipeIds;
    // [LabelText("金币")][SerializeField] int money;
    [LabelText("游戏币")] int gameCoin
    {
        get
        {
            return GameDataManager.Instance.GetProperty(PropertyType.GameGold).Value;
        }
        set
        {
            GameDataManager.Instance.SetProperty(PropertyType.GameGold, value);
            OnGameCoinChanged?.Invoke(value);
        }
    }
    public List<ItemInfo> ItemList => itemList;
    public List<long> UnlockedRecipeIds => unlockedRecipeIds;
    public int Money => GameDataManager.Instance.GetProperty(PropertyType.Gold).Value;
    public int GameCoin => gameCoin;
    [ShowInInspector] Dictionary<Guid, Action<ItemInfo>> itemListeners;
    // public event Action<int> OnMoneyChanged;
    public event Action<int> OnGameCoinChanged;
    public event Action<List<ItemInfo>> OnItemListChanged;
    
    void Awake()
    {
        itemListeners = new();
    }
    #region Money
    public void AddMoney(int value)
    {
        GameDataManager.Instance.AddProperty(PropertyType.Gold, value);
        // money += value;
        // if(money < 0)
        //     money = 0;
            
        // OnMoneyChanged?.Invoke(money); 
    }
    public void SubMoney(int value)
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.Gold,value);
        // money -= value;
        // if(money < 0)
        //     money = 0;

        // OnMoneyChanged?.Invoke(money); 
    }
    public bool HasMoney(int value)
    {
        return Money >= value;
    }
    #endregion
    #region Query
    // 获取指定类型的物品列表，方便按类型获取
    public List<ItemInfo> GetItemList(ItemType type)
    {
        List<ItemInfo> result = new();
        for(int i = 0; i < itemList.Count; i++)
        {
            if(itemList[i].Type == type)
                result.Add(itemList[i]);
        }
        return result;
    }

    // 按唯一标识获取物品实例，找不到返回 null
    public ItemInfo GetItem(Guid guid)
    {
        for(int i = 0; i < itemList.Count; i++)
        {
            if(itemList[i].Guid == guid)
                return itemList[i];
        }
        return null;
    }

    // 按配置 Id 获取第一个匹配的物品实例（同类可能存在多个堆叠），找不到返回 null
    public ItemInfo GetItem(long id)
    {
        for(int i = 0; i < itemList.Count; i++)
        {
            if(itemList[i].Id == id)
                return itemList[i];
        }
        return null;
    }

    // 统计某配置 Id 物品的总数量（跨所有堆叠）
    public int GetItemCount(long id)
    {
        int total = 0;
        for(int i = 0; i < itemList.Count; i++)
        {
            if(itemList[i].Id == id)
                total += itemList[i].Count;
        }
        return total;
    }
    #endregion
    #region Add
    public void AddItem(long id, int count)
    {
        ItemData data = ItemManager.St.GetItemData(id);
        if(data == null)
        {
            Debug.LogWarning($"PlayerBag missing item data: {id}", this);
            return;
        }

        AddItem(data, count);
    }
    public void AddItem(ItemInfo info)
    {
        if(info == null)
        {
            Debug.LogError("PlayerBag AddItem: info is null", this);
            return;
        }

        ItemData data = ItemManager.St.GetItemData(info.Id);
        if(data == null)
        {
            Debug.LogWarning($"PlayerBag missing item data: {info.Id}", this);
            return;
        }

        AddItem(data, info.Count);
    }

    // 按物品最大堆叠数添加：先填满已有未满的同类堆叠，剩余数量再拆分为新的堆叠
    void AddItem(ItemData data, int count)
    {
        if(count <= 0)
        {
            Debug.LogError("PlayerBag AddItem: count <= 0", this);
            return;
        }

        int maxNum = data.MaxCount > 0 ? data.MaxCount : int.MaxValue;
        int remaining = count;

        for(int i = 0; i < itemList.Count && remaining > 0; i++)
        {
            ItemInfo info = itemList[i];
            if(info.Id != data.Id || info.Count >= maxNum)
                continue;

            int add = Mathf.Min(maxNum - info.Count, remaining);
            info.AddCount(add);
            remaining -= add;
            NotifyItemChanged(info);
        }

        while(remaining > 0)
        {
            int stackCount = Mathf.Min(maxNum, remaining);
            ItemInfo info = ItemInfo.Create(data, stackCount);
            itemList.Add(info);
            remaining -= stackCount;
            NotifyItemChanged(info);
        }

        OnItemListChanged?.Invoke(itemList);
    }
    #endregion
    #region Consume
    public bool CanConsumeFoodMtItem(ItemInfo info, int count)
    {
        return info.Count >= count;
    }

    public void ConsumeItem(ItemInfo info, int count)
    {
        if(info == null)
        {
            Debug.LogError("PlayerBag ConsumeItem: info is null", this);
            return;
        }

        info.SubCount(count);

        bool removed = info.Count <= 0;
        if(removed)
            itemList.Remove(info);

        // 先通知监听者（此时 Count 可能已为 0，UI 可据此清空显示），再清理已移除物品的监听
        NotifyItemChanged(info);
        if(removed)
            itemListeners.Remove(info.Guid);

        OnItemListChanged?.Invoke(itemList);
    }
    #endregion
    #region ItemListener
    // 绑定单个物品实例的变化回调：当该物品数量变化或被移除（Count 归零）时触发。
    // 典型用法：UI 物品槽在显示某个 ItemInfo 时注册，离开时反注册。
    public void AddItemListener(Guid guid, Action<ItemInfo> callback)
    {
        if(itemListeners.TryGetValue(guid, out Action<ItemInfo> existing))
            itemListeners[guid] = existing + callback;
        else
            itemListeners[guid] = callback;
    }

    // 绑定并立即用当前状态回调一次：UI 注册时即可同步显示，无需调用方自己再手动刷新一遍
    public void AddItemListener(ItemInfo info, Action<ItemInfo> callback)
    {
        AddItemListener(info.Guid, callback);
        callback?.Invoke(info);
    }

    // 反注册指定回调；该物品再无监听时移除整个 key
    public void RemoveItemListener(Guid guid, Action<ItemInfo> callback)
    {
        if(!itemListeners.TryGetValue(guid, out Action<ItemInfo> existing))
            return;

        existing -= callback;
        if(existing == null)
            itemListeners.Remove(guid);
        else
            itemListeners[guid] = existing;
    }
    // 清空某个物品的全部监听
    public void ClearItemListener(Guid guid) => itemListeners.Remove(guid);

    // 触发单个物品的监听回调（物品自身变化时由内部调用）
    void NotifyItemChanged(ItemInfo info)
    {
        if(itemListeners == null)
            return;
        if(itemListeners.TryGetValue(info.Guid, out Action<ItemInfo> callback))
            callback?.Invoke(info);
    }
    #endregion
    #region Test
    // 食材道具(ItemType.Ingredient=6)在新配置中统一为 100000~100049：
    //   100000~100019 蔬果/主食、100020~100039 海鲜、100040~100049 调料。
    [Button]
    public void AddTestFoodMtItems()
    {
        // 蔬果 / 主食 100000~100019
        for(long id = 100000; id <= 100019; id++)
            AddItem(id, 9);
        // 海鲜 100020~100039
        for(long id = 100020; id <= 100039; id++)
            AddItem(id, 9);
        // 调料 100040~100049
        for(long id = 100040; id <= 100049; id++)
            AddItem(id, 9);
    }

    [Button]
    public void AddTestFoodMtItems2()
    {
        // 按现配置中几个配方所需食材各备一份，便于测试合成
        // 蛋炒饭配方(110001): 100000+100006
        AddItem(100000, 2); // 米饭
        AddItem(100006, 2); // 鸡蛋
        // 八宝菜配方(110002): 100007+100021+100013+100012
        AddItem(100007, 2); // 猪肉
        AddItem(100021, 2); // 鱿鱼
        AddItem(100013, 2); // 萝卜
        AddItem(100012, 2); // 香菇
        // 香煎鱼配方(110007): 100036+100040
        AddItem(100036, 2); // 青花鱼
        // 味增汤配方(110018): 100001+100015+100043
        AddItem(100001, 2); // 豆腐
        AddItem(100015, 2); // 海苔
        // 调料
        AddItem(100040, 5); // 食用盐
        AddItem(100043, 3); // 高汤
    }
    #endregion
    #region GameCoin
    public void SubGameCoin(int value)
    {
        // gameCoin -= value;
        // if(gameCoin < 0)
        //     gameCoin = 0;

        // OnGameCoinChanged?.Invoke(gameCoin);
        GameDataManager.Instance.RemoveProperty(PropertyType.GameGold, value);
        OnGameCoinChanged?.Invoke(gameCoin);
    }
    public void AddGameCoin(int value)
    {
        // gameCoin += value;
        // OnGameCoinChanged?.Invoke(gameCoin);
        GameDataManager.Instance.AddProperty(PropertyType.GameGold, value);
        OnGameCoinChanged?.Invoke(gameCoin);
    }
    public bool HasGameCoin(int value)
    {
        return gameCoin >= value;
    }
    #endregion
    #region Recipe
    public bool IsRecipeUnlocked(long recipeItemId) => unlockedRecipeIds.Contains(recipeItemId);

    public void UnlockRecipe(long recipeItemId)
    {
        if (!unlockedRecipeIds.Contains(recipeItemId))
            unlockedRecipeIds.Add(recipeItemId);
    }
    #endregion
    #region Save
    public string GUID => "PlayerBag";
    public void SaveData(GameSaveData data)
    {
        data.itemList = itemList;
    }
    public void LoadData(GameSaveData data)
    {
        itemList = data.itemList;
    }
    #endregion
}
