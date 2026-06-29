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

    [InfoBox("从 " + DataFolder + "/" + CsvFile + " 导入：第1行字段名表头，2/3行类型/中文标签，第4行起数据；按列名取值（列序随意）。\n" +
             "仅取「Id(=物品Id) + CraftCount」；名称/描述/图标/单价已改为按 Id 实时取自 ItemData，表中相应列为遗留，导入时忽略。", InfoMessageType.Info)]
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

            // 名称/描述/图标/单价统一取自 Id 对应的 ItemData，CSV 仅取「物品 Id + 单批数量」（其余列为历史遗留，忽略）。
            // 故此处 Id 须为有效的 ItemConfig 物品 Id；取不到物品配置则跳过。
            ItemData item = ItemManager.St?.GetItemData(id);
            if(item == null)
                continue;
            int.TryParse(Get(r, "CraftCount"), out int craftCount);
            dict[id] = FactoryProductData.Create(item, craftCount);
        }
        dataDict = dict;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryProductConfig] 导入完成，共 {dict.Count} 条。");
    }
#endif
}

/// <summary>
/// 工厂产品 = 背包物品(<see cref="ItemData"/>) + 工厂专有的单批数量。
/// 名称 / 描述 / 图标 / 单价均取自所属 <see cref="ItemData"/>（创建时缓存，不再冗余存储，避免与物品配置脱节）；
/// 仅「单批数量」是工厂独有的经济数值，单独保存。
/// </summary>
[Serializable]
public class FactoryProductData
{
    [SerializeField] long itemId;      // 对应背包物品配置 Id：加工完成后据此发放进背包
    [SerializeField] int craftCount;   // 单批数量（件）——ItemData 无此字段，工厂独有

    [System.NonSerialized] ItemData data;   // 创建时缓存的物品配置

    public long ItemId => itemId;
    public int CraftCount => Mathf.Max(1, craftCount);

    ItemData Data => data;

    // 名称 / 描述为物品多语言 Key（<see cref="LocalizeTableSet.InventoryItem"/> 表），图标为 AA Key，单价取 <see cref="ItemData.Value"/>
    public string NameKey => Data.Name;
    public string DescKey => Data.Desc;
    public string IconPath => Data.IconPath;
    public int UnitPrice => Data.Value;

    /// <summary>本产品单批总花费 = 单价 × 数量。</summary>
    public int TotalCost => UnitPrice * CraftCount;

    /// <summary>手办产品默认单批数量：ItemConfig 物品无「单批数量」字段，经济数值待策划确定，暂用占位常量。</summary>
    public const int DefaultCraftCount = 50;

    /// <summary>
    /// 次品物品 Id 相对正品的偏移：次品 Id = 正品 Id + 此值。次品售价为正品一半，仅由工厂加工按完成率产出，
    /// 不作为可加工产品（<see cref="FactoryMainPanel"/> 构建产品列表时会排除）。次品物品须已在 ItemConfig 中按此 Id 配置。
    /// </summary>
    public const long DefectiveIdOffset = 1000;

    /// <summary>由正品 Id 取其次品 Id（约定：正品 Id + <see cref="DefectiveIdOffset"/>）。</summary>
    public static long ToDefectiveId(long qualifiedId) => qualifiedId + DefectiveIdOffset;

    /// <summary>该 Id 是否为某正品的次品变体（其「Id - 偏移」在配置中存在且同为手办）。用于从可加工产品列表中排除次品。</summary>
    public static bool IsDefectiveId(long id)
    {
        ItemData baseItem = ItemManager.St != null ? ItemManager.St.GetItemData(id - DefectiveIdOffset) : null;
        return baseItem != null && baseItem.Type == ItemType.Figure;
    }

    /// <summary>由 ItemConfig 物品构建工厂产品：记录 Id 与单批数量，并缓存物品配置供展示取值。</summary>
    public static FactoryProductData Create(ItemData item, int craftCount = DefaultCraftCount)
    {
        return new FactoryProductData
        {
            itemId = item.Id,
            craftCount = craftCount,
            data = item,
        };
    }
}
