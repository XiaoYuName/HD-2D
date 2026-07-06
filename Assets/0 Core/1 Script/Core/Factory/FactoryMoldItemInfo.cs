using System;
using UnityEngine;
using XFramework;
/// <summary>
/// 工厂「物料制作」合成产物的<b>运行时自描述</b>物品：由「框架(FigureModel) + 贴纸(Painting)」现场合成，
/// 不再预生成并写入 ItemConfig，也不从 ItemData 查询。身份/展示（名称/描述/图标/售价/类型）全部随实例携带，
/// Id 由 <see cref="ComposeId"/> 按框架+贴纸推出（背包据此堆叠）。入包走 <see cref="InventoryManager.AddRuntimeItem"/>。
/// 图标另由 <see cref="FactoryMoldItemIcon"/> 按框架+贴纸实时三层合成，本类 <see cref="IconPath"/> 仅作无合成图时的回退。
/// </summary>
[Serializable]
public class FactoryMoldItemInfo : ItemInfo
{
    [SerializeField] long frameItemId;      // 来源框架(FigureModel)物品 Id
    [SerializeField] long paintingItemId;    // 来源贴纸(Painting)物品 Id
    [SerializeField] string frameNameKey;   // 框架名称多语言 Key（InventoryItem 表）
    [SerializeField] string frameDescKey;   // 框架描述多语言 Key
    [SerializeField] string paintingNameKey; // 贴纸名称多语言 Key
    [SerializeField] string frameIconPath;  // 框架图标 AA Key（无拍照图时作主图回退）
    [SerializeField] string paintingIconPath;// 贴纸图标 AA Key
    [SerializeField] int value;             // 售价 = 框架价值 + 画布上贴纸价值之和（合成时算好）
    #region Get
    public long FrameItemId => frameItemId;
    public long PaintingItemId => paintingItemId;
    public string FrameNameKey => frameNameKey;
    public string PaintingNameKey => paintingNameKey;
    public string FrameIconPath => frameIconPath;
    public string PaintingIconPath => paintingIconPath;
    public string Name =>  LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, FrameNameKey) + LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, PaintingNameKey);
    public string Desc => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, frameDescKey);
    #endregion

    #region 重写（自描述，不读 config data）
    // Id 是由「框架+贴纸」推出的合成<b>堆叠键</b>，不是配置表/物品数据库里的 Id（本物品在数据库查不到）。
    // 因此基类继承来的 id 字段对本类无意义、恒不使用——身份与堆叠完全由此重写值决定。
    public override long Id => ComposeId(frameItemId, paintingItemId);
    public override ItemType Type => ItemType.FactoryProductionMaterials;
    public override string Remark => frameNameKey + " + " + paintingNameKey;
    public override string IconPath => null; 
    public override int MaxCount => 999;
    public override int Value => value;                // 售价（框架 + 贴纸价值之和）
    #endregion

    /// <summary>
    /// 由「框架 Id + 贴纸 Id」推出确定性复合 Id：相同组合 → 相同 Id（背包据此堆叠）。
    /// 直接拼接规则(贴纸Id×1000000 + 框架Id)，与合成图/Mold 图文件名一致（见 <see cref="FactoryMoldSynthesis"/>），
    /// 如贴纸 200000 + 框架 210000 → 200000210000；数值远高于配置表 Id(最大约 23 万)，不与配置物品冲突。
    /// </summary>
    public static long ComposeId(long frameItemId, long stickerItemId)
        => FactoryMoldSynthesis.GetResultId(frameItemId, stickerItemId);

    /// <summary>
    /// 由背包里的框架+贴纸实例合成一件运行时产物。<paramref name="sellValue"/> 为售价(框架 Value + 贴纸 Value，合成时算好)。
    /// frame/sticker 为普通配置物品，其 Name/Desc/IconPath 在此读取快照存入本实例(本类自身不再回查 ItemData)。
    /// </summary>
    public static FactoryMoldItemInfo Create(ItemInfo frame, ItemInfo sticker, int count, int sellValue)
    {
        var info = new FactoryMoldItemInfo
        {
            frameItemId     = frame.Id,
            paintingItemId   = sticker.Id,
            frameNameKey    = frame.NameKey,
            frameDescKey    = frame.DescKey,
            paintingNameKey  = sticker.NameKey,
            frameIconPath   = frame.IconPath,
            paintingIconPath = sticker.IconPath,
            value           = sellValue,
        };
        info.AddCount(count);   // 基类 count 从 0 起，AddCount 设为目标数量
        return info;
    }
}
