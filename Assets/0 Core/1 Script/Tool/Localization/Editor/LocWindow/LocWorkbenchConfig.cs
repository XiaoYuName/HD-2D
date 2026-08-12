#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using Object = UnityEngine.Object;
using Sirenix.OdinInspector;

/// <summary>
/// 多语言工作台的持久化配置（ScriptableObject 单例）：记录扫描目录、字符串表缓存与「字符串表 ↔ CSV」关联。
/// 不再按目录扫描，而是逐个显式关联 CSV 文件（对象引用存储，移动/改名不受影响，文件被删除则自动失效）。
/// 首次访问 <see cref="St"/> 时自动在本目录创建资产；UI 见 <see cref="LocWorkbenchWindow"/>。
/// </summary>
public class LocWorkbenchConfig : SerializedScriptableObject
{
    const string DefaultScanPath = "Assets/AddressableAssets/Local/LocalizationTable/StringTable";
    const string AssetPath = "Assets/0 Core/1 Script/Tool/Localization/Editor/LocWindow/LocWorkbenchConfig.asset";
    [SerializeField] string scanPath = DefaultScanPath;
    [SerializeField] List<StringTableCollection> tableCache;
    [SerializeField] bool tableCacheReady;

    // key: 字符串表集合名（StringTableCollection.TableCollectionName）；value: 关联的 CSV 文件对象列表
    public Dictionary<string, List<Object>> mappings = new();
    #region St
    static LocWorkbenchConfig st;

    public static LocWorkbenchConfig St
    {
        get
        {
            if(st != null)
                return st;
            st = AssetDatabase.LoadAssetAtPath<LocWorkbenchConfig>(AssetPath);
            if(st == null)
            {
                st = CreateInstance<LocWorkbenchConfig>();
                AssetDatabase.CreateAsset(st, AssetPath);
                AssetDatabase.SaveAssets();
            }
            return st;
        }
    }

    void OnEnable() => st = this;

    void OnDisable()
    {
        if(st == this)
            st = null;
    }
    #endregion
    #region API
    public string GetScanPath()
        => string.IsNullOrWhiteSpace(scanPath) ? DefaultScanPath : scanPath;

    public bool CanSetScanPath(string path)
        => AssetDatabase.IsValidFolder(NormalizeAssetPath(path));

    public void SetScanPath(string path)
    {
        scanPath = NormalizeAssetPath(path);
        tableCache.Clear();
        tableCacheReady = false;
        Save();
    }

    /// <summary>返回扫描目录内的 StringTableCollection 缓存；首次访问时自动扫描。</summary>
    public List<StringTableCollection> GetTableCache()
    {
        if(!tableCacheReady)
            RefreshTableCache();

        if(tableCache.RemoveAll(collection => collection == null) > 0)
            Save();
        return tableCache.Where(collection => collection != null).ToList();
    }

    /// <summary>扫描配置目录及其子目录，并更新 StringTableCollection 缓存。</summary>
    public void RefreshTableCache()
    {
        string path = GetScanPath();
        tableCache.Clear();
        if(AssetDatabase.IsValidFolder(path))
        {
            foreach(string guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { path }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(assetPath);
                if(collection != null)
                    tableCache.Add(collection);
            }
        }
        tableCache = tableCache
            .Distinct()
            .OrderBy(collection => collection.TableCollectionName)
            .ToList();
        tableCacheReady = true;
        Save();
    }

    /// <summary>取表关联的全部 CSV 路径（按路径排序）；已被删除的文件会被自动清理。</summary>
    public List<string> GetCsvFiles(string tableName)
    {
        if(!mappings.TryGetValue(tableName, out List<Object> list))
            return new List<string>();

        var paths = new List<string>();
        bool changed = false;
        for(int i = list.Count - 1; i >= 0; i--)
        {
            Object obj = list[i];
            string path = obj == null ? null : AssetDatabase.GetAssetPath(obj);
            if(string.IsNullOrEmpty(path))
            {
                list.RemoveAt(i);
                changed = true;
                continue;
            }
            paths.Add(path);
        }
        if(changed)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        paths.Sort();
        return paths;
    }

    /// <summary>把若干 CSV 文件关联到表（已关联的自动跳过）。改动立即落盘。</summary>
    public void AddCsvFiles(string tableName, IEnumerable<string> csvPaths)
    {
        if(!mappings.TryGetValue(tableName, out List<Object> list))
            mappings[tableName] = list = new List<Object>();
        foreach(string path in csvPaths)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if(obj != null && !list.Contains(obj))
                list.Add(obj);
        }
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    /// <summary>取消一份 CSV 与表的关联（不删除文件本身）。表不再关联任何 CSV 时移除该表的条目。</summary>
    public void RemoveCsvFile(string tableName, string csvPath)
    {
        if(!mappings.TryGetValue(tableName, out List<Object> list))
            return;
        Object obj = AssetDatabase.LoadAssetAtPath<Object>(csvPath);
        list.RemoveAll(o => o == null || o == obj);
        if(list.Count == 0)
            mappings.Remove(tableName);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    /// <summary>字符串表重命名后同步其 CSV 关联。</summary>
    public void RenameTableCsvFiles(string oldTableName, string newTableName)
    {
        if(!mappings.TryGetValue(oldTableName, out List<Object> files))
            return;
        mappings.Remove(oldTableName);
        if(mappings.TryGetValue(newTableName, out List<Object> target))
            target.AddRange(files.Where(file => !target.Contains(file)));
        else
            mappings.Add(newTableName, files);
        Save();
    }

    /// <summary>删除字符串表时移除其 CSV 关联。</summary>
    public void RemoveTableCsvFiles(string tableName)
    {
        if(mappings.Remove(tableName))
            Save();
    }

    static string NormalizeAssetPath(string path)
    {
        string normalized = path?.Trim().Replace('\\', '/') ?? "";
        string assetsPath = Application.dataPath.Replace('\\', '/');
        if(normalized.Equals(assetsPath, System.StringComparison.OrdinalIgnoreCase))
            normalized = "Assets";
        else if(normalized.StartsWith(assetsPath + "/", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Assets/" + normalized.Substring(assetsPath.Length + 1);
        return normalized.TrimEnd('/');
    }

    void Save()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
    #endregion
}
#endif
