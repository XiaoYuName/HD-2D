using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XFramework;

/// <summary>ID 引用的一个候选项：显示「ID | 备注」，写回的是 <see cref="Id"/>。</summary>
internal readonly struct QuestRefOption
{
    public readonly long Id;
    public readonly string Label;

    public QuestRefOption(long id, string remark)
    {
        Id = id;
        Label = string.IsNullOrWhiteSpace(remark) ? id.ToString() : $"{id} | {remark}";
    }
}

/// <summary>
/// <see cref="QuestRefKind"/> → 候选 ID 列表。
/// 道具 / NPC / 对话 / 场景来自 Luban 导出的 JSON，任务 / 目标 / 条件来自任务数据库本身。
/// </summary>
internal static class QuestRefCatalog
{
    const string JsonDirectory = "Assets/AddressableAssets/Remote/Configs/LubanJson";

    static readonly Dictionary<QuestRefKind, (string File, string Id)> JsonSources = new()
    {
        { QuestRefKind.Item, ("tbitemdata.json", "ID") },
        { QuestRefKind.Npc, ("tbnpcdata.json", "Id") },
        { QuestRefKind.Dialogue, ("tbdialoguedata.json", "Id") },
        { QuestRefKind.MapScene, ("tbwordmapscenedata.json", "ID") },
        { QuestRefKind.Scene, ("tbgamescenedata.json", "ID") },
    };

    static readonly Dictionary<QuestRefKind, List<QuestRefOption>> JsonCache = new();

    public static IReadOnlyList<QuestRefOption> Options(QuestRefKind kind)
    {
        QuestConfig config = QuestConfigProvider.Config;
        return kind switch
        {
            QuestRefKind.Quest => FromConfig(config.QuestDict, data => data.Remark),
            QuestRefKind.Obj => FromConfig(config.ObjDict, data => data.Remark),
            QuestRefKind.Cond => FromConfig(config.CondDict, data => data.Remark),
            _ => FromJson(kind),
        };
    }

    /// <summary>重新导入配置、或 Luban JSON 重导之后清一次。</summary>
    public static void ClearCache() => JsonCache.Clear();

    static List<QuestRefOption> FromConfig<T>(IReadOnlyDictionary<long, T> source, Func<T, string> remark)
    {
        List<QuestRefOption> result = None();
        if (source == null) return result;

        foreach (KeyValuePair<long, T> pair in source) result.Add(new QuestRefOption(pair.Key, remark(pair.Value)));
        Sort(result);
        return result;
    }

    static List<QuestRefOption> FromJson(QuestRefKind kind)
    {
        if (JsonCache.TryGetValue(kind, out List<QuestRefOption> cached)) return cached;

        List<QuestRefOption> result = None();
        JsonCache[kind] = result;
        if (!JsonSources.TryGetValue(kind, out (string File, string Id) source)) return result;

        string path = Path.Combine(Directory.GetCurrentDirectory(), JsonDirectory, source.File);
        if (!File.Exists(path)) return result;

        try
        {
            foreach (JToken row in JArray.Parse(File.ReadAllText(path)))
            {
                long id = row.Value<long?>(source.Id) ?? 0;
                if (id > 0) result.Add(new QuestRefOption(id, row.Value<string>("Remark")));
            }
            Sort(result);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Quest] 读取下拉数据失败：{source.File}\n{exception.Message}");
        }
        return result;
    }

    // 0 一律排在最前：可选字段用它表示「不限 / 无」
    static List<QuestRefOption> None() => new() { new QuestRefOption(0, "无 / 不限") };

    static void Sort(List<QuestRefOption> options)
        => options.Sort((left, right) => left.Id.CompareTo(right.Id));
}
