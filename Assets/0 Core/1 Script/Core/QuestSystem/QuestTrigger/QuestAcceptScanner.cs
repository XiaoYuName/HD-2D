using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 「还没领的任务，现在能不能领」这件事的全部逻辑。从 QuestManager 抽出来，
    /// 它只管把事件转发进来，不关心怎么匹配触发、怎么建索引。
    /// </summary>
    public class QuestAcceptScanner
    {
        /// <summary>没有事件驱动、靠重扫触发的类型（读档、条件可能变化时都会重扫一遍）。</summary>
        static readonly QuestTriggerType[] PassiveTypes =
        {
            QuestTriggerType.None,
            QuestTriggerType.Auto,
            QuestTriggerType.RandomChance,
        };

        readonly IReadOnlyDictionary<long, QuestData> questDataDict;
        readonly Func<long, bool> isAccepted;
        readonly Action<long> accept;

        /// <summary>触发类型 → 用到它的任务ID。事件来了只遍历相关任务，不扫全表。</summary>
        readonly Dictionary<QuestTriggerType, List<long>> triggerIndex = new();

        public QuestAcceptScanner(IReadOnlyDictionary<long, QuestData> questDataDict,
            Func<long, bool> isAccepted, Action<long> accept)
        {
            this.questDataDict = questDataDict;
            this.isAccepted = isAccepted;
            this.accept = accept;

            foreach (QuestData data in questDataDict.Values)
            {
                foreach (IQuestTrigger trigger in data.Triggers) Index(trigger.Type, data.Id);
            }
        }

        void Index(QuestTriggerType type, long questId)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> list)) triggerIndex[type] = list = new List<long>();
            if (!list.Contains(questId)) list.Add(questId);
        }

        /// <summary>某个事件到了，看有没有任务因此可以领。</summary>
        public void ByTrigger(QuestTriggerType type, long id, int param)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> questIds)) return;

            for (int i = 0; i < questIds.Count; i++)
            {
                long questId = questIds[i];
                if (isAccepted(questId)) continue;

                foreach (IQuestTrigger trigger in questDataDict[questId].Triggers)
                {
                    if (trigger.Type != type || !trigger.IsHit(id, param)) continue;
                    if (CondManager.Instance.IsMatched(questDataDict[questId].AcceptCond)) accept(questId);
                    break;
                }
            }
        }

        /// <summary>重扫被动触发（未配触发 / Auto / 随机）。读档、天数变化、对话结束、任务交付后调。</summary>
        public void Passive()
        {
            foreach (QuestTriggerType type in PassiveTypes) ByTrigger(type, 0, 0);
        }

        /// <summary>当前场景有没有还没领、且靠停留触发的任务。没有就别起停留协程。</summary>
        public bool HasPendingStayTrigger(long sceneId)
        {
            if (!triggerIndex.TryGetValue(QuestTriggerType.EnterZoneStay, out List<long> questIds)) return false;

            foreach (long questId in questIds)
            {
                if (isAccepted(questId)) continue;

                foreach (IQuestTrigger trigger in questDataDict[questId].Triggers)
                {
                    if (trigger is EnterZoneStayQuestTrigger stay && stay.SceneId == sceneId) return true;
                }
            }
            return false;
        }
    }
}
