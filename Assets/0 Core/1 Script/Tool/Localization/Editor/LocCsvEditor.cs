#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通用「向多语言 CSV 追加一行条目」的编辑器接口（纯文本读写，不依赖 Unity 本地化运行时）。
/// 解析既有表头确定列序与语言代码，按原格式拼出新行并保存；文件的编码（是否带 BOM）与换行风格保持不变。
/// 表头沿用导出格式：Key,Id,Chinese (Simplified)(zh-CN),English(en),…（Id 列留空）。
/// UI 见 <see cref="LocCsvAppendWindow"/>；CSV → 字符串表合并见 <see cref="LocCsvMerger"/>。
/// </summary>
public static class LocCsvEditor
{
    public struct Column
    {
        public string header;   // 原表头文本，如 "Chinese (Simplified)(zh-CN)"
        public string code;     // 语言代码，如 "zh-CN"；Key/Id 等非语言列为 null
        public bool isKey;
        public bool isId;
    }

    public struct ParseResult
    {
        public bool ok;
        public string message;
        public List<Column> columns;   // 按列序排列
        public List<string> keys;      // 已有 Key（用于查重）
        public int keyIndex;           // Key 列下标（<0 表示无）
    }

    /// <summary>解析 CSV 表头与已有 Key 列表，供 UI 动态生成语言输入框 / 查重。</summary>
    public static ParseResult Parse(string csvText)
    {
        var res = new ParseResult { columns = new List<Column>(), keys = new List<string>(), keyIndex = -1 };
        if(string.IsNullOrEmpty(csvText))
        { res.message = "CSV 内容为空。"; return res; }

        List<List<string>> rows = LocCsvMerger.ParseCsv(csvText);
        if(rows.Count == 0)
        { res.message = "CSV 解析不出任何行。"; return res; }

        List<string> header = rows[0];
        for(int c = 0; c < header.Count; c++)
        {
            string h = header[c].Trim().TrimStart('﻿');
            var col = new Column { header = h };
            if(h.Equals("Key", StringComparison.OrdinalIgnoreCase)) { col.isKey = true; res.keyIndex = c; }
            else if(h.Equals("Id", StringComparison.OrdinalIgnoreCase)) col.isId = true;
            else col.code = ExtractLocale(h);
            res.columns.Add(col);
        }
        if(res.keyIndex < 0)
        { res.message = "CSV 表头缺少 Key 列。"; return res; }

        for(int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if(res.keyIndex >= row.Count) continue;
            string key = row[res.keyIndex].Trim();
            if(!string.IsNullOrEmpty(key)) res.keys.Add(key);
        }

        res.ok = true;
        res.message = "ok";
        return res;
    }

    /// <summary>按列序拼出一行 CSV（不含换行）：Key 与语言值加引号，Id 等非语言列留空。</summary>
    public static string BuildRow(IList<Column> columns, string key, IDictionary<string, string> valuesByCode)
    {
        var cells = new List<string>(columns.Count);
        foreach(Column col in columns)
        {
            if(col.isKey) cells.Add(Quote(key));
            else if(!string.IsNullOrEmpty(col.code) && valuesByCode != null
                    && valuesByCode.TryGetValue(col.code, out string v) && !string.IsNullOrEmpty(v))
                cells.Add(Quote(v));
            else cells.Add("");   // Id 列、未填语言列：留空（与表内既有空格保持一致）
        }
        return string.Join(",", cells);
    }

    /// <summary>
    /// 向 <paramref name="assetPath"/>（工程相对路径 Assets/…）追加一行：key + 各语言值（缺省留空）。
    /// 成功返回 true 并刷新资源；key 为空 / 已存在、文件缺 Key 列等情况返回 false 并写入 message。
    /// </summary>
    public static bool AppendEntry(string assetPath, string key, IDictionary<string, string> valuesByCode, out string message)
    {
        message = "";
        if(string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        { message = "请选择一个 .csv 文件。"; return false; }
        key = key?.Trim();
        if(string.IsNullOrEmpty(key))
        { message = "Key 不能为空。"; return false; }

        string fullPath = Path.GetFullPath(assetPath);
        if(!File.Exists(fullPath))
        { message = $"找不到文件：{assetPath}"; return false; }

        bool hasBom;
        using(var fs = File.OpenRead(fullPath))
        {
            var head = new byte[3];
            int n = fs.Read(head, 0, 3);
            hasBom = n == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        }

        string content = File.ReadAllText(fullPath);   // 自动识别 BOM，按 UTF-8 解码
        ParseResult parsed = Parse(content);
        if(!parsed.ok)
        { message = parsed.message; return false; }
        if(parsed.keys.Contains(key))
        { message = $"Key「{key}」已存在，未重复添加。"; return false; }

        string row = BuildRow(parsed.columns, key, valuesByCode);
        string nl = content.Contains("\r\n") ? "\r\n" : "\n";   // 沿用原文件换行风格
        if(content.Length > 0 && !content.EndsWith("\n")) content += nl;
        content += row + nl;

        File.WriteAllText(fullPath, content, new UTF8Encoding(hasBom));
        AssetDatabase.ImportAsset(assetPath);
        message = $"已向「{Path.GetFileName(assetPath)}」追加 Key「{key}」。";
        return true;
    }

    /// <summary>CSV 字段转义：用双引号包裹，内部的 " 转成 ""。</summary>
    public static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    // 取表头末尾括号内的语言代码：如 "Chinese (Simplified)(zh-CN)" → "zh-CN"，"English(en)" → "en"
    static string ExtractLocale(string header)
    {
        int open = header.LastIndexOf('(');
        int close = header.LastIndexOf(')');
        return (open >= 0 && close > open) ? header.Substring(open + 1, close - open - 1).Trim() : null;
    }
}
#endif
