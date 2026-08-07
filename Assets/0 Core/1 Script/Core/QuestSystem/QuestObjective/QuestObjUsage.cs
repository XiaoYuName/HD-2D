namespace XFramework
{
    /// <summary>各任务目标类型的正确写法，配置报错时打给策划看。</summary>
    public static class QuestObjUsage
    {
        public const string DayPassed = "DayPassed:天数";
        public const string Dialog = "Dialog:对话ID";
        public const string CompleteQuest = "CompleteQuest:任务ID";
        public const string HoldItem = "HoldItem:道具ID[:数量]";
        public const string NpcProp = "NpcProp:NPC ID:数值[:属性类型]";
        public const string CompleteGame = "CompleteGame:小游戏ID:局数[:结果 1胜/2负]";
        public const string DialogNpc = "DialogNpc:NPC ID[:次数]";
        public const string DialogNpcWithItem = "DialogNpcWithItem:NPC ID:道具ID[:次数]";
        public const string GiveGift = "GiveGift:NPC ID:礼物ID[:次数]（礼物ID 填 0 表示任意）";
        public const string BuyItem = "BuyItem:道具ID[:件数]（道具ID 填 0 表示任意）";
    }
}
