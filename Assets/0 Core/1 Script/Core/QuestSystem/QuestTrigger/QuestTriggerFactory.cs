using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestTriggerType"/> 分发到各自的触发实现。
    /// 加一种触发 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="IQuestTrigger"/> 实现 + 在这里注册。
    /// </summary>
    public static class QuestTriggerFactory
    {
        static readonly Dictionary<QuestTriggerType, Func<IQuestTrigger>> Registry = new()
        {
            { QuestTriggerType.None, () => new PassiveQuestTrigger(QuestTriggerType.None) },
            { QuestTriggerType.Auto, () => new PassiveQuestTrigger(QuestTriggerType.Auto) },
            { QuestTriggerType.EnterZone, () => new EnterZoneQuestTrigger() },
            { QuestTriggerType.ExitZone, () => new ExitZoneQuestTrigger() },
            { QuestTriggerType.EnterZoneStay, () => new EnterZoneStayQuestTrigger() },
            { QuestTriggerType.ClickNpc, () => new ClickNpcQuestTrigger() },
            { QuestTriggerType.DialogNpc, () => new DialogNpcQuestTrigger() },
            { QuestTriggerType.MiniGameEnd, () => new MiniGameEndQuestTrigger() },
            { QuestTriggerType.MiniGameResult, () => new MiniGameResultQuestTrigger() },
            { QuestTriggerType.RandomChance, () => new RandomChanceQuestTrigger() },
            { QuestTriggerType.PlotEnd, () => new PlotEndQuestTrigger() },
        };

        public static IQuestTrigger Create(QuestArgs config)
        {
            if (config == null) return null;

            QuestTriggerType type = config.GetHead(QuestTriggerType.None);
            if (!Registry.TryGetValue(type, out Func<IQuestTrigger> creator))
            {
                Debug.LogError($"[Quest] 任务 {config.QuestId} 的领取触发类型 {type} 还没注册实现: {config}");
                return null;
            }

            IQuestTrigger trigger = creator();
            trigger.Init(config);
            return trigger;
        }

        /// <summary>解析一整列。没配触发的任务补一条 None，让它走「只看领取条件」的重扫路径。</summary>
        public static List<IQuestTrigger> CreateList(string text, long questId)
        {
            List<QuestArgs> configs = QuestArgs.SplitList(text, questId);
            List<IQuestTrigger> result = new(configs.Count);
            foreach (QuestArgs config in configs)
            {
                IQuestTrigger trigger = Create(config);
                if (trigger != null) result.Add(trigger);
            }

            if (result.Count == 0) result.Add(new PassiveQuestTrigger(QuestTriggerType.None));
            return result;
        }

        public static void Register(QuestTriggerType type, Func<IQuestTrigger> creator) => Registry[type] = creator;
    }
}
