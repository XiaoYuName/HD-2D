#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MiniExcelLibs;
using UnityEditor;
using UnityEngine;

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
        var itemDict = new Dictionary<long, ItemData>();
        var recipes  = new List<FoodRecipe>();
        string path  = Path.GetFullPath(ExcelPath);

        foreach (IDictionary<string, object> row in (IEnumerable)MiniExcel.Query(path, useHeaderRow: true, sheetName: SheetName))
        {
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

        config.SetItemData(itemDict);
        config.SetFoodRecipes(recipes.ToArray());
        EditorUtility.SetDirty(config);
        Debug.Log($"ItemConfig: Imported {itemDict.Count} items, {recipes.Count} food recipes.");
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
}
#endif
