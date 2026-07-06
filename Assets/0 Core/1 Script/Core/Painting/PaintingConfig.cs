using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

/// <summary>贴纸(绘画)单条数据：来自 PaintingConfig.csv 的一行（不含按框架拼接的合成图，那部分是导入时自动生成）。</summary>
[Serializable]
public class PaintingRow
{
    public string remark;
    public int baseCost;
    public int drawProb30;     // 30灵感单张抽取概率
    public int drawProb50;     // 50灵感单张抽取概率
    public int drawProb100;    // 100灵感单张抽取概率
    public string defaultDisplayPath;  // 未选框架时的默认展示图 AA Key(CSV「DefaultDisplay」列)
}

/// <summary>
/// 贴纸(绘画) Id → 基础数据(成本/抽取概率/默认展示图)，以及「贴纸Id + 框架Id」组合下的合成图路径。
/// 数据来自 Data/Painting/PaintingConfig.csv：Id,Remark,BaseCost,DrawProb30Insp,DrawProb50Insp,DrawProb100Insp,DefaultDisplay,...
/// 后面从 210000MaxDrawingPicture 起的列只是命名规则说明，不导入；导入时改为按「本表已有贴纸Id × MoldFrameConfig.csv 已有框架Id」
/// 的全组合，自动拼出合成大图({贴纸Id}+{框架Id}DrawingPicture，画布用)与合成小图({贴纸Id}+{框架Id}ItemDrawingPicture，Item图标用)路径写入字典。
/// 用于 <see cref="FactoryMoldMgPanel"/>：选择框架后，同一张贴纸会按当前框架切换显示对应的合成图。
/// 通过菜单 MiniGame/Factory/PaintingConfig 创建资产，拖给面板 paintingConfig 字段。
/// </summary>
[CreateAssetMenu(fileName = "PaintingConfig", menuName = "MiniGame/Factory/PaintingConfig")]
public class PaintingConfig : SerializedScriptableObject
{
    [LabelText("贴纸Id → 数据")]
    [SerializeField] Dictionary<long, PaintingRow> paintingDict = new();

    [LabelText("(贴纸Id,框架Id) → 合成大图(画布用)")]
    [SerializeField] Dictionary<long, string> composedPicturePath = new();

    [LabelText("(贴纸Id,框架Id) → 合成小图(Item图标用)")]
    [SerializeField] Dictionary<long, string> composedItemPicturePath = new();

    // 「贴纸+框架」组合的字典 Key 直接复用合成结果物品 Id 编码(贴纸Id×1000000+框架Id)，与 ItemConfig 合成物品 Id 天然一致
    static long CombineKey(long paintingId, long frameId) => FactoryMoldSynthesis.GetResultId(frameId, paintingId);

    public PaintingRow GetRow(long paintingId) => paintingDict.TryGetValue(paintingId, out PaintingRow row) ? row : null;

    /// <summary>取贴纸基础成本；未配置返回 0。</summary>
    public int GetBaseCost(long paintingId) => GetRow(paintingId)?.baseCost ?? 0;
    public int GetDrawProb30(long paintingId) => GetRow(paintingId)?.drawProb30 ?? 0;
    public int GetDrawProb50(long paintingId) => GetRow(paintingId)?.drawProb50 ?? 0;
    public int GetDrawProb100(long paintingId) => GetRow(paintingId)?.drawProb100 ?? 0;

    /// <summary>未选框架时的默认展示图 AA Key；未配置返回空。</summary>
    public string GetDefaultDisplayPath(long paintingId) => GetRow(paintingId)?.defaultDisplayPath ?? "";

    /// <summary>取「贴纸+框架」组合下的合成大图(画布展示用) AA Key；该框架组合未生成时回退默认展示图。</summary>
    public string GetComposedPath(long paintingId, long frameId)
        => composedPicturePath.TryGetValue(CombineKey(paintingId, frameId), out string p) && !string.IsNullOrEmpty(p)
            ? p : GetDefaultDisplayPath(paintingId);

    /// <summary>取「贴纸+框架」组合下的合成小图(Item图标用) AA Key；未生成返回空。</summary>
    public string GetComposedItemPath(long paintingId, long frameId)
        => composedItemPicturePath.TryGetValue(CombineKey(paintingId, frameId), out string p) ? p : "";

#if UNITY_EDITOR
    public const string Dir = "Assets/0 Core/1 Script/Data/Painting/";
    public const string Csv = Dir + "PaintingConfig.csv";

    [PropertySpace(8)]
    [Button("一键从 PaintingConfig CSV 导入(含框架合成图组合)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("导入前 7 列(Id/Remark/BaseCost/三档抽取概率/默认展示图)；210000MaxDrawingPicture 起的列只是命名规则说明，不导入。" +
             "改为按「本表贴纸Id × MoldFrameConfig.csv 框架Id」全组合，自动拼出合成大图/小图路径写入字典(用字典查询，不必表里维护)。" +
             "Id 不可解析的行(BOM/类型/中文表头)自动跳过。", InfoMessageType.Info)]
    void ImportFromCsv()
    {
        string full = Path.GetFullPath(Csv);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[PaintingConfig] 未找到 CSV：{Csv}");
            return;
        }

        var newPaintingDict = new Dictionary<long, PaintingRow>();
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line))
                continue;
            string[] c = line.Split(CsvFormat.Comma);
            if(!long.TryParse(c[0].Trim().TrimStart(CsvFormat.Bom), out long id))
                continue;

            newPaintingDict[id] = new PaintingRow
            {
                remark = Cell(c, 1),
                baseCost = IntCell(c, 2),
                drawProb30 = IntCell(c, 3),
                drawProb50 = IntCell(c, 4),
                drawProb100 = IntCell(c, 5),
                defaultDisplayPath = AssetPathSet.BuildAssetPath(AssetPathSet.PaintingSpritePath, Cell(c, 6)),
            };
        }

        Dictionary<long, MoldFrameRow> frames = MoldFrameConfig.ReadFrameCsv(MoldFrameConfig.Csv);

        var newComposed = new Dictionary<long, string>();
        var newComposedItem = new Dictionary<long, string>();
        foreach(long paintingId in newPaintingDict.Keys)
        foreach(long frameId in frames.Keys)
        {
            long key = CombineKey(paintingId, frameId);
            newComposed[key] = AssetPathSet.BuildAssetPath(AssetPathSet.PaintingSpritePath, $"{paintingId}+{frameId}DrawingPicture");
            newComposedItem[key] = AssetPathSet.BuildAssetPath(AssetPathSet.PaintingSpritePath, $"{paintingId}+{frameId}ItemDrawingPicture");
        }

        paintingDict = newPaintingDict;
        composedPicturePath = newComposed;
        composedItemPicturePath = newComposedItem;

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PaintingConfig] 导入完成：贴纸 {paintingDict.Count} 种 × 框架 {frames.Count} 种 → 合成大图/小图各 {composedPicturePath.Count} 条。");
    }

    static string Cell(string[] c, int col) => col < c.Length ? c[col].Trim() : "";
    static int IntCell(string[] c, int col) => int.TryParse(Cell(c, col), out int v) ? v : 0;
#endif
}
