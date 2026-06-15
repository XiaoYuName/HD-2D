using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class InventoryManager : MonoSingleton<InventoryManager>,ISaveable
{
    private List<ItemBag> itemBags = new List<ItemBag>();
    
    
    #region ISaveable

    public string GUID => "InventoryManager";

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public GameSaveData GenerateSaveData()
    {
        GameSaveData gameSaveData = new GameSaveData();
        gameSaveData.itemBags = itemBags;
        return gameSaveData;
    }

    public void RestoreData(GameSaveData GameSave)
    {
        if (GameSave is { itemBags: { Count: > 0 } })
        {
            itemBags = GameSave.itemBags;
        }
        else
        {
            itemBags = new List<ItemBag>();
        }
    }
    

    #endregion

}

[System.Serializable]
public class ItemBag
{
    [HorizontalGroup("物品"),LabelText("物品ID")]
    public string itemID;
    [HorizontalGroup("物品"),LabelText("物品数量")]
    public int itemCount;
}
