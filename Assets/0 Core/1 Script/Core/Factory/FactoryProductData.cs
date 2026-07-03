using System;
using UnityEngine;

/// <summary>
/// 工厂产品 = 背包物品(<see cref="ItemData"/>) + 工厂专有的单批数量。
/// 名称 / 描述 / 图标 / 单价均取自所属 <see cref="ItemData"/>（创建时缓存，不再冗余存储，避免与物品配置脱节）；
/// 仅「单批数量」是工厂独有的经济数值，单独保存。
/// </summary>
[Serializable]
public class FactoryProductData
{
    [SerializeField] long itemId;      // 对应背包物品配置 Id：加工完成后据此发放进背包
    [SerializeField] int craftCount;   // 单批数量（件）——ItemData 无此字段，工厂独有

    [System.NonSerialized] ItemData data;   // 创建时缓存的物品配置
    // 图标覆盖：生产资料(模具)物品无独立 128×128 图标(ItemConfig.Icon 留空)，画布合成图走 FactoryMoldMgConfig.GetSpriteKey，
    // 由调用方（FactoryMainPanel）传入，覆盖下方 IconPath 默认取值。
    [System.NonSerialized] string iconOverride;

    public long ItemId => itemId;
    public int CraftCount => Mathf.Max(1, craftCount);

    ItemData Data => data;

    // 名称 / 描述为物品多语言 Key（<see cref="LocalizeTableSet.InventoryItem"/> 表），图标为 AA Key，单价取 <see cref="ItemData.Value"/>
    public string NameKey => Data.Name;
    public string DescKey => Data.Desc;
    public string IconPath => !string.IsNullOrEmpty(iconOverride) ? iconOverride : Data.IconPath;
    public int UnitPrice => Data.Value;

    /// <summary>本产品单批总花费 = 单价 × 数量。</summary>
    public int TotalCost => UnitPrice * CraftCount;

    /// <summary>手办产品默认单批数量：ItemConfig 物品无「单批数量」字段，经济数值待策划确定，暂用占位常量。</summary>
    public const int DefaultCraftCount = 50;

    /// <summary>
    /// 次品物品 Id 相对正品的偏移：次品 Id = 正品 Id + 此值。次品售价为正品一半，仅由工厂加工按完成率产出，
    /// 不作为可加工产品（<see cref="FactoryMainPanel"/> 构建产品列表时会排除）。次品物品须已在 ItemConfig 中按此 Id 配置。
    /// 注：周边商品 Id 由「框架Id×步长(1000)+贴纸偏移」编码而成（见 FactoryMoldMgConfig），偏移取 1000 会与相邻框架的商品 Id 撞车，
    /// 故取与 <see cref="MerchandiseIdOffset"/> 同级的 100000，确保落在完全空闲的号段。
    /// </summary>
    public const long DefectiveIdOffset = 100000;

    /// <summary>由正品 Id 取其次品 Id（约定：正品 Id + <see cref="DefectiveIdOffset"/>）。</summary>
    public static long ToDefectiveId(long qualifiedId) => qualifiedId + DefectiveIdOffset;

    /// <summary>该 Id 是否为某正品的次品变体（其「Id - 偏移」在配置中存在且同为手办）。用于从可加工产品列表中排除次品。</summary>
    public static bool IsDefectiveId(long id)
    {
        ItemData baseItem = ItemManager.St != null ? ItemManager.St.GetItemData(id - DefectiveIdOffset) : null;
        return baseItem != null && baseItem.Type == ItemType.Merchandise;
    }

    /// <summary>
    /// 生产资料(模具) Id 相对其加工完成后的周边商品(Merchandise) Id 的偏移：商品 Id = 生产资料 Id + 此值。
    /// 商品须已在 ItemConfig 中按此 Id 配置（见 Data/Factory/FactoryMerchandiseSupplement.csv）。
    /// </summary>
    public const long MerchandiseIdOffset = 100000;

    /// <summary>由生产资料(模具) Id 取其加工完成后的周边商品 Id（约定：生产资料 Id + <see cref="MerchandiseIdOffset"/>）。</summary>
    public static long ToMerchandiseId(long materialId) => materialId + MerchandiseIdOffset;

    /// <summary>由 ItemConfig 物品构建工厂产品：记录 Id 与单批数量，并缓存物品配置供展示取值。</summary>
    /// <param name="iconOverride">图标覆盖(AA Key)，未传时取物品配置自带图标（见 <see cref="IconPath"/>）。</param>
    public static FactoryProductData Create(ItemData item, int craftCount = DefaultCraftCount, string iconOverride = null)
    {
        return new FactoryProductData
        {
            itemId = item.Id,
            craftCount = craftCount,
            data = item,
            iconOverride = iconOverride,
        };
    }
}
