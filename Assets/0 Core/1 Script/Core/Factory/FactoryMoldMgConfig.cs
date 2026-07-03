using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
#endif

/// <summary>
/// 合成表一条：「框架(FigureModel) + 贴纸(Painting)」→ 合成结果物品 Id（该结果物品是 ItemConfig 里的真实配置物品）。
/// </summary>
[Serializable]
public class FactoryMoldCraftRecipe
{
    [LabelText("框架Id(FigureModel)")] public long frameId;
    [LabelText("贴纸Id(Painting)")] public long stickerId;
    [LabelText("合成结果物品Id")] public long resultId;
    [LabelText("备注(先贴纸后框架名)")] public string remark;
}

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

    [InfoBox("物品 Id → 模具蒙版图 AA Key（叠在贴纸最上层，遮住溢出框架外的部分）。仅部分框架需要，未配置不显示，非必配项。")]
    [LabelText("Id → 模具蒙版图 AA Key")]
    [SerializeField] Dictionary<long, string> maskKeys = new ();

    [LabelText("框架 Id → 基础成本")]
    [SerializeField] Dictionary<long, int> framePrices = new ();

    [LabelText("贴纸 Id → 基础成本")]
    [SerializeField] Dictionary<long, int> stickerPrices = new ();

    [Title("合成表（框架+贴纸 → 合成物品）")]
    [InfoBox("完成制作时用「框架Id+贴纸Id」在此查出合成结果物品 Id，再从 ItemConfig 取 ItemData 创建 ItemInfo。\n" +
             "本表与 Data/Factory/FactoryMoldCraftConfig.csv 互通，可用下方按钮一键生成/导入。")]
    [LabelText("合成配方(框架Id + 贴纸Id → 结果物品Id)")]
    [SerializeField] List<FactoryMoldCraftRecipe> craftRecipes = new ();

    // 运行时查询字典：key = 框架Id*因子 + 贴纸Id（贴纸Id 需 < 因子）。惰性构建，编辑期导入/生成后置空重建。
    [NonSerialized] Dictionary<long, long> craftLookup;
    const long CraftKeyFactor = 1_000_000L;

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
    /// 裸名支持相对子路径，素材已按类型分到该目录下的 MoldFrame(框架+蒙版)/Mold(合成成品图)/Sticker(贴纸) 三个子文件夹，
    /// 例如 "MoldFrame/Mold"、"Mold/Mold1Sticker1MoldFrame1"、"Sticker/Sticker1"。
    /// </summary>
    public static string BuildSpriteKey(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return "";
        string n = name.Trim().Replace("\\", "/");
        if(n.StartsWith("Assets/"))
            return n;

        if(!n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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
    public string GetMaskKey(long itemId) => maskKeys.TryGetValue(itemId, out string k) ? k : "";

    /// <summary>取框架售价；未配置返回 <paramref name="fallback"/>。</summary>
    public int GetFramePrice(long frameItemId)
        => framePrices.TryGetValue(frameItemId, out int v) ? v : 0;

    /// <summary>取贴纸售价；未配置返回 <paramref name="fallback"/>。</summary>
    public int GetStickerPrice(long stickerItemId)
        => stickerPrices.TryGetValue(stickerItemId, out int v) ? v : 0;

    /// <summary>
    /// 按「框架Id + 贴纸Id」查合成结果物品 Id（ItemConfig 里的真实物品 Id）；未配置返回 0。
    /// 结果 Id 供 <see cref="FactoryMoldMgPanel"/> 完成制作时用 ItemManager.GetItemData 取数并创建 ItemInfo。
    /// </summary>
    public long GetCraftResultId(long frameId, long stickerId)
    {
        craftLookup ??= BuildCraftLookup();
        return craftLookup.TryGetValue(CraftKey(frameId, stickerId), out long r) ? r : 0;
    }

    static long CraftKey(long frameId, long stickerId) => frameId * CraftKeyFactor + stickerId;

    Dictionary<long, long> BuildCraftLookup()
    {
        var dict = new Dictionary<long, long>(craftRecipes.Count);
        foreach(FactoryMoldCraftRecipe r in craftRecipes)
            if(r != null && r.resultId > 0)
                dict[CraftKey(r.frameId, r.stickerId)] = r.resultId;
        return dict;
    }
    #endregion

#if UNITY_EDITOR
    const string Dir = "Assets/0 Core/1 Script/Data/Factory/";
    const string SpriteCsv = Dir + "FactoryMoldSpriteConfig.csv";           // 表头 Id,Remark,SpriteKey,MaskKey（依次在第 3/4 列；合成成品图也走 SpriteKey）
    const string FramePriceCsv = Dir + "FactoryProductionMtPriceConfig.csv";// 表头 Id,Score,...（价在 Score=第 2 列）
    const string StickerPriceCsv = Dir + "PaintingPriceConfig.csv";         // 表头 Id,Score,...（价在 Score=第 2 列）
    const string CraftCsv = Dir + "FactoryMoldCraftConfig.csv";             // 合成表：表头 FrameId,StickerId,ResultId,Remark

    // —— 合成物品脚手架（Task 3，临时工具）——
    const string SupplementItemCsv = Dir + "FactorySynthesisItemSupplement.csv";  // 列同 ItemConfig.csv，供手动粘贴补充
    const string SupplementLocCsv  = Dir + "FactorySynthesisItemLoc.csv";         // 列同 ItemConfigL.csv，供手动粘贴补充
    // 读取框架/贴纸各语言译名以拼接合成名；路径统一由 ItemConfigPaths 管理，见 ItemConfigSupplement.cs

    // —— 周边商品(Merchandise)补充表：生产资料(模具)加工完成后发放的成品，Id = 生产资料 Id + FactoryProductData.MerchandiseIdOffset ——
    const string MerchandiseItemCsv = Dir + "FactoryMerchandiseSupplement.csv";   // 列同 ItemConfig.csv
    const string MerchandiseLocCsv  = Dir + "FactoryMerchandiseLoc.csv";          // 列同 ItemConfigL.csv

    // 合成结果 Id 编码约定（临时）：base + (框架Id-框架base)*步长 + (贴纸Id-贴纸base)。当前框架 210000+、贴纸 200000+，产物落在空闲的 4xxxxx 段。
    const long SynthResultIdBase  = 400000L;
    const long SynthFrameIdBase   = 210000L;
    const long SynthStickerIdBase = 200000L;
    const long SynthFrameStride   = 1000L;   // 每个框架下最多 1000 个贴纸

    static long ComposeSynthResultId(long frameId, long stickerId)
        => SynthResultIdBase + (frameId - SynthFrameIdBase) * SynthFrameStride + (stickerId - SynthStickerIdBase);

    [PropertySpace(8)]
    [Button("一键从 CSV 导入(全部)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("从 CSV 导入（清空覆盖）：精灵表(SpriteKey 第3列/MaskKey 第4列，合成成品图也配在 SpriteKey)、框架售价与贴纸售价(均取 Score 第2列)、合成表(FrameId,StickerId,ResultId)。" +
             "Id 不可解析的行(BOM/类型/中文表头)自动跳过。", InfoMessageType.Info)]
    void ImportAllFromCsv()
    {
        // CSV 只填精灵裸名，这里补成完整 AA Key（路径+扩展名），否则运行时按名字加载不到图标
        spriteKeys = ReadStrDict(SpriteCsv, 2);
        ResolveSpriteKeys(spriteKeys);
        maskKeys = ReadStrDict(SpriteCsv, 3);
        ResolveSpriteKeys(maskKeys);
        framePrices = ReadIntDict(FramePriceCsv, 1);
        stickerPrices = ReadIntDict(StickerPriceCsv, 1);
        craftRecipes = ReadCraftRecipes(CraftCsv);
        craftLookup = null;   // 重建运行时查询字典
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FactoryMoldMgConfig] 导入完成：精灵 {spriteKeys.Count}、蒙版 {maskKeys.Count}、框架售价 {framePrices.Count}、贴纸售价 {stickerPrices.Count}、合成表 {craftRecipes.Count}。");
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

    #region 合成表
    [PropertySpace(12)]
    [Button("生成合成表 + 合成物品补充表(ItemConfig/ItemConfigLoc)", ButtonSizes.Large), GUIColor(0.7f, 0.85f, 1f)]
    [InfoBox("枚举 ItemConfig 中所有 框架(FigureModel)×贴纸(Painting) 组合：\n" +
             "① 本资产合成表(craftRecipes) 并导出 FactoryMoldCraftConfig.csv；\n" +
             "② FactorySynthesisItemSupplement.csv（列同 ItemConfig.csv，手动粘贴补充合成物品）；\n" +
             "③ FactorySynthesisItemLoc.csv（列同 ItemConfigLoc.csv，名称=先贴纸名后框架名，手动粘贴补充）。\n" +
             "结果Id编码：400000+(框架Id-210000)*1000+(贴纸Id-200000)。补充表不自动导入 ItemConfig。", InfoMessageType.Warning)]
    void GenerateSynthesisScaffold()
    {
        ItemConfig cfg = LoadItemConfig();
        if(cfg == null)
        {
            Debug.LogError("[FactoryMoldMgConfig] 未找到 ItemConfig 资产，无法生成。");
            return;
        }

        // 框架 / 贴纸，按 Id 升序
        var frames = new List<ItemData>();
        var stickers = new List<ItemData>();
        foreach(ItemData d in cfg.ItemDataDict.Values)
        {
            if(d == null) continue;
            if(d.Type == ItemType.FigureModel) frames.Add(d);
            else if(d.Type == ItemType.Painting) stickers.Add(d);
        }
        if(frames.Count == 0 || stickers.Count == 0)
        {
            Debug.LogError($"[FactoryMoldMgConfig] 框架 {frames.Count} / 贴纸 {stickers.Count} 数量不足，无法生成组合。");
            return;
        }
        frames.Sort((a, b) => a.Id.CompareTo(b.Id));
        stickers.Sort((a, b) => a.Id.CompareTo(b.Id));

        // 读 ItemConfigL：key → 各语言译名单元格，用于拼接合成名（先贴纸后框架）
        ReadLoc(out string[] locHeader, out Dictionary<string, string[]> locMap, out int lastLangCol);

        var recipes = new List<FactoryMoldCraftRecipe>(frames.Count * stickers.Count);
        var itemSb = new StringBuilder();
        var locSb = new StringBuilder();
        var craftSb = new StringBuilder();

        // 表头
        itemSb.AppendLine("Id,Remark,Name,Icon,Desc,Type,TypeNum,备注,Synthesis,MaxNum,Shop,CurrencyType,Value,PurchaseRestriction,Quality");
        locSb.AppendLine(locHeader != null ? JoinCols(locHeader, lastLangCol) : "Key,Id,Chinese (Simplified)(zh-CN)");
        craftSb.AppendLine("FrameId,StickerId,ResultId,Remark");

        int outRange = 0;
        foreach(ItemData frame in frames)
        foreach(ItemData sticker in stickers)
        {
            long resultId = ComposeSynthResultId(frame.Id, sticker.Id);
            if(frame.Id < SynthFrameIdBase || sticker.Id < SynthStickerIdBase ||
               (sticker.Id - SynthStickerIdBase) >= SynthFrameStride)
                outRange++;

            string nameZh = sticker.Remark + frame.Remark;                 // 先贴纸名后框架名，如 星野休憩抱枕
            string nameKey = resultId + "Name";
            string descKey = resultId + "Text";
            int value = sticker.Value + frame.Value;
            int quality = Mathf.Max(sticker.Quality, frame.Quality);

            recipes.Add(new FactoryMoldCraftRecipe { frameId = frame.Id, stickerId = sticker.Id, resultId = resultId, remark = nameZh });
            craftSb.AppendLine($"{frame.Id},{sticker.Id},{resultId},{nameZh}");

            // ItemConfig 补充行（Type=11 生产资料；Icon/描述文本留待手动补，Value/品质给默认）
            itemSb.AppendLine($"{resultId},{nameZh},{nameKey},,{descKey},{(int)ItemType.FactoryProductionMaterials},,,,999,,1,{value},,{quality}");

            // ItemConfigL 补充：名称行(各语言=贴纸译名+框架译名)、描述行(占位=名称)
            string[] stickerLoc = LocOf(locMap, sticker.NameKey);
            string[] frameLoc = LocOf(locMap, frame.NameKey);
            locSb.AppendLine(BuildLocLine(nameKey, lastLangCol, stickerLoc, frameLoc, sticker.Remark, frame.Remark));
            locSb.AppendLine(BuildLocLine(descKey, lastLangCol, stickerLoc, frameLoc, sticker.Remark, frame.Remark));
        }

        craftRecipes = recipes;
        craftLookup = null;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        // 带 BOM 的 UTF-8：否则 Excel 会按系统 ANSI(GBK)解码导致中文乱码
        File.WriteAllText(Path.GetFullPath(CraftCsv), craftSb.ToString(), new UTF8Encoding(true));
        File.WriteAllText(Path.GetFullPath(SupplementItemCsv), itemSb.ToString(), new UTF8Encoding(true));
        File.WriteAllText(Path.GetFullPath(SupplementLocCsv), locSb.ToString(), new UTF8Encoding(true));
        AssetDatabase.Refresh();

        string warn = outRange > 0 ? $" ⚠ 有 {outRange} 个组合的框架/贴纸 Id 超出编码假定范围(框架≥{SynthFrameIdBase}、贴纸[{SynthStickerIdBase},+{SynthFrameStride}))，结果Id可能冲突，请检查。" : "";
        Debug.Log($"[FactoryMoldMgConfig] 已生成合成表 {recipes.Count} 条（框架 {frames.Count}×贴纸 {stickers.Count}）。\n" +
                  $"· 合成表：{CraftCsv}\n· ItemConfig 补充：{SupplementItemCsv}\n· ItemConfigL 补充：{SupplementLocCsv}\n" +
                  $"请把两张补充表内容手动粘贴进 ItemConfig.csv / ItemConfigL.csv 后重新导入，再用下方按钮检查。{warn}", this);
    }

    [PropertySpace(8)]
    [Button("【导入】补充表 → ItemConfig + 物品多语言(InventoryItem)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("把两张补充表一键导入（均为「合并」，不清空既有内容）：\n" +
             "① FactorySynthesisItemLoc.csv → InventoryItem 物品多语言表（同 Key 覆盖各语言值）；\n" +
             "② FactorySynthesisItemSupplement.csv → ItemConfig.asset 的 itemDataDict（同 Id 覆盖）。\n" +
             "导入后可用下方『检查』确认合成物品已全部配入 ItemConfig。", InfoMessageType.Info)]
    void ImportSynthesisSupplement()
    {
        // ① 多语言：合并进 InventoryItem 表（物品名 / 描述所在表），同 Key 覆盖
        int locAdded = 0, locUpdated = 0;
        string locFull = Path.GetFullPath(SupplementLocCsv);
        if(!File.Exists(locFull))
            Debug.LogError($"[FactoryMoldMgConfig] 未找到多语言补充表：{SupplementLocCsv}（可先点『生成…补充表』）");
        else
            (locAdded, locUpdated) = MergeLocIntoInventoryItem(File.ReadAllText(locFull, Encoding.UTF8));

        // ② 道具：合并进 ItemConfig.itemDataDict，同 Id 覆盖
        int itemAdded = 0, itemUpdated = 0;
        ItemConfig cfg = LoadItemConfig();
        if(cfg == null)
            Debug.LogError("[FactoryMoldMgConfig] 未找到 ItemConfig 资产，无法导入合成物品。");
        else
            (itemAdded, itemUpdated) = ItemConfigImporter.MergeItemsFromCsv(cfg, SupplementItemCsv);

        Debug.Log($"[FactoryMoldMgConfig] 补充表导入完成：\n" +
                  $"· 物品多语言(InventoryItem)：新增 Key {locAdded}，更新 Key {locUpdated}\n" +
                  $"· ItemConfig 道具：新增 {itemAdded}，更新 {itemUpdated}", this);
    }

    [PropertySpace(8)]
    [Button("【导入】周边商品补充表 → ItemConfig + 物品多语言(InventoryItem)", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    [InfoBox("把「生产资料(模具) 加工完成后发放的周边商品(Merchandise)」补充表一键导入（均为「合并」，不清空既有内容）：\n" +
             "① FactoryMerchandiseLoc.csv → InventoryItem 物品多语言表（同 Key 覆盖各语言值）；\n" +
             "② FactoryMerchandiseSupplement.csv → ItemConfig.asset 的 itemDataDict（同 Id 覆盖）。\n" +
             "结果 Id = 生产资料 Id + 100000（见 FactoryProductData.MerchandiseIdOffset），供加工小游戏结束发放使用。", InfoMessageType.Info)]
    void ImportMerchandiseSupplement()
    {
        // ① 多语言：合并进 InventoryItem 表，同 Key 覆盖
        int locAdded = 0, locUpdated = 0;
        string locFull = Path.GetFullPath(MerchandiseLocCsv);
        if(!File.Exists(locFull))
            Debug.LogError($"[FactoryMoldMgConfig] 未找到周边商品多语言补充表：{MerchandiseLocCsv}");
        else
            (locAdded, locUpdated) = MergeLocIntoInventoryItem(File.ReadAllText(locFull, Encoding.UTF8));

        // ② 道具：合并进 ItemConfig.itemDataDict，同 Id 覆盖
        int itemAdded = 0, itemUpdated = 0;
        ItemConfig cfg = LoadItemConfig();
        if(cfg == null)
            Debug.LogError("[FactoryMoldMgConfig] 未找到 ItemConfig 资产，无法导入周边商品。");
        else
            (itemAdded, itemUpdated) = ItemConfigImporter.MergeItemsFromCsv(cfg, MerchandiseItemCsv);

        Debug.Log($"[FactoryMoldMgConfig] 周边商品补充表导入完成：\n" +
                  $"· 物品多语言(InventoryItem)：新增 Key {locAdded}，更新 Key {locUpdated}\n" +
                  $"· ItemConfig 道具：新增 {itemAdded}，更新 {itemUpdated}", this);
    }

    // 把物品多语言补充 CSV 合并进 InventoryItem 字符串表集合（同 Key 覆盖各语言值）。
    // 通用 LocalizationCsvMerger 在 Editor 专用程序集，本运行时程序集访问不到，故就地实现精简合并。
    // 表头沿用 Unity 导出格式：Key,Id,Chinese (Simplified)(zh-CN),English(en),... 返回 (新增Key, 更新Key)。
    static (int added, int updated) MergeLocIntoInventoryItem(string csvText)
    {
        StringTableCollection col = LocalizationEditorSettings.GetStringTableCollection(LocalizeTableSet.InventoryItem);
        if(col == null)
        {
            Debug.LogError($"[FactoryMoldMgConfig] 未找到物品多语言表集合「{LocalizeTableSet.InventoryItem}」，无法导入多语言。");
            return (0, 0);
        }

        string[] lines = csvText.Replace("\r", "").Split('\n');
        if(lines.Length < 2)
            return (0, 0);

        // 表头：定位 Key 列与各语言列（语言列按表头末尾括号里的语言代码匹配集合中已有的语言表，缺失的列自动跳过）
        string[] header = lines[0].Split(',');
        int keyCol = -1;
        var localeCols = new List<(int col, StringTable table)>();
        for(int c = 0; c < header.Length; c++)
        {
            string h = header[c].Trim().TrimStart('﻿');
            if(h.Equals("Key", StringComparison.OrdinalIgnoreCase)) { keyCol = c; continue; }
            if(h.Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
            string code = ExtractLocale(h);
            if(!string.IsNullOrEmpty(code) && col.GetTable(code) is StringTable t)
                localeCols.Add((c, t));
        }
        if(keyCol < 0)
        {
            Debug.LogError("[FactoryMoldMgConfig] 多语言补充表缺少 Key 列，导入中止。");
            return (0, 0);
        }

        SharedTableData shared = col.SharedData;
        int added = 0, updated = 0;
        for(int i = 1; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cells = lines[i].Split(',');
            if(keyCol >= cells.Length) continue;
            string key = cells[keyCol].Trim();
            if(string.IsNullOrEmpty(key)) continue;

            if(shared.Contains(key)) updated++;
            else { shared.AddKey(key); added++; }

            foreach((int c, StringTable table) in localeCols)
            {
                if(c >= cells.Length) continue;
                string val = cells[c].Trim();
                if(string.IsNullOrEmpty(val)) continue;
                StringTableEntry entry = table.GetEntry(key) ?? table.AddEntry(key, val);
                entry.Value = val;   // 同 Key 覆盖
                EditorUtility.SetDirty(table);
            }
        }

        EditorUtility.SetDirty(shared);
        AssetDatabase.SaveAssets();
        return (added, updated);
    }

    // 取表头末尾括号内语言代码：如 "Chinese (Simplified)(zh-CN)" → "zh-CN"，"English(en)" → "en"
    static string ExtractLocale(string header)
    {
        int open = header.LastIndexOf('(');
        int close = header.LastIndexOf(')');
        return (open >= 0 && close > open) ? header.Substring(open + 1, close - open - 1).Trim() : null;
    }

    [PropertySpace(8)]
    [Button("【检查】ItemConfig 是否已配全部合成物品", ButtonSizes.Large), GUIColor(1f, 0.9f, 0.6f)]
    [InfoBox("按本资产合成表(craftRecipes) 逐条检查 ItemConfig：结果物品 Id 是否存在、且 Type 为 FactoryProductionMaterials(11)。缺失/类型不符打印到 Console。", InfoMessageType.Info)]
    void CheckSynthesisItems()
    {
        if(craftRecipes == null || craftRecipes.Count == 0)
        {
            Debug.LogWarning("[FactoryMoldMgConfig] 合成表为空，请先『生成/导入合成表』。");
            return;
        }
        ItemConfig cfg = LoadItemConfig();
        if(cfg == null)
        {
            Debug.LogError("[FactoryMoldMgConfig] 未找到 ItemConfig 资产，无法检查。");
            return;
        }

        var missing = new List<long>();
        var typeMismatch = new List<long>();
        int ok = 0;
        foreach(FactoryMoldCraftRecipe r in craftRecipes)
        {
            if(r == null || r.resultId <= 0) continue;
            ItemData d = cfg.GetItemData(r.resultId);
            if(d == null) missing.Add(r.resultId);
            else if(d.Type != ItemType.FactoryProductionMaterials) typeMismatch.Add(r.resultId);
            else ok++;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[FactoryMoldMgConfig] 合成物品检查：合成表 {craftRecipes.Count} 条，已正确配入 ItemConfig {ok} 个。");
        if(missing.Count > 0)
        {
            missing.Sort();
            sb.AppendLine($"  · ItemConfig 缺失(未配置)：{missing.Count} 个 → {string.Join(", ", missing)}");
        }
        if(typeMismatch.Count > 0)
        {
            typeMismatch.Sort();
            sb.AppendLine($"  · 类型不符(应为 FactoryProductionMaterials=11)：{typeMismatch.Count} 个 → {string.Join(", ", typeMismatch)}");
        }
        if(missing.Count == 0 && typeMismatch.Count == 0)
        {
            sb.AppendLine("  ✔ 合成表引用的合成物品已全部配入 ItemConfig。");
            Debug.Log(sb.ToString(), this);
        }
        else
        {
            Debug.LogWarning(sb.ToString(), this);
        }
    }

    // 读合成表 CSV：FrameId,StickerId,ResultId,Remark。表头/不可解析行自动跳过。
    static List<FactoryMoldCraftRecipe> ReadCraftRecipes(string assetPath)
    {
        var list = new List<FactoryMoldCraftRecipe>();
        string full = Path.GetFullPath(assetPath);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[FactoryMoldMgConfig] 未找到合成表 CSV：{assetPath}（可先点『生成合成表』）");
            return list;
        }
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line)) continue;
            string[] c = line.Split(',');
            if(!long.TryParse(c[0].Trim().TrimStart('﻿'), out long fid)) continue;
            long sid = c.Length > 1 && long.TryParse(c[1].Trim(), out long s) ? s : 0;
            long rid = c.Length > 2 && long.TryParse(c[2].Trim(), out long r) ? r : 0;
            string remark = c.Length > 3 ? c[3].Trim() : "";
            list.Add(new FactoryMoldCraftRecipe { frameId = fid, stickerId = sid, resultId = rid, remark = remark });
        }
        return list;
    }

    // 读 ItemConfigL.csv：首行=表头，其余 key→各语言单元格；lastLangCol=最后一个非空表头列下标（名称无逗号，简单切分即可）。
    static void ReadLoc(out string[] header, out Dictionary<string, string[]> map, out int lastLangCol)
    {
        header = null;
        map = new Dictionary<string, string[]>();
        lastLangCol = 2;
        string full = Path.GetFullPath(ItemConfigPaths.ItemConfigLocCsv);
        if(!File.Exists(full))
        {
            Debug.LogWarning($"[FactoryMoldMgConfig] 未找到 {ItemConfigPaths.ItemConfigLocCsv}，合成名将回退用 Remark 拼接。");
            return;
        }
        foreach(string line in File.ReadAllLines(full, Encoding.UTF8))
        {
            if(string.IsNullOrWhiteSpace(line)) continue;
            string[] cells = line.Split(',');
            if(header == null)
            {
                header = cells;
                for(int i = cells.Length - 1; i >= 2; i--)
                    if(!string.IsNullOrWhiteSpace(cells[i])) { lastLangCol = i; break; }
                continue;
            }
            string key = cells[0].Trim().TrimStart('﻿');
            if(!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = cells;
        }
    }

    static string[] LocOf(Dictionary<string, string[]> map, string key)
        => (!string.IsNullOrEmpty(key) && map.TryGetValue(key, out string[] c)) ? c : null;

    // 拼接一条合成物品的多语言行：col0=key，col1=空(Id)，col2..last = 贴纸译名+框架译名（缺则回退 Remark）
    static string BuildLocLine(string key, int lastLangCol, string[] stickerLoc, string[] frameLoc, string stickerFallback, string frameFallback)
    {
        var cells = new string[lastLangCol + 1];
        cells[0] = key;
        cells[1] = "";
        for(int c = 2; c <= lastLangCol; c++)
        {
            string s = Cell(stickerLoc, c, stickerFallback);
            string f = Cell(frameLoc, c, frameFallback);
            cells[c] = s + f;
        }
        return JoinCols(cells, lastLangCol);
    }

    static string Cell(string[] cells, int col, string fallback)
        => (cells != null && col < cells.Length && !string.IsNullOrWhiteSpace(cells[col])) ? cells[col].Trim() : fallback;

    static string JoinCols(string[] cells, int lastCol)
    {
        int n = Mathf.Min(lastCol + 1, cells.Length);
        var parts = new string[n];
        for(int i = 0; i < n; i++) parts[i] = cells[i];
        return string.Join(",", parts);
    }
    #endregion

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
