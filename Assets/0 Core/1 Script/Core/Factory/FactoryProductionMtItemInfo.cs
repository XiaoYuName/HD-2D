using System;
using UnityEngine;

/// <summary>
/// 工厂「物料制作」产出的<b>运行时物品实例</b>（<see cref="ItemType.FactoryProductionMaterials"/>）。
/// 由「框架(<see cref="ItemType.FigureModel"/>，如空徽章/抱枕/立牌)」+「贴纸(<see cref="ItemType.Painting"/>，女主绘画)」组合而成。
///
/// 设计：ItemData 是<b>静态配置</b>（来自 ItemConfig），ItemInfo 才是<b>运行时实例</b>。此组合物无穷多、无法预配，
/// 故继承 <see cref="ItemInfo"/> 而非 ItemData——把组合信息（框架/贴纸的 Id、名称 Key、图标）自描述地随实例携带，
/// 重写 <see cref="Id"/>/<see cref="Type"/>/<see cref="Name"/>/<see cref="Desc"/>/<see cref="IconPath"/>/<see cref="MaxCount"/>，
/// 完全不依赖 <c>data</c>（其 data 恒为 null），也无需按 Id 反查 ItemConfig。
/// <see cref="ComposeId"/> 让相同组合得到相同 Id，从而在背包按 Id 堆叠为同一格（见 <see cref="PlayerBag.AddRuntimeItem"/>）。
/// </summary>
[Serializable]
public class FactoryProductionMtItemInfo : ItemInfo
{
    [SerializeField] long frameItemId;      // 来源框架(FigureModel)物品 Id
    [SerializeField] long stickerItemId;    // 来源贴纸(Painting)物品 Id
    [SerializeField] string frameNameKey;   // 框架名称多语言 Key（InventoryItem 表）
    [SerializeField] string frameDescKey;   // 框架描述多语言 Key
    [SerializeField] string stickerNameKey; // 贴纸名称多语言 Key
    [SerializeField] string frameIconPath;  // 框架图标 AA Key（作主图）
    [SerializeField] string stickerIconPath;// 贴纸图标 AA Key
    [SerializeField] int value;             // 售价 = 框架价值 + 画布上贴纸价值之和（合成时算好）

    #region Get
    public long FrameItemId => frameItemId;
    public long StickerItemId => stickerItemId;
    public string FrameNameKey => frameNameKey;
    public string StickerNameKey => stickerNameKey;
    public string FrameIconPath => frameIconPath;
    public string StickerIconPath => stickerIconPath;
    #endregion

    #region 重写（自描述，不读 config data）
    // Id 是由「框架+贴纸」推出的合成<b>堆叠键</b>，不是配置表/物品数据库里的 Id（本物品在数据库查不到）。
    // 因此基类继承来的 id 字段对本类无意义、恒不使用——身份与堆叠完全由此重写值决定。
    public override long Id => ComposeId(frameItemId, stickerItemId);
    public override ItemType Type => ItemType.FactoryProductionMaterials;
    public override string Name => frameNameKey;       // 以框架(产品形态)为主名
    public override string Desc => frameDescKey;
    public override string IconPath => frameIconPath;  // 主图：框架图标（贴纸图标另存）
    public override int MaxCount => 999;
    public override int Value => value;                // 售价（框架 + 贴纸价值之和）
    #endregion

    // 运行时合成物 Id 基址：远高于配置表任何 Id（当前配置最大约 23 万），避免与配置物品冲突。
    const long RuntimeIdBase = 9_000_000_000_000L;
    // 复合 Id = 基址 + 框架Id*因子 + 贴纸Id。要求贴纸 Id < 因子（当前贴纸 Id≈20 万，安全），保证不同组合不撞 Id。
    const long FrameIdFactor = 1_000_000L;

    /// <summary>由「框架 Id + 贴纸 Id」推出确定性复合 Id：相同组合 → 相同 Id（背包据此堆叠）。</summary>
    public static long ComposeId(long frameItemId, long stickerItemId)
        => RuntimeIdBase + frameItemId * FrameIdFactor + stickerItemId;

    /// <summary>
    /// 由所选框架与贴纸的背包实例合成一件生产资料（数量 count）。<paramref name="sellValue"/> 为售价（框架 + 画布上贴纸价值之和）。
    /// guid / 创建时间由基类惰性补齐。
    /// </summary>
    public static FactoryProductionMtItemInfo Create(ItemInfo frame, ItemInfo sticker, int count, int sellValue)
    {
        if(frame == null || sticker == null)
        {
            Debug.LogError("FactoryProductionMtItemInfo.Create: frame / sticker 为空");
            return null;
        }

        var info = new FactoryProductionMtItemInfo
        {
            frameItemId     = frame.Id,
            stickerItemId   = sticker.Id,
            frameNameKey    = frame.Name,
            frameDescKey    = frame.Desc,
            stickerNameKey  = sticker.Name,
            frameIconPath   = frame.IconPath,
            stickerIconPath = sticker.IconPath,
            value           = sellValue,
        };
        info.AddCount(count);   // 基类 count 从 0 起，AddCount 设为目标数量
        return info;
    }
}
