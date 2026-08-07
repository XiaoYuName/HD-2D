using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestTriggerType"/> 分发到各自的触发实现。
    /// 加一种触发 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="IQuestTrigger"/> 实现 + 在这里注册
    /// + <see cref="QuestTriggerUsage"/> 登记写法。
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
            QuestTriggerType type = config.GetHead(QuestTriggerType.None);
            if (!Registry.TryGetValue(type, out Func<IQuestTrigger> creator))
            {
                throw new KeyNotFoundException($"[Quest] 任务 {config.QuestId} 的触发 \"{config.Raw}\" 类型 {type} 还没注册实现");
            }

            IQuestTrigger trigger = creator();
            trigger.Init(config);
            return trigger;
        }

        /// <summary>没配触发的任务补一条 None，让它走「只看领取条件」的重扫路径。</summary>
        public static IQuestTrigger[] CreateList(QuestArgs[] configs)
        {
            if (configs.Length == 0) return new IQuestTrigger[] { new PassiveQuestTrigger(QuestTriggerType.None) };

            IQuestTrigger[] result = new IQuestTrigger[configs.Length];
            for (int i = 0; i < configs.Length; i++) result[i] = Create(configs[i]);
            return result;
        }

        public static void Register(QuestTriggerType type, Func<IQuestTrigger> creator) => Registry[type] = creator;
    }
}
