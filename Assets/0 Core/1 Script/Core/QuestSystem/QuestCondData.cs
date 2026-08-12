using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    [Serializable]
    public class QuestItemRequirement
    {
        [SerializeField, QuestLabel("道具"), QuestRef(QuestRefKind.Item)] public long itemId;
        [SerializeField, QuestLabel("持有数量")] public int count = 1;
    }

    [Serializable]
    public class QuestCharacterRequirement
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] public long npcId;
        [SerializeField, QuestLabel("角色属性")] public CharacterPropType propType = CharacterPropType.Goodwill;
        [SerializeField, QuestLabel("需要数值")] public int value = 1;
    }

    /// <summary>
    /// 一条状态条件，各项之间是 AND。任务的领取条件和剧情的显示条件共用，判定在 <see cref="CondManager"/>。
    /// </summary>
    [Serializable]
    public class QuestCondData
    {
        [SerializeField, QuestLabel("备注")] string remark;

        [QuestLabel("道具持有要求")] public List<QuestItemRequirement> items = new();
        [QuestLabel("角色属性要求")] public List<QuestCharacterRequirement> characterProps = new();
        [QuestLabel("前置剧情")] public List<long> plotPrerequisites = new();
        [QuestLabel("前置对话"), QuestRef(QuestRefKind.Dialogue)] public List<long> dialoguePrerequisites = new();
        [QuestLabel("最早天数")] public int day;
        [QuestLabel("时间段")] public ShowRuleTimeType timeSlot = ShowRuleTimeType.All;
        [QuestLabel("前置任务"), QuestRef(QuestRefKind.Quest)] public List<long> questPrerequisites = new();
        [QuestLabel("满足时分支")] public List<long> satisfyBranches = new();
        [QuestLabel("不满足时分支")] public List<long> notSatisfyBranches = new();
        [QuestLabel("游戏分数")] public int gameScore;

        public long Id { get; private set; }
        public string Remark => remark;

        public void Init(long id) => Id = id;

        public override string ToString() => $"QuestCondData {Id} ({remark})";
    }
}
