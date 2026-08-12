using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;

internal enum QuestReferenceKind
{
    Quest,
    Objective,
    Condition,
    Item,
    Npc,
    Dialogue,
    MapScene,
    Scene,
}

internal readonly struct QuestReferenceOption
{
    public readonly long Id;
    public readonly string Label;

    public QuestReferenceOption(long id, string remark)
    {
        Id = id;
        Label = string.IsNullOrWhiteSpace(remark) ? id.ToString() : $"{id} | {remark}";
    }
}

/// <summary>为任务表中的 ID 单元格提供可读的下拉选项，数据本身仍保存为 long。</summary>
internal static class QuestEditorReferenceCatalog
{
    const string LubanJsonDirectory = "Assets/AddressableAssets/Remote/Configs/LubanJson";

    static readonly Dictionary<QuestReferenceKind, (string File, string Id)> JsonSources = new()
    {
        { QuestReferenceKind.Item, ("tbitemdata.json", "ID") },
        { QuestReferenceKind.Npc, ("tbnpcdata.json", "Id") },
        { QuestReferenceKind.Dialogue, ("tbdialoguedata.json", "Id") },
        { QuestReferenceKind.MapScene, ("tbwordmapscenedata.json", "ID") },
        { QuestReferenceKind.Scene, ("tbgamescenedata.json", "ID") },
    };

    static readonly Dictionary<QuestReferenceKind, List<QuestReferenceOption>> JsonCache = new();

    public static IReadOnlyList<QuestReferenceOption> GetOptions(
        QuestReferenceKind kind, QuestDatabaseData database = null)
    {
        database ??= QuestDatabaseProvider.Database;
        return kind switch
        {
            QuestReferenceKind.Quest => database?.Quests
                .OrderBy(data => data.id).Select(data => new QuestReferenceOption(data.id, data.remark)).ToArray()
                ?? Array.Empty<QuestReferenceOption>(),
            QuestReferenceKind.Objective => database?.Objectives
                .OrderBy(data => data.id).Select(data => new QuestReferenceOption(data.id, data.remark)).ToArray()
                ?? Array.Empty<QuestReferenceOption>(),
            QuestReferenceKind.Condition => database?.Conditions
                .OrderBy(data => data.id).Select(data => new QuestReferenceOption(data.id, data.remark)).ToArray()
                ?? Array.Empty<QuestReferenceOption>(),
            _ => GetJsonOptions(kind),
        };
    }

    public static string Format(long id, QuestReferenceKind kind, QuestDatabaseData database = null)
    {
        if (id == 0) return "无";
        QuestReferenceOption match = GetOptions(kind, database).FirstOrDefault(option => option.Id == id);
        return match.Id == id ? match.Label : $"{id} | 未找到";
    }

    public static void Invalidate() => JsonCache.Clear();

    static IReadOnlyList<QuestReferenceOption> GetJsonOptions(QuestReferenceKind kind)
    {
        if (JsonCache.TryGetValue(kind, out List<QuestReferenceOption> cached)) return cached;
        List<QuestReferenceOption> result = new();
        JsonCache[kind] = result;
        if (!JsonSources.TryGetValue(kind, out (string File, string Id) source)) return result;

        string path = Path.Combine(Directory.GetCurrentDirectory(), LubanJsonDirectory, source.File);
        if (!File.Exists(path)) return result;
        try
        {
            foreach (JToken row in JArray.Parse(File.ReadAllText(path)))
            {
                long id = row.Value<long?>(source.Id) ?? 0;
                if (id <= 0) continue;
                result.Add(new QuestReferenceOption(id, row.Value<string>("Remark")));
            }
            result.Sort((left, right) => left.Id.CompareTo(right.Id));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[QuestEditor] 读取下拉数据失败：{source.File}\n{exception.Message}");
        }
        return result;
    }
}

/// <summary>虚拟化表格复用的 ID 下拉框，显示可读名称但只写入数值 ID。</summary>
internal sealed class QuestReferenceDropdown : DropdownField
{
    const string NoneLabel = "0 | 无";

    readonly Dictionary<string, long> values = new(StringComparer.Ordinal);
    Action<long> onChanged;

    public QuestReferenceDropdown()
    {
        AddToClassList("quest-reference-dropdown");
        this.RegisterValueChangedCallback(evt =>
        {
            if (onChanged != null && values.TryGetValue(evt.newValue, out long id)) onChanged(id);
        });
    }

    public void Bind(long currentValue, QuestReferenceKind kind, QuestDatabaseData database,
        bool allowNone, Action<long> changed)
    {
        onChanged = changed;
        values.Clear();
        List<string> labels = new();
        if (allowNone)
        {
            labels.Add(NoneLabel);
            values.Add(NoneLabel, 0);
        }

        foreach (QuestReferenceOption option in QuestEditorReferenceCatalog.GetOptions(kind, database))
        {
            if (values.ContainsKey(option.Label)) continue;
            labels.Add(option.Label);
            values.Add(option.Label, option.Id);
        }

        string currentLabel = currentValue == 0 && allowNone
            ? NoneLabel
            : labels.FirstOrDefault(label => values[label] == currentValue);
        if (currentLabel == null)
        {
            currentLabel = $"{currentValue} | 未找到";
            labels.Insert(0, currentLabel);
            values[currentLabel] = currentValue;
        }

        choices = labels;
        SetValueWithoutNotify(currentLabel);
    }
}
