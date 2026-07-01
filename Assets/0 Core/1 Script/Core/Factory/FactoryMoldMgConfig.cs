using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

/// <summary>
/// 「物料制作」面板配置：集中管理画布精灵与框架/贴纸售价。
/// - 画布精灵：物品 Id → <b>画布显示精灵</b>(Addressable Key)，用于框架底图与贴纸高清显示，区别于物品 128×128 图标(查不到回退物品图标)。
/// - 框架售价：框架(FigureModel)物品 Id → 售价，来自 Data/Factory/FactoryProductionMtPriceConfig.csv（价取「Score」列）。
/// - 贴纸售价：贴纸(Painting)物品 Id → 售价，来自 Data/Factory/PaintingPriceConfig.csv（价取「Score」列）。
/// 预估售出价 = 框架售价 + 画布上各贴纸售价之和（面板 <see cref="FactoryMoldMgPanel"/> 计算展示）。
/// 通过菜单 MiniGame/Factory/FactoryMoldMgConfig 创建资产，拖给面板 moldConfig 字段。
/// </summary>
[CreateAssetMenu(fileName = "FactoryMoldMgConfig", menuName = "MiniGame/Factory/FactoryMoldMgConfig")]
public class FactoryMoldMgConfig : SerializedScriptableObject
{
    [InfoBox("物品 Id → 精灵 AA Key（框架/贴纸都在此配）。未配置时不回退物品图标，改用下方缺省图并 LogError。")]
    [LabelText("Id → 精灵 AA Key")]
    [SerializeField] Dictionary<long, string> spriteKeys = new ();

    [LabelText("缺省画布精灵 AA Key(未配置时回退并报错)")]
    [InfoBox("某物品没配画布精灵时用它兜底（避免画布空白），并 LogError 提醒补配。请指向一张醒目的占位/缺失图。")]
    [SerializeField] string defaultSpriteKey = "Sticker1";

    [LabelText("框架 Id → 售价")]
    [SerializeField] Dictionary<long, int> framePrices = new ();

    [LabelText("贴纸 Id → 售价")]
    [SerializeField] Dictionary<long, int> stickerPrices = new ();

    #region Get
    /// <summary>取该物品的画布精灵 AA Key；未配置时 LogError 并回退到 <see cref="defaultSpriteKey"/>（不再回退物品 128×128 图标）。</summary>
    public string GetSpriteKey(long itemId)
    {
        if(spriteKeys.TryGetValue(itemId, out string k) && !string.IsNullOrEmpty(k))
            return k;
        Debug.LogError($"[FactoryMoldMgConfig] 物品 {itemId} 未配置画布精灵，回退缺省图「{defaultSpriteKey}」。请在 FactoryMoldSpriteConfig.csv 补配后重新导入。");
        return BuildSpriteKey(defaultSpriteKey);
    }

    /// <summary>
    /// 把精灵名转为完整 AA Key：已是完整路径(Assets/ 开头)则原样返回，
    /// 否则补 <see cref="AssetPathSet.FactoryMoldMgSpritePath"/> 前缀与 .png 扩展名（AA Key = 资源完整路径含扩展名，与 ItemConfig 图标一致）。
    /// </summary>
    public static string BuildSpriteKey(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return "";
        string n = name.Trim().Replace("\\", "/");
        if(n.StartsWith("Assets/"))
            return n;
            
        return AssetPathSet.FactoryMoldMgSpritePath + n;
    }

    /// <summary>取框架售价；未配置返回 <paramref name="fallback"/>。</summary>
    public int GetFramePrice(long frameItemId)
        => framePrices.TryGetValue(frameItemId, out int v) ? v : 0;

    /// <summary>取贴纸售价；未配置返回 <paramref name="fallback"/>。</summary>
    public int GetStickerPrice(long stickerItemId)
        => stickerPrices.TryGetValue(stickerItemId, out int v) ? v : 0;
    #endregion

#if UNITY_EDITOR
    const string Dir = "Assets/0 Core/1 Script/Data/Factory/";
    const string SpriteCsv = Dir + "FactoryMoldSpriteConfig.csv";           // 表头 Id,Remark,SpriteKey（Key 在第 3 列）
    const string FramePriceCsv = Dir + "FactoryProductionMtPriceConfig.csv";// 表头 Id,Score,...（价在 Score=第 2 列）
    const string StickerPriceCsv = Dir + "PaintingPriceConfig.csv";         // 表头 Id,Score,...（价在 Score=第 2 列）

    [PropertySpace(8)]
    [Button("一键从 CSV 导入(全部)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("从上述三张 CSV 导入（清空覆盖）：精灵表(SpriteKey 第3列)、框架售价与贴纸售价(均取 Score 第2列)。" +
             "Id 不可解析的行(BOM/类型/中文表头)自动跳过。", InfoMessageType.Info)]
    void ImportAllFromCsv()
    {
        spriteKeys = ReadStrDict(SpriteCsv, 2);
        // CSV 只填精灵名，这里补成完整 AA Key（路径+扩展名），否则运行时按名字加载不到图标
        var spriteIds = new List<long>(spriteKeys.Keys);
        foreach(long id in spriteIds)
            spriteKeys[id] = BuildSpriteKey(spriteKeys[id]);
        framePrices = ReadIntDict(FramePriceCsv, 1);
        stickerPrices = ReadIntDict(StickerPriceCsv, 1);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryMoldMgConfig] 导入完成：精灵 {spriteKeys.Count}、框架售价 {framePrices.Count}、贴纸售价 {stickerPrices.Count}。");
    }

    [PropertySpace(8)]
    [Button("对照 ItemConfig 检查配置(查缺/查多)", ButtonSizes.Large), GUIColor(1f, 0.9f, 0.6f)]
    [InfoBox("对照 ItemConfig 里的框架(FigureModel)/贴纸(Painting)：\n" +
             "· 查缺——哪些物品没配画布精灵 / 售价；\n" +
             "· 查多——本表配了但 ItemConfig 中不存在、或类型不符的多余项。\n结果打印到 Console。", InfoMessageType.Info)]
    void CheckAgainstItemConfig()
    {
        ItemConfig cfg = LoadItemConfig();
        if(cfg == null)
        {
            Debug.LogError("[FactoryMoldMgConfig] 未找到 ItemConfig 资产，无法检查。");
            return;
        }

        // ItemConfig 里的框架 / 贴纸 Id 集合
        var frameIds = new HashSet<long>();
        var stickerIds = new HashSet<long>();
        foreach(ItemData d in cfg.ItemDataDict.Values)
        {
            if(d == null)
                continue;
            if(d.Type == ItemType.FigureModel)
                frameIds.Add(d.Id);
            else if(d.Type == ItemType.Painting)
                stickerIds.Add(d.Id);
        }

        // 已配画布精灵(值非空)的 Id
        var spriteConfigured = new HashSet<long>();
        foreach(KeyValuePair<long, string> kv in spriteKeys)
            if(!string.IsNullOrEmpty(kv.Value))
                spriteConfigured.Add(kv.Key);

        var validSprite = new HashSet<long>(frameIds);
        validSprite.UnionWith(stickerIds);

        var sb = new StringBuilder();
        sb.AppendLine($"[FactoryMoldMgConfig] 配置检查：ItemConfig 框架 {frameIds.Count} 种、贴纸 {stickerIds.Count} 种。");
        int issues = 0;

        // 查缺：框架 + 贴纸都要有画布精灵；框架要有框架售价、贴纸要有贴纸售价
        issues += AppendDiff(sb, "框架 缺【画布精灵】", frameIds, spriteConfigured);
        issues += AppendDiff(sb, "贴纸 缺【画布精灵】", stickerIds, spriteConfigured);
        issues += AppendDiff(sb, "框架 缺【框架售价】", frameIds, framePrices.Keys);
        issues += AppendDiff(sb, "贴纸 缺【贴纸售价】", stickerIds, stickerPrices.Keys);

        // 查多：本表配了但 ItemConfig 中不是对应类型 / 不存在
        issues += AppendDiff(sb, "多余【画布精灵】(非框架/贴纸或不存在)", spriteConfigured, validSprite);
        issues += AppendDiff(sb, "多余【框架售价】(非框架或不存在)", framePrices.Keys, frameIds);
        issues += AppendDiff(sb, "多余【贴纸售价】(非贴纸或不存在)", stickerPrices.Keys, stickerIds);

        if(issues == 0)
        {
            sb.AppendLine("  ✔ 无缺漏、无多余，配置与 ItemConfig 一致。");
            Debug.Log(sb.ToString(), this);
        }
        else
        {
            sb.AppendLine($"  共 {issues} 类问题，详见上方。");
            Debug.LogWarning(sb.ToString(), this);
        }
    }

    // 输出 required 中不在 have 里的 Id（缺/多两用）；返回本行是否有内容(0/1)。
    static int AppendDiff(StringBuilder sb, string label, ICollection<long> required, ICollection<long> have)
    {
        var diff = new List<long>();
        foreach(long id in required)
            if(!have.Contains(id))
                diff.Add(id);
        if(diff.Count == 0)
            return 0;
        diff.Sort();
        sb.AppendLine($"  · {label}：{diff.Count} 个 → {string.Join(", ", diff)}");
        return 1;
    }

    static ItemConfig LoadItemConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemConfig");
        if(guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<ItemConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // 读 CSV 为 Id→字符串(第 valueCol 列)。Id 不可解析(BOM/类型/中文表头)的行跳过；值为空的跳过。
    static Dictionary<long, string> ReadStrDict(string assetPath, int valueCol)
    {
        var dict = new Dictionary<long, string>();
        string full = Path.GetFullPath(assetPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[FactoryMoldMgConfig] 未找到 CSV：{assetPath}");
            return dict;
        }
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line))
                continue;
            string[] c = line.Split(',');
            if(!long.TryParse(c[0].Trim().TrimStart('﻿'), out long id))
                continue;
            string val = valueCol < c.Length ? c[valueCol].Trim() : "";
            if(!string.IsNullOrEmpty(val))
                dict[id] = val;
        }
        return dict;
    }

    // 读 CSV 为 Id→int(第 valueCol 列)。
    static Dictionary<long, int> ReadIntDict(string assetPath, int valueCol)
    {
        var dict = new Dictionary<long, int>();
        string full = Path.GetFullPath(assetPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[FactoryMoldMgConfig] 未找到 CSV：{assetPath}");
            return dict;
        }
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line))
                continue;
            string[] c = line.Split(',');
            if(!long.TryParse(c[0].Trim().TrimStart('﻿'), out long id))
                continue;
            if(valueCol < c.Length && int.TryParse(c[valueCol].Trim(), out int v))
                dict[id] = v;
        }
        return dict;
    }
#endif
}
