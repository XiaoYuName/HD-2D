using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using XFramework;

[InitializeOnLoad]
public static class QuestLubanMigration
{
    public const string DatabaseAssetPath = "Assets/Resources/Quest/QuestDatabase.asset";
    const string ExcelRoot = "ExcelTool/LubanTools/DataTables/Datas/";

    static QuestLubanMigration() => EditorApplication.delayCall += EnsureDatabase;

    // [MenuItem("Tools/QuestSystem/从 Luban Excel 迁移到 ScriptableObject")]
    public static void ImportFromMenu()
    {
        QuestDatabaseData database = ImportFromExcel(true);
        if (database != null) Selection.activeObject = database;
    }

    public static QuestDatabaseData ImportFromExcel(bool confirm)
    {
        QuestDatabaseData database = AssetDatabase.LoadAssetAtPath<QuestDatabaseData>(DatabaseAssetPath);
        if (database != null && confirm && !EditorUtility.DisplayDialog(
                "重新导入任务配置",
                "将用 5 张 Luban Excel 更新任务数据库。数据源模式和已有记录的迁移开关会保持不变；新增记录默认不启用。",
                "更新导入", "取消"))
            return null;

        bool isNew = database == null;
        if (isNew)
        {
            EnsureAssetFolder("Assets/Resources/Quest");
            database = ScriptableObject.CreateInstance<QuestDatabaseData>();
            database.SourceMode = QuestConfigSourceMode.LubanOnly;
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
        }
        else Undo.RecordObject(database, "导入 Luban 任务配置");

        HashSet<long> enabledCategoryIds = EnabledIds(database.Categories, data => data.id, data => data.enabled);
        HashSet<long> enabledQuestIds = EnabledIds(database.Quests, data => data.id, data => data.enabled);
        HashSet<long> enabledObjectiveIds = EnabledIds(database.Objectives, data => data.id, data => data.enabled);
        HashSet<long> enabledConditionIds = EnabledIds(database.Conditions, data => data.id, data => data.enabled);
        HashSet<QuestRewardType> enabledRewardTypes = database.RewardPresentations
            .Where(data => data.enabled).Select(data => data.type).ToHashSet();

        database.Categories.Clear();
        database.Quests.Clear();
        database.Objectives.Clear();
        database.Conditions.Clear();
        database.RewardPresentations.Clear();

        ImportCategories(database, enabledCategoryIds);
        ImportQuests(database, enabledQuestIds);
        ImportObjectives(database, enabledObjectiveIds);
        ImportRewardPresentations(database, enabledRewardTypes);
        ImportConditions(database, enabledConditionIds);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        QuestDatabaseProvider.ClearCache();
        Debug.Log($"[QuestEditor] 已导入 {database.Categories.Count} 类别、{database.Quests.Count} 任务、" +
                  $"{database.Objectives.Count} 目标、{database.Conditions.Count} 条件、" +
                  $"{database.RewardPresentations.Count} 奖励显示配置。当前数据源：{database.SourceMode}");
        return database;
    }

    /// <summary>首次加载时生成安全的 LubanOnly 快照，已有数据库绝不自动覆盖。</summary>
    static void EnsureDatabase()
    {
        if (AssetDatabase.LoadAssetAtPath<QuestDatabaseData>(DatabaseAssetPath) != null) return;

        try
        {
            ImportFromExcel(false);
            Debug.Log("[QuestEditor] 已创建 QuestDatabaseData，默认保持 LubanOnly，可在任务编辑器中逐步启用 SO 配置。");
        }
        catch (Exception e)
        {
            Debug.LogError($"[QuestEditor] 自动迁移任务 Excel 失败：{e}");
        }
    }

    static void ImportCategories(QuestDatabaseData database, ISet<long> enabledIds)
    {
        foreach (Dictionary<string, string> row in Rows("QuestCategoryData.xlsx"))
        {
            long id = Long(row, "Id");
            database.Categories.Add(new QuestCategoryDefinition
            {
                enabled = enabledIds.Contains(id),
                id = id,
                remark = Cell(row, "Remark"),
                name = Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.Category),
                desc = Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Category),
                icon = Icon(Cell(row, "IconKey")),
                questIds = LongList(Cell(row, "QuestId")),
                rewards = Rewards(Cell(row, "Reward")),
            });
        }
    }

    static void ImportQuests(QuestDatabaseData database, ISet<long> enabledIds)
    {
        foreach (Dictionary<string, string> row in Rows("QuestDataConfig.xlsx"))
        {
            long id = Long(row, "Id");
            database.Quests.Add(new QuestDefinition
            {
                enabled = enabledIds.Contains(id),
                id = id,
                remark = Cell(row, "Remark"),
                triggers = Triggers(Cell(row, "QuestTrigger")),
                acceptConditionId = Long(row, "AcceptCond"),
                name = Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.Quest),
                desc = Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Quest),
                icon = Icon(Cell(row, "IconKey")),
                objectiveIds = LongList(Cell(row, "QuestObjData")),
                rewards = Rewards(Cell(row, "Reward")),
                objectivesInOrder = Bool(row, "ObjInOrder"),
            });
        }
    }

    static void ImportObjectives(QuestDatabaseData database, ISet<long> enabledIds)
    {
        foreach (Dictionary<string, string> row in Rows("QuestObjConfig.xlsx"))
        {
            long id = Long(row, "Id");
            string extra = Cell(row, "ExtraCompleteCond");
            database.Objectives.Add(new QuestObjectiveDefinition
            {
                enabled = enabledIds.Contains(id),
                id = id,
                remark = Cell(row, "Remark"),
                desc = Loc(Cell(row, "DescKey"), QuestLocKey.Prefix.Objective),
                objective = Objective(Cell(row, "QuestObjData")),
                rewards = Rewards(Cell(row, "Reward")),
                hasExtra = !string.IsNullOrWhiteSpace(extra),
                extraDesc = Loc(Cell(row, "ExtraDescKey"), QuestLocKey.Prefix.Objective),
                extraObjective = string.IsNullOrWhiteSpace(extra) ? new QuestObjectiveSpec() : Objective(extra),
                extraRewards = Rewards(Cell(row, "ExtraReward")),
            });
        }
    }

    static void ImportRewardPresentations(QuestDatabaseData database, ISet<QuestRewardType> enabledTypes)
    {
        foreach (Dictionary<string, string> row in Rows("QuestRewardData.xlsx"))
        {
            QuestRewardType type = EnumValue(Cell(row, "Id"), QuestRewardType.None);
            database.RewardPresentations.Add(new QuestRewardPresentation
            {
                enabled = enabledTypes.Contains(type),
                type = type,
                remark = Cell(row, "Remark"),
                name = Loc(Cell(row, "NameKey"), QuestLocKey.Prefix.RewardName),
                icon = Icon(Cell(row, "IconKey")),
            });
        }
    }

    static void ImportConditions(QuestDatabaseData database, ISet<long> enabledIds)
    {
        foreach (Dictionary<string, string> row in Rows("QuestStoryCondData.xlsx"))
        {
            long id = Long(row, "Id");
            database.Conditions.Add(new QuestConditionDefinition
            {
                enabled = enabledIds.Contains(id),
                id = id,
                remark = Cell(row, "Remark"),
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
            });
        }
    }

    static HashSet<long> EnabledIds<T>(IEnumerable<T> records, Func<T, long> id, Func<T, bool> enabled)
        => records.Where(enabled).Select(id).ToHashSet();

    static List<QuestTriggerSpec> Triggers(string text)
    {
        QuestArgs[] args = QuestArgs.SplitList(text, "Excel 迁移");
        List<QuestTriggerSpec> result = new(args.Length);
        foreach (QuestArgs data in args)
        {
            QuestTriggerSpec spec = new() { type = data.GetHead(QuestTriggerType.None) };
            switch (spec.type)
            {
                case QuestTriggerType.EnterZone:
                case QuestTriggerType.ExitZone:
                    spec.mapSceneId = data.GetLong(0, 0);
                    spec.sceneId = data.GetLong(1, 0);
                    break;
                case QuestTriggerType.EnterZoneStay:
                    spec.mapSceneId = data.GetLong(0, 0);
                    spec.sceneId = data.GetLong(1, 0);
                    spec.staySeconds = data.GetInt(2, 1);
                    break;
                case QuestTriggerType.ClickNpc:
                case QuestTriggerType.DialogNpc:
                    spec.npcId = data.GetLong(0, 0);
                    break;
                case QuestTriggerType.MiniGameEnd:
                    spec.gameType = data.GetEnum(0, MiniGameType.None);
                    break;
                case QuestTriggerType.MiniGameResult:
                    spec.gameType = data.GetEnum(0, MiniGameType.None);
                    spec.gameResult = data.GetEnum(1, MiniGameResult.None);
                    break;
                case QuestTriggerType.RandomChance:
                    spec.permille = data.GetInt(0, 0);
                    break;
                case QuestTriggerType.PlotEnd:
                    spec.plotId = data.GetLong(0, 0);
                    break;
            }
            result.Add(spec);
        }
        return result;
    }

    static QuestObjectiveSpec Objective(string text)
    {
        QuestArgs data = QuestArgs.Split(text, "Excel 迁移");
        QuestObjectiveSpec result = new() { type = data.GetHead(QuestObjType.None) };
        switch (result.type)
        {
            case QuestObjType.DayPassed:
                result.count = data.GetInt(0, 1);
                break;
            case QuestObjType.Dialog:
                result.dialogueId = data.GetLong(0, 0);
                break;
            case QuestObjType.CompleteQuest:
                result.questId = data.GetLong(0, 0);
                break;
            case QuestObjType.HoldItem:
            case QuestObjType.BuyItem:
                result.itemId = data.GetLong(0, 0);
                result.count = data.GetInt(1, 1);
                break;
            case QuestObjType.CharacterProp:
                result.npcId = data.GetLong(0, 0);
                result.value = data.GetInt(1, 1);
                result.characterPropType = data.GetEnum(2, CharacterPropType.Goodwill);
                break;
            case QuestObjType.CompleteGame:
                result.gameType = data.GetEnum(0, MiniGameType.None);
                result.count = data.GetInt(1, 1);
                result.gameResult = data.GetEnum(2, MiniGameResult.None);
                break;
            case QuestObjType.DialogNpc:
                result.npcId = data.GetLong(0, 0);
                result.count = data.GetInt(1, 1);
                break;
            case QuestObjType.DialogNpcWithItem:
            case QuestObjType.GiveGift:
                result.npcId = data.GetLong(0, 0);
                result.itemId = data.GetLong(1, 0);
                result.count = data.GetInt(2, 1);
                break;
        }
        return result;
    }

    static List<QuestRewardSpec> Rewards(string text)
    {
        QuestArgs[] args = QuestArgs.SplitList(text, "Excel 迁移");
        List<QuestRewardSpec> result = new(args.Length);
        foreach (QuestArgs data in args)
        {
            QuestRewardSpec reward = new() { type = data.GetHead(QuestRewardType.None) };
            switch (reward.type)
            {
                case QuestRewardType.Item:
                    reward.itemId = data.GetLong(0, 0);
                    reward.amount = data.GetInt(1, 1);
                    break;
                case QuestRewardType.Coin:
                case QuestRewardType.GameCoin:
                    reward.amount = data.GetInt(0, 0);
                    break;
                case QuestRewardType.Affection:
                    reward.npcId = data.GetLong(0, 0);
                    reward.amount = data.GetInt(1, 0);
                    break;
            }
            result.Add(reward);
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

    static IEnumerable<string> Entries(string text)
        => string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    static List<long> LongList(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<long>();
        return text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLong).ToList();
    }

    static LocalizedString Loc(string key, string prefix)
    {
        if (string.IsNullOrWhiteSpace(key)) return new LocalizedString();
        key = key.Trim();
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) key = prefix + key.TrimStart('/');
        return new LocalizedString(LocTableSet.QuestSystem, key);
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
        => Enum.TryParse(text, true, out T value) ? value : fallback;

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
