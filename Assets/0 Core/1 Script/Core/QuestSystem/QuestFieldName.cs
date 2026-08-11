namespace XFramework
{
    /// <summary>
    /// 配置字段名，只用于 <see cref="QuestConfigValidator"/> 的报错文案（「天数 是 0，必须为正数」）。
    /// 目标和奖励共用一份，同一个概念在两边报出来的名字才一致。
    /// </summary>
    public static class QuestFieldName
    {
        public const string Day = "天数";
        public const string DialogueId = "对话ID";
        public const string GameId = "小游戏ID";
        public const string Count = "数量";
        public const string Value = "数值";
        public const string Affection = "好感度";

        public const string DescKey = "描述文本(DescKey)";
        public const string ExtraDescKey = "超额条件描述文本(ExtraDescKey)";
    }
}
