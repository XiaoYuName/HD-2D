using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
#endif

/// <summary>
/// 「物料制作」面板配置：集中管理画布精灵、模具蒙版与框架/贴纸的基础成本。
/// - 画布精灵：物品 Id → <b>画布显示精灵</b>(Addressable Key)，用于框架底图与贴纸高清显示，区别于物品 128×128 图标(查不到回退缺省图并 LogError)。
/// - 模具蒙版：物品 Id → 蒙版图 AA Key（叠在贴纸最上层，遮住溢出框架外的部分），仅部分框架需要，非必配。
/// - 基础成本：框架 Id → frameCost，来自 Data/Factory/FactoryFrameCostConfig.csv（成本取「Score」列）；
///            贴纸 Id → stickerCost，来自 Data/Factory/PaintingCostConfig.csv（成本取「Score」列）。
///            预估制作成本 = frameCost + stickerCost（面板 <see cref="FactoryMoldMgPanel"/> 计算展示）。
/// 合成结果物品 Id 由编码规则直接算出（见 <see cref="GetCraftResultId"/>），不再维护合成表；结果物品由
/// ItemConfig 导入时的 ItemConfigSupplementRunner 自动生成，本配置不参与生成。
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

    [InfoBox("物品 Id → 模具蒙版图 AA Key（叠在贴纸最上层，遮住溢出框架外的部分）。仅部分框架需要，未配置不显示，非必配项。")]
    [LabelText("Id → 模具蒙版图 AA Key")]
    [SerializeField] Dictionary<long, string> maskKeys = new ();

    [LabelText("框架 Id → 基础成本")]
    [SerializeField] Dictionary<long, int> frameCost = new ();

    [LabelText("贴纸 Id → 基础成本")]
    [SerializeField] Dictionary<long, int> stickerCost = new ();

    // 合成结果 Id 编码规则（须与 ItemConfigSupplement.FactoryProductionMaterialsSupplementStrategy 保持一致）：
    // 结果Id = 400000 + (框架Id-210000)*1000 + (贴纸Id-200000)。改这里也要同步改那边，否则查不到合成物品。
    const long SynthResultIdBase  = 400000L;
    const long SynthFrameIdBase   = 210000L;
    const long SynthStickerIdBase = 200000L;
    const long SynthFrameStride   = 1000L;   // 每个框架下最多 1000 个贴纸

    #region Get
    /// <summary>取该物品的画布精灵 AA Key；未配置时 LogError 并回退到 <see cref="defaultSpriteKey"/>（不再回退物品 128×128 图标）。</summary>
    public string GetSpriteKey(long itemId)
    {
        if(spriteKeys.TryGetValue(itemId, out string k) && !string.IsNullOrEmpty(k))
            return k;
        Debug.LogError($"[FactoryMoldMgConfig] 物品 {itemId} 未配置画布精灵，回退缺省图「{defaultSpriteKey}」。请在 FactoryMoldSpriteConfig.csv 补配后重新导入。");
        return BuildSpriteKey(defaultSpriteKey);
    }

    public static string BuildSpriteKey(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return "";
        string n = name.Trim().Replace("\\", "/");
        if(n.StartsWith("Assets/"))
            return n;

        if(!n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            n += ".png";
        return AssetPathSet.FactoryMoldMgSpritePath + n;
    }

    // 把 Id→精灵裸名字典就地转成 Id→完整 AA Key（导入 CSV 后统一补全路径）
    static void ResolveSpriteKeys(Dictionary<long, string> dict)
    {
        var ids = new List<long>(dict.Keys);
        foreach(long id in ids)
            dict[id] = BuildSpriteKey(dict[id]);
    }

    /// <summary>取该物品的模具蒙版图 AA Key；未配置返回空（非必配，多数物品无需蒙版）。</summary>
    public string GetMaskKey(long itemId)
    {
        if(maskKeys.TryGetValue(itemId, out string k) && !string.IsNullOrEmpty(k))
            return k;

        Debug.LogWarning($"[FactoryMoldMgConfig] 物品 {itemId} 未配置模具蒙版图。");
        return "";
    }

    /// <summary>取框架基础成本；未配置返回 0。</summary>
    public int GetFramePrice(long frameItemId)
        => frameCost.TryGetValue(frameItemId, out int v) ? v : 0;

    /// <summary>取贴纸基础成本；未配置返回 0。</summary>
    public int GetStickerPrice(long stickerItemId)
        => stickerCost.TryGetValue(stickerItemId, out int v) ? v : 0;

    /// <summary>
    /// 按「框架Id + 贴纸Id」用编码规则算出合成结果物品 Id（ItemConfig 里的真实生产资料物品 Id）。
    /// 规则与 ItemConfig 导入时生成生产资料的 ItemConfigSupplementRunner 一致，故不再维护合成表。
    /// 结果供 <see cref="FactoryMoldMgPanel"/> 完成制作时用 ItemManager.GetItemData 取数并创建 ItemInfo。
    /// </summary>
    public long GetCraftResultId(long frameId, long stickerId)
        => SynthResultIdBase + (frameId - SynthFrameIdBase) * SynthFrameStride + (stickerId - SynthStickerIdBase);
    #endregion

#if UNITY_EDITOR
    const string Dir = "Assets/0 Core/1 Script/Data/Factory/";
    const string SpriteCsv = Dir + "FactoryMoldSpriteConfig.csv";        // 表头 Id,Remark,SpriteKey,MaskKey（SpriteKey 第3列/MaskKey 第4列；合成成品图也走 SpriteKey）
    const string FrameCostCsv = Dir + "FactoryFrameCostConfig.csv";      // 表头 Id,Remark,Score,...（成本在 Score=第 3 列）
    const string StickerCostCsv = Dir + "PaintingCostConfig.csv";        // 表头 Id,Remark,Score,...（成本在 Score=第 3 列）

    [PropertySpace(8)]
    [Button("一键从 FactoryMoldSpriteConfig CSV 导入(全部)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("从 FactoryMoldSpriteConfig CSV 导入（清空覆盖）：精灵表(SpriteKey 第3列/MaskKey 第4列，合成成品图也配在 SpriteKey)、框架成本与贴纸成本(均取 Score 第3列)。" +
             "Id 不可解析的行(BOM/类型/中文表头)自动跳过。", InfoMessageType.Info)]
    void ImportAllFromCsv()
    {
        // CSV 只填精灵裸名，这里补成完整 AA Key（路径+扩展名），否则运行时按名字加载不到图标
        spriteKeys = ReadStrDict(SpriteCsv, 2);
        ResolveSpriteKeys(spriteKeys);
        maskKeys = ReadStrDict(SpriteCsv, 3);
        ResolveSpriteKeys(maskKeys);
        // 成本表列序为 Id,Remark,Score → Score 在第 3 列(下标 2)，取 Remark(下标 1) 会解析失败被跳过
        frameCost = ReadIntDict(FrameCostCsv, 2);
        stickerCost = ReadIntDict(StickerCostCsv, 2);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryMoldMgConfig] 导入完成：精灵 {spriteKeys.Count}、蒙版 {maskKeys.Count}、框架成本 {frameCost.Count}、贴纸成本 {stickerCost.Count}。");
    }

    [PropertySpace(8)]
    [Button("对照 ItemConfig 检查配置(查缺/查多)", ButtonSizes.Large), GUIColor(1f, 0.9f, 0.6f)]
    [InfoBox("对照 ItemConfig 里的框架(FigureModel)/贴纸(Painting)：\n" +
             "· 查缺——哪些物品没配画布精灵 / 成本；\n" +
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

        // 查缺：框架 + 贴纸都要有画布精灵；框架要有框架成本、贴纸要有贴纸成本
        issues += AppendDiff(sb, "框架 缺【画布精灵】", frameIds, spriteConfigured);
        issues += AppendDiff(sb, "贴纸 缺【画布精灵】", stickerIds, spriteConfigured);
        issues += AppendDiff(sb, "框架 缺【框架成本】", frameIds, frameCost.Keys);
        issues += AppendDiff(sb, "贴纸 缺【贴纸成本】", stickerIds, stickerCost.Keys);

        // 查多：本表配了但 ItemConfig 中不是对应类型 / 不存在
        issues += AppendDiff(sb, "多余【画布精灵】(非框架/贴纸或不存在)", spriteConfigured, validSprite);
        issues += AppendDiff(sb, "多余【框架成本】(非框架或不存在)", frameCost.Keys, frameIds);
        issues += AppendDiff(sb, "多余【贴纸成本】(非贴纸或不存在)", stickerCost.Keys, stickerIds);

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
