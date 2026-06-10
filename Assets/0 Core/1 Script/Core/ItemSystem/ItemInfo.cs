using System;
using UnityEngine;

[Serializable]
public class ItemInfo
{
    [SerializeField] long id;
    [SerializeReference] ItemData data;
    [SerializeField] int count;
    #region Get
    public long Id => id;
    public int Count => count;
    public ItemType Type => data.Type;
    public string Name => data.Name;
    public string Desc => data.Desc;
    public Sprite Icon => data.Icon;
    #endregion
    #region Func
    public void AddCount(int value)
    {
        count += value;
    }

    public void SubCount(int value)
    {
        count -= value;
        if(count < 0)
            Debug.LogError("SubCount 数量错误");
    }
    #endregion
    #region Create
    public static ItemInfo Create(ItemData data, int count)
    {
        return new ItemInfo
        {
            id = data.Id,
            data = data,
            count = count
        };
    }
    public static ItemInfo Create(long id, int count)
    {
        return new ItemInfo
        {
            id = id,
            data = ItemManager.St.GetItemData(id),
            count = count
        };
    }
    #endregion
}
