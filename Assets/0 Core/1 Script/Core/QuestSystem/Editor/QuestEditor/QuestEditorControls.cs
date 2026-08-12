using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

/// <summary>为 Luban 枚举提供稳定的双语显示名，序列化值保持不变。</summary>
internal sealed class QuestEnumLabelSet<TEnum> where TEnum : Enum
{
    readonly Dictionary<TEnum, string> labels;
    readonly Dictionary<string, TEnum> values;

    public QuestEnumLabelSet(Dictionary<TEnum, string> labels)
    {
        this.labels = labels;
        Choices = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Select(Get).ToArray();
        values = labels.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<string> Choices { get; }
    public string Get(TEnum value) => labels.TryGetValue(value, out string label) ? label : value.ToString();

    public bool GetValue(string label, out TEnum value) => values.TryGetValue(label, out value);
}

internal static class QuestObjectiveTypeLabels
{
    internal static readonly QuestEnumLabelSet<QuestObjType> Set = new(new Dictionary<QuestObjType, string>
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
    });

    public static string Get(QuestObjType value) => Set.Get(value);
}

internal static class QuestTriggerTypeLabels
{
    internal static readonly QuestEnumLabelSet<QuestTriggerType> Set = new(new Dictionary<QuestTriggerType, string>
    {
        { QuestTriggerType.None, "None - 仅检查接受条件" },
        { QuestTriggerType.Auto, "Auto - 自动尝试领取" },
        { QuestTriggerType.EnterZone, "Enter Zone - 进入区域" },
        { QuestTriggerType.ExitZone, "Exit Zone - 离开区域" },
        { QuestTriggerType.EnterZoneStay, "Enter Zone Stay - 在区域停留" },
        { QuestTriggerType.ClickNpc, "Click Npc - 点击指定 NPC" },
        { QuestTriggerType.DialogNpc, "Dialog Npc - 与指定 NPC 对话" },
        { QuestTriggerType.MiniGameEnd, "Mini Game End - 小游戏结束" },
        { QuestTriggerType.MiniGameResult, "Mini Game Result - 小游戏产生指定结果" },
        { QuestTriggerType.RandomChance, "Random Chance - 随机概率触发" },
        { QuestTriggerType.PlotEnd, "Plot End - 剧情结束（暂未实现）" },
    });

    public static string Get(QuestTriggerType value) => Set.Get(value);
}

internal static class QuestRewardTypeLabels
{
    internal static readonly QuestEnumLabelSet<QuestRewardType> Set = new(new Dictionary<QuestRewardType, string>
    {
        { QuestRewardType.None, "None - 未设置" },
        { QuestRewardType.Item, "Item - 道具" },
        { QuestRewardType.Coin, "Coin - 金币" },
        { QuestRewardType.GameCoin, "Game Coin - 游戏币" },
        { QuestRewardType.Affection, "Affection - 好感度" },
    });

    public static string Get(QuestRewardType value) => Set.Get(value);
}

internal abstract class QuestEnumDropdown<TEnum> : DropdownField where TEnum : Enum
{
    readonly QuestEnumLabelSet<TEnum> labels;
    Action<TEnum> onChanged;

    protected QuestEnumDropdown(QuestEnumLabelSet<TEnum> labels)
    {
        this.labels = labels;
        choices = labels.Choices.ToList();
        this.RegisterValueChangedCallback(evt =>
        {
            if (onChanged != null && labels.GetValue(evt.newValue, out TEnum value)) onChanged(value);
        });
    }

    public void Bind(TEnum currentValue, Action<TEnum> changed)
    {
        onChanged = null;
        SetValueWithoutNotify(labels.Get(currentValue));
        onChanged = changed;
    }
}

internal sealed class QuestObjectiveTypeDropdown : QuestEnumDropdown<QuestObjType>
{
    public QuestObjectiveTypeDropdown() : base(QuestObjectiveTypeLabels.Set) { }
}

internal sealed class QuestTriggerTypeDropdown : QuestEnumDropdown<QuestTriggerType>
{
    public QuestTriggerTypeDropdown() : base(QuestTriggerTypeLabels.Set) { }
}

internal sealed class QuestRewardTypeDropdown : QuestEnumDropdown<QuestRewardType>
{
    public QuestRewardTypeDropdown() : base(QuestRewardTypeLabels.Set) { }
}

/// <summary>将单一 Sprite 选择转换为 Addressables Sprite 引用。</summary>
internal static class QuestIconEditorUtility
{
    public static Sprite GetSprite(AssetReferenceSprite reference)
    {
        if (reference == null || !reference.RuntimeKeyIsValid()) return null;
        string path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
        if (string.IsNullOrEmpty(path)) return null;
        if (string.IsNullOrEmpty(reference.SubObjectName))
            return reference.editorAsset as Sprite ?? AssetDatabase.LoadAssetAtPath<Sprite>(path);

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == reference.SubObjectName);
    }

    public static AssetReferenceSprite Create(Sprite sprite)
    {
        if (sprite == null) return null;
        AssetReferenceSprite reference = new(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite)));
        if (AssetDatabase.IsSubAsset(sprite)) reference.SetEditorSubObject(sprite);
        return reference;
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
