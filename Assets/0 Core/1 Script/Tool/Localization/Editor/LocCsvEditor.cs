#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>
/// 解析多语言 CSV 表头/已有 Key（纯文本，不依赖 Unity 本地化运行时），供 <see cref="LocWorkbenchWindow"/> 动态生成表格列。
/// 表头沿用导出格式：Key,Id,Chinese (Simplified)(zh-CN),English(en),…（Id 列留空）。
/// CSV → 字符串表合并见 <see cref="LocCsvMerger"/>。
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
