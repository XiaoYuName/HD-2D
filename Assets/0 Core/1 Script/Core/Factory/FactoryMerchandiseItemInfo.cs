using System;
using XFramework;

// ===== 新实现（基于新的 ItemInfo 基类）=====
[Serializable]
public class FactoryMerchandiseItemInfo : FactoryComposedItemInfo
{
    /// <summary>产出品级：正品 / 次品。</summary>
    public enum QualityGrade
    {
        Qualified = 0,   // 正品
        Defective = 1,   // 次品
    }

    public QualityGrade Grade;

    // 正品/次品 Id 偏移：结果 Id = 生产资料 Id(贴纸Id×1000000+框架Id) + 偏移，均落在同一贴纸的 100 万号段内，互不冲突
    public const long QualifiedIdOffset = 100000;
    public const long DefectiveIdOffset = 200000;

    /// <summary>无参构造：序列化/反序列化用。</summary>
    public FactoryMerchandiseItemInfo() { }

    /// <summary>构造：Id/Count 交给基类，Grade 直接写入，MaterialType 固定为周边货物。创建请走 <see cref="FactoryComposedItemInfoEt.CreateMerchandiseItem"/>。</summary>
    public FactoryMerchandiseItemInfo(long id, int count, QualityGrade grade) : base(id, count)
    {
        Grade = grade;
        MaterialType = ItemMaterialType.Merchandise;
    }

    // 是否次品/名称(次品后缀)/售价(次品减半)/合成 Id/构造 均已迁至 FactoryComposedItemInfoEt 扩展方法（本类禁止 Create 静态工厂与 "=>" 成员）。
}
