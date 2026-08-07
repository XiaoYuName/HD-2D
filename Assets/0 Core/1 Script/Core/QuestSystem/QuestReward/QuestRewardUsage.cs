using System.Collections.Generic;

namespace XFramework
{
    /// <summary>各奖励类型的正确写法，配置报错时打给策划看。</summary>
    public static class QuestRewardUsage
    {
        public static readonly IReadOnlyDictionary<QuestRewardType, QuestUsage> Map =
            new Dictionary<QuestRewardType, QuestUsage>
            {
                { QuestRewardType.Item, new QuestUsage(1, "Item:道具ID[:数量]") },
                { QuestRewardType.Coin, new QuestUsage(1, "Coin:数值") },
                { QuestRewardType.GameCoin, new QuestUsage(1, "GameCoin:数值") },
                { QuestRewardType.Goodwill, new QuestUsage(2, "Goodwill:NPC ID:数值") },
            };
    }
}
