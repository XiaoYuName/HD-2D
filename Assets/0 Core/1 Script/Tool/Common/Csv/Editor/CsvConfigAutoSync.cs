using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 「CSV → Config SO」通用导入器：服务于挂了 [CsvSyncedConfig] 的 SO（约定含 dataDict / csvTable 两个序列化字段）。
/// · 自动：检测到任意 .csv 被改动（重新导入）时，找到 csvTable 引用了它的 SO 自动导入；
/// · 手动：SO 资产 Inspector 右上角齿轮菜单「从 CSV 导入配置」。
/// 导入规则：Id 列作字典 key（key 类型支持 string/long/int 等基础类型），其余列按「列名 = 字段名（忽略大小写）」
/// 反射填充数据类，支持 string/int/float/bool/long/double/Color/枚举、List&lt;T&gt;（单元格写 "A、B、C"，顿号或分号分隔），
/// 以及 Dictionary&lt;TKey,TValue&gt;（单元格写 "Key1:Value1;Key2:Value2"，TKey/TValue 为前述基础类型，
/// 用于一行多值消耗/奖励等场景）。新 Config 接入零编辑器代码。
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

        // 约定：第一个 Dictionary<TKey, TData> 序列化字段即为数据字典（TKey 为 string/long/int 等可解析基础类型）
        FieldInfo dictField = so.GetType().GetFields(Flags).FirstOrDefault(f =>
            f.FieldType.IsGenericType
            && f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
        if(dictField == null)
        {
            Debug.LogError($"[{name}] 未找到 Dictionary<TKey, TData> 字段，无法导入。");
            return false;
        }

        CsvTool.Table t = CsvTool.ReadCsv(Path.GetFullPath(AssetDatabase.GetAssetPath(csv)));
        if(t == null)
            return false;

        // 表头必须是字段名行。策划从 Excel/WPS 另存时常在最前面多出 Column1,Column2... 一行标题，
        // 会让整表读不到 Id 列而静默导入 0 条，这里直接拦下并提示。
        if(!t.Header.ContainsKey("Id"))
        {
            Debug.LogError($"[{name}] 表头没有 Id 列，当前首行为「{string.Join(",", t.Header.OrderBy(kv => kv.Value).Select(kv => kv.Key).Take(5))}...」。"
                           + "CSV 首行必须是字段名，请删掉多余的标题行（如 Column1,Column2...）后重试。");
            return false;
        }

        Type keyType = dictField.FieldType.GetGenericArguments()[0];
        Type dataType = dictField.FieldType.GetGenericArguments()[1];
        FieldInfo[] fields = dataType.GetFields(Flags);
        var dict = (IDictionary)Activator.CreateInstance(dictField.FieldType);

        foreach(string[] r in t.Rows)
        {
            string id = t.Get(r, "Id");
            if(string.IsNullOrEmpty(id))
                continue;
            if(!TryParseCell(keyType, id, t.GetTypeDeclaration("Id"), out object key))
            {
                Debug.LogError($"[{name}] Id「{id}」：无法解析为字典 key 类型 {keyType.Name}，跳过该行。");
                continue;
            }

            object d = Activator.CreateInstance(dataType);
            foreach(FieldInfo f in fields)
            {
                if(!t.Header.ContainsKey(f.Name))   // Header 忽略大小写，nameKey 能匹配列 NameKey
                    continue;
                string cell = t.Get(r, f.Name);
                if(TryParseCell(f.FieldType, cell, t.GetTypeDeclaration(f.Name), out object v))
                    f.SetValue(d, v);
                else
                    Debug.LogError($"[{name}] 行「{id}」列「{f.Name}」：无法把“{cell}”解析为 {f.FieldType.Name}，保持默认值。");
            }
            dict[key] = d;
        }

        // 一条都没解析出来多半是表结构坏了，别用空字典覆盖掉 SO 里的已有数据
        if(dict.Count == 0)
        {
            Debug.LogError($"[{name}] 表格解析出 0 条数据，已放弃导入（保留 SO 原有内容）。请检查 CSV 表头与数据行是否对齐：{AssetDatabase.GetAssetPath(csv)}");
            return false;
        }

        dictField.SetValue(so, dict);
        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{name}] 表格导入完成，共 {dict.Count} 条。");
        return true;
    }

    static bool TryParseCell(Type type, string s, CsvTypeDeclaration declaration, out object value)
    {
        value = null;
        Type nullableType = Nullable.GetUnderlyingType(type);
        if(nullableType != null)
        {
            if(string.IsNullOrWhiteSpace(s)) { value = null; return true; }
            return TryParseCell(nullableType, s, declaration, out value);
        }

        if(IsScalarType(type))
            return TryParseScalar(type, s, out value);

        if(type.IsArray)
            return TryParseArray(type, s, declaration, out value);

        // List<T>：默认兼容顿号/分号/加号，也可在类型后用 #sep= 自定义。
        // 用于多值列（如 FishConfig 的 AllowedRods 多根鱼竿、TimeSlots 多个时段，FactoryEquip 的各级费用/加成）。
        if(IsGenericType(type, typeof(List<>)))
            return TryParseList(type, s, declaration, out value);

        // Dictionary<TKey,TValue>：单元格写作 "Key1:Value1;Key2:Value2"（分号分项、冒号分key/value），
        // 支持多值消耗/奖励等场景（如 GameEnterPanelConfig 的 Consumes 列，一行同时配多种资源消耗）。
        if(IsGenericType(type, typeof(Dictionary<,>)))
            return TryParseDictionary(type, s, declaration, out value);

        // 自定义复合类型：SomeType#sep=+ 会按声明顺序把各段填入实例字段。
        // 这可直接覆盖 Luban bean 一类的 Table+Value、Vector 等小型值对象。
        if(!string.IsNullOrEmpty(declaration?.Separator))
            return TryParseComposite(type, s, declaration.Separator, out value);
        return false;
    }

    static bool IsScalarType(Type type)
        => type == typeof(string)
           || type == typeof(int)
           || type == typeof(float)
           || type == typeof(bool)
           || type == typeof(long)
           || type == typeof(double)
           || type == typeof(Color)
           || type.IsEnum;

    static bool TryParseScalar(Type type, string text, out object value)
    {
        value = null;
        if(type == typeof(string)) { value = text; return true; }
        if(type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)) { value = intValue; return true; }
        if(type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue)) { value = floatValue; return true; }
        if(type == typeof(bool) && bool.TryParse(text, out bool boolValue)) { value = boolValue; return true; }
        if(type == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue)) { value = longValue; return true; }
        if(type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue)) { value = doubleValue; return true; }
        if(type == typeof(Color) && ColorUtility.TryParseHtmlString(text, out Color colorValue)) { value = colorValue; return true; }
        return type.IsEnum && Enum.TryParse(type, text, true, out value);
    }

    static bool IsGenericType(Type type, Type genericType)
        => type.IsGenericType && type.GetGenericTypeDefinition() == genericType;

    static bool TryParseArray(Type type, string text, CsvTypeDeclaration declaration, out object value)
    {
        value = null;
        Type elementType = type.GetElementType();
        string[] cells = SplitValues(text, declaration?.Separator);
        Array array = Array.CreateInstance(elementType, cells.Length);
        for(int i = 0; i < cells.Length; i++)
        {
            if(!TryParseCell(elementType, cells[i].Trim(), null, out object item))
                return false;
            array.SetValue(item, i);
        }
        value = array;
        return true;
    }

    static bool TryParseList(Type type, string text, CsvTypeDeclaration declaration, out object value)
    {
        value = null;
        Type elementType = type.GetGenericArguments()[0];
        var list = (IList)Activator.CreateInstance(type);
        foreach(string item in SplitValues(text, declaration?.Separator))
        {
            if(string.IsNullOrWhiteSpace(item))
                continue;
            if(!TryParseCell(elementType, item.Trim(), null, out object itemValue))
                return false;
            list.Add(itemValue);
        }
        value = list;
        return true;
    }

    static bool TryParseDictionary(Type type, string text, CsvTypeDeclaration declaration, out object value)
    {
        value = null;
        Type[] genericTypes = type.GetGenericArguments();
        Type keyType = genericTypes[0];
        Type valueType = genericTypes[1];
        var dictionary = (IDictionary)Activator.CreateInstance(type);
        string entrySeparator = string.IsNullOrEmpty(declaration?.Separator) ? ";" : declaration.Separator;
        string keyValueSeparator = string.IsNullOrEmpty(declaration?.KeyValueSeparator) ? ":" : declaration.KeyValueSeparator;

        foreach(string entry in SplitBy(text, entrySeparator))
        {
            if(string.IsNullOrWhiteSpace(entry))
                continue;

            // 只切第一个分隔符，允许 value 本身包含该字符（如 URL 中的冒号）。
            int separatorIndex = entry.IndexOf(keyValueSeparator, StringComparison.Ordinal);
            if(separatorIndex <= 0
               || !TryParseCell(keyType, entry.Substring(0, separatorIndex).Trim(), null, out object key)
               || !TryParseCell(valueType, entry.Substring(separatorIndex + keyValueSeparator.Length).Trim(), null, out object itemValue))
                return false;
            dictionary[key] = itemValue;
        }

        value = dictionary;
        return true;
    }

    static string[] SplitValues(string text, string separator)
    {
        if(string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        return string.IsNullOrEmpty(separator)
            ? text.Split('、', ';', '+')
            : SplitBy(text, separator);
    }

    static string[] SplitBy(string text, string separator)
        => text.Split(new[] { separator }, StringSplitOptions.None);

    static bool TryParseComposite(Type type, string text, string separator, out object value)
    {
        value = null;
        FieldInfo[] fields = type.GetFields(Flags)
            .Where(f => !f.IsStatic && !f.IsNotSerialized)
            .OrderBy(f => f.MetadataToken)
            .ToArray();
        string[] parts = SplitBy(text, separator);
        if(fields.Length == 0 || parts.Length > fields.Length)
            return false;

        object instance;
        try
        {
            instance = type.IsValueType ? Activator.CreateInstance(type) : FormatterServices.GetUninitializedObject(type);
            for(int i = 0; i < parts.Length; i++)
            {
                if(!TryParseCell(fields[i].FieldType, parts[i].Trim(), null, out object fieldValue))
                    return false;
                fields[i].SetValue(instance, fieldValue);
            }
        }
        catch
        {
            return false;
        }

        value = instance;
        return true;
    }
}
