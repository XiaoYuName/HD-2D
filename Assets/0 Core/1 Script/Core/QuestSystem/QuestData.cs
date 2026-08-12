using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace XFramework
{
    /// <summary>
    /// 一个任务的统一运行时配置。既可从旧 Luban 行解析，也可从强类型 ScriptableObject 记录构建；
    /// 构建完成后，任务逻辑不再关心配置来源。
    ///
    /// 目标下沉到了 <see cref="QuestObjConfigData"/>（每条目标自带奖励和超额），这里只持引用。
    /// </summary>
    public class QuestData
    {
        // 解析完就不再变，直接用 readonly 字段，不额外包一层只读属性
        public readonly long Id;
        public readonly string Remark;
        public readonly string NameKey;
        public readonly string DescKey;
        public readonly string IconKey;
        public readonly AssetReferenceSprite Icon;

        readonly LocalizedString localizedName;
        readonly LocalizedString localizedDesc;

        /// <summary>领取条件ID，走 <see cref="CondManager"/>。</summary>
        public readonly long AcceptCond;

        /// <summary>领取触发，多条之间是 OR。</summary>
        public readonly IQuestTrigger[] Triggers;

        /// <summary>本任务的目标，按配表顺序。</summary>
        public readonly QuestObjConfigData[] Objs;

        /// <summary>
        /// 目标要按 <see cref="Objs"/> 的顺序逐条完成。
        /// 顺序模式下同时只有当前那一条在监听事件，否则 <c>HoldItem</c> 这类状态型目标会因为
        /// 玩家身上早就有道具而提前达成、直接发奖，顺序就形同虚设。
        /// </summary>
        public readonly bool ObjInOrder;

        /// <summary>任务整体完成时发的奖励，目标各自的奖励在 <see cref="QuestObjConfigData.Rewards"/>。</summary>
        public readonly IQuestReward[] Rewards;

        public string Name => localizedName != null && !localizedName.IsEmpty
            ? localizedName.GetLocalizedString()
            : QuestLocText.Get(NameKey);
        public string Desc => localizedDesc != null && !localizedDesc.IsEmpty
            ? localizedDesc.GetLocalizedString()
            : QuestLocText.Get(DescKey);

        public QuestData(QuestDataConfig config, IReadOnlyDictionary<long, QuestObjConfigData> objDict)
        {
            Id = config.Id;
            Remark = config.Remark;
            NameKey = config.NameKey;
            DescKey = config.DescKey;
            IconKey = config.IconKey;
            AcceptCond = config.AcceptCond;
            ObjInOrder = config.ObjInOrder;

            string owner = $"任务 {Id}";

            QuestArgs[] triggerArgs = QuestArgs.SplitList(config.QuestTrigger, owner);
            QuestConfigValidator.ValidateUsage(triggerArgs, QuestTriggerUsage.Map);
            Triggers = QuestTriggerFactory.CreateList(triggerArgs);

            Objs = new QuestObjConfigData[config.QuestObjData.Count];
            for (int i = 0; i < Objs.Length; i++)
            {
                long objId = config.QuestObjData[i];
                if (!objDict.TryGetValue(objId, out QuestObjConfigData obj))
                {
                    throw new KeyNotFoundException(
                        $"[Quest] 任务 {Id} 引用的目标 {objId} 在任务目标表里不存在");
                }
                Objs[i] = obj;
            }

            Rewards = ParseRewards(config.Reward, owner);
        }

        public QuestData(QuestDefinition config, IReadOnlyDictionary<long, QuestObjConfigData> objDict)
        {
            Id = config.id;
            Remark = config.remark;
            localizedName = config.name;
            localizedDesc = config.desc;
            Icon = config.icon;
            AcceptCond = config.acceptConditionId;
            ObjInOrder = config.objectivesInOrder;

            string owner = $"任务 {Id}";
            Triggers = QuestTriggerFactory.CreateList(config.triggers, owner);
            Objs = new QuestObjConfigData[config.objectiveIds.Count];
            for (int i = 0; i < Objs.Length; i++)
            {
                long objId = config.objectiveIds[i];
                if (!objDict.TryGetValue(objId, out QuestObjConfigData obj))
                    throw new KeyNotFoundException($"[Quest] 任务 {Id} 引用的目标 {objId} 不存在");
                Objs[i] = obj;
            }
            Rewards = QuestRewardFactory.CreateList(config.rewards, owner);
        }

        public void Validate() => QuestConfigValidator.ValidateRewards(Rewards);

        /// <summary>奖励列的解析在任务、目标、类别三处都一样，收在这里。</summary>
        public static IQuestReward[] ParseRewards(string text, string owner)
        {
            QuestArgs[] args = QuestArgs.SplitList(text, owner);
            QuestConfigValidator.ValidateUsage(args, QuestRewardUsage.Map);
            return QuestRewardFactory.CreateList(args);
        }

        public override string ToString() => $"QuestData {Id} ({Remark})";
    }
}
