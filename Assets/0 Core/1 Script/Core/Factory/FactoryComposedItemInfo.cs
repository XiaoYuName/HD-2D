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
    [SerializeField] protected long frameItemId;      // 来源框架(FigureModel)物品 Id
    [SerializeField] protected long paintingItemId;   // 来源贴纸(Painting)物品 Id
    [SerializeField] protected string frameNameKey;   // 框架名称多语言 Key（InventoryItem 表）
    [SerializeField] protected string frameDescKey;   // 框架描述多语言 Key
    [SerializeField] protected string paintingNameKey; // 贴纸名称多语言 Key
    [SerializeField] protected string frameIconPath;  // 框架图标 AA Key（无拍照图时作主图回退）
    [SerializeField] protected string paintingIconPath;// 贴纸图标 AA Key

    /// <summary>无参构造：序列化/反序列化用。</summary>
    protected FactoryComposedItemInfo() { }

    /// <summary>子类用：把合成堆叠键(id)、数量、类型写入基类 ItemInfo。</summary>
    protected FactoryComposedItemInfo(long id, int count, ItemType itemType) : base(id, count, itemType) { }

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

    /// <summary>售价 = 框架售价 + 贴纸售价（各自取 ItemData.Shop.Value）。子类可重写（如次品减半）。</summary>
    public virtual int Value => (InventoryManager.Instance.GetItemData(frameItemId)?.Shop?.Value ?? 0)
                              + (InventoryManager.Instance.GetItemData(paintingItemId)?.Shop?.Value ?? 0);

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
}
