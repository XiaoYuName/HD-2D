using System.Collections.Generic;

namespace XFramework
{
    /// <summary>各任务目标类型的正确写法，配置报错时打给策划看。</summary>
    public static class QuestObjUsage
    {
        public static readonly IReadOnlyDictionary<QuestObjType, QuestUsage> Map =
            new Dictionary<QuestObjType, QuestUsage>
            {
                { QuestObjType.DayPassed, new QuestUsage(1, "DayPassed:天数") },
                { QuestObjType.Dialog, new QuestUsage(1, "Dialog:对话ID") },
                { QuestObjType.CompleteQuest, new QuestUsage(1, "CompleteQuest:任务ID") },
                { QuestObjType.HoldItem, new QuestUsage(1, "HoldItem:道具ID[:数量]") },
                { QuestObjType.CharacterProp, new QuestUsage(2, "CharacterProp:NPC ID:数值[:属性类型]") },
                { QuestObjType.CompleteGame, new QuestUsage(2, "CompleteGame:小游戏ID:局数[:结果 1胜/2负]") },
                { QuestObjType.DialogNpc, new QuestUsage(1, "DialogNpc:NPC ID[:次数]") },
                { QuestObjType.DialogNpcWithItem, new QuestUsage(2, "DialogNpcWithItem:NPC ID:道具ID[:次数]") },
                { QuestObjType.GiveGift, new QuestUsage(2, "GiveGift:NPC ID:礼物ID[:次数]（礼物ID 填 0 表示任意）") },
                { QuestObjType.BuyItem, new QuestUsage(1, "BuyItem:道具ID[:件数]（道具ID 填 0 表示任意）") },
            };
    }
}
