using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>限定只能拖任务配置资产的 AssetReference。</summary>
    [Serializable]
    public class AssetRefQuestConfig : AssetReferenceT<QuestConfig>
    {
        public AssetRefQuestConfig(string guid) : base(guid) { }
    }

    /// <summary>
    /// 任务配置的唯一来源。Odin 序列化，所以字典和多态实例都能直接进资产 ——
    /// 这里存的就是运行时用的那批对象（<see cref="QuestData"/>、<see cref="QuestObjConfigData"/>…），
    /// 没有「配置类 → 运行时类」的抄写层，目标/触发/奖励各自的参数就长在各自的实现类上。
    ///
    /// 里面的对象整局共用且只读，任何随玩家变化的东西都在 <see cref="QuestInfo"/> / <see cref="QuestObjStateInfo"/> 一侧。
    /// </summary>
    [CreateAssetMenu(fileName = nameof(QuestConfig), menuName = EditorMenuSet.Quest + nameof(QuestConfig))]
    public class QuestConfig : SerializedScriptableObject
    {
        public const string AssetFolder = "Assets/AddressableAssets/Remote/Configs/Quest";
        public const string AssetPath = AssetFolder + "/QuestConfig.asset";

        // 五个字典的 Key 就是 ID。平时走 Tools/任务编辑器，这里的 Odin 抽屉是不开窗口时的直接编辑入口
        [SerializeField, LabelText("任务"), DictionaryDrawerSettings(KeyLabel = "任务 ID", ValueLabel = "任务")]
        Dictionary<long, QuestData> questDict = new();

        [SerializeField, LabelText("任务目标"), DictionaryDrawerSettings(KeyLabel = "目标 ID", ValueLabel = "目标")]
        Dictionary<long, QuestObjConfigData> objDict = new();

        [SerializeField, LabelText("任务类别"), DictionaryDrawerSettings(KeyLabel = "类别 ID", ValueLabel = "类别")]
        Dictionary<long, QuestCategory> categoryDict = new();

        [SerializeField, LabelText("接受条件"), DictionaryDrawerSettings(KeyLabel = "条件 ID", ValueLabel = "条件")]
        Dictionary<long, QuestCondData> condDict = new();

        [SerializeField, LabelText("奖励显示"), DictionaryDrawerSettings(KeyLabel = "奖励类型", ValueLabel = "显示")]
        Dictionary<QuestRewardType, QuestRewardPresentation> rewardViewDict = new();

        public IReadOnlyDictionary<long, QuestData> QuestDict => questDict;
        public IReadOnlyDictionary<long, QuestObjConfigData> ObjDict => objDict;
        public IReadOnlyDictionary<long, QuestCategory> CategoryDict => categoryDict;
        public IReadOnlyDictionary<long, QuestCondData> CondDict => condDict;
        public IReadOnlyDictionary<QuestRewardType, QuestRewardPresentation> RewardViewDict => rewardViewDict;

        /// <summary>
        /// 把任务里的目标 ID 解成对象引用 —— 配置里是 ID 图，运行时要的是对象图，这一步只能在加载后做。
        /// 其余记录的 Id 是序列化字段，编辑器保存时已由 <see cref="EditorSyncIds"/> 写好。
        /// <see cref="QuestManager"/> 启动时调一次；不做「只跑一次」的缓存，编辑器里改完配置重新进游戏要能生效。
        /// </summary>
        public void Init()
        {
            foreach (KeyValuePair<long, QuestData> pair in questDict) pair.Value.Init(pair.Key, this);
        }

        public bool GetCond(long id, out QuestCondData result) => condDict.TryGetValue(id, out result);

        public bool GetRewardView(QuestRewardType type, out QuestRewardPresentation result)
            => rewardViewDict.TryGetValue(type, out result);

#if UNITY_EDITOR
        [Button("校验配置"), PropertyOrder(-1)]
        void CheckConfig() => QuestConfigValidator.CheckStructure(this);

        void OnValidate() => EditorSyncIds();

        /// <summary>
        /// 把字典 Key 回填成各条记录的 Id。Key 仍是唯一权威，字段只是它的快照，所以任何改动之后都要重跑一遍
        /// —— Inspector 走 <c>OnValidate</c>，任务编辑器走 <c>MarkDirty</c>，导入走 <see cref="EditorReplace"/>。
        /// </summary>
        public void EditorSyncIds()
        {
            bool changed = false;

            foreach (KeyValuePair<long, QuestObjConfigData> pair in objDict)
            {
                if (pair.Value == null || pair.Value.Id == pair.Key) continue;
                pair.Value.EditorSetId(pair.Key);
                changed = true;
            }

            foreach (KeyValuePair<long, QuestCategory> pair in categoryDict)
            {
                if (pair.Value == null || pair.Value.Id == pair.Key) continue;
                pair.Value.EditorSetId(pair.Key);
                changed = true;
            }

            foreach (KeyValuePair<long, QuestCondData> pair in condDict)
            {
                if (pair.Value == null || pair.Value.Id == pair.Key) continue;
                pair.Value.EditorSetId(pair.Key);
                changed = true;
            }

            foreach (KeyValuePair<QuestRewardType, QuestRewardPresentation> pair in rewardViewDict)
            {
                if (pair.Value == null || pair.Value.Type == pair.Key) continue;
                pair.Value.EditorSetType(pair.Key);
                changed = true;
            }

            // 打包出去的是序列化值，回填完必须落盘，否则运行时读到的还是 0
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }

        // 任务编辑器窗口要增删记录，所以编辑器下给出可写视图；运行时一律走上面那几个只读属性
        public Dictionary<long, QuestData> EditorQuests => questDict;
        public Dictionary<long, QuestObjConfigData> EditorObjs => objDict;
        public Dictionary<long, QuestCategory> EditorCategories => categoryDict;
        public Dictionary<long, QuestCondData> EditorConds => condDict;
        public Dictionary<QuestRewardType, QuestRewardPresentation> EditorRewardViews => rewardViewDict;

        /// <summary>一次性导入用（<c>QuestLubanMigration</c>），正常编辑走 Inspector。</summary>
        public void EditorReplace(
            Dictionary<long, QuestData> newQuests,
            Dictionary<long, QuestObjConfigData> newObjs,
            Dictionary<long, QuestCategory> newCategories,
            Dictionary<long, QuestCondData> newConds,
            Dictionary<QuestRewardType, QuestRewardPresentation> newRewardViews)
        {
            questDict = newQuests;
            objDict = newObjs;
            categoryDict = newCategories;
            condDict = newConds;
            rewardViewDict = newRewardViews;

            EditorSyncIds();
        }
#endif
    }
}
