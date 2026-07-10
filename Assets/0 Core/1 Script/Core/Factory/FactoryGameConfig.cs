using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
#endif

/// <summary>
/// 工厂加工（传送带下压）小游戏配置：单局时长、传送带节奏、良品率、下压判定区间、积分与奖励、消耗。
/// 通过菜单 MiniGame/FactoryGameConfig 创建资产，挂到 <see cref="FactoryProcessGameManager"/> 上。
/// 备注：传送带速度、单件奖励等理论上应由「流水线生产力 + 装备模具属性」推导（见策划案 2.2 / 3.2），模具系统尚未实现，本配置先以固定值驱动。
/// 良品率 / 生产量已接入升级设备系统：实际生效值 = <see cref="BaseYieldRate"/> / <see cref="BaseProductionVolume"/> + 对应设备加成之和（见 <see cref="FactoryEquipManager.SumBonus"/>），由 <see cref="FactoryProcessGameManager"/> 汇总。
/// </summary>
[CreateAssetMenu(fileName = "FactoryGameConfig", menuName = "MiniGame/FactoryGameConfig")]
public class FactoryGameConfig : ScriptableObject
{
    [Title("节奏")]
    [LabelText("传送带速度(归一化/秒)"), MinValue(0.01f)][SerializeField] float beltSpeed = 0.3f;
    [LabelText("出货间隔(秒)"), MinValue(0.1f)][SerializeField] float spawnInterval = 0.9f;

    [Title("基础养成数值（设备升级在此基础上叠加，见升级设备系统）")]
    [LabelText("基本生产量"), MinValue(0)][SerializeField] int baseProductionVolume = 50;
    [LabelText("基础良品率(%)"), Range(0, 100)][SerializeField] int baseYieldRate = 50;

    [Title("下压判定（下压区中心 / 宽度由凹槽 UI 在预制里的位置决定，此处仅配完美区容差）")]
    [LabelText("完美区半宽(GOOD 区，归一化)"), Range(0.005f, 0.5f)][SerializeField] float goodHalfWidth = 0.035f;

    [Title("音游序列（下压小游戏音符节奏，后续接入见 FactoryProcessPanel）")]
    [LabelText("音符基础时间间隔(秒)"), MinValue(0.05f)][SerializeField] float noteInterval = 0.5f;
    [LabelText("各流水线出货节奏序列 [0=空拍 非0=出一件]")][SerializeField] List<FactoryNoteRow> assemblyLineNoteSequences;

    [Title("操作 / 惩罚")]
    [LabelText("按键 CD(秒)"), MinValue(0f)][SerializeField] float pressCooldown = 0.2f;
    [LabelText("不良品卡机时长(秒)"), MinValue(0f)][SerializeField] float jamDuration = 1.5f;

    [Title("积分 / 奖励")]
    [LabelText("OK 得分"), MinValue(0)][SerializeField] int okScore = 60;
    [LabelText("GOOD 得分"), MinValue(0)][SerializeField] int goodScore = 100;
    [LabelText("每件成功奖励金币"), MinValue(0)][SerializeField] int rewardPerSuccess = 50;

    [Title("消耗")]
    [LabelText("开局 / 再来一局消耗体力"), MinValue(0)][SerializeField] int startSpCost = 30;

    [Title("评价图标（音游打包评价飘字，下标对应 FactoryEvaluateType）")]
    [LabelText("评价图标列表 [0]=PERFECT [1]=GOOD [2]=MISS")][SerializeField] Sprite[] evalIcons;

    [Title("打包盒预制（产品压制后变为打包盒，正品 / 次品为两种物品）")]
    [LabelText("正品打包盒预制")][SerializeField] GameObject qualifiedBoxPrefab;
    [LabelText("次品打包盒预制")][SerializeField] GameObject defectiveBoxPrefab;

    #region Get
    public float BeltSpeed => beltSpeed;
    public float SpawnInterval => spawnInterval;
    /// <summary>基本生产量（设备「生产量」加成在此基础上叠加）。</summary>
    public int BaseProductionVolume => baseProductionVolume;
    /// <summary>基础良品率（百分比，设备「良品率」加成在此基础上叠加）。</summary>
    public int BaseYieldRate => baseYieldRate;
    /// <summary>完美区（GOOD）半宽，归一化；判定中心 / OK 区宽度由凹槽 UI 决定，见 <see cref="FactoryProcessGameManager.SetPressZone"/>。</summary>
    public float GoodHalfWidth => goodHalfWidth;
    /// <summary>音符基础时间间隔（秒），音游序列节奏基准。</summary>
    public float NoteInterval => noteInterval;
    /// <summary>各流水线的出货节奏序列（每条流水线一组：0=空拍，非 0=出一件徽章）。徽章的轻/重/不良品类型不取自此表，由开局预生成队列决定。</summary>
    public IReadOnlyList<List<FactoryNoteType>> AssemblyLineNoteSequences => assemblyLineNoteSequences?.ConvertAll(r => r.notes);
    /// <summary>按键 / 点击 CD（秒），CD 内输入无效，防连打。</summary>
    public float PressCooldown => pressCooldown;
    /// <summary>不良品处理失败（点错 / 漏掉）时机器卡住的时长（秒），期间无法操作。</summary>
    public float JamDuration => jamDuration;
    public int OkScore => okScore;
    public int GoodScore => goodScore;
    public int RewardPerSuccess => rewardPerSuccess;
    public int StartSpCost => startSpCost;

    /// <summary>旧版下压面板用（FactoryProcessPanel），音游重做后随其一并清理。</summary>
    public Sprite QualifiedEvalIcon => evalIcons[0];
    /// <summary>旧版下压面板用（FactoryProcessPanel），音游重做后随其一并清理。</summary>
    public Sprite DefectiveEvalIcon => evalIcons[1];
    /// <summary>音游评价图标，按 <see cref="FactoryEvaluateType"/> 取下标。</summary>
    public Sprite[] EvalIcons => evalIcons;
    /// <summary>正品打包盒预制（合格品压制后变为此盒）。</summary>
    public GameObject QualifiedBoxPrefab => qualifiedBoxPrefab;
    /// <summary>次品打包盒预制（次品压制后变为此盒）。</summary>
    public GameObject DefectiveBoxPrefab => defectiveBoxPrefab;
    #endregion

#if UNITY_EDITOR
    #region CSV 导入 / 导出（仅标量数值；评价图标、打包盒预制等资产引用需手动指定）
    const string CsvFolder = "0 Core/1 Script/Data/Factory";
    const string CsvFileName = "FactoryGameConfig.csv";
    static string CsvPath => Path.Combine(Application.dataPath, CsvFolder, CsvFileName);

    /// <summary>
    /// 可由 CSV 配置的标量字段表（不含 Sprite / GameObject 等资产引用）。导出与导入共用此表，保证字段一致、无重复定义。
    /// </summary>
    static readonly FieldDef[] CsvFields =
    {
        new("BeltSpeed",            "float", "传送带速度(归一化/秒)",  c => Str(c.beltSpeed),              (c, s) => c.beltSpeed = PF(s, c.beltSpeed)),
        new("SpawnInterval",        "float", "出货间隔(秒)",          c => Str(c.spawnInterval),          (c, s) => c.spawnInterval = PF(s, c.spawnInterval)),
        new("BaseProductionVolume", "int",   "基本生产量",            c => c.baseProductionVolume.ToString(),(c, s) => c.baseProductionVolume = PI(s, c.baseProductionVolume)),
        new("BaseYieldRate",        "int",   "基础良品率(%)",         c => c.baseYieldRate.ToString(),    (c, s) => c.baseYieldRate = PI(s, c.baseYieldRate)),
        new("GoodHalfWidth",        "float", "完美区半宽(GOOD 区)",    c => Str(c.goodHalfWidth),          (c, s) => c.goodHalfWidth = PF(s, c.goodHalfWidth)),
        new("NoteInterval",         "float", "音符基础时间间隔(秒)",   c => Str(c.noteInterval),           (c, s) => c.noteInterval = PF(s, c.noteInterval)),
        new("FactoryAssemblyLine",  "int[]", "工厂流水线出货节奏序列(0空拍非0出货,行内+分隔,行间++分隔)", c => SA(c.assemblyLineNoteSequences), (c, s) => c.assemblyLineNoteSequences = PA(s, c.assemblyLineNoteSequences)),
        new("PressCooldown",        "float", "按键CD(秒)",            c => Str(c.pressCooldown),          (c, s) => c.pressCooldown = PF(s, c.pressCooldown)),
        new("JamDuration",          "float", "不良品卡机时长(秒)",     c => Str(c.jamDuration),            (c, s) => c.jamDuration = PF(s, c.jamDuration)),
        new("OkScore",              "int",   "OK 得分",               c => c.okScore.ToString(),          (c, s) => c.okScore = PI(s, c.okScore)),
        new("GoodScore",            "int",   "GOOD 得分",             c => c.goodScore.ToString(),        (c, s) => c.goodScore = PI(s, c.goodScore)),
        new("RewardPerSuccess",     "int",   "每件成功奖励金币",       c => c.rewardPerSuccess.ToString(), (c, s) => c.rewardPerSuccess = PI(s, c.rewardPerSuccess)),
        new("StartSpCost",          "int",   "开局 / 再来一局消耗体力", c => c.startSpCost.ToString(),      (c, s) => c.startSpCost = PI(s, c.startSpCost)),
    };
    
    [Button("一键从 CSV 导入", ButtonSizes.Large), GUIColor(0.6f, 1f, 0.6f)]
    void ImportFromTable()
    {
        string path = CsvPath;
        if(!File.Exists(path))
        {
            Debug.LogWarning($"[FactoryGameConfig] 未找到表格（请先导出）：{path}");
            return;
        }

        string[] lines = File.ReadAllLines(path, new UTF8Encoding(true));
        if(lines.Length < 2)
        {
            Debug.LogWarning($"[FactoryGameConfig] 表格无数据：{path}");
            return;
        }

        // 按列名定位 Key / Value 列（列序随意），表头允许带 UTF-8 BOM
        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] headerCells = lines[0].Split(',');
        for(int i = 0; i < headerCells.Length; i++)
        {
            string name = headerCells[i].Trim().TrimStart('﻿');
            if(!string.IsNullOrEmpty(name) && !header.ContainsKey(name))
                header[name] = i;
        }
        if(!header.TryGetValue("Key", out int keyIdx) || !header.TryGetValue("Value", out int valIdx))
        {
            Debug.LogWarning($"[FactoryGameConfig] 表头缺少 Key / Value 列：{path}");
            return;
        }

        var map = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
        foreach(FieldDef f in CsvFields)
            map[f.Key] = f;

        // 从第2行起逐行按 Key 匹配；列名翻译行 / 未知行（Key 不在字段表内）自动跳过
        int applied = 0;
        for(int i = 1; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;
            string[] r = lines[i].Split(',');
            if(keyIdx >= r.Length)
                continue;

            string key = r[keyIdx].Trim().TrimStart('﻿');
            if(!map.TryGetValue(key, out FieldDef field))
                continue;
            field.Set(this, valIdx < r.Length ? r[valIdx].Trim() : string.Empty);
            applied++;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
        if(applied < CsvFields.Length)
            Debug.LogWarning($"[FactoryGameConfig] 仅应用 {applied}/{CsvFields.Length} 项，" +
                             "请检查表格 Key 列是否与字段名一致（区分大小写无所谓，但拼写要对）。");
        Debug.Log($"[FactoryGameConfig] 导入完成，应用 {applied}/{CsvFields.Length} 项。");
    }
    [PropertySpace(8)]
    [InfoBox("仅「标量数值」可经 CSV 配置；评价图标 / 打包盒预制等资产引用请在上方手动指定。\n" +
             "表头：Key(字段名) / Type(类型) / Label(中文说明) / Value(值)；导出后用 Excel 编辑「Value」列再导入即可" +
             "（UTF-8 含 BOM，中文不乱码；导入按列名取 Key、Value，列序随意，Type / Label 仅供阅读）。", InfoMessageType.Info)]
    [Button("导出为 CSV 表格", ButtonSizes.Large), GUIColor(0.6f, 0.85f, 1f)]
    void ExportToTable()
    {
        var sb = new StringBuilder();
        sb.Append("Key,Type,Label,Value\n");   // 第1行：英文列名（导入按此定位 Key / Value 列）
        sb.Append("字段名,类型,说明,值\n");      // 第2行：列名中文翻译（仅供阅读，导入时跳过）
        foreach(FieldDef f in CsvFields)
            sb.Append(f.Key).Append(',').Append(f.Type).Append(',').Append(f.Label).Append(',').Append(f.Get(this)).Append('\n');

        string path = CsvPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[FactoryGameConfig] 已导出 {CsvFields.Length} 项到：{path}");
    }
    static string Str(float v) => v.ToString(CultureInfo.InvariantCulture);
    // 解析失败（空白 / 非法）时回退到原值，避免误清零
    static float PF(string s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    static int PI(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
    // 工厂流水线按键序列：每条流水线的音符值（0无/1上/2左/3右）行内用「+」分隔，行（流水线）之间用「++」分隔
    static string SA(List<FactoryNoteRow> lines)
    {
        var rowStrs = new List<string>(lines.Count);
        foreach(FactoryNoteRow row in lines)
        {
            var nums = new List<string>(row.notes.Count);
            foreach(FactoryNoteType k in row.notes)
                nums.Add(((int)k).ToString());
            rowStrs.Add(string.Join("+", nums));
        }
        return string.Join("++", rowStrs);
    }
    static List<FactoryNoteRow> PA(string s, List<FactoryNoteRow> fallback)
    {
        if(string.IsNullOrWhiteSpace(s))
            return fallback;
        var lines = new List<FactoryNoteRow>();
        foreach(string row in s.Split(new[] { "++" }, StringSplitOptions.None))
        {
            if(string.IsNullOrWhiteSpace(row))
                continue;
            var line = new List<FactoryNoteType>();
            foreach(string p in row.Split('+'))
                if(int.TryParse(p.Trim(), out int v) && v >= 0 && v <= 3)
                    line.Add((FactoryNoteType)v);
            lines.Add(new FactoryNoteRow { notes = line });
        }
        return lines.Count > 0 ? lines : fallback;
    }

    /// <summary>一个可导出 / 导入的标量字段：列名 Key、类型 Type、中文标签 Label、取值（→字符串）、赋值（字符串→字段）。</summary>
    readonly struct FieldDef
    {
        public readonly string Key;
        public readonly string Type;
        public readonly string Label;
        public readonly Func<FactoryGameConfig, string> Get;
        public readonly Action<FactoryGameConfig, string> Set;

        public FieldDef(string key, string type, string label,
            Func<FactoryGameConfig, string> get, Action<FactoryGameConfig, string> set)
        {
            Key = key;
            Type = type;
            Label = label;
            Get = get;
            Set = set;
        }
    }
    #endregion
#endif
}