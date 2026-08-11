using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

[InitializeOnLoad]
public static class LocEditorBridgeSetup
{
    static LocEditorBridgeSetup() => LocEditorBridge.SetHandlers(GetTables, GetPreviewText, EnsureKey);

    static IEnumerable<string> GetTables() => LocalizationEditorSettings
        .GetStringTableCollections()
        .Where(c => c != null)
        .Select(c => c.TableCollectionName)
        .Distinct()
        .OrderBy(x => x);

    static string GetPreviewText(string tableName, string key)
    {
        if(string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key))
            return string.Empty;

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if(collection == null)
            return "未找到本地化表";

        // 多语言表顺序不固定，优先取简体中文表做预览
        IEnumerable<StringTable> tables = collection.StringTables;
        StringTable table = tables.FirstOrDefault(t => t.LocaleIdentifier.Code.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault(t => t.LocaleIdentifier.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault();
        if(table == null)
            return "当前表没有语言内容";

        StringTableEntry entry = table.GetEntry(key);
        return entry == null ? "未找到 Key 对应文本" : entry.LocalizedValue;
    }

    static void EnsureKey(string tableName, string key, string zhDefault)
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if(collection == null)
        {
            Debug.LogWarning($"[LocalizeTool] 未找到字符串表 {tableName}");
            return;
        }

        SharedTableData shared = collection.SharedData;
        if(!shared.Contains(key))
            shared.AddKey(key);

        if(!string.IsNullOrEmpty(zhDefault) && collection.GetTable("zh-CN") is StringTable zhTable)
        {
            StringTableEntry entry = zhTable.GetEntry(key);
            if(entry == null || string.IsNullOrEmpty(entry.Value))
                zhTable.AddEntry(key, zhDefault);
            EditorUtility.SetDirty(zhTable);
        }
        EditorUtility.SetDirty(shared);
    }
}