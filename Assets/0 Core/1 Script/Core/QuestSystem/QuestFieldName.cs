namespace XFramework
{
    /// <summary>
    /// 配置字段名，只用于 <see cref="QuestConfigValidator"/> 的报错文案（「天数 是 0，必须为正数」）。
    /// 目标、触发、奖励共用一份，同一个概念在几边报出来的名字才一致。
    /// </summary>
    public static class QuestFieldName
    {
        public const string Day = "天数";
        public const string DialogueId = "对话ID";
        public const string GameType = "小游戏类型";
        public const string Count = "数量";
        public const string Value = "数值";
        public const string Affection = "好感度";
        public const string Permille = "触发概率(千分比)";
    }
}
