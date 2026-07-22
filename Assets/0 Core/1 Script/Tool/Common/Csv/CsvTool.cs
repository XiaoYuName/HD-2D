using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
#endif

/// <summary>
/// 通用 CSV 解析工具
/// </summary>
public static class CsvTool
{
#if UNITY_EDITOR
    /// <summary>一张解析后的 CSV：按列名（首行表头）取值，避免依赖列顺序。</summary>
    public class Table
    {
        public Dictionary<string, int> Header;   // 列名 -> 列索引
        public Dictionary<string, CsvTypeDeclaration> Types; // 列名 -> 第二行类型声明
        public List<string[]> Rows;               // 数据行

        // 按列名取值；列不存在或越界返回空串
        public string Get(string[] row, string colName)
            => (Header.TryGetValue(colName, out int idx) && row != null && idx < row.Length)
                ? row[idx] : string.Empty;

        public CsvTypeDeclaration GetTypeDeclaration(string colName)
            => Types != null && Types.TryGetValue(colName, out CsvTypeDeclaration type) ? type : null;
    }

    /// <summary>
    /// 读取 CSV：第1行为字段名，第2行为类型；第3行可选为格式行（sep=/kvsep=），随后一行为中文标签。
    /// 未传 dataStartLine 时自动识别三行旧表头或四行新表头。
    /// 支持 UTF-8 BOM，每个单元格自动去引号转义（Excel 另存 CSV 常见的整格加引号）。
    /// </summary>
    public static Table ReadCsv(string path, int dataStartLine = -1)
    {
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[CsvTool] 未找到表格：{path}");
            return null;
        }
        string[] lines = ReadAllLinesShared(path);
        if(lines == null || lines.Length < 3)
        {
            Debug.LogWarning($"[CsvTool] 表格行数不足：{path}");
            return null;
        }

        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, CsvTypeDeclaration>(StringComparer.OrdinalIgnoreCase);
        string[] headerCells = SplitLine(lines[0]);
        string[] typeCells = lines.Length > 1 ? SplitLine(lines[1]) : Array.Empty<string>();
        string[] formatCells = lines.Length > 2 ? SplitLine(lines[2]) : Array.Empty<string>();
        bool hasFormatRow = IsFormatRow(formatCells);
        if(dataStartLine < 0)
            dataStartLine = hasFormatRow ? 4 : 3;
        for(int i = 0; i < headerCells.Length; i++)
        {
            string name = headerCells[i];
            if(!string.IsNullOrEmpty(name) && !header.ContainsKey(name))
            {
                header[name] = i;
                types[name] = CsvTypeDeclaration.Parse(
                    i < typeCells.Length ? typeCells[i] : string.Empty,
                    hasFormatRow && i < formatCells.Length ? formatCells[i] : string.Empty);
            }
        }

        var rows = new List<string[]>();
        for(int i = dataStartLine; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            rows.Add(SplitLine(lines[i]));
        }
        return new Table { Header = header, Types = types, Rows = rows };
    }

    public static bool IsFormatRow(string[] cells)
    {
        bool foundOption = false;
        foreach(string raw in cells)
        {
            string cell = (raw ?? string.Empty).Trim();
            if(string.IsNullOrEmpty(cell) || cell == "-")
                continue;
            if(cell.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)
               || cell.StartsWith("kvsep=", StringComparison.OrdinalIgnoreCase)
               || cell.StartsWith("sep2=", StringComparison.OrdinalIgnoreCase)
               || cell.StartsWith("#sep=", StringComparison.OrdinalIgnoreCase))
            {
                foundOption = true;
                continue;
            }
            return false;
        }
        return foundOption;
    }

    /// <summary>
    /// 以共享方式读取全部行：文件被 Excel/WPS 打开（持有写锁）时也能读，避免 Sharing violation。
    /// 偶发的瞬时锁（如另存瞬间）重试几次，仍失败返回 null 并告警。
    /// </summary>
    public static string[] ReadAllLinesShared(string path, int retries = 3)
    {
        for(int attempt = 0; ; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var sr = new StreamReader(fs, new UTF8Encoding(true));
                var lines = new List<string>();
                string line;
                while((line = sr.ReadLine()) != null)
                    lines.Add(line);
                return lines.ToArray();
            }
            catch(IOException e)
            {
                if(attempt >= retries)
                {
                    Debug.LogWarning($"[CsvTool] 读取表格失败（文件被占用？重试 {retries} 次后放弃）：{path}\n{e.Message}");
                    return null;
                }
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// 按逗号切分一行 CSV（RFC4180 引号感知：引号内的逗号不当分隔符，""转义为"），
    /// 兼容 Excel 另存为 CSV 时给每个单元格加双引号、以及首格残留 BOM 的情况。
    /// 供 CsvTool/CsvConfigCodeGen 共用，取代原来"先按逗号硬切、再逐格去引号"的做法
    /// （硬切法遇到引号内的逗号——如类型列的 Dictionary&lt;K,V&gt;——会把一格错切成两格）。
    /// </summary>
    public static string[] SplitLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for(int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if(inQuotes)
            {
                if(c == CsvFormat.Quote)
                {
                    if(i + 1 < line.Length && line[i + 1] == CsvFormat.Quote) { sb.Append(CsvFormat.Quote); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if(c == CsvFormat.Quote) inQuotes = true;
                else if(c == CsvFormat.Comma) { cells.Add(Finish(sb)); sb.Clear(); }
                else sb.Append(c);
            }
        }
        cells.Add(Finish(sb));
        return cells.ToArray();

        static string Finish(StringBuilder b) => b.ToString().Trim().TrimStart(CsvFormat.Bom);
    }

    // 解析失败（空白 / 非法）时回退到指定默认值
    public static int ParseInt(string s, int fallback) => int.TryParse(s, out int v) ? v : fallback;
    public static float ParseFloat(string s, float fallback) => float.TryParse(s, out float v) ? v : fallback;

    /// <summary>解析形如 #RRGGBB / #RRGGBBAA 的颜色字符串；解析失败回退到指定默认值。</summary>
    public static Color ParseColor(string s, Color fallback) => ColorUtility.TryParseHtmlString(s, out Color c) ? c : fallback;
#endif
}
