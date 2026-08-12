using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using Sirenix.OdinInspector;

namespace XFramework
{
    public enum QuestConfigSourceMode
    {
        [InspectorName("仅 Luban")] LubanOnly,
        [InspectorName("SO 优先，缺失时回退 Luban")] PreferScriptableObject,
        [InspectorName("仅 ScriptableObject")] ScriptableObjectOnly,
    }

    [Serializable]
    public class QuestTriggerSpec
    {
        [InspectorName("触发类型")] public QuestTriggerType type = QuestTriggerType.Auto;
        [InspectorName("大场景 ID")] public long mapSceneId;
        [InspectorName("小场景 ID")] public long sceneId;
        [InspectorName("停留秒数")] public int staySeconds = 1;
        [InspectorName("NPC ID")] public long npcId;
        [InspectorName("小游戏类型")] public MiniGameType gameType;
        [InspectorName("小游戏结果")] public MiniGameResult gameResult;
        [InspectorName("触发概率（千分比）"), Range(0, 1000)] public int permille;
        [InspectorName("剧情 ID")] public long plotId;
    }

    [Serializable]
    public class QuestObjectiveSpec
    {
        [InspectorName("目标类型")] public QuestObjType type = QuestObjType.DayPassed;
        [InspectorName("次数 / 数量")] public int count = 1;
        [InspectorName("目标数值")] public int value = 1;
        [InspectorName("对话 ID")] public long dialogueId;
        [InspectorName("任务 ID")] public long questId;
        [InspectorName("道具 ID")] public long itemId;
        [InspectorName("NPC ID")] public long npcId;
        [InspectorName("角色属性")] public CharacterPropType characterPropType = CharacterPropType.Goodwill;
        [InspectorName("小游戏类型")] public MiniGameType gameType;
        [InspectorName("小游戏结果")] public MiniGameResult gameResult;
    }

    [Serializable]
    public class QuestRewardSpec
    {
        [InspectorName("奖励类型")] public QuestRewardType type = QuestRewardType.Coin;
        [InspectorName("道具 ID")] public long itemId;
        [InspectorName("NPC ID")] public long npcId;
        [InspectorName("数量 / 数值")] public int amount = 1;
    }

    [Serializable]
    public class QuestCategoryDefinition
    {
        [InspectorName("启用 SO 配置")] public bool enabled = true;
        [InspectorName("类别 ID")] public long id;
        [InspectorName("备注")] public string remark;
        [InspectorName("名称")] public LocalizedString name = new();
        [InspectorName("描述")] public LocalizedString desc = new();
        [InspectorName("图标")] public AssetReferenceSprite icon;
        [InspectorName("任务 ID 列表")] public List<long> questIds = new();
        [InspectorName("类别奖励")] public List<QuestRewardSpec> rewards = new();
    }

    [Serializable]
    public class QuestDefinition
    {
        [InspectorName("启用 SO 配置")] public bool enabled = true;
        [InspectorName("任务 ID")] public long id;
        [InspectorName("备注")] public string remark;
        [InspectorName("领取触发（任一满足）")] public List<QuestTriggerSpec> triggers = new();
        [InspectorName("接受条件 ID")] public long acceptConditionId;
        [InspectorName("名称")] public LocalizedString name = new();
        [InspectorName("描述")] public LocalizedString desc = new();
        [InspectorName("图标")] public AssetReferenceSprite icon;
        [InspectorName("目标 ID 列表")] public List<long> objectiveIds = new();
        [InspectorName("任务奖励")] public List<QuestRewardSpec> rewards = new();
        [InspectorName("目标按顺序完成")] public bool objectivesInOrder;
    }

    [Serializable]
    public class QuestObjectiveDefinition
    {
        [InspectorName("启用 SO 配置")] public bool enabled = true;
        [InspectorName("目标 ID")] public long id;
        [InspectorName("备注")] public string remark;
        [InspectorName("目标描述")] public LocalizedString desc = new();
        [InspectorName("主目标")] public QuestObjectiveSpec objective = new();
        [InspectorName("目标奖励")] public List<QuestRewardSpec> rewards = new();
        [InspectorName("启用超额目标")] public bool hasExtra;
        [InspectorName("超额描述")] public LocalizedString extraDesc = new();
        [InspectorName("超额目标")] public QuestObjectiveSpec extraObjective = new();
        [InspectorName("超额奖励")] public List<QuestRewardSpec> extraRewards = new();
    }

    [Serializable]
    public class QuestItemRequirement
    {
        [InspectorName("道具 ID")] public long itemId;
        [InspectorName("持有数量")] public int count = 1;
    }

    [Serializable]
    public class QuestCharacterRequirement
    {
        [InspectorName("NPC ID")] public long npcId;
        [InspectorName("角色属性")] public CharacterPropType propType = CharacterPropType.Goodwill;
        [InspectorName("需要数值")] public int value = 1;
    }

    [Serializable]
    public class QuestConditionDefinition
    {
        [InspectorName("启用 SO 配置")] public bool enabled = true;
        [InspectorName("条件 ID")] public long id;
        [InspectorName("备注")] public string remark;
        [InspectorName("道具持有要求")] public List<QuestItemRequirement> items = new();
        [InspectorName("角色属性要求")] public List<QuestCharacterRequirement> characterProps = new();
        [InspectorName("前置剧情 ID")] public List<long> plotPrerequisites = new();
        [InspectorName("前置对话 ID")] public List<long> dialoguePrerequisites = new();
        [InspectorName("最早天数")] public int day;
        [InspectorName("时间段")] public ShowRuleTimeType timeSlot = ShowRuleTimeType.All;
        [InspectorName("前置任务 ID")] public List<long> questPrerequisites = new();
        [InspectorName("满足时分支 ID")] public List<long> satisfyBranches = new();
        [InspectorName("不满足时分支 ID")] public List<long> notSatisfyBranches = new();
        [InspectorName("游戏分数")] public int gameScore;
    }

    [Serializable]
    public class QuestRewardPresentation
    {
        [InspectorName("启用 SO 配置")] public bool enabled = true;
        [InspectorName("奖励类型")] public QuestRewardType type;
        [InspectorName("备注")] public string remark;
        [InspectorName("显示名称")] public LocalizedString name = new();
        [InspectorName("显示图标")] public AssetReferenceSprite icon;
    }

#if UNITY_EDITOR
    [Serializable]
    public class QuestEditorColumnLayout
    {
        public string columnId;
        public float width;
    }

    [Serializable]
    public class QuestEditorTableLayout
    {
        public string tableId;
        public List<QuestEditorColumnLayout> columns = new();
    }
#endif

    [CreateAssetMenu(fileName = nameof(QuestDatabaseData), menuName = EditorMenuSet.Quest + nameof(QuestDatabaseData))]
    public class QuestDatabaseData : SerializedScriptableObject
    {
        public const string ResourcePath = "Quest/QuestDatabase";

        [SerializeField, InspectorName("数据源模式")] QuestConfigSourceMode sourceMode;
        [SerializeField, InspectorName("任务类别")] List<QuestCategoryDefinition> categories = new();
        [SerializeField, InspectorName("任务")] List<QuestDefinition> quests = new();
        [SerializeField, InspectorName("任务目标")] List<QuestObjectiveDefinition> objectives = new();
        [SerializeField, InspectorName("接受条件")] List<QuestConditionDefinition> conditions = new();
        [SerializeField, InspectorName("奖励显示")] List<QuestRewardPresentation> rewardPresentations = new();
#if UNITY_EDITOR
        [SerializeField, HideInInspector] List<QuestEditorTableLayout> editorTableLayouts = new();
#endif

        public QuestConfigSourceMode SourceMode
        {
            get => sourceMode;
            set => sourceMode = value;
        }

        public List<QuestCategoryDefinition> Categories => categories;
        public List<QuestDefinition> Quests => quests;
        public List<QuestObjectiveDefinition> Objectives => objectives;
        public List<QuestConditionDefinition> Conditions => conditions;
        public List<QuestRewardPresentation> RewardPresentations => rewardPresentations;
#if UNITY_EDITOR
        public List<QuestEditorTableLayout> EditorTableLayouts => editorTableLayouts;
#endif

        public bool GetCondition(long id, out QuestConditionDefinition result)
        {
            result = conditions.Find(data => data.enabled && data.id == id);
            return result != null;
        }

        public bool GetRewardPresentation(QuestRewardType type, out QuestRewardPresentation result)
        {
            result = rewardPresentations.Find(data => data.enabled && data.type == type);
            return result != null;
        }
    }

    public static class QuestDatabaseProvider
    {
        static QuestDatabaseData database;

        public static QuestDatabaseData Database
            => database != null ? database : database = Resources.Load<QuestDatabaseData>(QuestDatabaseData.ResourcePath);

        public static bool UseScriptableObject
            => Database != null && Database.SourceMode != QuestConfigSourceMode.LubanOnly;

        public static bool UseLuban
            => Database == null || Database.SourceMode != QuestConfigSourceMode.ScriptableObjectOnly;

        public static void ClearCache() => database = null;
    }
}
