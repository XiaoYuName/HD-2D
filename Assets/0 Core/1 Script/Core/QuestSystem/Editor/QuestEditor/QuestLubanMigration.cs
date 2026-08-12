using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;

/// <summary>
/// 一次性迁移：把旧的 5 张 Luban Excel 读成新的任务数据库（多态实例 + 字典）。
/// 导完这一轮、确认数据没问题之后，这个文件连同 Luban 任务表就可以删掉了 —— 任务配置以后只认 SO。
///
/// 目标/触发/奖励的参数是各实现类的私有序列化字段（正常靠 Inspector 填），
/// 这里是导入的一次性代码，所以直接按字段名反射写入，不给运行时类开公共 setter。
/// </summary>
public static class QuestLubanMigration
{
    public const string DatabaseAssetPath = "Assets/Resources/Quest/QuestDatabase.asset";
    const string ExcelRoot = "ExcelTool/LubanTools/DataTables/Datas/";

    // [MenuItem("Tools/QuestSystem/从 Luban Excel 导入任务数据库（一次性）")]
    public static void ImportFromMenu()
    {
        QuestDatabaseData database = AssetDatabase.LoadAssetAtPath<QuestDatabaseData>(DatabaseAssetPath);
        if (database != null && !EditorUtility.DisplayDialog(
                "重新导入任务配置",
                "将用 5 张 Luban Excel 覆盖整个任务数据库，现有内容全部丢弃。",
                "覆盖导入", "取消"))
            return;

        if (database == null)
        {
            EnsureAssetFolder("Assets/Resources/Quest");
            database = ScriptableObject.CreateInstance<QuestDatabaseData>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
        }
        else Undo.RecordObject(database, "导入 Luban 任务配置");

        Dictionary<long, QuestData> quests = ImportQuests();
        Dictionary<long, QuestObjConfigData> objs = ImportObjs();
        Dictionary<long, QuestCategory> categories = ImportCategories();
        Dictionary<long, QuestCondData> conds = ImportConds();
        Dictionary<QuestRewardType, QuestRewardPresentation> rewardViews = ImportRewardViews();

        database.EditorReplace(quests, objs, categories, conds, rewardViews);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        QuestDatabaseProvider.ClearCache();
        QuestRefCatalog.ClearCache();
        Selection.activeObject = database;

        Debug.Log($"[Quest] 已导入 {categories.Count} 类别、{quests.Count} 任务、{objs.Count} 目标、"
                  + $"{conds.Count} 条件、{rewardViews.Count} 奖励显示。");
    }

    #region 各表

    static Dictionary<long, QuestData> ImportQuests()
    {
        Dictionary<long, QuestData> result = new();
        foreach (Dictionary<string, string> row in Rows("QuestDataConfig.xlsx"))
        {
            QuestData data = new();
            Set(data, "remark", Cell(row, "Remark"));
            Set(data, "name", Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.Quest));
            Set(data, "desc", Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Quest));
            Set(data, "icon", Icon(Cell(row, "IconKey")));
            Set(data, "triggers", Triggers(Cell(row, "QuestTrigger")));
            Set(data, "acceptCond", Long(row, "AcceptCond"));
            Set(data, "objIds", LongList(Cell(row, "QuestObjData")));
            Set(data, "rewards", Rewards(Cell(row, "Reward")));
            Set(data, "objInOrder", Bool(row, "ObjInOrder"));
            result[Long(row, "Id")] = data;
        }
        return result;
    }

    static Dictionary<long, QuestObjConfigData> ImportObjs()
    {
        Dictionary<long, QuestObjConfigData> result = new();
        foreach (Dictionary<string, string> row in Rows("QuestObjConfig.xlsx"))
        {
            string extraText = Cell(row, "ExtraCompleteCond");

            QuestObjData target = Objective(Cell(row, "QuestObjData"),
                Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Objective));
            QuestObjData extra = string.IsNullOrWhiteSpace(extraText)
                ? null
                : Objective(extraText, Loc(Cell(row, "ExtraDescKey"), QuestLocKey.Prefix.Objective));

            QuestObjConfigData data = new();
            Set(data, "remark", Cell(row, "Remark"));
            Set(data, "target", target);
            Set(data, "rewards", Rewards(Cell(row, "Reward")));
            Set(data, "extra", extra);
            Set(data, "extraRewards", Rewards(Cell(row, "ExtraReward")));
            result[Long(row, "Id")] = data;
        }
        return result;
    }

    static Dictionary<long, QuestCategory> ImportCategories()
    {
        Dictionary<long, QuestCategory> result = new();
        foreach (Dictionary<string, string> row in Rows("QuestCategoryData.xlsx"))
        {
            QuestCategory data = new();
            Set(data, "remark", Cell(row, "Remark"));
            Set(data, "name", Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.Category));
            Set(data, "desc", Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Category));
            Set(data, "icon", Icon(Cell(row, "IconKey")));
            Set(data, "questIds", LongList(Cell(row, "QuestId")));
            Set(data, "rewards", Rewards(Cell(row, "Reward")));
            result[Long(row, "Id")] = data;
        }
        return result;
    }

    static Dictionary<long, QuestCondData> ImportConds()
    {
        Dictionary<long, QuestCondData> result = new();
        foreach (Dictionary<string, string> row in Rows("QuestStoryCondData.xlsx"))
        {
            QuestCondData data = new()
            {
                items = ItemRequirements(Cell(row, "ItemOwn")),
                characterProps = CharacterRequirements(Cell(row, "NpcProp")),
                plotPrerequisites = LongList(Cell(row, "PlotPre")),
                dialoguePrerequisites = LongList(Cell(row, "DlgPre")),
                day = Int(row, "Day"),
                timeSlot = EnumValue(Cell(row, "TimeSlot"), ShowRuleTimeType.All),
                questPrerequisites = LongList(Cell(row, "QuestPre")),
                satisfyBranches = LongList(Cell(row, "Satisfy")),
                notSatisfyBranches = LongList(Cell(row, "NotSatisfy")),
                gameScore = Int(row, "GameScore"),
            };
            Set(data, "remark", Cell(row, "Remark"));
            result[Long(row, "Id")] = data;
        }
        return result;
    }

    static Dictionary<QuestRewardType, QuestRewardPresentation> ImportRewardViews()
    {
        Dictionary<QuestRewardType, QuestRewardPresentation> result = new();
        foreach (Dictionary<string, string> row in Rows("QuestRewardData.xlsx"))
        {
            QuestRewardType type = EnumValue(Cell(row, "Id"), QuestRewardType.None);
            if (type == QuestRewardType.None) continue;

            QuestRewardPresentation data = new();
            Set(data, "remark", Cell(row, "Remark"));
            Set(data, "name", Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.RewardName));
            Set(data, "icon", Icon(Cell(row, "IconKey")));
            result[type] = data;
        }
        return result;
    }

    #endregion

    #region 位置参数 → 多态实例

    static List<IQuestTrigger> Triggers(string text)
    {
        List<IQuestTrigger> result = new();
        foreach (Args args in Args.SplitList(text))
        {
            QuestTriggerType type = args.Head(QuestTriggerType.None);
            switch (type)
            {
                case QuestTriggerType.EnterZone:
                    result.Add(Zone(new EnterZoneQuestTrigger(), args));
                    break;
                case QuestTriggerType.ExitZone:
                    result.Add(Zone(new ExitZoneQuestTrigger(), args));
                    break;
                case QuestTriggerType.EnterZoneStay:
                    EnterZoneStayQuestTrigger stay = Zone(new EnterZoneStayQuestTrigger(), args);
                    Set(stay, "needSeconds", Math.Max(1, args.Int(2, 1)));
                    result.Add(stay);
                    break;
                case QuestTriggerType.ClickNpc:
                    result.Add(Id(new ClickNpcQuestTrigger(), args));
                    break;
                case QuestTriggerType.DialogNpc:
                    result.Add(Id(new DialogNpcQuestTrigger(), args));
                    break;
                case QuestTriggerType.MiniGameEnd:
                    MiniGameEndQuestTrigger game = new();
                    Set(game, "gameType", args.EnumAt(0, MiniGameType.None));
                    result.Add(game);
                    break;
                case QuestTriggerType.MiniGameResult:
                    MiniGameResultQuestTrigger gameResult = new();
                    Set(gameResult, "gameType", args.EnumAt(0, MiniGameType.None));
                    Set(gameResult, "needResult", args.EnumAt(1, MiniGameResult.None));
                    result.Add(gameResult);
                    break;
                case QuestTriggerType.RandomChance:
                    RandomChanceQuestTrigger chance = new();
                    Set(chance, "permille", Mathf.Clamp(args.Int(0, 1000), 1, 1000));
                    result.Add(chance);
                    break;
                case QuestTriggerType.PlotEnd:
                    PlotEndQuestTrigger plot = new();
                    Set(plot, "plotId", args.Long(0, 0));
                    result.Add(plot);
                    break;
                default:
                    // None / Auto 都是「重扫时只看接受条件」，新结构里合成了一个
                    result.Add(new AutoQuestTrigger());
                    break;
            }
        }
        return result;
    }

    static T Zone<T>(T trigger, Args args) where T : ZoneQuestTrigger
    {
        Set(trigger, "mapSceneId", args.Long(0, 0));
        Set(trigger, "sceneId", args.Long(1, 0));
        return trigger;
    }

    static T Id<T>(T trigger, Args args) where T : IdQuestTrigger
    {
        Set(trigger, "targetId", args.Long(0, 0));
        return trigger;
    }

    static QuestObjData Objective(string text, LocKeyRef desc)
    {
        Args args = Args.Split(text);
        if (args == null) return null;

        QuestObjData result = Create(args);
        if (result != null) Set(result, "desc", desc);
        return result;
    }

    static QuestObjData Create(Args args)
    {
        QuestObjType type = args.Head(QuestObjType.None);
        switch (type)
        {
            case QuestObjType.DayPassed:
                return Build(new DayPassedObjData(), ("needDay", Math.Max(1, args.Int(0, 1))));
            case QuestObjType.Dialog:
                return Build(new DialogObjData(), ("dialogueId", args.Long(0, 0)));
            case QuestObjType.CompleteQuest:
                return Build(new CompleteQuestObjData(), ("targetQuestId", args.Long(0, 0)));
            case QuestObjType.HoldItem:
                return Build(new HoldItemObjData(),
                    ("itemId", args.Long(0, 0)), ("needCount", Math.Max(1, args.Int(1, 1))));
            case QuestObjType.BuyItem:
                return Build(new BuyItemObjData(),
                    ("itemId", args.Long(0, 0)), ("needCount", Math.Max(1, args.Int(1, 1))));
            case QuestObjType.CharacterProp:
                return Build(new CharacterPropObjData(),
                    ("npcId", args.Long(0, 0)), ("needValue", Math.Max(1, args.Int(1, 1))),
                    ("propType", args.EnumAt(2, CharacterPropType.Goodwill)));
            case QuestObjType.CompleteGame:
                return Build(new CompleteGameObjData(),
                    ("gameType", args.EnumAt(0, MiniGameType.None)), ("needCount", Math.Max(1, args.Int(1, 1))),
                    ("needResult", args.EnumAt(2, MiniGameResult.None)));
            case QuestObjType.DialogNpc:
                return Build(new DialogNpcObjData(),
                    ("npcId", args.Long(0, 0)), ("needCount", Math.Max(1, args.Int(1, 1))));
            case QuestObjType.DialogNpcWithItem:
                return Build(new DialogNpcWithItemObjData(),
                    ("npcId", args.Long(0, 0)), ("itemId", args.Long(1, 0)),
                    ("needCount", Math.Max(1, args.Int(2, 1))));
            case QuestObjType.GiveGift:
                return Build(new GiveGiftObjData(),
                    ("npcId", args.Long(0, 0)), ("itemId", args.Long(1, 0)),
                    ("needCount", Math.Max(1, args.Int(2, 1))));
            default:
                Debug.LogError($"[Quest] 导入：目标类型 {type}（\"{args.Raw}\"）没有对应实现，跳过");
                return null;
        }
    }

    static List<IQuestReward> Rewards(string text)
    {
        List<IQuestReward> result = new();
        foreach (Args args in Args.SplitList(text))
        {
            QuestRewardType type = args.Head(QuestRewardType.None);
            switch (type)
            {
                case QuestRewardType.Item:
                    result.Add(Build(new ItemQuestReward(),
                        ("itemId", args.Long(0, 0)), ("count", Math.Max(1, args.Int(1, 1)))));
                    break;
                case QuestRewardType.Coin:
                    result.Add(Build(new CoinQuestReward(), ("value", Math.Max(1, args.Int(0, 1)))));
                    break;
                case QuestRewardType.GameCoin:
                    result.Add(Build(new GameCoinQuestReward(), ("value", Math.Max(1, args.Int(0, 1)))));
                    break;
                case QuestRewardType.Affection:
                    result.Add(Build(new AffectionQuestReward(),
                        ("npcId", args.Long(0, 0)), ("value", Math.Max(1, args.Int(1, 1)))));
                    break;
                default:
                    Debug.LogError($"[Quest] 导入：奖励类型 {type}（\"{args.Raw}\"）没有对应实现，跳过");
                    break;
            }
        }
        return result;
    }

    static List<QuestItemRequirement> ItemRequirements(string text)
    {
        List<QuestItemRequirement> result = new();
        foreach (string entry in Entries(text))
        {
            string[] values = entry.Split('+');
            result.Add(new QuestItemRequirement
            {
                itemId = ParseLong(values.ElementAtOrDefault(0)),
                count = ParseInt(values.ElementAtOrDefault(1), 1),
            });
        }
        return result;
    }

    static List<QuestCharacterRequirement> CharacterRequirements(string text)
    {
        List<QuestCharacterRequirement> result = new();
        foreach (string entry in Entries(text))
        {
            string[] values = entry.Split('+');
            result.Add(new QuestCharacterRequirement
            {
                npcId = ParseLong(values.ElementAtOrDefault(0)),
                propType = EnumValue(values.ElementAtOrDefault(1), CharacterPropType.Goodwill),
                value = ParseInt(values.ElementAtOrDefault(2), 1),
            });
        }
        return result;
    }

    #endregion

    #region 反射写字段

    static T Build<T>(T target, params (string Field, object Value)[] values)
    {
        foreach ((string field, object value) in values) Set(target, field, value);
        return target;
    }

    static void Set(object target, string fieldName, object value)
    {
        FieldInfo field = null;
        for (Type type = target.GetType(); type != null && field == null; type = type.BaseType)
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null)
        {
            Debug.LogError($"[Quest] 导入：{target.GetType().Name} 没有字段 {fieldName}，导入代码和数据类不同步了");
            return;
        }
        field.SetValue(target, value);
    }

    #endregion

    #region 单元格解析

    /// <summary>旧配置里的一段位置参数：<c>类型:参数1:参数2</c>，多段用 <c>/</c> 分隔。导完即弃。</summary>
    sealed class Args
    {
        public string Raw = string.Empty;
        string head = string.Empty;
        string[] values = Array.Empty<string>();

        public T Head<T>(T fallback) where T : struct
            => Enum.TryParse(head, true, out T parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;

        public long Long(int index, long fallback)
            => Has(index) ? ParseLong(values[index]) : fallback;

        public int Int(int index, int fallback)
            => Has(index) ? ParseInt(values[index], fallback) : fallback;

        public T EnumAt<T>(int index, T fallback) where T : struct, Enum
            => Has(index) ? EnumValue(values[index], fallback) : fallback;

        bool Has(int index) => index >= 0 && index < values.Length && values[index].Length > 0;

        public static Args Split(string text)
        {
            List<Args> list = SplitList(text);
            return list.Count == 0 ? null : list[0];
        }

        public static List<Args> SplitList(string text)
        {
            List<Args> result = new();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (string rawEntry in text.Split('/'))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0) continue;

                string[] parts = entry.Split(':');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                Args args = new() { Raw = entry, head = parts[0] };
                if (parts.Length > 1)
                {
                    args.values = new string[parts.Length - 1];
                    Array.Copy(parts, 1, args.values, 0, args.values.Length);
                }
                result.Add(args);
            }
            return result;
        }
    }

    static IEnumerable<string> Entries(string text)
        => string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    static List<long> LongList(string text)
        => string.IsNullOrWhiteSpace(text)
            ? new List<long>()
            : text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseLong).ToList();

    static LocKeyRef Loc(string key, string prefix)
    {
        if (string.IsNullOrWhiteSpace(key)) return new LocKeyRef();
        key = key.Trim();
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) key = prefix + key.TrimStart('/');
        return new LocKeyRef { Table = LocTableSet.QuestSystem, Value = key };
    }

    static AssetReferenceSprite Icon(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string guid = AssetDatabase.AssetPathToGUID(QuestAssetPath.IconRoot + key + ".png");
        return string.IsNullOrEmpty(guid) ? null : new AssetReferenceSprite(guid);
    }

    static List<Dictionary<string, string>> Rows(string fileName)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return QuestXlsxReader.Read(Path.Combine(projectRoot, ExcelRoot, fileName));
    }

    static string Cell(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out string value) ? value.Trim() : string.Empty;

    static long Long(IReadOnlyDictionary<string, string> row, string key) => ParseLong(Cell(row, key));
    static int Int(IReadOnlyDictionary<string, string> row, string key) => ParseInt(Cell(row, key));

    static bool Bool(IReadOnlyDictionary<string, string> row, string key)
        => bool.TryParse(Cell(row, key), out bool value) && value;

    static long ParseLong(string text)
        => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;

    static int ParseInt(string text, int fallback = 0)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    static T EnumValue<T>(string text, T fallback) where T : struct, Enum
        => Enum.TryParse(text, true, out T value) && Enum.IsDefined(typeof(T), value) ? value : fallback;

    static void EnsureAssetFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1))
        {
            string child = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, part);
            current = child;
        }
    }

    #endregion
}

static class QuestXlsxReader
{
    const string SheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static List<Dictionary<string, string>> Read(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        List<string> sharedStrings = ReadSharedStrings(archive);
        XmlDocument sheet = ReadFirstSheet(archive);
        XmlNamespaceManager ns = new(sheet.NameTable);
        ns.AddNamespace("x", SheetNs);

        Dictionary<int, string> headers = new();
        XmlNode headerRow = sheet.SelectSingleNode("//x:sheetData/x:row[@r='1']", ns);
        foreach (XmlElement cell in headerRow.SelectNodes("x:c", ns))
        {
            int column = ColumnIndex(cell.GetAttribute("r"));
            string name = CellValue(cell, sharedStrings, ns);
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("##")) headers[column] = name;
        }

        List<Dictionary<string, string>> result = new();
        foreach (XmlElement row in sheet.SelectNodes("//x:sheetData/x:row", ns))
        {
            if (!int.TryParse(row.GetAttribute("r"), out int rowNumber) || rowNumber < 4) continue;

            Dictionary<string, string> values = new();
            foreach (XmlElement cell in row.SelectNodes("x:c", ns))
            {
                int column = ColumnIndex(cell.GetAttribute("r"));
                if (headers.TryGetValue(column, out string name))
                    values[name] = CellValue(cell, sharedStrings, ns);
            }
            if (values.TryGetValue("Id", out string id) && !string.IsNullOrWhiteSpace(id)) result.Add(values);
        }
        return result;
    }

    static XmlDocument ReadFirstSheet(ZipArchive archive)
    {
        XmlDocument workbook = ReadXml(archive, "xl/workbook.xml");
        XmlNamespaceManager workbookNs = new(workbook.NameTable);
        workbookNs.AddNamespace("x", SheetNs);
        workbookNs.AddNamespace("r", OfficeRelNs);
        string relationshipId = ((XmlElement)workbook.SelectSingleNode("//x:sheets/x:sheet", workbookNs))
            .GetAttribute("id", OfficeRelNs);

        XmlDocument relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        XmlNamespaceManager relNs = new(relationships.NameTable);
        relNs.AddNamespace("r", PackageRelNs);
        XmlElement relationship = (XmlElement)relationships.SelectSingleNode(
            $"//r:Relationship[@Id='{relationshipId}']", relNs);
        string target = relationship.GetAttribute("Target").Replace('\\', '/').TrimStart('/');
        if (!target.StartsWith("xl/", StringComparison.Ordinal)) target = "xl/" + target;
        return ReadXml(archive, target);
    }

    static List<string> ReadSharedStrings(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return new List<string>();

        XmlDocument document = new();
        using (Stream stream = entry.Open()) document.Load(stream);
        XmlNamespaceManager ns = new(document.NameTable);
        ns.AddNamespace("x", SheetNs);
        List<string> result = new();
        foreach (XmlNode item in document.SelectNodes("//x:si", ns))
        {
            string text = string.Concat(item.SelectNodes(".//x:t", ns).Cast<XmlNode>().Select(node => node.InnerText));
            result.Add(text);
        }
        return result;
    }

    static string CellValue(XmlElement cell, IReadOnlyList<string> sharedStrings, XmlNamespaceManager ns)
    {
        string type = cell.GetAttribute("t");
        if (type == "inlineStr") return string.Concat(
            cell.SelectNodes(".//x:t", ns).Cast<XmlNode>().Select(node => node.InnerText));

        string value = cell.SelectSingleNode("x:v", ns)?.InnerText ?? string.Empty;
        return type == "s" && int.TryParse(value, out int index) && index < sharedStrings.Count
            ? sharedStrings[index]
            : value;
    }

    static XmlDocument ReadXml(ZipArchive archive, string entryName)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"Excel 内缺少 {entryName}");
        XmlDocument document = new();
        using Stream stream = entry.Open();
        document.Load(stream);
        return document;
    }

    static int ColumnIndex(string cellReference)
    {
        int result = 0;
        foreach (char c in cellReference)
        {
            if (!char.IsLetter(c)) break;
            result = result * 26 + char.ToUpperInvariant(c) - 'A' + 1;
        }
        return result - 1;
    }
}
