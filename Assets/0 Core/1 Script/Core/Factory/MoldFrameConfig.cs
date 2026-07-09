using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

/// <summary>框架(模具)单条数据：来自 MoldFrameConfig.csv 的一行。</summary>
[Serializable]
public class MoldFrameRow
{
    public string remark;
    public int type;
    public int score;          // 基础成本
    public string maskPath;    // 画布用蒙版图 AA Key(CSV「BaseMap」列，实际内容是 Mask 图)
    public string framePath;   // 画布用框架图 AA Key(CSV「Framework」列)
    public string itemMaskPath;    // Item 小图标用蒙版图 AA Key(CSV「Item底图」列)
    public string itemFramePath;   // Item 小图标用框架图 AA Key(CSV「Item框架」列)
}

/// <summary>
/// 框架(模具) Id → 基础数据(成本/画布蒙版&框架图/Item小图标蒙版&框架图)。
/// 由 <see cref="FactoryMoldMgPanel"/> 在选择框架时取图与成本；供 <see cref="PaintingConfig"/> 导入时读取框架 Id 列表以生成「贴纸×框架」的合成图组合。
/// 数据来自 Data/Factory/MoldFrameConfig.csv：Id,Remark,Type,Remark(品质备注,未使用),Cost,BaseMap,Framework,BaseMap,Framework（第6-9列后两个 BaseMap/Framework 对应 Item 小图标版本）。
/// 通过菜单 MiniGame/Factory/MoldFrameConfig 创建资产，拖给面板 frameConfig 字段。
/// </summary>
[CreateAssetMenu(fileName = "MoldFrameConfig", menuName = "MiniGame/Factory/MoldFrameConfig")]
public class MoldFrameConfig : SerializedScriptableObject
{
    [LabelText("框架Id → 数据")]
    [SerializeField] Dictionary<long, MoldFrameRow> frameDict = new();

    public IReadOnlyCollection<long> AllFrameIds => frameDict.Keys;

    public MoldFrameRow GetRow(long frameId) => frameDict.TryGetValue(frameId, out MoldFrameRow row) ? row : null;

    public string GetMaskPath(long frameId) => GetRow(frameId)?.maskPath ?? "";
    public string GetFramePath(long frameId) => GetRow(frameId)?.framePath ?? "";
    public string GetItemMaskPath(long frameId) => GetRow(frameId)?.itemMaskPath ?? "";
    public string GetItemFramePath(long frameId) => GetRow(frameId)?.itemFramePath ?? "";

    /// <summary>取框架基础成本；未配置返回 0。</summary>
    public int GetScore(long frameId) => GetRow(frameId)?.score ?? 0;

#if UNITY_EDITOR
    public const string Dir = "Assets/0 Core/1 Script/Data/Factory/";
    public const string Csv = Dir + "MoldFrameConfig.csv";

    [PropertySpace(8)]
    [PropertyOrder(1)]
    [Button("① 一键从 MoldFrameConfig CSV 导入(全部)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("【流程 ①】先导入框架数据。表头 Id,Remark,Type,Remark(品质备注,未使用),Cost,BaseMap,Framework,BaseMap,Framework：第5列 Cost→score(基础成本)、第6列 BaseMap→maskPath(蒙版)、第7列 Framework→framePath(框架)，" +
             "第8-9列为 Item 小图标版本→itemMaskPath/itemFramePath。路径 = MoldFrame 精灵目录 + 裸文件名。" +
             "Id 不可解析的行(BOM/类型/中文表头)自动跳过。", InfoMessageType.Info)]
    void ImportFromCsv()
    {
        frameDict = ReadFrameCsv(Csv);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MoldFrameConfig] 导入完成：框架 {frameDict.Count} 条。");
    }

    [PropertySpace(4)]
    [PropertyOrder(2)]
    [Button("② 生成合成图 Mold(256×256) —— 需先执行①", ButtonSizes.Large), GUIColor(0.7f, 0.9f, 1f)]
    [InfoBox("【流程 ②】按「已导入框架 × PaintingConfig.csv 贴纸」全组合合成 256×256 成品图：\n" +
             "取 Item 版三图叠合——ItemFramework(挂轴) + ItemBaseMap(绿色画心遮罩，立绘只在绿区显示) + {贴纸}+{框架}ItemDrawingPicture(立绘)，挂轴盖最上层。\n" +
             "命名 = 合成物品Id(贴纸×1000000+框架).png，写入 Mold 目录。**会先清空 Mold 目录再全量重建**(物品增删也不残留)。\n" +
             "合成物品 Icon 已由 ItemConfigSupplement 指向此目录，之后重导 ItemConfig 即自动对上图标。", InfoMessageType.Info)]
    void GenerateComposites()
    {
        if(frameDict == null || frameDict.Count == 0)
        {
            Debug.LogError("[MoldFrameConfig] 请先执行 ① 导入框架数据再生成合成图。");
            return;
        }

        List<long> paintingIds = ReadPaintingIds(PaintingConfig.Csv);
        if(paintingIds.Count == 0)
        {
            Debug.LogError($"[MoldFrameConfig] 未从 {PaintingConfig.Csv} 读到任何贴纸 Id，无法生成合成图。");
            return;
        }

        // 先清空 Mold 目录：全量重建，物品增删也不残留旧图
        string outDir = Path.GetFullPath(AssetPathSet.MoldComposedSpritePath);
        Directory.CreateDirectory(outDir);
        foreach(string f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
        foreach(string f in Directory.GetFiles(outDir, "*.png.meta")) File.Delete(f);

        int made = 0, skipped = 0;
        var writtenAssetPaths = new List<string>();   // 生成的资产相对路径，导入完成后统一设置 TextureImporter
        foreach(KeyValuePair<long, MoldFrameRow> kv in frameDict)
        {
            long frameId = kv.Key;
            Texture2D frameTex = LoadReadablePng(kv.Value.itemFramePath);
            Texture2D maskTex = LoadReadablePng(kv.Value.itemMaskPath);
            if(frameTex == null || maskTex == null)
            {
                skipped += paintingIds.Count;
                if(frameTex != null) DestroyImmediate(frameTex);
                if(maskTex != null) DestroyImmediate(maskTex);
                continue;
            }

            foreach(long paintingId in paintingIds)
            {
                string drawPath = AssetPathSet.BuildAssetPath(AssetPathSet.PaintingSpritePath, $"{paintingId}+{frameId}ItemDrawingPicture");
                Texture2D paintTex = LoadReadablePng(drawPath);
                if(paintTex == null) { skipped++; continue; }

                Texture2D composed = Composite(frameTex, maskTex, paintTex, CompositeSize);
                long itemId = FactoryMoldSynthesis.GetResultId(frameId, paintingId);
                File.WriteAllBytes(Path.Combine(outDir, itemId + ".png"), composed.EncodeToPNG());
                writtenAssetPaths.Add(AssetPathSet.MoldComposedSpritePath + itemId + ".png");

                DestroyImmediate(composed);
                DestroyImmediate(paintTex);
                made++;
            }
            DestroyImmediate(frameTex);
            DestroyImmediate(maskTex);
        }

        AssetDatabase.Refresh();   // 先让编辑器识别新写入的 PNG，再逐个改导入设置
        foreach(string assetPath in writtenAssetPaths)
            ApplySpriteImport(assetPath);

        AssetDatabase.SaveAssets();
        Debug.Log($"[MoldFrameConfig] 合成完成：生成 {made} 张 → {AssetPathSet.MoldComposedSpritePath}（跳过缺图 {skipped}）。SpriteMode=Single、缩放=Bilinear。");
    }

    // 合成图导入设置：Sprite(2D and UI)、SpriteMode=Single、缩放算法=Bilinear
    static void ApplySpriteImport(string assetPath)
    {
        if(AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
        importer.SetPlatformTextureSettings(platform);

        importer.SaveAndReimport();
    }

    const int CompositeSize = 256;

    // 从磁盘读 PNG 为可读 Texture2D(绕过导入设置的 isReadable，用 LoadImage 得到的纹理天然可读)
    static Texture2D LoadReadablePng(string assetPath)
    {
        if(string.IsNullOrEmpty(assetPath)) return null;
        string full = Path.GetFullPath(assetPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[MoldFrameConfig] 合成缺图，跳过：{assetPath}");
            return null;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if(!tex.LoadImage(File.ReadAllBytes(full))) { DestroyImmediate(tex); return null; }
        return tex;
    }

    // 三层叠合：立绘(按绿色遮罩裁切) 在下，挂轴框架 盖在最上层。按归一化 UV 采样，兼容不同源尺寸，输出固定 size×size。
    static Texture2D Composite(Texture2D frame, Texture2D mask, Texture2D paint, int size)
    {
        var outTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for(int y = 0; y < size; y++)
        for(int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size;
            float v = (y + 0.5f) / size;
            Color m = mask.GetPixelBilinear(u, v);
            // 绿色画心区(g 明显大于 r/b) = 立绘可见区；用遮罩 alpha 做边缘覆盖，其余置透明
            float cover = (m.g > m.r && m.g > m.b && m.g > 0.3f) ? m.a : 0f;
            Color p = paint.GetPixelBilinear(u, v);
            Color under = new Color(p.r, p.g, p.b, p.a * cover);
            Color over = frame.GetPixelBilinear(u, v);
            px[y * size + x] = AlphaOver(under, over);
        }
        outTex.SetPixels(px);
        outTex.Apply(false);
        return outTex;
    }

    // 标准 alpha over 合成：over 盖在 under 之上
    static Color AlphaOver(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if(a <= 0f) return new Color(0f, 0f, 0f, 0f);
        float inv = under.a * (1f - over.a);
        return new Color(
            (over.r * over.a + under.r * inv) / a,
            (over.g * over.a + under.g * inv) / a,
            (over.b * over.a + under.b * inv) / a,
            a);
    }

    // 读 PaintingConfig.csv 首列贴纸 Id（跳过 BOM/类型/中文表头等不可解析行）
    static List<long> ReadPaintingIds(string csvPath)
    {
        var ids = new List<long>();
        string full = Path.GetFullPath(csvPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[MoldFrameConfig] 未找到 {csvPath}");
            return ids;
        }
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line)) continue;
            string[] c = line.Split(CsvFormat.Comma);
            if(long.TryParse(c[0].Trim().TrimStart(CsvFormat.Bom), out long id))
                ids.Add(id);
        }
        return ids;
    }

    /// <summary>读取 MoldFrameConfig.csv 为 Id→数据字典；供 <see cref="PaintingConfig"/> 导入时取框架 Id 列表用于生成合成图组合。</summary>
    public static Dictionary<long, MoldFrameRow> ReadFrameCsv(string assetPath)
    {
        var dict = new Dictionary<long, MoldFrameRow>();
        string full = Path.GetFullPath(assetPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[MoldFrameConfig] 未找到 CSV：{assetPath}");
            return dict;
        }

        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line))
                continue;
            string[] c = line.Split(CsvFormat.Comma);
            if(!long.TryParse(c[0].Trim().TrimStart(CsvFormat.Bom), out long id))
                continue;

            dict[id] = new MoldFrameRow
            {
                remark = Cell(c, 1),
                type = IntCell(c, 2),
                score = IntCell(c, 4),
                maskPath = AssetPathSet.BuildAssetPath(AssetPathSet.MoldFrameSpritePath, Cell(c, 5)),
                framePath = AssetPathSet.BuildAssetPath(AssetPathSet.MoldFrameSpritePath, Cell(c, 6)),
                itemMaskPath = AssetPathSet.BuildAssetPath(AssetPathSet.MoldFrameSpritePath, Cell(c, 7)),
                itemFramePath = AssetPathSet.BuildAssetPath(AssetPathSet.MoldFrameSpritePath, Cell(c, 8)),
            };
        }
        return dict;
    }

    static string Cell(string[] c, int col) => col < c.Length ? c[col].Trim() : "";
    static int IntCell(string[] c, int col) => int.TryParse(Cell(c, col), out int v) ? v : 0;
#endif
}
