using System;
using UnityEngine;
using XFramework;

/// <summary>
/// 「框架(FigureModel) + 贴纸(Painting)」组合而成的<b>运行时自描述</b>物品的公共基类：身份/展示信息（名称/描述/图标来源）
/// 全部随实例携带，不查 TbItemData。派生类：<see cref="FactoryMoldItemInfo"/>(生产资料/模具)、
/// <see cref="FactoryMerchandiseItemInfo"/>(加工产出的周边商品，正品/次品)。图标由 <see cref="FactoryMoldItemIcon"/>
/// 按框架+贴纸实时三层合成。
///
/// 迁移说明：原本继承旧 ItemInfo 并 override 其 config-backed 虚属性(Id/Type/Value/Remark/...)。
/// 新的 ItemInfo(见 InventoryManager.cs)只保留 ID/ItemType/Count 等基础字段，元数据改由 ItemStackExtensions 查表；
/// 自描述物品不查表——ID/ItemType 在构造时写入基类，展示数据用自身字段。旧实现见下方 #if false 块。
/// </summary>

#if false // ===== 旧实现（依赖已删除的旧 ItemInfo 虚成员），保留备查 =====
[Serializable]
public abstract class FactoryComposedItemInfo : ItemInfo
{
    [SerializeField] protected long frameItemId;      // 来源框架(FigureModel)物品 Id
    [SerializeField] protected long paintingItemId;   // 来源贴纸(Painting)物品 Id
    [SerializeField] protected string frameNameKey;   // 框架名称多语言 Key（InventoryItem 表）
    [SerializeField] protected string frameDescKey;   // 框架描述多语言 Key
    [SerializeField] protected string paintingNameKey; // 贴纸名称多语言 Key
    [SerializeField] protected string frameIconPath;  // 框架图标 AA Key（无拍照图时作主图回退）
    [SerializeField] protected string paintingIconPath;// 贴纸图标 AA Key

    #region Get
    public long FrameItemId => frameItemId;
    public long PaintingItemId => paintingItemId;
    public string FrameNameKey => frameNameKey;
    public string FrameDescKey => frameDescKey;
    public string PaintingNameKey => paintingNameKey;
    public string FrameIconPath => frameIconPath;
    public string PaintingIconPath => paintingIconPath;
    public virtual string Name => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, FrameNameKey) + LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, PaintingNameKey);
    public string Desc => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, frameDescKey);

    /// <summary>制作成本 = 框架基础成本(MoldFrameConfig.Score) + 贴纸基础成本(PaintingConfig.BaseCost)。</summary>
    public int Cost
    {
        get
        {
            MoldFrameConfig moldFrameConfig = AssetsManager.Instance.LoadAssets<MoldFrameConfig>(AssetKeys.MoldFrameConfigPath);
            PaintingConfig paintingConfig = AssetsManager.Instance.LoadAssets<PaintingConfig>(AssetKeys.PaintingConfigPath);

            int cost = moldFrameConfig.GetScore(frameItemId) + paintingConfig.GetBaseCost(paintingItemId);

            AssetsManager.Instance.FreeAsset(AssetKeys.MoldFrameConfigPath);
            AssetsManager.Instance.FreeAsset(AssetKeys.PaintingConfigPath);

            return cost;
        }
    }
    #endregion

    #region 重写（自描述，不读 config data）
    public override string Remark => frameNameKey + " + " + paintingNameKey;
    public override int MaxCount => 999;
    public override int Shop => 0;
    public override int Quality => (InventoryManager.Instance.GetItemData(frameItemId).Quality + InventoryManager.Instance.GetItemData(paintingItemId).Quality) / 2;
    public override string IconPath => null;
    public override string NameKey => null;
    public override string DescKey => null;
    #endregion
}
#endif

// ===== 新实现（基于新的 ItemInfo 基类）=====
[Serializable]
public abstract class FactoryComposedItemInfo : ItemInfo
{
    public long FrameItemId;      // 来源框架(FigureModel)物品 Id
    public long PaintingItemId;   // 来源贴纸(Painting)物品 Id
    public ItemType ItemType = ItemType.Material;
    public ItemMaterialType MaterialType;

    /// <summary>无参构造：序列化/反序列化用。</summary>
    protected FactoryComposedItemInfo() { }
    protected FactoryComposedItemInfo(long id, int count) : base(id, count) { }
}

// 扩展方法：身份/展示(名称/描述)/售价/成本/构造，均按 FrameItemId/PaintingItemId 现查 ItemData，
// 不在实例上自带冗余字段；本类与派生类禁止 Create 类静态工厂方法与 "=>" 表达式体成员，相关逻辑统一收在此处。
public static class FactoryComposedItemInfoEt
{
    public static ItemType GetItemType(this FactoryComposedItemInfo info)
    {
        return info.ItemType;
    }

    public static ItemMaterialType GetMaterialItemType(this FactoryComposedItemInfo info)
    {
        return info.MaterialType;
    }

    /// <summary>售价 = 框架 + 贴纸的配置售价(Shop.Value)之和。</summary>
    public static int GetValue(this FactoryComposedItemInfo info)
    {
        return (InventoryManager.Instance.GetItemData(info.FrameItemId)?.Shop?.Value ?? 0)
             + (InventoryManager.Instance.GetItemData(info.PaintingItemId)?.Shop?.Value ?? 0);
    }

    /// <summary>制作成本 = 框架基础成本(MoldFrameConfig.Score) + 贴纸基础成本(PaintingConfig.BaseCost)。</summary>
    public static int GetCost(this FactoryComposedItemInfo info)
    {
        MoldFrameConfig moldFrameConfig = AssetsManager.Instance.LoadAssets<MoldFrameConfig>(AssetKeys.MoldFrameConfigPath);
        PaintingConfig paintingConfig = AssetsManager.Instance.LoadAssets<PaintingConfig>(AssetKeys.PaintingConfigPath);

        int cost = moldFrameConfig.GetScore(info.FrameItemId) + paintingConfig.GetBaseCost(info.PaintingItemId);

        AssetsManager.Instance.FreeAsset(AssetKeys.MoldFrameConfigPath);
        AssetsManager.Instance.FreeAsset(AssetKeys.PaintingConfigPath);
        return cost;
    }

    /// <summary>名称 = 框架名称 + 贴纸名称（当前语言，现查 ItemData.NameKey）。</summary>
    public static string GetName(this FactoryComposedItemInfo info)
    {
        ItemData frame = InventoryManager.Instance.GetItemData(info.FrameItemId);
        ItemData painting = InventoryManager.Instance.GetItemData(info.PaintingItemId);
        string frameName = frame?.NameKey != null ? LanguageManager.Instance.GetLocalizedString(frame.NameKey.Table, frame.NameKey.Value) : string.Empty;
        string paintingName = painting?.NameKey != null ? LanguageManager.Instance.GetLocalizedString(painting.NameKey.Table, painting.NameKey.Value) : string.Empty;
        return frameName + paintingName;
    }

    /// <summary>描述 = 框架描述（当前语言，现查 ItemData.DescKey）。</summary>
    public static string GetDesc(this FactoryComposedItemInfo info)
    {
        ItemData frame = InventoryManager.Instance.GetItemData(info.FrameItemId);
        if (frame?.DescKey == null)
            return string.Empty;
        return LanguageManager.Instance.GetLocalizedString(frame.DescKey.Table, frame.DescKey.Value);
    }

    /// <summary>合成堆叠键：按框架+贴纸推出，不是配置表里的 Id（本物品在表里查不到）。</summary>
    public static long ComposeMoldId(long frameItemId, long paintingItemId)
    {
        return FactoryMoldSynthesis.GetResultId(frameItemId, paintingItemId);
    }

    /// <summary>由来源框架/贴纸(背包物品实例)现场合成一份生产资料(<see cref="FactoryMoldItemInfo"/>)，数量为 count，框架不消耗。</summary>
    public static FactoryMoldItemInfo CreateMoldItem(ItemInfo frame, ItemInfo sticker, int count)
    {
        long id = ComposeMoldId(frame.ID, sticker.ID);
        return new FactoryMoldItemInfo(id, count)
        {
            FrameItemId = frame.ID,
            PaintingItemId = sticker.ID,
        };
    }

    // 正品/次品 Id 偏移：结果 Id = 生产资料 Id(贴纸Id×1000000+框架Id) + 偏移，均落在同一贴纸的 100 万号段内，互不冲突
    public static long ComposeMerchandiseId(long frameItemId, long paintingItemId, FactoryMerchandiseItemInfo.QualityGrade grade)
    {
        long baseId = FactoryMoldSynthesis.GetResultId(frameItemId, paintingItemId);
        long offset = grade == FactoryMerchandiseItemInfo.QualityGrade.Defective ? FactoryMerchandiseItemInfo.DefectiveIdOffset : FactoryMerchandiseItemInfo.QualifiedIdOffset;
        return baseId + offset;
    }

    /// <summary>由来源生产资料(框架+贴纸身份)与品级现场合成一份周边商品(<see cref="FactoryMerchandiseItemInfo"/>)，数量为 count。</summary>
    public static FactoryMerchandiseItemInfo CreateMerchandiseItem(this FactoryMoldItemInfo material, FactoryMerchandiseItemInfo.QualityGrade grade, int count)
    {
        long id = ComposeMerchandiseId(material.FrameItemId, material.PaintingItemId, grade);
        return new FactoryMerchandiseItemInfo(id, count, grade)
        {
            FrameItemId = material.FrameItemId,
            PaintingItemId = material.PaintingItemId,
        };
    }

    /// <summary>是否为次品。</summary>
    public static bool IsDefective(this FactoryMerchandiseItemInfo info)
    {
        return info.Grade == FactoryMerchandiseItemInfo.QualityGrade.Defective;
    }

    /// <summary>名称：次品在正品名后追加多语言后缀（如「熊猫徽章（次品）」，Key 见 FactoryLocKeySet.Main.DefectiveSuffix）。</summary>
    public static string GetName(this FactoryMerchandiseItemInfo info)
    {
        string name = GetName((FactoryComposedItemInfo)info);
        return info.IsDefective()
            ? name + LanguageManager.Instance.GetLocalizedString(LocTableSet.Factory, FactoryLocKeySet.Main.DefectiveSuffix)
            : name;
    }

    /// <summary>售价：框架+贴纸售价之和(基类)，次品再减半(整除，不四舍五入)。</summary>
    public static int GetValue(this FactoryMerchandiseItemInfo info)
    {
        int baseValue = GetValue((FactoryComposedItemInfo)info);
        return info.IsDefective() ? baseValue / 2 : baseValue;
    }
}