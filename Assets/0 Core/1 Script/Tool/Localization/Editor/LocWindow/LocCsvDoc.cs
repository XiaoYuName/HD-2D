#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// 一份多语言 CSV 的内存文档模型：加载 → 行级编辑 → 按原格式写回。
/// 保留原文件的 BOM、换行风格、表头行原文与 Id 列值；行格式与 <see cref="LocCsvEditor"/>/LocCsv.ps1 一致
/// （Key 与非空语言值加引号，Id 与空语言列留空）。供 <see cref="LocWorkbenchWindow"/> 的表格编辑区使用。
/// </summary>
public class LocCsvDoc
{
    public class Row
    {
        public string key = "";
        public string id = "";                              // 原样保留，UI 不编辑
        public Dictionary<string, string> values = new();   // 语言代码 → 文本
    }

    public string assetPath;    // 工程相对路径 Assets/…
    public bool hasBom;
    public string newline;
    public string headerLine;   // 原始表头行文本，保存时原样写回
    public List<LocCsvEditor.Column> columns;
    public List<Row> rows = new();

    public List<string> LocaleCodes
        => columns.Where(c => !string.IsNullOrEmpty(c.code)).Select(c => c.code).ToList();

    public static LocCsvDoc Load(string assetPath, out string error)
    {
        error = null;
        string fullPath = Path.GetFullPath(assetPath);
        if(!File.Exists(fullPath))
        { error = $"找不到文件：{assetPath}"; return null; }

        byte[] head = new byte[3];
        using(FileStream fs = File.OpenRead(fullPath))
        {
            int n = fs.Read(head, 0, 3);
            if(n < 3) head[0] = 0;
        }

        string content = File.ReadAllText(fullPath);
        LocCsvEditor.ParseResult parsed = LocCsvEditor.Parse(content);
        if(!parsed.ok)
        { error = parsed.message; return null; }

        var doc = new LocCsvDoc
        {
            assetPath = assetPath,
            hasBom = head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF,
            newline = content.Contains("\r\n") ? "\r\n" : "\n",
            columns = parsed.columns,
        };

        int nlIdx = content.IndexOf('\n');
        doc.headerLine = (nlIdx >= 0 ? content.Substring(0, nlIdx) : content).TrimEnd('\r');

        int idIdx = parsed.columns.FindIndex(c => c.isId);
        List<List<string>> raw = LocCsvMerger.ParseCsv(content);
        for(int r = 1; r < raw.Count; r++)
        {
            List<string> cells = raw[r];
            if(parsed.keyIndex >= cells.Count)
                continue;
            string key = cells[parsed.keyIndex].Trim();
            if(string.IsNullOrEmpty(key))
                continue;

            var row = new Row { key = key };
            if(idIdx >= 0 && idIdx < cells.Count)
                row.id = cells[idIdx];
            for(int c = 0; c < parsed.columns.Count; c++)
            {
                string code = parsed.columns[c].code;
                if(!string.IsNullOrEmpty(code))
                    row.values[code] = c < cells.Count ? cells[c] : "";
            }
            doc.rows.Add(row);
        }
        return doc;
    }

    /// <summary>写回前校验：空 Key / 文件内重复 Key。返回 null 表示通过。</summary>
    public string Validate()
    {
        var seen = new HashSet<string>();
        for(int i = 0; i < rows.Count; i++)
        {
            string key = rows[i].key?.Trim();
            if(string.IsNullOrEmpty(key))
                return $"第 {i + 1} 行 Key 为空，无法保存。";
            if(!seen.Add(key))
                return $"Key「{key}」在本 CSV 内重复，无法保存。";
        }
        return null;
    }

    public bool Save(out string error)
    {
        error = Validate();
        if(error != null)
            return false;

        var sb = new StringBuilder();
        sb.Append(headerLine).Append(newline);
        foreach(Row row in rows)
            sb.Append(BuildLine(row)).Append(newline);

        File.WriteAllText(Path.GetFullPath(assetPath), sb.ToString(), new UTF8Encoding(hasBom));
        return true;
    }

    // 按列序拼一行：Key 加引号；Id 非空才加引号写回；语言值非空加引号、空则留空
    string BuildLine(Row row)
    {
        var cells = new List<string>(columns.Count);
        foreach(LocCsvEditor.Column col in columns)
        {
            if(col.isKey)
                cells.Add(LocCsvEditor.Quote(row.key.Trim()));
            else if(col.isId)
                cells.Add(string.IsNullOrEmpty(row.id) ? "" : LocCsvEditor.Quote(row.id));
            else if(!string.IsNullOrEmpty(col.code)
                    && row.values.TryGetValue(col.code, out string v) && !string.IsNullOrEmpty(v))
                cells.Add(LocCsvEditor.Quote(v));
            else
                cells.Add("");
        }
        return string.Join(",", cells);
    }
}
#endif
