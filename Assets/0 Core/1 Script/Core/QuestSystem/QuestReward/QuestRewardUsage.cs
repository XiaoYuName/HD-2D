namespace XFramework
{
    /// <summary>
    /// 各奖励类型的正确写法，配置报错时打给策划看。
    /// 改了写法记得同步这里 —— 报错信息里显示的就是这些字符串。
    /// </summary>
    public static class QuestRewardUsage
    {
        public const string Item = "Item:道具ID[:数量]";
        public const string Coin = "Coin:数值";
        public const string GameCoin = "GameCoin:数值";
        public const string Goodwill = "Goodwill:NPC ID:数值";
    }
}
