using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System;
public class PlayerBag : MonoBehaviour
{
    [SerializeField] List<ItemInfo> itemList;
    [LabelText("已解锁配方ID")][SerializeField] List<long> unlockedRecipeIds;
    [LabelText("金币")][SerializeField] int money;

    public List<ItemInfo> ItemList => itemList;
    public List<long> UnlockedRecipeIds => unlockedRecipeIds;
    public int Money => money;

    public event Action OnMoneyChanged;
    public event Action OnItemChanged;
    #region Money
    public void AddMoney(int value)
    {
        money += value;
        if(money < 0)
            money = 0;
            
        OnMoneyChanged?.Invoke(); 
    }
    public void SubMoney(int value)
    {
        money -= value;
        if(money < 0)
            money = 0;
        OnMoneyChanged?.Invoke(); 
    }
    public bool HasMoney(int value)
    {
        return money >= value;
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
        }

        while(remaining > 0)
        {
            int stackCount = Mathf.Min(maxNum, remaining);
            itemList.Add(ItemInfo.Create(data, stackCount));
            remaining -= stackCount;
        }

        OnItemChanged?.Invoke();
    }
    #endregion
    #region Consume
    public bool CanConsumeFoodMtItem(ItemInfo info, int count)
    {
        return info.Count >= count;
    }

    public void ConsumeItem(ItemInfo info, int count)
    {
        info.SubCount(count);
        if(info.Count <= 0)
            itemList.Remove(info);
            
         OnItemChanged?.Invoke();
    }
    #endregion
    #region Test
    [Button]
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

    [Button]
    public void AddTestFoodMtItems2()
    {
        // 食材（蔬菜 600xxx / 鱼类 610xxx / 调料 620xxx）
        // 番茄炒蛋(700005): 600005+600006+620000+620001
        AddItem(600005, 2); // 番茄
        AddItem(600006, 2); // 鸡蛋
        // 清炒白萝卜(700000): 600000+620000+620001
        AddItem(600000, 2); // 白萝卜
        // 鲫鱼鲜汤(700015): 610000+600000+620000+620005
        AddItem(610000, 1); // 鲫鱼
        // 清蒸鲈鱼(700021): 610007+620005+620000
        AddItem(610007, 1); // 鲈鱼
        // 调料
        AddItem(620000, 5); // 食用精盐
        AddItem(620001, 3); // 白砂糖
        AddItem(620002, 3); // 酿造米醋
        AddItem(620005, 3); // 白胡椒粉
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
}
