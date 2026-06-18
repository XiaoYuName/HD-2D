#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// 通用「CSV → 字符串表集合」合并核心：把一份多语言 CSV 合并进任意一个 Unity 本地化字符串表集合。
/// 仅含逻辑（解析 / 分析 / 合并），UI 由 <see cref="LocalizationCsvMergeWindow"/> 提供。
///
/// CSV 表头沿用 Unity 本地化导出格式：Key,Id,Chinese (Simplified)(zh-CN),English(en),...
///   · Key 列必填；Id 列可留空（按 Key 自动建/取，不依赖固定 Id）；
///   · 语言列按表头末尾括号里的语言代码匹配集合中的语言表，集合里没有的语言列自动跳过。
/// 合并策略：CSV 里缺失的 Key 新增；已存在的 Key 按 <c>overwrite</c> 决定是否覆盖；
///   CSV 里没有的 Key 一律不动（不会删除目标表里已有的其它内容）。
/// </summary>
public static class LocalizationCsvMerger
{
    // 匹配 {占位符}：花括号内有非空、不含花括号的内容（如 {0}、{gold}），跳过字面 {} 空花括号。
    // 与 AutoMarkSmartString 保持一致：导入即自动开启 IsSmart，省去事后手动勾选 / 单独跑标记工具。
    static readonly Regex SmartPattern = new(@"\{[^{}]+\}", RegexOptions.Compiled);

    public struct Result
    {
        public bool ok;
        public string message;
        public int keyCount;                  // CSV 数据行里的有效 Key 数
        public int added;                     // 合并时新增的 Key 数
        public int updated;                   // 合并时命中的已存在 Key 数
        public int cleared;                   // 导入前清空目标表时删除的 Key 数（仅 Import(clearFirst:true) 时 > 0）
        public int smartMarked;               // 因含 {占位符} 而自动标记 Smart 的条目数
        public List<string> matchedLocales;   // 与目标集合匹配上的语言代码
        public List<string> missingLocales;   // CSV 里有、但目标集合没有的语言代码
    }

    /// <summary>仅解析与分析，不写盘（供面板预览：能解析出多少 Key、哪些语言列匹配/缺失）。</summary>
    public static Result Analyze(string csvText, StringTableCollection collection)
        => Run(csvText, collection, write: false, overwrite: false);

    /// <summary>把 CSV 合并进目标集合并保存。<paramref name="overwrite"/>=false 时只填充原本为空的语言值。</summary>
    public static Result Merge(string csvText, StringTableCollection collection, bool overwrite)
        => Run(csvText, collection, write: true, overwrite: overwrite);

    /// <summary>
    /// 清空目标集合：删除全部 Key 及各语言条目。用于「重建表 / 去除冗余项」前置步骤
    /// （合并只增不删，旧 Key 会残留；先清空再导入即可让表只保留当前 CSV 里的项）。返回清空前的 Key 数。
    /// </summary>
    public static int Clear(StringTableCollection collection)
    {
        if(collection == null)
            return 0;
        int count = collection.SharedData.Entries.Count;
        foreach(StringTable t in collection.StringTables)
        {
            t.Clear();
            EditorUtility.SetDirty(t);
        }
        collection.SharedData.Clear();
        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
        return count;
    }

    /// <summary>仅解析与分析多份 CSV 的汇总（不写盘，供面板预览）。</summary>
    public static Result AnalyzeMany(IList<string> csvTexts, StringTableCollection collection)
        => Aggregate(csvTexts, collection, write: false, overwrite: false, clearFirst: false);

    /// <summary>
    /// 把多份 CSV 依次合并进目标集合。<paramref name="clearFirst"/>=true 时先清空目标表，
    /// 实现「重建表」——结果只保留这些 CSV 里的 Key，移除其余冗余项。汇总各份结果返回。
    /// </summary>
    public static Result Import(IList<string> csvTexts, StringTableCollection collection, bool overwrite, bool clearFirst)
        => Aggregate(csvTexts, collection, write: true, overwrite: overwrite, clearFirst: clearFirst);

    static Result Aggregate(IList<string> csvTexts, StringTableCollection collection, bool write, bool overwrite, bool clearFirst)
    {
        var agg = new Result { matchedLocales = new List<string>(), missingLocales = new List<string>() };
        if(collection == null)
            return Fail(ref agg, "目标字符串表集合为空。");
        if(csvTexts == null || csvTexts.Count == 0)
            return Fail(ref agg, "未提供任何 CSV。");

        if(write && clearFirst)
            agg.cleared = Clear(collection);

        var matched = new HashSet<string>();
        var missing = new HashSet<string>();
        int valid = 0;
        foreach(string csv in csvTexts)
        {
            if(string.IsNullOrEmpty(csv))
                continue;
            Result r = write ? Merge(csv, collection, overwrite) : Analyze(csv, collection);
            if(!r.ok)
                return Fail(ref agg, r.message);
            valid++;
            agg.keyCount += r.keyCount;
            agg.added += r.added;
            agg.updated += r.updated;
            agg.smartMarked += r.smartMarked;
            foreach(string l in r.matchedLocales) matched.Add(l);
            foreach(string l in r.missingLocales) missing.Add(l);
        }
        if(valid == 0)
            return Fail(ref agg, "没有有效的 CSV 内容。");

        agg.matchedLocales.AddRange(matched);
        agg.missingLocales.AddRange(missing);
        agg.ok = true;
        agg.message = "ok";
        return agg;
    }

    static Result Run(string csvText, StringTableCollection collection, bool write, bool overwrite)
    {
        var res = new Result { matchedLocales = new List<string>(), missingLocales = new List<string>() };
        if(collection == null)
            return Fail(ref res, "目标字符串表集合为空。");
        if(string.IsNullOrEmpty(csvText))
            return Fail(ref res, "CSV 内容为空。");

        List<List<string>> rows = ParseCsv(csvText);
        if(rows.Count < 2)
            return Fail(ref res, "CSV 无数据行（至少需表头 + 1 行）。");

        // 解析表头：定位 Key 列与各语言列
        List<string> header = rows[0];
        int keyCol = -1;
        var localeCols = new List<(int col, StringTable table)>();
        for(int c = 0; c < header.Count; c++)
        {
            string h = header[c].Trim().TrimStart('﻿');
            if(h.Equals("Key", StringComparison.OrdinalIgnoreCase)) { keyCol = c; continue; }
            if(h.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;

            string code = ExtractLocale(h);
            if(string.IsNullOrEmpty(code))
                continue;
            if(collection.GetTable(code) is StringTable t)
            {
                localeCols.Add((c, t));
                res.matchedLocales.Add(code);
            }
            else
            {
                res.missingLocales.Add(code);
            }
        }
        if(keyCol < 0)
            return Fail(ref res, "CSV 表头缺少 Key 列。");

        SharedTableData shared = collection.SharedData;
        for(int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if(keyCol >= row.Count)
                continue;
            string key = row[keyCol].Trim();
            if(string.IsNullOrEmpty(key))
                continue;

            res.keyCount++;
            bool exists = shared.Contains(key);
            if(exists) res.updated++; else res.added++;

            if(!write)
                continue;

            if(!exists)
                shared.AddKey(key);

            foreach((int col, StringTable table) in localeCols)
            {
                if(col >= row.Count)
                    continue;
                string val = row[col];
                if(string.IsNullOrEmpty(val))
                    continue;

                StringTableEntry entry = table.GetEntry(key);
                if(entry == null)
                    entry = table.AddEntry(key, val);
                else if(overwrite || string.IsNullOrEmpty(entry.Value))
                    entry.Value = val;

                // 含 {占位符} 的文本导入后自动开启 Smart String（标准列映射不携带该元数据）。
                if(entry != null && !entry.IsSmart && !string.IsNullOrEmpty(entry.Value)
                    && SmartPattern.IsMatch(entry.Value))
                {
                    entry.IsSmart = true;
                    res.smartMarked++;
                }
                EditorUtility.SetDirty(table);
            }
        }

        if(write)
        {
            EditorUtility.SetDirty(shared);
            AssetDatabase.SaveAssets();
        }

        res.ok = true;
        res.message = "ok";
        return res;
    }

    static Result Fail(ref Result res, string msg)
    {
        res.ok = false;
        res.message = msg;
        return res;
    }

    // 取表头末尾括号内的语言代码：如 "Chinese (Simplified)(zh-CN)" → "zh-CN"，"English(en)" → "en"
    static string ExtractLocale(string header)
    {
        int open = header.LastIndexOf('(');
        int close = header.LastIndexOf(')');
        return (open >= 0 && close > open) ? header.Substring(open + 1, close - open - 1).Trim() : null;
    }

    // 最小 RFC4180 CSV 解析：支持引号字段内的逗号与换行，"" 表示字面量引号
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for(int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if(inQuotes)
            {
                if(ch == '"')
                {
                    if(i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }   // 转义引号
                    else inQuotes = false;
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else
            {
                switch(ch)
                {
                    case '"': inQuotes = true; break;
                    case ',': row.Add(sb.ToString()); sb.Clear(); break;
                    case '\r': break;
                    case '\n': row.Add(sb.ToString()); sb.Clear(); rows.Add(row); row = new List<string>(); break;
                    default: sb.Append(ch); break;
                }
            }
        }
        if(sb.Length > 0 || row.Count > 0)   // 收尾：文件末尾无换行时补最后一行
        {
            row.Add(sb.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
#endif
