#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// 一键把「工厂」目录下全部多语言 CSV 合并进 Factory 字符串表的便捷入口。
/// Factory 表集合不存在时自动创建（含工程内全部语言）；通用合并逻辑见 <see cref="LocalizationCsvMerger"/>。
/// </summary>
public static class FactoryLocalizationImporter
{
    const string CsvDir = "0 Core/1 Script/Data/Factory";
    const string CollectionDir = "Assets/AddressableAssets/Local/LocalizationTable/StringTable/Factory";

    [MenuItem("Tools/工厂小游戏/导入「工厂」全部多语言 → Factory 表")]
    public static void ImportFactory()
    {
        string dir = Path.Combine(Application.dataPath, CsvDir);
        if(!Directory.Exists(dir))
        {
            Debug.LogError($"[FactoryLoc] 未找到目录：{dir}");
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.csv", SearchOption.TopDirectoryOnly);
        if(files.Length == 0)
        {
            Debug.LogWarning($"[FactoryLoc] 目录下无 CSV：{dir}");
            return;
        }

        StringTableCollection col = LocalizationEditorSettings.GetStringTableCollection(LocalizeTableSet.Factory);
        if(col == null)
        {
            if(!Directory.Exists(CollectionDir))
                Directory.CreateDirectory(CollectionDir);
            col = LocalizationEditorSettings.CreateStringTableCollection(LocalizeTableSet.Factory, CollectionDir);
            Debug.Log($"[FactoryLoc] 已新建字符串表集合：{LocalizeTableSet.Factory}（{CollectionDir}）。");
        }

        int added = 0, updated = 0, smart = 0, merged = 0;
        foreach(string file in files)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);

            // 只处理本地化 CSV（含 Key 列），跳过数据配表（如 FactoryEquip.csv 表头为 Id,Name,...）
            if(!HasKeyColumn(text))
            {
                Debug.Log($"[FactoryLoc] 跳过非本地化表：{Path.GetFileName(file)}（无 Key 列）。");
                continue;
            }

            LocalizationCsvMerger.Result r = LocalizationCsvMerger.Merge(text, col, overwrite: true);
            if(!r.ok)
            {
                Debug.LogError($"[FactoryLoc] {Path.GetFileName(file)} 导入失败：{r.message}");
                continue;
            }
            merged++;
            added += r.added;
            updated += r.updated;
            smart += r.smartMarked;
        }
        Debug.Log($"[FactoryLoc] 导入完成（合并 {merged} 个本地化 CSV → {LocalizeTableSet.Factory}）：新增 Key {added}，更新 Key {updated}，自动标记 Smart {smart}。");
    }

    // 首行是否含 Key 列（区分本地化表与数据配表）
    static bool HasKeyColumn(string csvText)
    {
        int end = csvText.IndexOf('\n');
        string header = end >= 0 ? csvText.Substring(0, end) : csvText;
        foreach(string cell in header.Split(','))
            if(cell.Trim().TrimStart('﻿').Trim('"').Equals("Key", System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
#endif
