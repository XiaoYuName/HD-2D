using UnityEngine;
using XFramework;

/// <summary>
/// ItemInfo(背包物品实例，见 InventoryManager.cs)的扩展方法：
/// 把物品的配置元数据(名称/描述/图标/售价/品质/堆叠上限等)从 ItemInfo 本体剥离出来，
/// 统一通过 TbItemData(Luban 表) 查询，避免给 ItemInfo 塞冗余成员。
/// 运行时自描述物品(FactoryComposedItemInfo 及其子类)不走这里，直接用自身携带的字段。
/// </summary>
public static class ItemInfoExtensions
{
    /// <summary>取该物品的配置表定义(TbItemData)。运行时自描述物品(查不到表)返回 null。</summary>
    public static ItemData GetItemData(this ItemInfo stack)
    {
        if (stack == null)
        {
            Debug.LogWarning("ItemInfo is null");
            return null;
        }

        return InventoryManager.Instance.GetItemData(stack.ID);
    }

    /// <summary>备注(策划标识用，非玩家可见文案)。</summary>
    public static string GetRemark(this ItemInfo item) => item.GetItemData()?.Remark;

    /// <summary>名称多语言 Key(InventoryItem 表)。喂给 LocalizeStringEvent / SetText。</summary>
    public static string GetNameKey(this ItemInfo item) => item.GetItemData()?.NameKey?.Value;
    public static string GetNameTable(this ItemInfo item) => item.GetItemData()?.NameKey.Table;
    /// <summary>描述多语言 Key。</summary>
    public static string GetDescKey(this ItemInfo item) => item.GetItemData()?.DescKey?.Value;

    /// <summary>图标资源名(Addressable Key)。喂给 Image.SetIcon。</summary>
    public static string GetIconName(this ItemInfo item) => item.GetItemData()?.IconName;
    public static string GetIconPath(this ItemInfo item) => GamePathTools.CombinationItemIconPath(item.GetIconName());
    /// <summary>堆叠上限。未配置(≤0)时视为无上限(int.MaxValue)。</summary>
    public static int GetMaxCount(this ItemInfo item)
    {
        ItemData data = item.GetItemData();
        return data.MaxNum > 0 ? data.MaxNum : int.MaxValue;
    }

    /// <summary>物品价值/售价(取自出售配置 Shop.Value)。无配置返回 0。</summary>
    public static int GetValue(this ItemInfo item) => item.GetItemData()?.Shop?.Value ?? 0;

    /// <summary>品质。无配置返回普通(C)。</summary>
    public static ItemQuality GetQuality(this ItemInfo item)
    {
        ItemData data = item.GetItemData();
        return data.Quality;
    }

    /// <summary>当前语言下解析好的名称文案。</summary>
    public static string GetName(this ItemInfo item)
    {
        ItemData data = item.GetItemData();
        if (data?.NameKey == null) return string.Empty;
        return LanguageManager.Instance.GetLocalizedString(data.NameKey.Table, data.NameKey.Value);
    }

    /// <summary>当前语言下解析好的描述文案。</summary>
    public static string GetDesc(this ItemInfo item)
    {
        ItemData data = item.GetItemData();
        if (data?.DescKey == null) return string.Empty;
        return LanguageManager.Instance.GetLocalizedString(data.DescKey.Table, data.DescKey.Value);
    }

    /// <summary>增加数量(等价旧 ItemInfo.AddCount)。</summary>
    public static void AddCount(this ItemInfo item, int value) => item.Count += value;

    /// <summary>扣减数量(等价旧 ItemInfo.SubCount)。扣成负数会报错(不改动，交由调用方保证)。</summary>
    public static void SubCount(this ItemInfo item, int value)
    {
        item.Count -= value;
        if (item.Count < 0)
            Debug.LogError("SubCount 数量错误");
    }
}
