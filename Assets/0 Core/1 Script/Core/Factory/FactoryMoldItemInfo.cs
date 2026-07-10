using System;
using XFramework;

[Serializable]
public class FactoryMoldItemInfo : FactoryComposedItemInfo
{
    /// <summary>无参构造：序列化/反序列化用。</summary>
    public FactoryMoldItemInfo() { }

    /// <summary>构造：Id/Count 交给基类，MaterialType 固定为生产资料。创建请走 <see cref="FactoryComposedItemInfoEt.CreateMoldItem"/>。</summary>
    public FactoryMoldItemInfo(long id, int count) : base(id, count)
    {
        MaterialType = ItemMaterialType.FactoryProductionMaterials;
    }
}
