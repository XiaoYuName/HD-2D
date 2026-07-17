using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 工厂「升级设备」静态配置（对应策划案 2.1 流水线车间硬件）：6 种可升级设备
/// （伺服注塑机 / 温控涂装间 / 视觉分拣机 / 模具温控系统 / AI 视觉移印机 / 真空固化线），
/// 每种含分等级的升级费用与加成（良品率或生产量，二选一）。Id 作字典 key。
/// 走 CsvConfigAutoSync 管线：把 Data/Factory/FactoryEquipConfig.csv 拖到 csvTable 字段，
/// CSV 变更自动同步；也可在资产 Inspector 右上角齿轮菜单「从 CSV 导入配置」手动导入。
/// 备注：当前数值取自 FactoryEquipConfig.csv（占位/策划临时值），加成如何并入生产力公式（2.2）待数值确定后再接。
/// </summary>
[CreateAssetMenu(fileName = nameof(FactoryEquipConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(FactoryEquipConfig))]
[CsvSyncedConfig]
public class FactoryEquipConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<int, FactoryEquipData> dataDict;   // Id → 行数据，由 CSV 自动导入
    [SerializeField] Object csvTable;                              // 拖入 FactoryEquipConfig.csv；变更自动同步，齿轮菜单可手动导入

    public Dictionary<int, FactoryEquipData> DataDict => dataDict;
}

/// <summary>设备加成类型：良品率 / 生产量（每台设备二选一）。</summary>
public enum FactoryEquipBonusType
{
    None,
    /// <summary>良品率加成（百分比型）。</summary>
    Yield,
    /// <summary>生产量加成（数量型）。</summary>
    ProductionVolume,
}

/// <summary>
/// 单台流水线设备配置：分等级的升级费用与加成。等级语义：初始 0 级（尚无加成），满级 = MaxLevel（= 数组长度，默认 10）。
/// 加成 Bonus[level-1] = 处于该级（level≥1）的加成值，0 级为 0；费用 Cost[level] = 从该级升到下一级的花费。
/// 字段按「列名 = 字段名（忽略大小写）」由 CsvConfigAutoSync 反射填充；Cost/Bonus 单元格用「、」或「;」分隔。
/// </summary>
[Serializable]
public class FactoryEquipData
{
    [LabelText("Id")][SerializeField] int id;
    [LabelText("名称多语言Key")][SerializeField] string nameKey;
    [LabelText("备注(中文名)")][SerializeField] string remark;
    [LabelText("描述多语言Key")][SerializeField] string descKey;
    [LabelText("图标(AA Key)")][SerializeField] string iconKey;
    [LabelText("各级升级费用")][SerializeField] List<int> cost;
    [LabelText("加成类型")][SerializeField] FactoryEquipBonusType bonusType;
    [LabelText("各级加成值")][SerializeField] List<int> bonus;

    public int Id => id;
    /// <summary>名称多语言 Key（Factory 表，文案见 FactoryUpgradePanelLoc.csv）。</summary>
    public string NameKey => nameKey;
    public string Remark => remark;
    /// <summary>描述多语言 Key（Factory 表，文案见 FactoryUpgradePanelLoc.csv）。</summary>
    public string DescKey => descKey;
    /// <summary>图标 Addressable Key（当前格子未用，备用）。</summary>
    public string IconKey => iconKey;
    public FactoryEquipBonusType BonusType => bonusType;

    /// <summary>初始等级（所有设备默认从此级开始，尚未产生任何加成）。</summary>
    public const int BaseLevel = 0;

    /// <summary>最大等级（= 费用/加成数组的较大长度，至少 1）。</summary>
    public int MaxLevel
    {
        get
        {
            int c = cost != null ? cost.Count : 0;
            int b = bonus != null ? bonus.Count : 0;
            return Mathf.Max(1, Mathf.Max(c, b));
        }
    }

    /// <summary>从 <paramref name="level"/> 升到下一级的费用；满级 / 越界返回 0。</summary>
    public int GetUpgradeCost(int level)
    {
        if(cost == null || level < BaseLevel || level >= MaxLevel)
            return 0;
        return level >= 0 && level < cost.Count ? cost[level] : 0;
    }

    /// <summary>处于 <paramref name="level"/> 等级时的加成值（0 级尚未升级，无加成）。</summary>
    public int GetBonus(int level)
    {
        if(bonus == null || bonus.Count == 0 || level <= 0)
            return 0;
        int idx = Mathf.Clamp(level, 1, bonus.Count) - 1;
        return bonus[idx];
    }
}
