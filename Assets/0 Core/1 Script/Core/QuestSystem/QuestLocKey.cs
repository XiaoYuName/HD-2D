namespace XFramework
{
    /// <summary>
    /// 任务系统的多语言 Key，统一走 <see cref="LocTableSet.QuestSystem"/> 表。
    /// 文案在 <c>Assets/0 Core/1 Script/Data/QuestSystem/</c> 下的 CSV 里，别在业务代码里写魔法字符串。
    /// </summary>
    public static class QuestLocKey
    {
        /// <summary>奖励描述，文案在 QuestRewardDataLoc.csv。</summary>
        public static class Reward
        {
            const string Prefix = "QuestReward/";

            public const string Item = Prefix + nameof(Item);
            public const string Coin = Prefix + nameof(Coin);
            public const string GameCoin = Prefix + nameof(GameCoin);
            public const string Goodwill = Prefix + nameof(Goodwill);
        }
    }
}
