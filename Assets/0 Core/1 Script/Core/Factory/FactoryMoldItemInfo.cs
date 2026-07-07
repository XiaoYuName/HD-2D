using System;
using XFramework;
/// <summary>
/// 工厂「物料制作」合成产物的<b>运行时自描述</b>物品：由「框架(FigureModel) + 贴纸(Painting)」现场合成，
/// 不再预生成并写入 ItemConfig，也不从 ItemData 查询。身份/展示（名称/描述/图标/售价/类型）全部随实例携带，
/// Id 由 <see cref="ComposeId"/> 按框架+贴纸推出（背包据此堆叠）。入包走 <see cref="InventoryManager.AddRuntimeItem"/>。
/// 图标另由 <see cref="FactoryMoldItemIcon"/> 按框架+贴纸实时三层合成，本类 <see cref="IconPath"/> 仅作无合成图时的回退。
/// </summary>
[Serializable]
public class FactoryMoldItemInfo : FactoryComposedItemInfo
{
    #region 重写（自描述，不读 config data）
    // Id 是由「框架+贴纸」推出的合成<b>堆叠键</b>，不是配置表/物品数据库里的 Id（本物品在数据库查不到）。
    // 因此基类继承来的 id 字段对本类无意义、恒不使用——身份与堆叠完全由此重写值决定。
    public override long Id => ComposeId(frameItemId, paintingItemId);
    public override ItemType Type => ItemType.FactoryProductionMaterials;
    public override int Value => InventoryManager.Instance.GetItemData(frameItemId).Value + InventoryManager.Instance.GetItemData(paintingItemId).Value;
    #endregion
    public static long ComposeId(long frameItemId, long stickerItemId)
        => FactoryMoldSynthesis.GetResultId(frameItemId, stickerItemId);

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
        };
        info.AddCount(count);   // 基类 count 从 0 起，AddCount 设为目标数量
        return info;
    }
}
