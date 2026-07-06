#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// ItemConfig 相关 CSV 路径的统一出处：ItemConfig.csv / ItemConfigLoc.csv 及两张补充表均落在同一目录下。
/// 所有需要这些路径的地方（<see cref="ItemConfigImporter"/>、<see cref="ItemConfigSupplementRunner"/>、
/// </summary>
public static class ItemConfigPaths
{
    public const string Dir = "Assets/0 Core/1 Script/Data/ItemConfig/";
    public const string ItemConfigCsv = Dir + "ItemConfig.csv";
    public const string ItemConfigLocCsv = Dir + "ItemConfigLoc.csv";
    public const string FactorySupCsv = Dir + "ItemConfigFactorySup.csv";
    public const string FactorySupLocCsv = Dir + "ItemConfigFactorySupLoc.csv";
}

/// <summary>
/// ItemConfig CSV/Excel 导入时的“补充策略”上下文：携带正在构建的道具字典与 ItemConfigLoc 多语言查询表，
/// 供各 <see cref="IItemSupplementStrategy"/> 读取基础道具、写入新道具，以及记录导出用的 CSV/多语言行。
/// 策略按注册顺序执行，后一策略可读到前一策略新写入 ItemDict 的道具（如周边商品依赖生产资料）。
/// </summary>
public class ItemSupplementContext
{
    public Dictionary<long, ItemData> ItemDict;
    public readonly Dictionary<string, string[]> LocMap = new();
    public int LastLangCol = 8;
    public readonly List<string> SupRows = new();
    public readonly List<string> SupLocRows = new();
    // 生产资料 Id → 其来源的(框架Id, 贴纸Id)，由 FactoryProductionMaterialsSupplementStrategy 写入，供后续策略（如周边商品）取用
    public readonly Dictionary<long, (long frameId, long paintingId)> MaterialOrigins = new();

    public string[] LocOf(string key) => (!string.IsNullOrEmpty(key) && LocMap.TryGetValue(key, out string[] c)) ? c : null;
}

/// <summary>ItemConfig 补充策略：基于当前道具字典生成额外道具，直接写入 <see cref="ItemSupplementContext.ItemDict"/>。</summary>
public interface IItemSupplementStrategy
{
    void Generate(ItemSupplementContext ctx);
}

/// <summary>拼接补充表 CSV 行的小工具：按参数顺序对应列顺序，避免手写逗号数错位。</summary>
public static class ItemSupplementCsv
{
    public static string Row(params object[] fields) => string.Join(",", Array.ConvertAll(fields, f => f?.ToString() ?? ""));

    public static string Cell(string[] cells, int col, string fallback)
        => (cells != null && col < cells.Length && !string.IsNullOrWhiteSpace(cells[col])) ? cells[col].Trim() : fallback;
}

/// <summary>
/// 补充策略 1：工厂生产资料(FactoryProductionMaterials) = 手办模型(FigureModel) × 女主绘画(Painting) 的全组合。
/// Remark/名称/描述 = 贴纸 Remark + 框架 Remark 拼接；品质取两者品质均值(向下取整，不四舍五入)；
/// 货币价格固定 0，货币类型固定 1；背包上限 999；图标 = Mold 目录合成成品图(命名=合成物品Id，由 MoldFrameConfig「② 生成合成图」产出)。
/// 结果 Id 编码需与 FactoryMoldSynthesis、FactoryProductData 的周边/次品偏移换算保持一致，不可随意更改。
/// </summary>
public class FactoryProductionMaterialsSupplementStrategy : IItemSupplementStrategy
{
    public void Generate(ItemSupplementContext ctx)
    {
        var frames = new List<ItemData>();
        var paintings = new List<ItemData>();
        foreach (ItemData d in ctx.ItemDict.Values)
        {
            if (d == null) continue;
            if (d.Type == ItemType.FigureModel) frames.Add(d);
            else if (d.Type == ItemType.Painting) paintings.Add(d);
        }
        if (frames.Count == 0 || paintings.Count == 0) return;
        frames.Sort((a, b) => a.Id.CompareTo(b.Id));
        paintings.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (ItemData frame in frames)
        foreach (ItemData painting in paintings)
        {
            long id = FactoryMoldSynthesis.GetResultId(frame.Id, painting.Id);
            string remark = painting.Remark + frame.Remark;
            string nameKey = id + "Name";
            string descKey = id + "Text";
            int quality = (frame.Quality + painting.Quality) / 2;
            // 图标 = MoldFrameConfig「② 生成合成图」在 Mold 目录产出的合成成品图，命名 = 本合成物品 Id
            string icon = AssetPathSet.MoldComposedSpritePath + id + ".png";

            if (ctx.ItemDict.TryGetValue(id, out ItemData existing) && existing.Type != ItemType.FactoryProductionMaterials)
                Debug.LogWarning($"[ItemConfigSupplement] 生产资料 Id={id} 与已有道具(Type={existing.Type})冲突，已被覆盖，请检查 Id 编码范围是否被占用。");

            ctx.ItemDict[id] = ItemData.Create(id, remark, nameKey, descKey, ItemType.FactoryProductionMaterials,
                999, 0, 1, 0, null, icon, quality);
            ctx.MaterialOrigins[id] = (frame.Id, painting.Id);

            ctx.SupRows.Add(ItemSupplementCsv.Row(id, remark, nameKey, icon, descKey, (int)ItemType.FactoryProductionMaterials,
                "", "", "", 999, "", 1, 0, "", quality));

            string[] paintingLoc = ctx.LocOf(painting.NameKey);
            string[] frameLoc = ctx.LocOf(frame.NameKey);
            ctx.SupLocRows.Add(BuildCombinedLocRow(nameKey, ctx, paintingLoc, frameLoc, painting.Remark, frame.Remark));
            ctx.SupLocRows.Add(BuildCombinedLocRow(descKey, ctx, paintingLoc, frameLoc, painting.Remark, frame.Remark));
        }
    }

    // 拼接一条多语言行：col0=key，col1=空(Id)，col2..last = 贴纸译名+框架译名（缺译文时回退各自 Remark）
    static string BuildCombinedLocRow(string key, ItemSupplementContext ctx, string[] paintingLoc, string[] frameLoc, string paintingFallback, string frameFallback)
    {
        var cells = new string[ctx.LastLangCol + 1];
        cells[0] = key;
        cells[1] = "";
        for (int c = 2; c <= ctx.LastLangCol; c++)
        {
            string p = ItemSupplementCsv.Cell(paintingLoc, c, paintingFallback);
            string f = ItemSupplementCsv.Cell(frameLoc, c, frameFallback);
            cells[c] = p + f;
        }
        return string.Join(",", cells);
    }
}

/// <summary>
/// 补充策略 2：周边商品(Merchandise) = 每个工厂生产资料对应的「正品 + 次品」两条道具。
/// 正品 Id = 生产资料 Id + <see cref="FactoryProductData.MerchandiseIdOffset"/>；次品 Id = 正品 Id + <see cref="FactoryProductData.DefectiveIdOffset"/>。
/// 名称/描述/图标/背包上限/品质均直接复用生产资料的对应字段（次品名称追加"（次品）"后缀，需单独写一条多语言；其余沿用生产资料的多语言，不重复导出）；
/// 货币价格 = 框架 + 贴纸 的货币价格之和，次品价格为正品的一半（整除，不四舍五入）。
/// </summary>
public class MerchandiseSupplementStrategy : IItemSupplementStrategy
{
    const string DefectiveSuffix = "（次品）";

    public void Generate(ItemSupplementContext ctx)
    {
        var materials = new List<ItemData>();
        foreach (ItemData d in ctx.ItemDict.Values)
            if (d != null && d.Type == ItemType.FactoryProductionMaterials)
                materials.Add(d);
        materials.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (ItemData material in materials)
        {
            if (!ctx.MaterialOrigins.TryGetValue(material.Id, out var origin)) continue;
            if (!ctx.ItemDict.TryGetValue(origin.frameId, out ItemData frame)) continue;
            if (!ctx.ItemDict.TryGetValue(origin.paintingId, out ItemData painting)) continue;

            int genuineValue = frame.Value + painting.Value;
            int defectiveValue = genuineValue / 2;
            long genuineId = material.Id + FactoryProductData.MerchandiseIdOffset;
            long defectiveId = genuineId + FactoryProductData.DefectiveIdOffset;

            WarnIfConflict(ctx, genuineId);
            WarnIfConflict(ctx, defectiveId);

            // 正品：名称/描述/图标/背包上限/品质完全复用生产资料的字段值，无需新增多语言行
            ctx.ItemDict[genuineId] = ItemData.Create(genuineId, material.Remark, material.NameKey, material.DescKey,
                ItemType.Merchandise, material.MaxCount, 0, 1, genuineValue, null, material.IconPath, material.Quality);
            ctx.SupRows.Add(ItemSupplementCsv.Row(genuineId, material.Remark, material.NameKey, material.IconPath, material.DescKey,
                (int)ItemType.Merchandise, "", "", "", material.MaxCount, "", 1, genuineValue, "", material.Quality));

            // 次品：名称追加"（次品）"（需新增一条多语言），其余字段与正品一致
            string defectiveRemark = material.Remark + DefectiveSuffix;
            string defectiveNameKey = defectiveId + "Name";
            ctx.ItemDict[defectiveId] = ItemData.Create(defectiveId, defectiveRemark, defectiveNameKey, material.DescKey,
                ItemType.Merchandise, material.MaxCount, 0, 1, defectiveValue, null, material.IconPath, material.Quality);
            ctx.SupRows.Add(ItemSupplementCsv.Row(defectiveId, defectiveRemark, defectiveNameKey, material.IconPath, material.DescKey,
                (int)ItemType.Merchandise, "", "", "", material.MaxCount, "", 1, defectiveValue, "", material.Quality));

            ctx.SupLocRows.Add(BuildDefectiveNameLocRow(defectiveNameKey, ctx, ctx.LocOf(material.NameKey), material.Remark));
        }
    }

    static void WarnIfConflict(ItemSupplementContext ctx, long id)
    {
        if (ctx.ItemDict.TryGetValue(id, out ItemData existing) && existing.Type != ItemType.Merchandise)
            Debug.LogWarning($"[ItemConfigSupplement] 周边商品 Id={id} 与已有道具(Type={existing.Type})冲突，已被覆盖，请检查 Id 编码范围是否被占用。");
    }

    static string BuildDefectiveNameLocRow(string key, ItemSupplementContext ctx, string[] nameLoc, string fallback)
    {
        var cells = new string[ctx.LastLangCol + 1];
        cells[0] = key;
        cells[1] = "";
        for (int c = 2; c <= ctx.LastLangCol; c++)
            cells[c] = ItemSupplementCsv.Cell(nameLoc, c, fallback) + DefectiveSuffix;
        return string.Join(",", cells);
    }
}

/// <summary>
/// ItemConfig 补充流水线：按顺序执行各 <see cref="IItemSupplementStrategy"/>，并把结果导出到
/// ItemConfigFactorySup.csv（供查阅补充道具）与 ItemConfigFactorySupLoc.csv（供查阅补充多语言）。
/// 由 <see cref="ItemConfigImporter.BuildAndApply"/> 在每次导入(Excel/CSV)后调用；itemDict 会被就地写入新道具，
/// 因此每次全量导入都会用当前 FigureModel/Painting 重新生成，无需手动粘贴维护。
/// </summary>
public static class ItemConfigSupplementRunner
{
    const string SupHeader = "Id,Remark,Name,Icon,Desc,Type,TypeNum,备注,Synthesis,MaxNum,Shop,CurrencyType,Value,PurchaseRestriction,Quality";
    const string DefaultLocHeader = "Key,Id,Chinese (Simplified)(zh-CN),Chinese (Traditional)(zh-TW),English(en),Japanese (Japan)(ja-JP),Korean(ko),Thai(th),Vietnamese(vi)";

    static readonly IItemSupplementStrategy[] Strategies =
    {
        new FactoryProductionMaterialsSupplementStrategy(),
        new MerchandiseSupplementStrategy(),
    };

    /// <summary>对 itemDict 就地追加补充道具，并导出补充表/补充多语言表两份 CSV 供查阅。</summary>
    public static void Run(Dictionary<long, ItemData> itemDict)
    {
        var ctx = new ItemSupplementContext { ItemDict = itemDict };
        string locHeader = ReadLoc(ctx);

        foreach (IItemSupplementStrategy strategy in Strategies)
            strategy.Generate(ctx);

        WriteCsv(ItemConfigPaths.FactorySupCsv, SupHeader, ctx.SupRows);
        WriteCsv(ItemConfigPaths.FactorySupLocCsv, locHeader ?? DefaultLocHeader, ctx.SupLocRows);

        Debug.Log($"[ItemConfigSupplementRunner] 补充道具 {ctx.SupRows.Count} 条已写入字典并导出 → {ItemConfigPaths.FactorySupCsv}；多语言 {ctx.SupLocRows.Count} 条 → {ItemConfigPaths.FactorySupLocCsv}。");
    }

    // 读 ItemConfigLoc.csv：首行=表头（原样返回供补充表复用），其余 key→各语言单元格；lastLangCol=最后一个非空表头列下标
    static string ReadLoc(ItemSupplementContext ctx)
    {
        string full = Path.GetFullPath(ItemConfigPaths.ItemConfigLocCsv);
        if (!File.Exists(full))
        {
            Debug.LogWarning($"[ItemConfigSupplementRunner] 未找到 {ItemConfigPaths.ItemConfigLocCsv}，补充道具的名称/描述多语言将无法回填译文（回退用 Remark 拼接）。");
            return null;
        }

        string header = null;
        foreach (string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] cells = line.Split(',');
            if (header == null)
            {
                // 原表头行常有多余的尾部空列(Excel 导出遗留)，只保留到最后一个非空语言列，避免表头比数据行宽
                for (int i = cells.Length - 1; i >= 2; i--)
                    if (!string.IsNullOrWhiteSpace(cells[i])) { ctx.LastLangCol = i; break; }
                var headerCells = new string[ctx.LastLangCol + 1];
                for (int i = 0; i <= ctx.LastLangCol; i++)
                    headerCells[i] = i < cells.Length ? cells[i].TrimStart('﻿').Trim() : "";
                header = string.Join(",", headerCells);
                continue;
            }
            string key = cells[0].Trim().TrimStart('﻿');
            if (!string.IsNullOrEmpty(key) && !ctx.LocMap.ContainsKey(key))
                ctx.LocMap[key] = cells;
        }
        return header;
    }

    static void WriteCsv(string assetPath, string header, List<string> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        foreach (string row in rows)
            sb.AppendLine(row);
        // 带 BOM 的 UTF-8：否则 Excel 会按系统 ANSI(GBK) 解码导致中文乱码
        File.WriteAllText(Path.GetFullPath(assetPath), sb.ToString(), new UTF8Encoding(true));
    }
}
#endif
