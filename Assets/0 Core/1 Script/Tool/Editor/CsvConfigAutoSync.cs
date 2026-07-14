using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 「CSV → Config SO」通用导入器：服务于挂了 [CsvSyncedConfig] 的 SO（约定含 dataDict / csvTable 两个序列化字段）。
/// · 自动：检测到任意 .csv 被改动（重新导入）时，找到 csvTable 引用了它的 SO 自动导入；
/// · 手动：SO 资产 Inspector 右上角齿轮菜单「从 CSV 导入配置」。
/// 导入规则：Id 列作字典 key，其余列按「列名 = 字段名（忽略大小写）」反射填充数据类，
/// 支持 string/int/float/bool/long/double/Color/枚举，以及 Dictionary&lt;TKey,TValue&gt;（单元格写
/// "Key1:Value1;Key2:Value2"，TKey/TValue 为前述基础类型，用于一行多值消耗/奖励等场景）。新 Config 接入零编辑器代码。
/// </summary>
public class CsvConfigAutoSync : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        string[] csvs = imported.Where(p => p.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).ToArray();
        if(csvs.Length == 0)
            return;

        // 延迟到本轮导入结束后再执行，避免在导入回调里再触发 SetDirty/SaveAssets 的重入问题
        EditorApplication.delayCall += () => Sync(csvs);
    }

    static void Sync(string[] csvs)
    {
        var csvSet = new HashSet<string>(csvs, StringComparer.OrdinalIgnoreCase);
        foreach(Type type in TypeCache.GetTypesWithAttribute<CsvSyncedConfigAttribute>())
        {
            if(type.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(type))
                continue;

            foreach(string guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                Object csv = so ? GetCsvTable(so) : null;
                if(!csv || !csvSet.Contains(AssetDatabase.GetAssetPath(csv)))
                    continue;

                if(Import(so))
                    Debug.Log($"[CsvConfigAutoSync] 检测到 {AssetDatabase.GetAssetPath(csv)} 变更，已自动同步到 {assetPath}。");
            }
        }
    }

    [MenuItem("CONTEXT/ScriptableObject/从 CSV 导入配置", true)]
    static bool ImportMenuValidate(MenuCommand cmd)
        => cmd.context && cmd.context.GetType().IsDefined(typeof(CsvSyncedConfigAttribute), true);

    [MenuItem("CONTEXT/ScriptableObject/从 CSV 导入配置")]
    static void ImportMenu(MenuCommand cmd) => Import((ScriptableObject)cmd.context);

    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static Object GetCsvTable(ScriptableObject so)
        => so.GetType().GetField("csvTable", Flags)?.GetValue(so) as Object;

    /// <summary>把 csvTable 引用的表格导入进 so 的 dataDict（整体覆盖）。</summary>
    public static bool Import(ScriptableObject so)
    {
        string name = so.GetType().Name;
        Object csv = GetCsvTable(so);
        if(!csv)
        {
            Debug.LogError($"[{name}] csvTable 未指定表格引用，无法导入。");
            return false;
        }

        // 约定：第一个 Dictionary<string, TData> 序列化字段即为数据字典
        FieldInfo dictField = so.GetType().GetFields(Flags).FirstOrDefault(f =>
            f.FieldType.IsGenericType
            && f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
            && f.FieldType.GetGenericArguments()[0] == typeof(string));
        if(dictField == null)
        {
            Debug.LogError($"[{name}] 未找到 Dictionary<string, TData> 字段，无法导入。");
            return false;
        }

        CsvTool.Table t = CsvTool.ReadCsv(Path.GetFullPath(AssetDatabase.GetAssetPath(csv)));
        if(t == null)
            return false;

        Type dataType = dictField.FieldType.GetGenericArguments()[1];
        FieldInfo[] fields = dataType.GetFields(Flags);
        var dict = (IDictionary)Activator.CreateInstance(dictField.FieldType);

        foreach(string[] r in t.Rows)
        {
            string id = t.Get(r, "Id");
            if(string.IsNullOrEmpty(id))
                continue;

            object d = Activator.CreateInstance(dataType);
            foreach(FieldInfo f in fields)
            {
                if(!t.Header.ContainsKey(f.Name))   // Header 忽略大小写，nameKey 能匹配列 NameKey
                    continue;
                string cell = t.Get(r, f.Name);
                if(TryParseCell(f.FieldType, cell, out object v))
                    f.SetValue(d, v);
                else
                    Debug.LogError($"[{name}] 行「{id}」列「{f.Name}」：无法把“{cell}”解析为 {f.FieldType.Name}，保持默认值。");
            }
            dict[id] = d;
        }

        dictField.SetValue(so, dict);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{name}] 表格导入完成，共 {dict.Count} 条。");
        return true;
    }

    static bool TryParseCell(Type type, string s, out object value)
    {
        value = null;
        if(type == typeof(string)) { value = s; return true; }
        if(type == typeof(int))    { if(int.TryParse(s, out int v))      { value = v; return true; } return false; }
        if(type == typeof(float))  { if(float.TryParse(s, out float v))  { value = v; return true; } return false; }
        if(type == typeof(bool))   { if(bool.TryParse(s, out bool v))    { value = v; return true; } return false; }
        if(type == typeof(long))   { if(long.TryParse(s, out long v))    { value = v; return true; } return false; }
        if(type == typeof(double)) { if(double.TryParse(s, out double v)){ value = v; return true; } return false; }
        if(type == typeof(Color))  { if(ColorUtility.TryParseHtmlString(s, out Color v)) { value = v; return true; } return false; }
        if(type.IsEnum)
        {
            try { value = Enum.Parse(type, s, true); return true; }
            catch { return false; }
        }
        // Dictionary<TKey,TValue>：单元格写作 "Key1:Value1;Key2:Value2"（分号分项、冒号分key/value），
        // 支持多值消耗/奖励等场景（如 GameEnterPanelConfig 的 Consumes 列，一行同时配多种资源消耗）。
        if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];
            var dict = (IDictionary)Activator.CreateInstance(type);
            if(!string.IsNullOrEmpty(s))
            {
                foreach(string entry in s.Split(';'))
                {
                    if(string.IsNullOrWhiteSpace(entry))
                        continue;
                    string[] kv = entry.Split(':');
                    if(kv.Length != 2
                       || !TryParseCell(keyType, kv[0].Trim(), out object k)
                       || !TryParseCell(valueType, kv[1].Trim(), out object v))
                        return false;
                    dict[k] = v;
                }
            }
            value = dict;
            return true;
        }
        return false;
    }
}
