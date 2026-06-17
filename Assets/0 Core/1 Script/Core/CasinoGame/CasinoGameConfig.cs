using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using System.IO;
using System.Text;
#endif

[CreateAssetMenu(fileName = "CasinoGameConfig", menuName = "MiniGame/CasinoGameConfig")]
public class CasinoGameConfig: SerializedScriptableObject
{
    [SerializeField] Dictionary<int, CasinoGameItemData> dataDict;

    public Dictionary<int, CasinoGameItemData> DataDict => dataDict;

#if UNITY_EDITOR
    // ====================== 表格导入 ======================
    // 表格目录（相对 Assets）与文件名。CSV 约定：第1行字段名表头，2/3行为类型/中文标签，从第4行起为数据。
    const string DataFolder = "0 Core/1 Script/Data/CasinoGame";
    const string CsvFile = "赌场游戏配置.csv";

    [InfoBox("从 " + DataFolder + "/" + CsvFile + " 读取并覆盖配置：Id 作为字典 key，按列名取值（列序随意），支持 UTF-8 BOM。", InfoMessageType.Info)]
    [Button("一键从表格导入配置", ButtonSizes.Large)]
    void ImportFromTable()
    {
        string path = Path.Combine(Application.dataPath, DataFolder, CsvFile);
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;

        var dict = new Dictionary<int, CasinoGameItemData>();
        foreach(string[] r in t.Rows)
        {
            if(!int.TryParse(t.Get(r, "Id"), out int id))
            {
                string raw = t.Get(r, "Id");
                if(!string.IsNullOrWhiteSpace(raw))
                    Debug.LogWarning($"[CasinoGameConfig] 非法ID（需为整数）：{raw}，已跳过。");
                continue;
            }
            int.TryParse(t.Get(r, "ConsumeSp"), out int consumeSp);
            int.TryParse(t.Get(r, "ConsumeCoin"), out int consumeCoin);
            dict[id] = CasinoGameItemData.Create(
                name: t.Get(r, "Name"),
                remark: t.Get(r, "Remark"),
                desc: t.Get(r, "Desc"),
                iconResPath: t.Get(r, "Icon"),
                consumeSp: consumeSp,
                consumeCoin: consumeCoin,
                panelId: t.Get(r, "PanelId"));
        }
        dataDict = dict;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[CasinoGameConfig] 表格导入完成，共 {dict.Count} 条。");
    }

    // 一张解析后的 CSV：按列名（首行表头）取值，避免依赖列顺序
    class CsvTable
    {
        public Dictionary<string, int> Header;   // 列名 -> 列索引
        public List<string[]> Rows;              // 数据行（从第 4 行起）

        // 按列名取值；列不存在或越界返回空串
        public string Get(string[] row, string colName)
        {
            return (Header.TryGetValue(colName, out int idx) && row != null && idx < row.Length)
                ? row[idx].Trim()
                : string.Empty;
        }
    }

    // 读取 CSV：第 1 行为字段名表头，2/3 行为类型/中文标签，从第 4 行起为数据。支持 UTF-8 BOM。
    static CsvTable ReadCsv(string path)
    {
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[CasinoGameConfig] 未找到表格：{path}");
            return null;
        }
        string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
        if(lines.Length < 4)
        {
            Debug.LogWarning($"[CasinoGameConfig] 表格行数不足：{path}");
            return null;
        }

        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] headerCells = lines[0].Split(',');
        for(int i = 0; i < headerCells.Length; i++)
        {
            string name = headerCells[i].Trim().TrimStart('﻿');   // 去掉首格可能残留的 BOM
            if(!string.IsNullOrEmpty(name) && !header.ContainsKey(name))
                header[name] = i;
        }

        var rows = new List<string[]>();
        for(int i = 3; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            rows.Add(lines[i].Split(','));
        }
        return new CsvTable { Header = header, Rows = rows };
    }
#endif
}

[Serializable]
public class CasinoGameItemData
{
    [SerializeField] string name;
    [SerializeField] string remark;
    [SerializeField] string desc;
    [SerializeField] string iconResPath;
    [SerializeField] int consumeSp;
    [SerializeField] int consumeCoin;
    [SerializeField] string panelId;

    public string Name => name;
    public string Remark => remark;
    public string Desc => desc;
    // 图标只暴露 AA Key，由 UI 层通过 Image.SetIcon 异步加载，配置本身不引用 Sprite
    public string IconResPath => iconResPath;
    public int ConsumeSp => consumeSp;
    public int ConsumeCoin => consumeCoin;
    public string PanelId => panelId;

    public static CasinoGameItemData Create(string name, string remark, string desc, string iconResPath,
        int consumeSp, int consumeCoin, string panelId)
    {
        return new CasinoGameItemData
        {
            name = name,
            remark = remark,
            desc = desc,
            iconResPath = iconResPath,
            consumeSp = consumeSp,
            consumeCoin = consumeCoin,
            panelId = panelId,
        };
    }
}
