using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Text;
#endif

/// <summary>
/// 工厂「升级设备」静态配置（对应策划案 2.1 流水线车间硬件）：6 种可升级设备
/// （伺服注塑机 / 温控涂装间 / 视觉分拣机 / 模具温控系统 / AI 视觉移印机 / 真空固化线），
/// 每种含分等级的升级费用与加成（良品率或生产量，二选一）。Id 作字典 key。
/// 通过菜单 MiniGame/FactoryEquipmentConfig 创建资产；可从 Data/Factory/FactoryEquip.csv 一键导入。
/// 备注：当前数值取自 FactoryEquip.csv（占位/策划临时值），加成如何并入生产力公式（2.2）待数值确定后再接。
/// </summary>
[CreateAssetMenu(fileName = "FactoryEquipmentConfig", menuName = "MiniGame/FactoryEquipmentConfig")]
public class FactoryEquipmentConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<int, FactoryEquipData> dataDict;

    public Dictionary<int, FactoryEquipData> DataDict => dataDict;

#if UNITY_EDITOR
    const string DataFolder = "0 Core/1 Script/Data/Factory";
    const string CsvFile = "FactoryEquipConfig.csv";

    [InfoBox("从 " + DataFolder + "/" + CsvFile + " 导入：第1行字段名表头，2/3行类型/中文标签，第4行起数据；按列名取值（列序随意）。\n" +
             "费用/加成数组用「+」分隔（如 100+200+300）。良品率(Yield)与生产量(ProductionVolume)二者哪列有值即为该设备的加成类型；" +
             "名称(Name)/描述(Desc) 为多语言 Key（文案在 Data/Factory/FactoryUpgradePanel.csv），图标(Icon) 为 Addressable Key。", InfoMessageType.Info)]
    [Button("一键从表格导入设备配置", ButtonSizes.Large)]
    void ImportFromTable()
    {
        string path = Path.Combine(Application.dataPath, DataFolder, CsvFile);
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[FactoryEquipmentConfig] 未找到表格：{path}");
            return;
        }

        string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
        if(lines.Length < 4)
        {
            Debug.LogWarning($"[FactoryEquipmentConfig] 表格行数不足：{path}");
            return;
        }

        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] headerCells = lines[0].Split(',');
        for(int i = 0; i < headerCells.Length; i++)
        {
            string name = headerCells[i].Trim().TrimStart('﻿');
            if(!string.IsNullOrEmpty(name) && !header.ContainsKey(name))
                header[name] = i;
        }

        string Get(string[] row, string col) =>
            header.TryGetValue(col, out int idx) && idx < row.Length ? row[idx].Trim() : string.Empty;

        var dict = new Dictionary<int, FactoryEquipData>();
        for(int i = 3; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            string[] r = lines[i].Split(',');
            if(!int.TryParse(Get(r, "Id"), out int id))
                continue;

            int[] cost = ParseIntArray(Get(r, "Cost"));
            int[] yield = ParseIntArray(Get(r, "Yield"));
            int[] volume = ParseIntArray(Get(r, "ProductionVolume"));

            // 良品率 / 生产量二选一：哪列有值即为该设备的加成类型（以表为准，不与策划案分类强对齐）
            FactoryEquipBonusType bonusType;
            int[] bonus;
            if(yield.Length > 0)
            {
                bonusType = FactoryEquipBonusType.Yield;
                bonus = yield;
            }
            else if(volume.Length > 0)
            {
                bonusType = FactoryEquipBonusType.ProductionVolume;
                bonus = volume;
            }
            else
            {
                bonusType = FactoryEquipBonusType.None;
                bonus = Array.Empty<int>();
            }

            dict[id] = FactoryEquipData.Create(id, Get(r, "Name"), Get(r, "Remark"),
                Get(r, "Desc"), Get(r, "Icon"), cost, bonusType, bonus);
        }

        dataDict = dict;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryEquipmentConfig] 导入完成，共 {dict.Count} 条。");
    }

    // 解析「+」分隔的整型数组（"100+200+300"）；空串返回空数组
    static int[] ParseIntArray(string s)
    {
        if(string.IsNullOrWhiteSpace(s))
            return Array.Empty<int>();

        string[] parts = s.Split('+');
        List<int> result = new (parts.Length);
        foreach(string p in parts)
            if(int.TryParse(p.Trim(), out int v))
                result.Add(v);
        return result.ToArray();
    }
#endif
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
/// </summary>
[Serializable]
public class FactoryEquipData
{
    [LabelText("Id")][SerializeField] int id;
    [LabelText("名称多语言Key")][SerializeField] string nameKey;
    [LabelText("备注(中文名)")][SerializeField] string remark;
    [LabelText("描述多语言Key")][SerializeField] string descKey;
    [LabelText("图标(AA Key)")][SerializeField] string iconKey;
    [LabelText("各级升级费用")][SerializeField] int[] cost;
    [LabelText("加成类型")][SerializeField] FactoryEquipBonusType bonusType;
    [LabelText("各级加成值")][SerializeField] int[] bonus;

    public int Id => id;
    /// <summary>名称多语言 Key（Factory 表，文案见 FactoryUpgradePanel.csv）。</summary>
    public string NameKey => nameKey;
    public string Remark => remark;
    /// <summary>描述多语言 Key（Factory 表，文案见 FactoryUpgradePanel.csv）。</summary>
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
            int c = cost != null ? cost.Length : 0;
            int b = bonus != null ? bonus.Length : 0;
            return Mathf.Max(1, Mathf.Max(c, b));
        }
    }

    /// <summary>从 <paramref name="level"/> 升到下一级的费用；满级 / 越界返回 0。</summary>
    public int GetUpgradeCost(int level)
    {
        if(cost == null || level < BaseLevel || level >= MaxLevel)
            return 0;
        return level >= 0 && level < cost.Length ? cost[level] : 0;
    }

    /// <summary>处于 <paramref name="level"/> 等级时的加成值（0 级尚未升级，无加成）。</summary>
    public int GetBonus(int level)
    {
        if(bonus == null || bonus.Length == 0 || level <= 0)
            return 0;
        int idx = Mathf.Clamp(level, 1, bonus.Length) - 1;
        return bonus[idx];
    }

    public static FactoryEquipData Create(int id, string nameKey, string remark, string descKey, string iconKey,
        int[] cost, FactoryEquipBonusType bonusType, int[] bonus)
    {
        return new FactoryEquipData
        {
            id = id,
            nameKey = nameKey,
            remark = remark,
            descKey = descKey,
            iconKey = iconKey,
            cost = cost ?? Array.Empty<int>(),
            bonusType = bonusType,
            bonus = bonus ?? Array.Empty<int>(),
        };
    }
}
