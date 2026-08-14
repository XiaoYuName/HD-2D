using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;
using XFramework;

/// <summary>
/// 任务编辑器自己的撤销栈。不走 Unity 的 Undo：配置是 Odin 序列化的字典 ＋ 多态实例，
/// 进不了 SerializedProperty，Unity 的快照拿不到真正的数据。
///
/// 一步 = 整份配置的一张 Odin 二进制快照。表再大也就是几百条记录，够用且不会出现「只回滚了一半」。
/// </summary>
class QuestEditorUndo
{
    const int MaxSteps = 100;

    class Snapshot
    {
        public byte[] Bytes;
        public List<Object> Refs;
    }

    /// <summary>Odin 认这个类型，字段顺序要和 <see cref="QuestConfig.EditorReplace"/> 对上。</summary>
    class Payload
    {
        public Dictionary<long, QuestData> Quests;
        public Dictionary<long, QuestObjConfigData> Objs;
        public Dictionary<long, QuestCategory> Categories;
        public Dictionary<long, QuestCondData> Conds;
        public Dictionary<QuestRewardType, QuestRewardPresentation> RewardViews;
    }

    readonly List<Snapshot> undoSteps = new();
    readonly List<Snapshot> redoSteps = new();

    /// <summary>上一次改动之后的状态。<see cref="Record"/> 压的就是它 —— 它一定早于这次改动。</summary>
    Snapshot baseline;

    /// <summary>连续改同一个字段合成一步，否则打字每敲一个字符都成一步。</summary>
    object coalesceKey;

    public int UndoCount => undoSteps.Count;
    public int RedoCount => redoSteps.Count;

    /// <summary>换配置资产 / 重新导入之后调，历史作废。</summary>
    public void Reset(QuestConfig config)
    {
        undoSteps.Clear();
        redoSteps.Clear();
        coalesceKey = null;
        baseline = Capture(config);
    }

    /// <summary>
    /// 改动之后调。<paramref name="key"/> 相同的连续改动并成一步，传 null 表示这次单独算一步。
    /// </summary>
    public void Record(QuestConfig config, object key)
    {
        if (config == null) return;

        if (baseline == null)
        {
            baseline = Capture(config);
            return;
        }

        // 新的一步：把改动前的状态压进去。同一串连续改动只压第一次
        if (key == null || !Equals(key, coalesceKey))
        {
            undoSteps.Add(baseline);
            if (undoSteps.Count > MaxSteps) undoSteps.RemoveAt(0);
            redoSteps.Clear();
        }

        coalesceKey = key;
        baseline = Capture(config);
    }

    public bool Undo(QuestConfig config) => Step(config, undoSteps, redoSteps);

    public bool Redo(QuestConfig config) => Step(config, redoSteps, undoSteps);

    bool Step(QuestConfig config, List<Snapshot> from, List<Snapshot> to)
    {
        if (config == null || from.Count == 0) return false;

        to.Add(Capture(config));
        Snapshot target = from[^1];
        from.RemoveAt(from.Count - 1);

        Restore(config, target);
        baseline = Capture(config);
        coalesceKey = null;
        return true;
    }

    static Snapshot Capture(QuestConfig config)
    {
        if (config == null) return null;

        Payload payload = new()
        {
            Quests = config.EditorQuests,
            Objs = config.EditorObjs,
            Categories = config.EditorCategories,
            Conds = config.EditorConds,
            RewardViews = config.EditorRewardViews,
        };

        byte[] bytes = SerializationUtility.SerializeValue(payload, DataFormat.Binary, out List<Object> refs);
        return new Snapshot { Bytes = bytes, Refs = refs };
    }

    static void Restore(QuestConfig config, Snapshot snapshot)
    {
        Payload payload = SerializationUtility.DeserializeValue<Payload>(
            snapshot.Bytes, DataFormat.Binary, snapshot.Refs);

        config.EditorReplace(
            payload.Quests ?? new Dictionary<long, QuestData>(),
            payload.Objs ?? new Dictionary<long, QuestObjConfigData>(),
            payload.Categories ?? new Dictionary<long, QuestCategory>(),
            payload.Conds ?? new Dictionary<long, QuestCondData>(),
            payload.RewardViews ?? new Dictionary<QuestRewardType, QuestRewardPresentation>());
    }
}
