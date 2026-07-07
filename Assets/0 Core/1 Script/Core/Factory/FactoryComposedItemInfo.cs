using System;
using UnityEngine;
using XFramework;

/// <summary>
/// 「框架(FigureModel) + 贴纸(Painting)」组合而成的<b>运行时自描述</b>物品的公共基类：身份/展示信息（名称/描述/图标来源）
/// 全部随实例携带，不查 ItemConfig / ItemData。派生类：<see cref="FactoryMoldItemInfo"/>(生产资料/模具)、
/// <see cref="FactoryMerchandiseItemInfo"/>(加工产出的周边商品，正品/次品)。图标由 <see cref="FactoryMoldItemIcon"/>
/// 按框架+贴纸实时三层合成。
/// </summary>
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
    public string Name => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, FrameNameKey) + LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, PaintingNameKey);
    public string Desc => LanguageManager.Instance.GetLocalizedString(LocTableSet.InventoryItem, frameDescKey);
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
