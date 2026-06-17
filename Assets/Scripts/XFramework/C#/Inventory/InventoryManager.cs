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
    private void RegisterAllItemChange(Action<List<ItemBag>> action)
    {
        AllItemChange += action;
    }

    /// <summary>
    /// 反注册背包内所有物品变化回调
    /// </summary>
    /// <param name="action"></param>
    private void UnregisterAllItemChange(Action<List<ItemBag>> action)
    {
        AllItemChange += action;
    }

    private Dictionary<string, Action<ItemBag>> ItemChangeCallBack = new Dictionary<string, Action<ItemBag>>();
    
    /// <summary>
    /// 注册游戏背包变化回调
    /// </summary>
    /// <param name="itemID">物品ID</param>
    /// <param name="CallBack">回调函数</param>
    /// <param name="isTrigger">是否注册时就触发一次</param>
    public void  RegisterItemBagChangAction(string itemID, Action<ItemBag> CallBack,bool isTrigger=true)
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
    public void  UnregisterItemBagChangAction(string itemID, Action<ItemBag> callback)
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


    private void TriggerItemBag(ItemBag bag)
    {
        
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
}
