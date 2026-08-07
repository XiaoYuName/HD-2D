namespace XFramework
{
    /// <summary>各领取触发类型的正确写法，配置报错时打给策划看。</summary>
    public static class QuestTriggerUsage
    {
        public const string EnterZone = "EnterZone:场景ID";
        public const string ExitZone = "ExitZone:场景ID";
        public const string EnterZoneStay = "EnterZoneStay:场景ID:停留秒数";
        public const string ClickNpc = "ClickNpc:NPC ID";
        public const string DialogNpc = "DialogNpc:NPC ID";
        public const string MiniGameEnd = "MiniGameEnd:小游戏ID";
        public const string MiniGameResult = "MiniGameResult:小游戏ID:结果（1 胜 / 2 负）";
        public const string RandomChance = "RandomChance:千分比";
        public const string PlotEnd = "PlotEnd:剧情ID";
    }
}
