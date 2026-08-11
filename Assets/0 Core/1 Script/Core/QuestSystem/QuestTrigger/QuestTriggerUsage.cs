using System.Collections.Generic;

namespace XFramework
{
    /// <summary>各领取触发类型的正确写法，配置报错时打给策划看。</summary>
    public static class QuestTriggerUsage
    {
        public static readonly IReadOnlyDictionary<QuestTriggerType, QuestUsage> Map =
            new Dictionary<QuestTriggerType, QuestUsage>
            {
                { QuestTriggerType.None, new QuestUsage(0, "不配触发，只看领取条件") },
                { QuestTriggerType.Auto, new QuestUsage(0, "Auto") },
                { QuestTriggerType.EnterZone, new QuestUsage(1, "EnterZone:大场景ID:小场景ID（小场景可省略 = 只认进大场景）") },
                { QuestTriggerType.ExitZone, new QuestUsage(1, "ExitZone:大场景ID:小场景ID（小场景可省略 = 只认出大场景）") },
                { QuestTriggerType.EnterZoneStay, new QuestUsage(3, "EnterZoneStay:大场景ID:小场景ID:停留秒数（小场景写 0 = 整个大场景累计）") },
                { QuestTriggerType.ClickNpc, new QuestUsage(1, "ClickNpc:NPC ID") },
                { QuestTriggerType.DialogNpc, new QuestUsage(1, "DialogNpc:NPC ID") },
                { QuestTriggerType.MiniGameEnd, new QuestUsage(1, "MiniGameEnd:小游戏类型") },
                { QuestTriggerType.MiniGameResult, new QuestUsage(2, "MiniGameResult:小游戏类型:结果（Win / Lose）") },
                { QuestTriggerType.RandomChance, new QuestUsage(1, "RandomChance:千分比") },
                { QuestTriggerType.PlotEnd, new QuestUsage(1, "PlotEnd:剧情ID") },
            };
    }
}
