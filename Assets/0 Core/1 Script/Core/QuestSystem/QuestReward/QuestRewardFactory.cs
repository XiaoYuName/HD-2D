using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestRewardType"/> 分发到各自的奖励实现。
    /// 加一种奖励 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="IQuestReward"/> 实现 + 在这里注册
    /// + <see cref="QuestRewardUsage"/> 登记写法。
    /// </summary>
    public static class QuestRewardFactory
    {
        static readonly Dictionary<QuestRewardType, Func<IQuestReward>> Registry = new()
        {
            { QuestRewardType.Item, () => new ItemQuestReward() },
            { QuestRewardType.Coin, () => new CoinQuestReward() },
            { QuestRewardType.GameCoin, () => new GameCoinQuestReward() },
            { QuestRewardType.Affection, () => new AffectionQuestReward() },
        };

        public static IQuestReward Create(QuestArgs config)
        {
            QuestRewardType type = config.GetHead(QuestRewardType.None);
            if (!Registry.TryGetValue(type, out Func<IQuestReward> creator))
            {
                throw new KeyNotFoundException($"[Quest] {config.Owner} 的奖励 \"{config.Raw}\" 类型 {type} 还没注册实现");
            }

            IQuestReward reward = creator();
            reward.Init(config);
            return reward;
        }

        public static IQuestReward[] CreateList(QuestArgs[] configs)
        {
            IQuestReward[] result = new IQuestReward[configs.Length];
            for (int i = 0; i < configs.Length; i++) result[i] = Create(configs[i]);
            return result;
        }

        public static void Grant(IQuestReward[] rewards)
        {
            foreach (IQuestReward reward in rewards) reward.Reward();
        }

        public static void Register(QuestRewardType type, Func<IQuestReward> creator) => Registry[type] = creator;
    }
}
