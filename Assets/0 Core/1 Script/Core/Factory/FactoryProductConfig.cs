using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using System.Text;
#endif

/// <summary>
/// 工厂可加工的「周边产品种类」静态配置：Id 作字典 key，名称 / 描述为 Factory 多语言表的 Key。
/// 通过菜单 MiniGame/FactoryProductConfig 创建资产；可从 Data/Factory/FactoryProductConfig.csv 一键导入。
/// 备注：单价 / 单批数量等经济数值依赖策划案 4.3 成本售价公式，当前为占位值，待数值确定后覆盖。
/// </summary>
[CreateAssetMenu(fileName = "FactoryProductConfig", menuName = "MiniGame/FactoryProductConfig")]
public class FactoryProductConfig : SerializedScriptableObject
{
    [SerializeField] Dictionary<int, FactoryProductData> dataDict = new ();

    public Dictionary<int, FactoryProductData> DataDict => dataDict;

#if UNITY_EDITOR
    const string DataFolder = "0 Core/1 Script/Data/Factory";
    const string CsvFile = "FactoryProductConfig.csv";

    [InfoBox("从 " + DataFolder + "/" + CsvFile + " 导入：第1行字段名表头，2/3行类型/中文标签，第4行起数据；按列名取值（列序随意）。", InfoMessageType.Info)]
    [Button("一键从表格导入产品配置", ButtonSizes.Large)]
    void ImportFromTable()
    {
        string path = Path.Combine(Application.dataPath, DataFolder, CsvFile);
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[FactoryProductConfig] 未找到表格：{path}");
            return;
        }

        string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
        if(lines.Length < 4)
        {
            Debug.LogWarning($"[FactoryProductConfig] 表格行数不足：{path}");
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

        var dict = new Dictionary<int, FactoryProductData>();
        for(int i = 3; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            string[] r = lines[i].Split(',');
            if(!int.TryParse(Get(r, "Id"), out int id))
                continue;

            int.TryParse(Get(r, "UnitPrice"), out int unitPrice);
            int.TryParse(Get(r, "CraftCount"), out int craftCount);
            dict[id] = FactoryProductData.Create(
                nameKey: Get(r, "Name"),
                descKey: Get(r, "Desc"),
                iconPath: Get(r, "Icon"),
                unitPrice: unitPrice,
                craftCount: craftCount);
        }
        dataDict = dict;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryProductConfig] 导入完成，共 {dict.Count} 条。");
    }
#endif
}

[Serializable]
public class FactoryProductData
{
    [SerializeField] string nameKey;
    [SerializeField] string descKey;
    [SerializeField] string iconPath;
    [SerializeField] int unitPrice;    // 单价（¥/个）
    [SerializeField] int craftCount;   // 单批数量（件）

    public string NameKey => nameKey;
    public string DescKey => descKey;
    public string IconPath => iconPath;
    public int UnitPrice => unitPrice;
    public int CraftCount => Mathf.Max(1, craftCount);

    /// <summary>本产品单批总花费 = 单价 × 数量。</summary>
    public int TotalCost => unitPrice * CraftCount;

    public static FactoryProductData Create(string nameKey, string descKey, string iconPath, int unitPrice, int craftCount)
    {
        return new FactoryProductData
        {
            nameKey = nameKey,
            descKey = descKey,
            iconPath = iconPath,
            unitPrice = unitPrice,
            craftCount = craftCount,
        };
    }
}
