#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MiniExcelLibs;
using UnityEditor;
using UnityEngine;

/// <summary>ItemConfig 相关 CSV 路径的统一出处：ItemConfig.csv / ItemConfigLoc.csv 均落在同一目录下。</summary>
public static class ItemConfigPaths
{
    public const string Dir = "Assets/0 Core/1 Script/Data/ItemConfig/";
    public const string ItemConfigCsv = Dir + "ItemConfig.csv";
    public const string ItemConfigLocCsv = Dir + "ItemConfigLoc.csv";
}

public static class ItemConfigImporter
{
    const string ExcelPath = "Assets/AddressableAssets/Remote/Configs/Config.xlsx";
    const string SheetName = "道具配置";
    // 图标所在的 AA 远程组目录，导入时仅记录 AA Key（资源路径），运行时再动态加载，实现资源分离
    const string ItemIconPath = "Assets/AddressableAssets/Remote/Texture2D/Item/";
    const string ItemIconExtension = ".png";

    const string ColId          = "Id";
    const string ColRemark      = "Remark";
    const string ColName        = "Name";
    const string ColDesc        = "Desc";
    const string ColType        = "Type";
    const string ColTypeNum     = "TypeNum";
    const string ColSynthesis   = "Synthesis";
    const string ColMaxNum      = "MaxNum";
    const string ColShop        = "Shop";
    const string ColCurrencyType = "CurrencyType";
    const string ColValue       = "Value";
    const string ColIcon        = "Icon";
    const string ColPurchase    = "PurchaseRestriction";
    const string ColQuality     = "Quality";
    const char   ArraySep       = '+';

    public static void Import(ItemConfig config)
    {
        string path = Path.GetFullPath(ExcelPath);
        var rows = new List<IDictionary<string, object>>();
        foreach (IDictionary<string, object> row in (IEnumerable)MiniExcel.Query(path, useHeaderRow: true, sheetName: SheetName))
            rows.Add(row);

        BuildAndApply(config, rows, $"Excel/{SheetName}");
    }

    public static void ImportFromCsv(ItemConfig config)
    {
        string path = Path.GetFullPath(ItemConfigPaths.ItemConfigCsv);
        if (!File.Exists(path))
        {
            Debug.LogError($"[ItemConfig] CSV 文件不存在: {ItemConfigPaths.ItemConfigCsv}");
            return;
        }

        var rows = ReadCsvRows(File.ReadAllText(path));
        BuildAndApply(config, rows, $"CSV/{ItemConfigPaths.ItemConfigCsv}");
    }

    /// <summary>
    /// 把一份「列同 ItemConfig.csv」的补充 CSV「合并」进现有配置：同 Id 覆盖、新 Id 追加，
    /// 不清空既有道具、也不改动食谱（补充表均为普通道具）。供工厂合成物品补充表等外部一键导入复用。
    /// 返回 (新增, 更新) 计数；文件不存在时返回 (0,0)。
    /// </summary>
    public static (int added, int updated) MergeItemsFromCsv(ItemConfig config, string csvPath)
    {
        string path = Path.GetFullPath(csvPath);
        if (!File.Exists(path))
        {
            Debug.LogError($"[ItemConfig] 补充 CSV 文件不存在: {csvPath}");
            return (0, 0);
        }

        Dictionary<long, ItemData> dict = config.ItemDataDict;
        if (dict == null)
        {
            dict = new Dictionary<long, ItemData>();
            config.SetItemData(dict);
        }

        int added = 0, updated = 0;
        foreach (var row in ReadCsvRows(File.ReadAllText(path)))
        {
            // Id 不可解析的行（类型说明行 / 中文表头）自动跳过
            if (!long.TryParse(Cell(row, ColId), out long id)) continue;

            if (dict.ContainsKey(id)) updated++; else added++;

            dict[id] = ItemData.Create(
                id,
                Cell(row, ColRemark) ?? "",
                Cell(row, ColName) ?? "",
                Cell(row, ColDesc) ?? "",
                (ItemType)ParseInt(Cell(row, ColType)),
                ParseInt(Cell(row, ColMaxNum)),
                ParseInt(Cell(row, ColShop)),
                ParseInt(Cell(row, ColCurrencyType)),
                ParseInt(Cell(row, ColValue)),
                ParseIntArray(Cell(row, ColPurchase)),
                BuildIconKey(Cell(row, ColIcon)),
                ParseInt(Cell(row, ColQuality))
            );
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemConfig] 合并补充表 {Path.GetFileName(csvPath)}：新增 {added}，更新 {updated}，当前共 {dict.Count} 项。");
        return (added, updated);
    }

    /// <summary>
    /// 将表格行（Excel / CSV 通用）解析为道具字典与食谱列表，并以"清空后覆盖"的方式写回配置。
    /// </summary>
    static void BuildAndApply(ItemConfig config, IEnumerable<IDictionary<string, object>> rows, string source)
    {
        var itemDict = new Dictionary<long, ItemData>();
        var recipes  = new List<FoodRecipe>();

        foreach (var row in rows)
        {
            // Id 不可解析的行（如类型说明行 long/str、中文表头行）自动跳过
            if (!long.TryParse(Cell(row, ColId), out long id)) continue;

            var itemType = (ItemType)ParseInt(Cell(row, ColType));

            itemDict[id] = ItemData.Create(
                id,
                Cell(row, ColRemark) ?? "",
                Cell(row, ColName) ?? "",
                Cell(row, ColDesc) ?? "",
                itemType,
                ParseInt(Cell(row, ColMaxNum)),
                ParseInt(Cell(row, ColShop)),
                ParseInt(Cell(row, ColCurrencyType)),
                ParseInt(Cell(row, ColValue)),
                ParseIntArray(Cell(row, ColPurchase)),
                BuildIconKey(Cell(row, ColIcon)),
                ParseInt(Cell(row, ColQuality))
            );

            // Recipe 类型道具：TypeNum 为食材 ID 列表，Synthesis 为结果道具 ID（空则结果为自身）
            if (itemType == ItemType.Recipe)
            {
                long[] ingredients = ParseLongArray(Cell(row, ColTypeNum));
                long synthesis     = ParseLong(Cell(row, ColSynthesis));
                long resultId      = synthesis > 0 ? synthesis : id;

                if (ingredients != null && ingredients.Length > 0)
                    recipes.Add(FoodRecipe.Create(id, resultId, ingredients));
            }
        }

        // SetItemData / SetFoodRecipes 直接替换整个集合，天然实现"清空后覆盖"
        config.SetItemData(itemDict);
        config.SetFoodRecipes(recipes.ToArray());
        EditorUtility.SetDirty(config);
        AssetDatabase.Refresh();
        Debug.Log($"ItemConfig: Imported {itemDict.Count} items, {recipes.Count} food recipes from {source}.");
    }

    static string Cell(IDictionary<string, object> row, string col)
        => row.TryGetValue(col, out var v) ? v?.ToString() : null;

    static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;
    static long ParseLong(string s) => long.TryParse(s, out long v) ? v : 0;

    /// <summary>
    /// 由图片名称构建 Addressable Key（资源完整路径），导入时不加载实际 Sprite，运行时再按需加载。
    /// </summary>
    static string BuildIconKey(string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName)) return "";

        string normalized = iconName.Trim().Replace("\\", "/");
        if (normalized.StartsWith("Assets/"))
            return normalized;

        string extension = Path.GetExtension(normalized);
        string fileName = string.IsNullOrEmpty(extension)
            ? normalized + ItemIconExtension
            : normalized;
        return ItemIconPath + fileName;
    }

    static long[] ParseLongArray(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] parts = s.Split(ArraySep);
        long[] result = new long[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = long.TryParse(parts[i].Trim(), out long v) ? v : 0;
        return result;
    }

    static int[] ParseIntArray(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        string[] parts = s.Split(ArraySep);
        int[] result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = int.TryParse(parts[i].Trim(), out int v) ? v : 0;
        return result;
    }

    #region CSV
    /// <summary>
    /// 将 CSV 文本解析为以首行表头为键的行集合；首行视为表头，空表头列被忽略。
    /// </summary>
    static List<IDictionary<string, object>> ReadCsvRows(string text)
    {
        var result = new List<IDictionary<string, object>>();
        var raw = TokenizeCsv(text);
        if (raw.Count == 0) return result;

        string[] headers = raw[0];
        for (int r = 1; r < raw.Count; r++)
        {
            string[] cells = raw[r];
            var dict = new Dictionary<string, object>();
            for (int c = 0; c < headers.Length; c++)
            {
                string key = headers[c];
                if (string.IsNullOrWhiteSpace(key) || dict.ContainsKey(key)) continue;
                dict[key] = c < cells.Length ? cells[c] : "";
            }
            result.Add(dict);
        }
        return result;
    }

    /// <summary>
    /// RFC4180 风格的 CSV 分词：支持引号包裹、字段内逗号、转义双引号（""）。
    /// 单元格内的换行（多由 Excel 导出 CSV 时单元格含换行符产生）会被清除，避免污染本地化键。
    /// </summary>
    static List<string[]> TokenizeCsv(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool rowHasContent = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(CleanCell(sb.ToString())); sb.Clear(); rowHasContent = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(CleanCell(sb.ToString())); sb.Clear();
                    rows.Add(fields.ToArray()); fields.Clear(); rowHasContent = false;
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        if (rowHasContent || sb.Length > 0)
        {
            fields.Add(CleanCell(sb.ToString()));
            rows.Add(fields.ToArray());
        }
        return rows;
    }

    // 去除单元格内的换行符并裁剪首尾空白：CSV 中部分单元格含换行（如分行书写的本地化键），需还原为单行
    static string CleanCell(string s) => s.Replace("\r", "").Replace("\n", "").Trim();
    #endregion
}
#endif
