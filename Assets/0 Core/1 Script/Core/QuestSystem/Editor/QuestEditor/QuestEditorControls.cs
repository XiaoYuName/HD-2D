using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using XFramework;

internal sealed class QuestLocalizationOption
{
    public string Key;
    public string Preview;

    public string SearchText => $"{Key} {Preview}";
}

/// <summary>任务编辑器使用的多语言 Key 读取与展示逻辑。</summary>
internal static class QuestLocalizationEditorUtility
{
    public static IReadOnlyList<QuestLocalizationOption> GetOptions(string keyPrefix)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(LocTableSet.QuestSystem);
        if (collection?.SharedData == null) return Array.Empty<QuestLocalizationOption>();

        StringTable previewTable = LocalizationKeySelectorUtility.GetPreferredTable(
            collection.StringTables.ToList());
        return collection.SharedData.Entries
            .Where(entry => entry != null &&
                            !string.IsNullOrEmpty(entry.Key) &&
                            entry.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
            .Select(entry => new QuestLocalizationOption
            {
                Key = entry.Key,
                Preview = LocalizationKeySelectorUtility.FormatPreview(
                    previewTable?.GetEntry(entry.Key)?.LocalizedValue ?? string.Empty),
            })
            .OrderBy(option => option.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public static string GetKey(LocalizedString value)
    {
        if (value == null || value.IsEmpty) return string.Empty;
        TableEntryReference reference = value.TableEntryReference;
        if (reference.ReferenceType == TableEntryReference.Type.Name) return reference.Key;

        var collection = LocalizationEditorSettings.GetStringTableCollection(value.TableReference);
        return collection?.SharedData == null
            ? string.Empty
            : reference.ResolveKeyName(collection.SharedData) ?? string.Empty;
    }

    public static string GetDisplayPath(LocalizedString value)
    {
        string key = GetKey(value);
        return string.IsNullOrEmpty(key) ? "未配置" : $"{LocTableSet.QuestSystem}/{key}";
    }

    public static string GetPreview(LocalizedString value)
    {
        string key = GetKey(value);
        return string.IsNullOrEmpty(key)
            ? string.Empty
            : LocalizationKeySelectorUtility.GetPreviewText(LocTableSet.QuestSystem, key) ?? string.Empty;
    }

    public static LocalizedString Create(string key)
        => string.IsNullOrEmpty(key) ? new LocalizedString() : new LocalizedString(LocTableSet.QuestSystem, key);
}

/// <summary>任务目标类型的双语显示名；序列化值仍保持 Luban 枚举。</summary>
internal static class QuestObjectiveTypeLabels
{
    static readonly QuestObjType[] Values = Enum.GetValues(typeof(QuestObjType)).Cast<QuestObjType>().ToArray();
    static readonly Dictionary<QuestObjType, string> Labels = new()
    {
        { QuestObjType.None, "None - 未设置" },
        { QuestObjType.DayPassed, "Day Passed - 经过指定天数" },
        { QuestObjType.Dialog, "Dialog - 完成指定对话" },
        { QuestObjType.CompleteQuest, "Complete Quest - 完成指定任务" },
        { QuestObjType.HoldItem, "Hold Item - 持有指定道具" },
        { QuestObjType.CharacterProp, "Character Prop - 角色属性达到数值" },
        { QuestObjType.CompleteGame, "Complete Game - 完成小游戏" },
        { QuestObjType.DialogNpc, "Dialog Npc - 与指定 NPC 对话" },
        { QuestObjType.DialogNpcWithItem, "Dialog Npc With Item - 携带道具与 NPC 对话" },
        { QuestObjType.GiveGift, "Give Gift - 给 NPC 赠送礼物" },
        { QuestObjType.BuyItem, "Buy Item - 购买指定道具" },
    };

    public static IReadOnlyList<string> Choices { get; } = Values.Select(Get).ToArray();

    public static string Get(QuestObjType type)
        => Labels.TryGetValue(type, out string label) ? label : type.ToString();

    public static bool TryParse(string label, out QuestObjType type)
    {
        foreach ((QuestObjType value, string text) in Labels)
        {
            if (!string.Equals(text, label, StringComparison.Ordinal)) continue;
            type = value;
            return true;
        }

        type = QuestObjType.None;
        return false;
    }
}

/// <summary>表格与参数弹层共用的双语任务目标类型下拉框。</summary>
internal sealed class QuestObjectiveTypeDropdown : DropdownField
{
    Action<QuestObjType> onChanged;

    public QuestObjectiveTypeDropdown()
    {
        choices = QuestObjectiveTypeLabels.Choices.ToList();
        this.RegisterValueChangedCallback(evt =>
        {
            if (onChanged != null && QuestObjectiveTypeLabels.TryParse(evt.newValue, out QuestObjType value))
                onChanged(value);
        });
    }

    public void Bind(QuestObjType currentValue, Action<QuestObjType> changed)
    {
        onChanged = null;
        SetValueWithoutNotify(QuestObjectiveTypeLabels.Get(currentValue));
        onChanged = changed;
    }
}

/// <summary>道具 ID 搜索框：支持按 ID 或名称过滤，数据仍只保存 long。</summary>
internal sealed class QuestItemSearchField : VisualElement
{
    const string NoneLabel = "0 | 无";

    readonly Label currentValue = new();
    readonly ToolbarSearchField search = new();
    readonly ListView results = new();
    readonly List<QuestReferenceOption> allOptions = new();
    readonly List<QuestReferenceOption> filteredOptions = new();

    Action<long> onChanged;

    public QuestItemSearchField(string label)
    {
        AddToClassList("quest-reference-search");

        VisualElement header = new();
        header.AddToClassList("quest-reference-search-header");
        Label title = new(label);
        title.AddToClassList("quest-reference-search-label");
        currentValue.AddToClassList("quest-reference-search-current");
        header.Add(title);
        header.Add(currentValue);
        Add(header);

        VisualElement searchRow = new();
        searchRow.AddToClassList("quest-reference-search-toolbar");
        Label searchLabel = new("搜索 ID / 名称");
        searchLabel.AddToClassList("quest-reference-search-hint");
        search.AddToClassList("quest-reference-search-input");
        search.tooltip = "输入道具 ID 或中文名称";
        searchRow.Add(searchLabel);
        searchRow.Add(search);
        Add(searchRow);

        results.fixedItemHeight = 24;
        results.selectionType = SelectionType.Single;
        results.makeItem = () => new Label();
        results.bindItem = (element, index) => ((Label)element).text = filteredOptions[index].Label;
        results.AddToClassList("quest-reference-search-results");
        results.AddToClassList("is-hidden");
        results.selectionChanged += selected =>
        {
            foreach (object item in selected)
            {
                if (item is not QuestReferenceOption option) continue;
                Select(option);
                break;
            }
        };
        Add(results);

        search.RegisterCallback<FocusInEvent>(_ => RefreshResults(string.Empty));
        search.RegisterValueChangedCallback(evt => RefreshResults(evt.newValue));
        search.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CloseResults();
                evt.StopPropagation();
            }
            else if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) &&
                     filteredOptions.Count > 0)
            {
                Select(filteredOptions[0]);
                evt.StopPropagation();
            }
        });
    }

    public void Bind(long currentId, QuestDatabaseData database, bool allowNone, Action<long> changed)
    {
        onChanged = changed;
        allOptions.Clear();
        if (allowNone) allOptions.Add(new QuestReferenceOption(0, "无"));
        allOptions.AddRange(QuestEditorReferenceCatalog.GetOptions(QuestReferenceKind.Item, database));

        QuestReferenceOption? current = allOptions
            .Where(option => option.Id == currentId)
            .Cast<QuestReferenceOption?>()
            .FirstOrDefault();
        currentValue.text = current.HasValue
            ? current.Value.Label
            : currentId == 0 && allowNone ? NoneLabel : $"{currentId} | 未找到";
        search.SetValueWithoutNotify(string.Empty);
        CloseResults();
    }

    void RefreshResults(string query)
    {
        filteredOptions.Clear();
        IEnumerable<QuestReferenceOption> source = allOptions;
        if (!string.IsNullOrWhiteSpace(query))
        {
            string value = query.Trim();
            source = source.Where(option =>
                option.Label.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        filteredOptions.AddRange(source.Take(200));
        results.itemsSource = filteredOptions;
        results.RefreshItems();
        results.ClearSelection();
        results.EnableInClassList("is-hidden", filteredOptions.Count == 0);
    }

    void Select(QuestReferenceOption option)
    {
        currentValue.text = option.Label;
        search.SetValueWithoutNotify(string.Empty);
        CloseResults();
        onChanged?.Invoke(option.Id);
    }

    void CloseResults()
    {
        results.ClearSelection();
        results.AddToClassList("is-hidden");
    }
}
