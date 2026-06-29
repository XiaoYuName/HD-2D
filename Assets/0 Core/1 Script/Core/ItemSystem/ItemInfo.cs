using System;
using UnityEngine;
using Sirenix.OdinInspector;

[Serializable]
public class ItemInfo
{
    // 物品实例的唯一标识，序列化为字符串（Unity 无法直接序列化 System.Guid）
    [ShowInInspector] string Remark => data?.Remark;
    [SerializeField] string guid;
    [SerializeField] long id;
    [SerializeReference] ItemData data;
    [SerializeField] int count;
    // 物品实例的获取时间，序列化为 Ticks（Unity 无法直接序列化 System.DateTime）
    [SerializeField] long createTimeTicks;
    // 运行时缓存，避免每次访问都解析字符串
    [NonSerialized] Guid cachedGuid;
    #region Get
    // 物品实例唯一标识：用于按“单个物品”绑定/监听其变化（见 PlayerBag.AddItemListen）。
    // 缺失时惰性生成，保证编辑器手填、反序列化与运行时创建的每个实例都有有效 Guid。
    public Guid Guid
    {
        get
        {
            if(cachedGuid == Guid.Empty)
            {
                if(string.IsNullOrEmpty(guid) || !Guid.TryParse(guid, out cachedGuid))
                {
                    cachedGuid = Guid.NewGuid();
                    guid = cachedGuid.ToString();
                }
            }
            return cachedGuid;
        }
    }
    public long Id => id;
    public int Count => count;
    // 物品实例的获取时间。缺失时（旧存档/编辑器手填）惰性补为当前时间。
    public DateTime CreateTime
    {
        get
        {
            if(createTimeTicks <= 0)
                createTimeTicks = DateTime.Now.Ticks;
            return new DateTime(createTimeTicks);
        }
    }
    public ItemType Type => data.Type;
    public string Name => data.Name;
    public string Desc => data.Desc;
    public string IconPath => data.IconPath;
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
            guid = Guid.NewGuid().ToString(),
            id = data.Id,
            data = data,
            count = count,
            createTimeTicks = DateTime.Now.Ticks
        };
    }
    public static ItemInfo Create(long id, int count)
    {
        return new ItemInfo
        {
            guid = Guid.NewGuid().ToString(),
            id = id,
            data = ItemManager.St.GetItemData(id),
            count = count,
            createTimeTicks = DateTime.Now.Ticks
        };
    }
    #endregion
}
