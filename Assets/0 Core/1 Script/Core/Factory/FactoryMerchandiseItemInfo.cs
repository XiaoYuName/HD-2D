using System;
using UnityEngine;

/// <summary>
/// 工厂加工（传送带下压）产出的<b>运行时自描述</b>周边商品：由生产资料(<see cref="FactoryMoldItemInfo"/>：框架+贴纸)
/// 加工产出，不再预生成并写入 ItemConfig，也不从 ItemData 查询。身份/展示（名称/描述/图标）随实例携带，随
/// <see cref="Grade"/>（正品/次品）变化售价（次品为正品一半）。Id 由 <see cref="ComposeId"/> 按框架+贴纸+品级推出
/// （背包据此堆叠，正品/次品分开堆叠）。入包走 <see cref="InventoryManager.AddRuntimeItem"/>。
/// </summary>
[Serializable]
public class FactoryMerchandiseItemInfo : FactoryComposedItemInfo
{
    /// <summary>产出品级：正品 / 次品。</summary>
    public enum QualityGrade
    {
        Qualified = 0,   // 正品
        Defective = 1,   // 次品
    }

    [SerializeField] QualityGrade grade;

    public QualityGrade Grade => grade;
    public bool IsDefective => grade == QualityGrade.Defective;

    #region 重写（自描述，不读 config data）
    // Id 是由「框架+贴纸+品级」推出的合成<b>堆叠键</b>，不是配置表/物品数据库里的 Id（本物品在数据库查不到）。
    public override long Id => ComposeId(frameItemId, paintingItemId, grade);
    public override ItemType Type => ItemType.Merchandise;
    // 售价：框架+贴纸价值的平均值（同 FactoryMoldItemInfo，直接截断），次品再减半（整除，不四舍五入）
    public override int Value
    {
        get
        {
            int baseValue = InventoryManager.Instance.GetItemData(frameItemId).Value + InventoryManager.Instance.GetItemData(paintingItemId).Value;
            return IsDefective ? baseValue / 2 : baseValue;
        }
    }
    #endregion

    // 正品/次品 Id 偏移：结果 Id = 生产资料 Id(贴纸Id×1000000+框架Id) + 偏移，均落在同一贴纸的 100 万号段内，互不冲突
    public const long QualifiedIdOffset = 100000;
    public const long DefectiveIdOffset = 200000;

    public static long ComposeId(long frameItemId, long paintingItemId, QualityGrade grade)
        => FactoryMoldSynthesis.GetResultId(frameItemId, paintingItemId) + (grade == QualityGrade.Defective ? DefectiveIdOffset : QualifiedIdOffset);

    /// <summary>由来源生产资料（框架+贴纸身份）与品级构建一份周边商品，数量为 <paramref name="count"/>。</summary>
    public static FactoryMerchandiseItemInfo Create(FactoryComposedItemInfo material, QualityGrade grade, int count)
    {
        var info = new FactoryMerchandiseItemInfo
        {
            frameItemId = material.FrameItemId,
            paintingItemId = material.PaintingItemId,
            frameNameKey = material.FrameNameKey,
            frameDescKey = material.FrameDescKey,
            paintingNameKey = material.PaintingNameKey,
            frameIconPath = material.FrameIconPath,
            paintingIconPath = material.PaintingIconPath,
            grade = grade,
        };
        info.AddCount(count);
        return info;
    }
}
