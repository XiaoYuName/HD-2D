
#if false
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
    [SerializeField] string frameIconPath;  // 框架图标 AA Key（无拍照图时作主图回退）
    [SerializeField] string stickerIconPath;// 贴纸图标 AA Key
    [SerializeField] int value;             // 售价 = 框架价值 + 画布上贴纸价值之和（合成时算好）
    // 现场拍照的「框架+贴纸」合成图（PNG 字节，随存档序列化；后续 MemoryPack 直接带上）。
    // HideInInspector：避免几十 KB 的字节数组在 Inspector 里逐字节绘制导致卡顿（仍参与序列化）。
    [HideInInspector][SerializeField] byte[] iconData;
    [NonSerialized] Sprite iconSpriteCache; // 由 iconData 惰性解码出的运行时 Sprite（不序列化）

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
    public override string Remark => frameNameKey + " + " + stickerNameKey;
    public override string Name => frameNameKey;       // 以框架(产品形态)为主名
    public override string Desc => frameDescKey;
    public override string IconPath => frameIconPath;  // 无拍照图时的回退主图（框架图标）
    // 拍照合成图：由 iconData 字节惰性解码；有则物品图标用它（见 IconLoadExtension.SetIcon(ItemInfo)）
    public override Sprite IconSprite
    {
        get
        {
            if(iconSpriteCache != null)
                return iconSpriteCache;
            if(iconData == null || iconData.Length == 0)
                return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if(!tex.LoadImage(iconData))   // 解码 PNG
                return null;
            iconSpriteCache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return iconSpriteCache;
        }
    }
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
    /// <paramref name="iconPng"/> 为「框架+贴纸」现场拍照的合成图 PNG 字节（可空），非空时作为该物品图标。guid / 创建时间由基类惰性补齐。
    /// </summary>
    public static FactoryProductionMtItemInfo Create(ItemInfo frame, ItemInfo sticker, int count, int sellValue, byte[] iconPng = null)
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
            iconData        = iconPng,
        };
        info.AddCount(count);   // 基类 count 从 0 起，AddCount 设为目标数量
        return info;
    }
}
#endif
