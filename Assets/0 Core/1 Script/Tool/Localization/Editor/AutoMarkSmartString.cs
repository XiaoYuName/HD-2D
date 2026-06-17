using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

/// <summary>
/// 批量标记 Smart String。
/// 成因：导入 CSV / Google Sheet 时 Unity Localization 不会根据文本里的 {} 占位符
/// 自动开启条目的 IsSmart 标志（标准列映射不携带该元数据），导致含 {0}、{gold} 等
/// 占位符的条目导入后仍是普通字符串，需要逐个手动勾选 Smart。
/// 处理：扫描所有 String 表集合，凡是文本里匹配到 {xxx} 形式占位符的条目就设 IsSmart = true。
/// 非破坏性，可重复执行；导入完跑一次即可。
/// </summary>
public static class AutoMarkSmartString
{
    // 匹配 {占位符}：花括号内有非空、不含花括号的内容，跳过字面 {} 空花括号。
    static readonly Regex SmartPattern = new Regex(@"\{[^{}]+\}", RegexOptions.Compiled);

    [MenuItem("Tools/Localization/自动标记 Smart String")]
    public static void MarkAll()
    {
        int count = 0;
        foreach (var collection in LocalizationEditorSettings.GetStringTableCollections())
        {
            foreach (var table in collection.StringTables)
            {
                foreach (var entry in table.Values)
                {
                    if (!entry.IsSmart && !string.IsNullOrEmpty(entry.Value)
                        && SmartPattern.IsMatch(entry.Value))
                    {
                        entry.IsSmart = true;
                        count++;
                    }
                }
                EditorUtility.SetDirty(table);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMarkSmartString] 已标记 {count} 个 Smart String 条目。");
    }
}
