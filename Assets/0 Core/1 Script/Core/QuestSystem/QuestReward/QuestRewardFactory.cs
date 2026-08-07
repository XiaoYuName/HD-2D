using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestRewardType"/> 分发到各自的奖励实现。
    /// 加一种奖励 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="IQuestReward"/> 实现 + 在这里注册，和加任务目标一样。
    /// </summary>
    public static class QuestRewardFactory
    {
        static readonly Dictionary<QuestRewardType, Func<IQuestReward>> Registry = new()
        {
            { QuestRewardType.Item, () => new ItemQuestReward() },
            { QuestRewardType.Coin, () => new CoinQuestReward() },
            { QuestRewardType.GameCoin, () => new GameCoinQuestReward() },
            { QuestRewardType.Goodwill, () => new GoodwillQuestReward() },
        };

        public static IQuestReward Create(QuestArgs config)
        {
            if (config == null) return null;

            QuestRewardType type = config.GetHead(QuestRewardType.None);
            if (!Registry.TryGetValue(type, out Func<IQuestReward> creator))
            {
                Debug.LogError($"[Quest] 任务 {config.QuestId} 的奖励类型 {type} 还没注册实现: {config}");
                return null;
            }

            IQuestReward reward = creator();
            reward.Init(config);
            return reward;
        }

        /// <summary>解析一整列。启动时调，写法有误当场报错。</summary>
        public static List<IQuestReward> CreateList(string text, long questId)
        {
            List<QuestArgs> configs = QuestArgs.SplitList(text, questId);
            List<IQuestReward> result = new(configs.Count);
            foreach (QuestArgs config in configs)
            {
                IQuestReward reward = Create(config);
                if (reward != null) result.Add(reward);
            }
            return result;
        }

        public static void Grant(List<IQuestReward> rewards)
        {
            if (rewards == null) return;
            foreach (IQuestReward reward in rewards) reward.Reward();
        }

        public static void Register(QuestRewardType type, Func<IQuestReward> creator) => Registry[type] = creator;
    }
}
