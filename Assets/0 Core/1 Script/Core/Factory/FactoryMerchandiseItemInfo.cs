using System;
using XFramework;

[Serializable]
public class FactoryMerchandiseItemInfo : FactoryComposedItemInfo
{
    // 结果 Id = 生产资料 Id(贴纸Id×1000000+框架Id) + 偏移，落在同一贴纸的 100 万号段内，与生产资料/其它物品互不冲突
    public const long IdOffset = 100000;

    /// <summary>无参构造：序列化/反序列化用。</summary>
    public FactoryMerchandiseItemInfo() { }

    /// <summary>构造：Id/Count 交给基类，MaterialType 固定为周边货物。创建请走 <see cref="FactoryComposedItemInfoEt.CreateMerchandiseItem"/>。</summary>
    public FactoryMerchandiseItemInfo(long id, int count) : base(id, count)
    {
        MaterialType = ItemMaterialType.Merchandise;
    }

    public FactoryMerchandiseItemInfo(long id, int count, long frameItemId, long paintingItemId) : base(id, count)
    {
        MaterialType = ItemMaterialType.Merchandise;
        FrameItemId = frameItemId;
        PaintingItemId = paintingItemId;
    }

    // 名称/售价/合成 Id/构造 均已迁至 FactoryComposedItemInfoEt 扩展方法（本类禁止 Create 静态工厂与 "=>" 成员）。
}
