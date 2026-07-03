using UnityEngine;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Linq;
#endif

[CreateAssetMenu(fileName = "PhotoStudioGameConfig", menuName = "Scene/PhotoStudioGameConfig")]
public sealed class PhotoStudioGameConfig : SerializedScriptableObject
{
    [FoldoutGroup("基础配置")]
    [LabelText("消耗行动力")][SerializeField] int photoCosumeAp = 1;
    [FoldoutGroup("基础配置")]
    [LabelText("拍摄配置倒计时时间")][SerializeField] int cameraConfigCountDownTime = 30;
    [FoldoutGroup("基础配置")]
    [LabelText("对焦小游戏倒计时时间")][SerializeField] int focusGameCountDownTime = 30;
    [FoldoutGroup("基础配置")]
    [LabelText("对焦小游戏每秒在瞄准区域内成功得分")][SerializeField] int focusGameSuccessScore = 10;
    [FoldoutGroup("基础配置")]
    [LabelText("对焦小游戏每秒在瞄准区域内失败得分")][SerializeField] int focusGameFailScore = -5;
    [FoldoutGroup("基础配置")]
    [LabelText("对焦小游戏目标得分")][SerializeField] int focusGameTargetScore = 100;

    [FoldoutGroup("结算评分")]
    [InfoBox("照片质量分数 = 适配度 ×0.5 + 对焦积分 ×0.5。适配度 = 所选背景的基础分 + 该背景对所选[服装/姿势/光影]的加成。", InfoMessageType.Info)]
    [LabelText("适配度权重")][Range(0f, 1f)][SerializeField] float adaptationWeight = 0.5f;
    [FoldoutGroup("结算评分")]
    [LabelText("对焦积分权重")][Range(0f, 1f)][SerializeField] float focusWeight = 0.5f;
    [FoldoutGroup("结算评分")]
    [LabelText("适配度档位（按最低分从高到低）")][SerializeField] AdaptationLevelConfig[] adaptationLevels;
    [FoldoutGroup("结算评分")]
    [LabelText("照片质量档位（按最低分从高到低）")][SerializeField] PhotoQualityTierConfig[] qualityTiers;
    // 供编辑器工具（同步精灵到本地化库）读取
    public PhotoQualityTierConfig[] QualityTiers => qualityTiers;

    [FoldoutGroup("光影效果预设")]
    [LabelText("光影预设字典(ID->预设)")][SerializeField] Dictionary<long, PhotoLightingPreset> lightingPresets = new();
    [FoldoutGroup("光影效果预设")]
    [LabelText("默认光影ID")][SerializeField] long defaultLightingId;

    [FoldoutGroup("背景图配置")]
    [LabelText("背景图字典(ID->配置)")][SerializeField] Dictionary<long, PhotoSceneBgConfig> sceneBgConfigs = new();
    [FoldoutGroup("背景图配置")]
    [LabelText("默认背景ID")][SerializeField] long defaultBgId;

    [FoldoutGroup("姿势配置")]
    [LabelText("姿势字典(ID->配置)")][SerializeField] Dictionary<long, PhotoPostureConfigItem> postureConfigs = new();
    [FoldoutGroup("姿势配置")]
    [LabelText("默认姿势ID")][SerializeField] long defaultPostureId;

    [FoldoutGroup("服装配置")]
    [LabelText("服装字典(ID->配置)")][SerializeField] Dictionary<long, PhotoClothesConfigItem> clothesConfigs = new();
    [FoldoutGroup("服装配置")]
    [LabelText("默认服装ID")][SerializeField] long defaultClothesId;

    [FoldoutGroup("角色图配置")]
    [InfoBox("角色图由 姿势ID + 服装ID 组合决定；字典 key 为角色图自身ID")]
    [LabelText("角色图字典(ID->配置)")][SerializeField] Dictionary<long, PhotoCharacterConfig> characterConfigs = new();

    public int PhotoCosumeAp => photoCosumeAp;
    public int CameraConfigCountDownTime => cameraConfigCountDownTime;
    public int FocusGameCountDownTime => focusGameCountDownTime;
    public int FocusGameSuccessScore => focusGameSuccessScore;
    public int FocusGameFailScore => focusGameFailScore;
    public int FocusGameTargetScore => focusGameTargetScore;
    public IReadOnlyDictionary<long, PhotoLightingPreset> LightingPresets => lightingPresets;
    public long DefaultLightingId => defaultLightingId;
    public IReadOnlyDictionary<long, PhotoSceneBgConfig> SceneBgConfigs => sceneBgConfigs;
    public long DefaultBgId => defaultBgId;
    public IReadOnlyDictionary<long, PhotoPostureConfigItem> PostureConfigs => postureConfigs;
    public long DefaultPostureId => defaultPostureId;
    public IReadOnlyDictionary<long, PhotoClothesConfigItem> ClothesConfigs => clothesConfigs;
    public long DefaultClothesId => defaultClothesId;
    public IReadOnlyDictionary<long, PhotoCharacterConfig> CharacterConfigs => characterConfigs;

    // ====================== 按ID查找 ======================
    public PhotoLightingPreset GetLighting(long id) => Lookup(lightingPresets, id);
    public PhotoSceneBgConfig GetSceneBg(long id) => Lookup(sceneBgConfigs, id);
    public PhotoPostureConfigItem GetPosture(long id) => Lookup(postureConfigs, id);
    public PhotoClothesConfigItem GetClothes(long id) => Lookup(clothesConfigs, id);

    static T Lookup<T>(Dictionary<long, T> dict, long id) where T : class
        => (dict != null && dict.TryGetValue(id, out T v)) ? v : null;

    // ====================== 结算评分 ======================
    // 适配度 = 所选背景的基础分 + 该背景对[所选服装/姿势/光影ID]的加成
    public int GetAdaptationScore(long bgId, long clothesId, long postureId, long lightingId)
    {
        PhotoSceneBgConfig bg = GetSceneBg(bgId);
        if(bg == null)
            return 0;
        return bg.baseScore
            + BonusValue(bg.clothesBonus, clothesId)
            + BonusValue(bg.postureBonus, postureId)
            + BonusValue(bg.effectBonus, lightingId);
    }

    static int BonusValue(Dictionary<long, int> dict, long id)
        => (dict != null && dict.TryGetValue(id, out int v)) ? v : 0;

    // 适配度档位：取第一个满足 适配度 >= minScore 的档位（列表按 minScore 从高到低）
    public AdaptationLevelConfig GetAdaptationLevel(int adaptationScore)
    {
        return PickByMinScore(adaptationLevels, adaptationScore, c => c.minScore);
    }

    // 照片质量分数 = 适配度 × 适配度权重 + 对焦积分 × 对焦积分权重
    public int GetQualityScore(int adaptationScore, int focusScore)
    {
        return Mathf.RoundToInt(adaptationScore * adaptationWeight + focusScore * focusWeight);
    }

    // 照片质量档位：取第一个满足 质量分 >= minScore 的档位（列表按 minScore 从高到低）
    public PhotoQualityTierConfig GetQualityTier(int qualityScore)
    {
        return PickByMinScore(qualityTiers, qualityScore, c => c.minScore);
    }

    static T PickByMinScore<T>(T[] list, int score, Func<T, int> getMin) where T : class
    {
        if(list == null || list.Length == 0)
            return null;
        T fallback = list[list.Length - 1];
        foreach(T item in list)
        {
            if(score >= getMin(item))
                return item;
        }
        return fallback;
    }

    // 由 姿势ID + 服装ID 组合解析对应的角色图
    public Sprite GetCharacterSprite(long postureId, long clothesId)
    {
        if(characterConfigs == null)
            return null;
        foreach(PhotoCharacterConfig c in characterConfigs.Values)
        {
            if(c.postureId == postureId && c.clothesId == clothesId)
                return c.characterSprite;
        }
        return null;
    }

#if UNITY_EDITOR
    // 一键填充几套常用拍照光影预设
    [FoldoutGroup("光影效果预设")]
    [Button("填充默认光影预设")]
    void FillDefaultPresets()
    {
        // id / name(key) 与 光影效果.csv 对齐，这样「一键从表格导入」时能按 ID 匹配并保留这里的视觉参数
        lightingPresets = new Dictionary<long, PhotoLightingPreset>
        {
            { 100000, new PhotoLightingPreset { id = 100000, name = "LightingNaturalName", brightness = 1f, contrast = 1f, saturation = 1f,
                temperature = 0f, gradeColor = Color.white, vignetteIntensity = 0f } },

            { 100001, new PhotoLightingPreset { id = 100001, name = "LightingWarmSunName", brightness = 1.08f, contrast = 1.05f, saturation = 1.1f,
                temperature = 0.35f, gradeColor = new Color(1f, 0.96f, 0.88f), vignetteIntensity = 0.15f } },

            { 100002, new PhotoLightingPreset { id = 100002, name = "LightingCoolName", brightness = 0.98f, contrast = 1.1f, saturation = 0.95f,
                temperature = -0.35f, gradeColor = new Color(0.9f, 0.95f, 1f), vignetteIntensity = 0.2f } },

            { 100003, new PhotoLightingPreset { id = 100003, name = "LightingSoftName", brightness = 1.12f, contrast = 0.9f, saturation = 0.95f,
                temperature = 0.1f, gradeColor = new Color(1f, 0.99f, 0.97f), vignetteIntensity = 0.1f } },

            { 100004, new PhotoLightingPreset { id = 100004, name = "LightingDramaticName", brightness = 0.95f, contrast = 1.4f, saturation = 1.15f,
                temperature = -0.1f, gradeColor = Color.white, vignetteIntensity = 0.55f, vignetteStart = 0.35f } },

            { 100005, new PhotoLightingPreset { id = 100005, name = "LightingNightName", brightness = 0.8f, contrast = 1.2f, saturation = 0.85f,
                temperature = -0.45f, gradeColor = new Color(0.78f, 0.84f, 1f), vignetteIntensity = 0.5f,
                vignetteStart = 0.3f, vignetteColor = new Color(0.02f, 0.03f, 0.08f) } },
        };
        if(!lightingPresets.ContainsKey(defaultLightingId))
            defaultLightingId = 100000;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // 一键填充默认的适配度/照片质量档位（分值与奖励为起步值，可再微调），并补齐多语言 key
    [FoldoutGroup("结算评分")]
    [Button("填充默认结算档位")]
    void FillDefaultTiers()
    {
        adaptationLevels = new[]
        {
            new AdaptationLevelConfig { level = AdaptationLevel.Best,   name = "AdaptBest",   minScore = 300 },
            new AdaptationLevelConfig { level = AdaptationLevel.Fit,    name = "AdaptFit",    minScore = 250 },
            new AdaptationLevelConfig { level = AdaptationLevel.Normal, name = "AdaptNormal", minScore = 200 },
            new AdaptationLevelConfig { level = AdaptationLevel.Bad,    name = "AdaptBad",    minScore = 0   },
        };
        EnsureLocalizeKey("AdaptBest", "最优适配");
        EnsureLocalizeKey("AdaptFit", "适配");
        EnsureLocalizeKey("AdaptNormal", "一般");
        EnsureLocalizeKey("AdaptBad", "不好");

        qualityTiers = new[]
        {
            new PhotoQualityTierConfig { tier = PhotoQualityTier.Perfect, name = "QualityPerfect", commentKey = "CommentPerfect", minScore = 180 },
            new PhotoQualityTierConfig { tier = PhotoQualityTier.Great,   name = "QualityGreat",   commentKey = "CommentGreat",   minScore = 150 },
            new PhotoQualityTierConfig { tier = PhotoQualityTier.Good,    name = "QualityGood",    commentKey = "CommentGood",    minScore = 120 },
            new PhotoQualityTierConfig { tier = PhotoQualityTier.Normal,  name = "QualityNormal",  commentKey = "CommentNormal",  minScore = 80  },
            new PhotoQualityTierConfig { tier = PhotoQualityTier.Bad,     name = "QualityBad",     commentKey = "CommentBad",     minScore = 0   },
        };
        EnsureLocalizeKey("QualityPerfect", "完美拍照");
        EnsureLocalizeKey("QualityGreat", "优秀");
        EnsureLocalizeKey("QualityGood", "不错");
        EnsureLocalizeKey("QualityNormal", "普通");
        EnsureLocalizeKey("QualityBad", "糟糕");
        EnsureLocalizeKey("CommentPerfect", "太完美了~");
        EnsureLocalizeKey("CommentGreat", "干得不错~");
        EnsureLocalizeKey("CommentGood", "感觉不错哦~");
        EnsureLocalizeKey("CommentNormal", "还可以啦~");
        EnsureLocalizeKey("CommentBad", "下次会更好~");

        UnityEditor.EditorUtility.SetDirty(this);
    }

    // ====================== 表格导入 ======================
    // 数据表目录（相对 Assets）。各表列约定：Id, Remark(中文/备注), Name(多语言key), Resources(资源名)
    // 场景表额外有 Clothing/Posture/Effect 三列（"+"分隔的 int 加成数组）。
    // 角色图表额外有 PostureId/ClothesId 两列（long），表示该角色图由 姿势ID+服装ID 组合决定。
    const string DataFolder = "0 Core/1 Script/Data/PhotoStudio";
    const string CsvLighting = "光影效果.csv";
    const string CsvClothes = "女主服装(拍摄).csv";
    const string CsvPosture = "拍摄姿势.csv";
    const string CsvScene = "拍摄场景.csv";
    const string CsvCharacter = "拍摄角色图.csv";
    const string LocalizeTable = "PhotoStudio";
    // 场景背景图目录：图片按 Id（100000.jpg）命名，也有按 Name key 命名的副本
    const string SceneBgFolder = "Assets/AddressableAssets/Remote/Texture2D/SceneBg";

    [FoldoutGroup("表格导入")]
    [InfoBox("从 " + DataFolder + " 读取 5 张 CSV 导入（光影/服装/姿势/场景/角色图）：Name 列作为多语言 key，Remark 列写入 PhotoStudio 表 zh-CN 默认值，Resources 列按名加载 Sprite（空则留 null）。角色图表的 PostureId+ClothesId 决定该图对应的姿势+服装组合。", InfoMessageType.Info)]
    [Button("一键从表格导入配置", ButtonSizes.Large)]
    void ImportFromTables()
    {
        string root = Path.Combine(Application.dataPath, DataFolder);

        ImportLighting(Path.Combine(root, CsvLighting));
        ImportClothes(Path.Combine(root, CsvClothes));
        ImportPosture(Path.Combine(root, CsvPosture));
        ImportScene(Path.Combine(root, CsvScene));
        ImportCharacter(Path.Combine(root, CsvCharacter));

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[PhotoStudioGameConfig] 表格导入完成。");
    }

    // 光影：表中无视觉参数，按 ID 匹配已有预设以保留手调值，只更新 id/name(key)
    void ImportLighting(string path)
    {
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;

        Dictionary<long, PhotoLightingPreset> existing = lightingPresets ?? new Dictionary<long, PhotoLightingPreset>();
        var dict = new Dictionary<long, PhotoLightingPreset>();
        foreach(string[] r in t.Rows)
        {
            if(!TryParseId(t.Get(r, "Id"), out long id))
                continue;
            string remark = t.Get(r, "Remark"), nameKey = t.Get(r, "Name");
            EnsureLocalizeKey(nameKey, remark);
            PhotoLightingPreset preset = existing.TryGetValue(id, out PhotoLightingPreset old) ? old : new PhotoLightingPreset();
            preset.id = id;
            preset.name = nameKey;
            dict[id] = preset;
        }
        lightingPresets = dict;
        EnsureDefault(dict, ref defaultLightingId);
        Debug.Log($"[PhotoStudioGameConfig] 光影导入 {dict.Count} 条（视觉参数保留按ID匹配的旧值）。");
    }

    void ImportClothes(string path)
    {
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;
        var dict = new Dictionary<long, PhotoClothesConfigItem>();
        foreach(string[] r in t.Rows)
        {
            if(!TryParseId(t.Get(r, "Id"), out long id))
                continue;
            string remark = t.Get(r, "Remark"), nameKey = t.Get(r, "Name"), res = t.Get(r, "Resources");
            EnsureLocalizeKey(nameKey, remark);
            dict[id] = new PhotoClothesConfigItem { id = id, name = nameKey, sprite = LoadSpriteByName(res) };
        }
        clothesConfigs = dict;
        EnsureDefault(dict, ref defaultClothesId);
        Debug.Log($"[PhotoStudioGameConfig] 服装导入 {dict.Count} 条。");
    }

    void ImportPosture(string path)
    {
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;
        var dict = new Dictionary<long, PhotoPostureConfigItem>();
        foreach(string[] r in t.Rows)
        {
            if(!TryParseId(t.Get(r, "Id"), out long id))
                continue;
            string remark = t.Get(r, "Remark"), nameKey = t.Get(r, "Name"), res = t.Get(r, "Resources");
            EnsureLocalizeKey(nameKey, remark);
            dict[id] = new PhotoPostureConfigItem { id = id, name = nameKey, sprite = LoadSpriteByName(res) };
        }
        postureConfigs = dict;
        EnsureDefault(dict, ref defaultPostureId);
        Debug.Log($"[PhotoStudioGameConfig] 姿势导入 {dict.Count} 条。");
    }

    // 场景需在 服装/姿势/光影 之后导入：加成列按各自字典的顺序（= CSV 行序）映射到对应 ID
    void ImportScene(string path)
    {
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;

        var clothesIds = new List<long>(clothesConfigs.Keys);
        var postureIds = new List<long>(postureConfigs.Keys);
        var lightingIds = new List<long>(lightingPresets.Keys);

        var dict = new Dictionary<long, PhotoSceneBgConfig>();
        foreach(string[] r in t.Rows)
        {
            if(!TryParseId(t.Get(r, "Id"), out long id))
                continue;
            string remark = t.Get(r, "Remark"), nameKey = t.Get(r, "Name"), res = t.Get(r, "Resources");
            EnsureLocalizeKey(nameKey, remark);
            dict[id] = new PhotoSceneBgConfig
            {
                id = id,
                name = nameKey,
                bgSprite = LoadSceneBgSprite(id.ToString(), nameKey, res),
                clothesBonus = ZipBonus(ParseBonusArray(t.Get(r, "Clothing")), clothesIds),
                postureBonus = ZipBonus(ParseBonusArray(t.Get(r, "Posture")), postureIds),
                effectBonus = ZipBonus(ParseBonusArray(t.Get(r, "Effect")), lightingIds),
            };
        }
        sceneBgConfigs = dict;
        EnsureDefault(dict, ref defaultBgId);
        Debug.Log($"[PhotoStudioGameConfig] 场景导入 {dict.Count} 条。");
    }

    // 角色图：由 姿势ID + 服装ID 组合唯一对应一张角色图；字典 key 为角色图自身ID
    void ImportCharacter(string path)
    {
        CsvTable t = ReadCsv(path);
        if(t == null)
            return;
        var dict = new Dictionary<long, PhotoCharacterConfig>();
        foreach(string[] r in t.Rows)
        {
            if(!TryParseId(t.Get(r, "Id"), out long id))
                continue;
            TryParseId(t.Get(r, "PostureId"), out long postureId);
            TryParseId(t.Get(r, "ClothesId"), out long clothesId);
            string remark = t.Get(r, "Remark"), nameKey = t.Get(r, "Name"), res = t.Get(r, "Resources");
            EnsureLocalizeKey(nameKey, remark);
            dict[id] = new PhotoCharacterConfig
            {
                id = id,
                postureId = postureId,
                clothesId = clothesId,
                name = nameKey,
                characterSprite = LoadSpriteByName(res),
            };
        }
        characterConfigs = dict;
        Debug.Log($"[PhotoStudioGameConfig] 角色图导入 {dict.Count} 条。");
    }

    // 解析 long ID；非法则跳过该行
    static bool TryParseId(string s, out long id)
    {
        if(long.TryParse((s ?? string.Empty).Trim(), out id))
            return true;
        if(!string.IsNullOrWhiteSpace(s))
            Debug.LogWarning($"[PhotoStudioGameConfig] 非法ID（需为整数）：{s}，已跳过。");
        return false;
    }

    // 把按行序的加成数组，依字典 key 顺序映射为 ID->加成
    static Dictionary<long, int> ZipBonus(int[] bonuses, List<long> ids)
    {
        var d = new Dictionary<long, int>();
        if(bonuses == null || ids == null)
            return d;
        int n = Mathf.Min(bonuses.Length, ids.Count);
        for(int i = 0; i < n; i++)
            d[ids[i]] = bonuses[i];
        return d;
    }

    // 默认ID不在字典中时，回退为字典首个 key
    static void EnsureDefault<T>(Dictionary<long, T> dict, ref long defaultId)
    {
        if(dict == null || dict.Count == 0 || dict.ContainsKey(defaultId))
            return;
        foreach(long k in dict.Keys)
        {
            defaultId = k;
            return;
        }
    }

    // 一张解析后的 CSV：按列名（首行表头）取值，避免依赖列顺序
    class CsvTable
    {
        public Dictionary<string, int> Header;   // 列名 -> 列索引
        public List<string[]> Rows;              // 数据行（从第 4 行起）

        // 按列名取值；列不存在或越界返回空串
        public string Get(string[] row, string colName)
        {
            return (Header.TryGetValue(colName, out int idx) && row != null && idx < row.Length)
                ? row[idx].Trim()
                : string.Empty;
        }
    }

    // 读取 CSV：第 1 行为字段名表头，2/3 行为类型/中文标签，从第 4 行起为数据。
    // 按列名取值，列顺序随意（场景表与其它表列序不一致也能正确导入）。支持 UTF-8 BOM。
    static CsvTable ReadCsv(string path)
    {
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[PhotoStudioGameConfig] 未找到表格：{path}");
            return null;
        }
        string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
        if(lines.Length < 4)
        {
            Debug.LogWarning($"[PhotoStudioGameConfig] 表格行数不足：{path}");
            return null;
        }

        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] headerCells = lines[0].Split(',');
        for(int i = 0; i < headerCells.Length; i++)
        {
            string name = headerCells[i].Trim().TrimStart('﻿');   // 去掉首格可能残留的 BOM
            if(!string.IsNullOrEmpty(name) && !header.ContainsKey(name))
                header[name] = i;
        }

        var rows = new List<string[]>();
        for(int i = 3; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            rows.Add(lines[i].Split(','));
        }
        return new CsvTable { Header = header, Rows = rows };
    }

    // 解析 "10+20+30" 形式的加成数组；空串返回空数组
    static int[] ParseBonusArray(string s)
    {
        if(string.IsNullOrWhiteSpace(s))
            return new int[0];
        return s.Split('+')
            .Select(p => int.TryParse(p.Trim(), out int v) ? v : 0)
            .ToArray();
    }

    // 加载场景背景图：依次按 Id / Name key / Resources 名在 SceneBg 目录查找，全找不到才返回 null
    static Sprite LoadSceneBgSprite(string id, string nameKey, string resName)
    {
        foreach(string candidate in new[] { id, nameKey, resName })
        {
            Sprite s = LoadSpriteInFolder(SceneBgFolder, candidate);
            if(s != null)
                return s;
        }
        Debug.LogWarning($"[PhotoStudioGameConfig] 场景背景图未找到（id={id}, key={nameKey}, res={resName}），已留空");
        return null;
    }

    // 在指定目录下按文件名（不含扩展名）精确加载 Sprite
    static Sprite LoadSpriteInFolder(string folder, string fileBaseName)
    {
        if(string.IsNullOrWhiteSpace(fileBaseName))
            return null;
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{fileBaseName} t:Sprite", new[] { folder });
        foreach(string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileNameWithoutExtension(assetPath) == fileBaseName)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        return null;
    }

    // 按资源名查找工程内 Sprite；空名或找不到返回 null
    static Sprite LoadSpriteByName(string resName)
    {
        if(string.IsNullOrWhiteSpace(resName))
            return null;
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{resName} t:Sprite");
        foreach(string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if(Path.GetFileNameWithoutExtension(assetPath) == resName)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        Debug.LogWarning($"[PhotoStudioGameConfig] 未找到 Sprite 资源：{resName}（已留空）");
        return null;
    }

    // 把 key 补进 PhotoStudio 字符串表，并用 Remark 作为 zh-CN 默认值（已有非空值则不覆盖）
    static void EnsureLocalizeKey(string key, string zhDefault)
    {
        if(string.IsNullOrEmpty(key))
            return;
        LocTool.EnsureKey(LocalizeTable, key, zhDefault);
    }
#endif
}

// 一项背景图（拍摄场景）配置。对应表格：拍摄场景.csv
[Serializable]
public class PhotoSceneBgConfig
{
    [LabelText("ID")] public long id;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表），由表格 Name 列导入")] public string name;
    [LabelText("背景图"), PreviewField(60, ObjectFieldAlignment.Left)] public Sprite bgSprite;
    [LabelText("基础分")] public int baseScore;
    [LabelText("适配女主服装加成"), Tooltip("服装ID -> 加成")] public Dictionary<long, int> clothesBonus = new();
    [LabelText("适配女主拍摄姿势加成"), Tooltip("姿势ID -> 加成")] public Dictionary<long, int> postureBonus = new();
    [LabelText("适配光影效果加成"), Tooltip("光影ID -> 加成")] public Dictionary<long, int> effectBonus = new();
}

// 一项角色图配置（角色立绘风格参考 Housemaid1 / Housemaid2 等 Q 版立绘）
// 字典 key 为角色图自身ID；由 姿势ID + 服装ID 组合唯一对应一张角色图
[Serializable]
public class PhotoCharacterConfig
{
    [LabelText("ID")] public long id;
    [LabelText("姿势ID")] public long postureId;
    [LabelText("服装ID")] public long clothesId;
    [LabelText("名称")] public string name;
    [LabelText("角色图"), PreviewField(60, ObjectFieldAlignment.Left)] public Sprite characterSprite;
}
// 姿势。对应表格：拍摄姿势.csv
[Serializable]
public class PhotoPostureConfigItem
{
    [LabelText("ID")] public long id;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表），由表格 Name 列导入")] public string name;
    [LabelText("资源"), PreviewField(60, ObjectFieldAlignment.Left)] public Sprite sprite;
}
// 服装。对应表格：女主服装(拍摄).csv
[Serializable]
public class PhotoClothesConfigItem
{
    [LabelText("ID")] public long id;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表），由表格 Name 列导入")] public string name;
    [LabelText("资源"), PreviewField(60, ObjectFieldAlignment.Left)] public Sprite sprite;
}

// 一套拍照光影预设。对应表格：光影效果.csv（视觉参数不在表中，导入时按 ID 保留已有手调值）
[Serializable]
public class PhotoLightingPreset
{
    [LabelText("ID")] public long id;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表），由表格 Name 列导入")] public string name = "自然光";
    [LabelText("亮度/曝光")][Range(0f, 3f)] public float brightness = 1f;
    [LabelText("对比度")][Range(0f, 3f)] public float contrast = 1f;
    [LabelText("饱和度")][Range(0f, 3f)] public float saturation = 1f;
    [LabelText("色温(暖-冷)")][Range(-1f, 1f)] public float temperature = 0f;
    [LabelText("染色(相乘)")] public Color gradeColor = Color.white;
    [LabelText("暗角强度")][Range(0f, 1f)] public float vignetteIntensity = 0f;
    [LabelText("暗角起始")][Range(0f, 1f)] public float vignetteStart = 0.5f;
    [LabelText("暗角颜色")] public Color vignetteColor = Color.black;
}

// 适配度档位（不好/一般/适配/最优适配）
public enum AdaptationLevel
{
    Bad,        // 不好
    Normal,     // 一般
    Fit,        // 适配
    Best,       // 最优适配
}

// 照片质量档位（糟糕/普通/不错/优秀/完美）
public enum PhotoQualityTier
{
    Bad,        // 糟糕
    Normal,     // 普通
    Good,       // 不错
    Great,      // 优秀
    Perfect,    // 完美拍照
}

// 一档适配度配置
[Serializable]
public class AdaptationLevelConfig
{
    [LabelText("档位")] public AdaptationLevel level;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表）")] public string name;
    [LabelText("达到该档的最低适配度")] public int minScore;
}

// 一档照片质量配置：决定档位名、点评气泡、评价用图与奖励数值
[Serializable]
public class PhotoQualityTierConfig
{
    [LabelText("档位")] public PhotoQualityTier tier;
    [LabelText("名称Key"), Tooltip("多语言 key（PhotoStudio 表）；同时作为档位名精灵在资源表中的 key")] public string name;
    [LabelText("点评气泡Key"), Tooltip("多语言 key（PhotoStudio 表）")] public string commentKey;
    [LabelText("达到该档的最低质量分")] public int minScore;
    [LabelText("评价用图"), PreviewField(60, ObjectFieldAlignment.Left)] public Sprite reviewSprite;
    [LabelText("档位名图(源)"), PreviewField(60, ObjectFieldAlignment.Left), Tooltip("质量档位名的精灵图。由编辑器工具 PhotoStudioLocalizationTool 同步进本地化资源表；运行时按语言从资源表取图，不直接使用此字段")] public Sprite tierNameSprite;
    [LabelText("照片素材道具ID"), Tooltip("该品质对应的照片素材道具ID；0 表示暂不入库（灵感/好感/压力按 积分/10 计算，不在此配置）")] public long photoItemId;
}
