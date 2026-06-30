using System;
using UnityEngine;

/// <summary>
/// 工厂「物料制作」产出的运行时物品数据（<see cref="ItemType.FactoryProductionMaterials"/>）。
/// 由「框架(<see cref="ItemType.FigureModel"/>，如空徽章/抱枕/立牌)」+「贴纸(<see cref="ItemType.Painting"/>，女主绘画)」组合而成，
/// 组合无穷多，无法预先在 ItemConfig 里逐一配置，故为<b>运行时物品</b>：其 <see cref="ItemData"/> 不在 ItemConfig 字典中，
/// 而是自描述地随物品实例（<see cref="ItemInfo"/> 的 <c>data</c>）携带——拿到实例即拿到全部信息，无需按 Id 反查配置。
///
/// 设计要点：
/// - 继承 <see cref="ItemData"/>，复用其全部展示字段（名称/图标/价值/品质等），并额外记录来源框架/贴纸的 Id、名称 Key、图标，便于富展示与回溯。
/// - <see cref="ComposeId"/> 由「框架 Id + 贴纸 Id」推出确定性复合 Id：相同组合得到相同 Id，从而在背包里自动堆叠为同一格（见 <see cref="PlayerBag.AddRuntimeItem"/>）。
/// - 入包走 <see cref="PlayerBag.AddRuntimeItem"/>（不查 ItemConfig），区别于普通物品的 <c>AddItem(id)</c>。
/// </summary>
[Serializable]
public class FactoryProductionMaterialsData : ItemData
{
    [SerializeField] long frameItemId;     // 来源框架(FigureModel)物品 Id
    [SerializeField] long stickerItemId;   // 来源贴纸(Painting)物品 Id
    [SerializeField] string frameNameKey;  // 框架名称多语言 Key（InventoryItem 表）
    [SerializeField] string stickerNameKey;// 贴纸名称多语言 Key（InventoryItem 表）
    [SerializeField] string frameIconPath; // 框架图标 AA Key
    [SerializeField] string stickerIconPath;// 贴纸图标 AA Key

    #region Get
    public long FrameItemId => frameItemId;
    public long StickerItemId => stickerItemId;
    public string FrameNameKey => frameNameKey;
    public string StickerNameKey => stickerNameKey;
    public string FrameIconPath => frameIconPath;
    public string StickerIconPath => stickerIconPath;
    #endregion

    // 运行时合成物 Id 基址：远高于配置表任何 Id（当前配置最大约 23 万），避免与配置物品冲突。
    const long RuntimeIdBase = 9_000_000_000_000L;
    // 复合 Id = 基址 + 框架Id*因子 + 贴纸Id。要求贴纸 Id < 因子（当前贴纸 Id≈20 万，安全），以保证不同组合不撞 Id。
    const long FrameIdFactor = 1_000_000L;

    /// <summary>由「框架 Id + 贴纸 Id」推出确定性复合 Id：相同组合 → 相同 Id（背包据此堆叠）。</summary>
    public static long ComposeId(long frameItemId, long stickerItemId)
        => RuntimeIdBase + frameItemId * FrameIdFactor + stickerItemId;

    /// <summary>该 Id 是否为本类运行时合成物（落在合成物 Id 区间）。</summary>
    public static bool IsRuntimeId(long id) => id >= RuntimeIdBase;

    /// <summary>
    /// 由所选框架与贴纸的物品配置合成一件生产资料。名称/图标默认取框架（产品形态），同时另存贴纸名称/图标供富展示；
    /// 价值取「框架价值 + 贴纸价值」，品质取两者较高者。最大堆叠 999，相同组合自动堆叠。
    /// </summary>
    public static FactoryProductionMaterialsData Create(ItemData frame, ItemData sticker)
    {
        if(frame == null || sticker == null)
        {
            Debug.LogError("FactoryProductionMaterialsData.Create: frame / sticker 为空");
            return null;
        }

        var d = new FactoryProductionMaterialsData
        {
            frameItemId     = frame.Id,
            stickerItemId   = sticker.Id,
            frameNameKey    = frame.Name,
            stickerNameKey  = sticker.Name,
            frameIconPath   = frame.IconPath,
            stickerIconPath = sticker.IconPath,
        };

        d.SetBaseData(
            id:                  ComposeId(frame.Id, sticker.Id),
            remark:              $"{frame.Remark}+{sticker.Remark}",   // 仅调试可读
            name:                frame.Name,                          // 名称 Key：以框架(产品形态)为主名
            desc:                frame.Desc,
            type:                ItemType.FactoryProductionMaterials,
            maxCount:            999,
            shop:                0,
            currencyType:        frame.CurrencyType,
            value:               frame.Value + sticker.Value,
            purchaseRestriction: null,
            iconPath:            frame.IconPath,                       // 主图：框架图标（贴纸图标另存）
            quality:             Mathf.Max(frame.Quality, sticker.Quality)
        );
        return d;
    }
}
