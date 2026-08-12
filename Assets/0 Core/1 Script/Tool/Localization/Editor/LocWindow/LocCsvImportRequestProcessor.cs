#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// 处理 LocCsv.ps1 -Import 写入 Library 的一次性请求。
/// Unity 编辑器保持打开时，会按多语言工作台的 CSV 映射对受影响的表执行增量或重建导入。
/// </summary>
[InitializeOnLoad]
public static class LocCsvImportRequestProcessor
{
    [Serializable]
    sealed class ImportRequest
    {
        public string[] csvPaths;
        public bool clearFirst;
        public string requestedAt;
    }

    static readonly string RequestPath =
        Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/LocCsvImportRequest.json"));
    static double nextCheckAt;

    static LocCsvImportRequestProcessor() => EditorApplication.update += Tick;

    static void Tick()
    {
        if(EditorApplication.timeSinceStartup < nextCheckAt)
            return;
        nextCheckAt = EditorApplication.timeSinceStartup + 0.5;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath))
            return;

        try
        {
            string json = File.ReadAllText(RequestPath);
            ImportRequest request = JsonUtility.FromJson<ImportRequest>(json);
            // JSON 完整解析后再消费，避免恰好撞上 PowerShell 写文件时丢掉半截请求。
            File.Delete(RequestPath);
            Process(request);
        }
        catch(Exception e)
        {
            Debug.LogError($"[LocCsv] 自动导入请求处理失败：{e}");
        }
    }

    static void Process(ImportRequest request)
    {
        if(request?.csvPaths == null || request.csvPaths.Length == 0)
            return;

        AssetDatabase.Refresh();
        var requested = new HashSet<string>(
            request.csvPaths.Select(NormalizeFullPath), StringComparer.OrdinalIgnoreCase);
        var imported = new List<string>();

        foreach(StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
        {
            List<string> mappedPaths = LocWorkbenchConfig.St.GetCsvFiles(collection.TableCollectionName);
            if(!mappedPaths.Any(path => requested.Contains(NormalizeFullPath(path))))
                continue;

            var texts = new List<string>();
            var seenKeys = new Dictionary<string, string>();
            foreach(string assetPath in mappedPaths)
            {
                string fullPath = NormalizeFullPath(assetPath);
                if(!File.Exists(fullPath))
                    continue;
                string text = File.ReadAllText(fullPath);
                if(!CollectKeys(text, assetPath, seenKeys, out string duplicate))
                {
                    Debug.LogError($"[LocCsv] 「{collection.TableCollectionName}」自动导入已中止：{duplicate}");
                    texts.Clear();
                    break;
                }
                texts.Add(text);
            }

            if(texts.Count == 0)
                continue;
            LocCsvMerger.Result result = LocCsvMerger.Import(
                texts, collection, overwrite: true, clearFirst: request.clearFirst);
            if(!result.ok)
            {
                Debug.LogError($"[LocCsv] 「{collection.TableCollectionName}」自动导入失败：{result.message}");
                continue;
            }
            imported.Add(collection.TableCollectionName);
            string mode = request.clearFirst ? $"重建（清空 {result.cleared}）" : "增量";
            Debug.Log($"[LocCsv] 已自动{mode}导入「{collection.TableCollectionName}」：新增 {result.added}，更新 {result.updated}。");
        }

        if(imported.Count == 0)
            Debug.LogWarning("[LocCsv] 收到自动导入请求，但没有 CSV 命中多语言工作台映射。请先在 Tools/Loc/多语言工作台中关联 CSV。");
    }

    static bool CollectKeys(string csvText, string assetPath,
        Dictionary<string, string> seen, out string duplicate)
    {
        duplicate = null;
        List<List<string>> rows = LocCsvMerger.ParseCsv(csvText);
        if(rows.Count == 0)
            return true;
        int keyColumn = rows[0].FindIndex(h =>
            h.Trim().TrimStart('﻿').Equals("Key", StringComparison.OrdinalIgnoreCase));
        if(keyColumn < 0)
            return true;
        for(int i = 1; i < rows.Count; i++)
        {
            if(keyColumn >= rows[i].Count)
                continue;
            string key = rows[i][keyColumn].Trim();
            if(string.IsNullOrEmpty(key))
                continue;
            if(seen.TryGetValue(key, out string previous))
            {
                duplicate = $"重复 Key「{key}」：{previous} / {assetPath}";
                return false;
            }
            seen[key] = assetPath;
        }
        return true;
    }

    static string NormalizeFullPath(string path)
    {
        if(Path.IsPathRooted(path))
            return Path.GetFullPath(path).Replace('\\', '/');
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)).Replace('\\', '/');
    }
}
#endif
