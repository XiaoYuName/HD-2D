using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 一个任务的配置，直接存在 <see cref="QuestConfig"/> 的任务字典里，读出来就是运行时用的这一份。
    /// 目标下沉到了 <see cref="QuestObjConfigData"/>（每条目标自带奖励和超额），这里只按 ID 引用。
    /// </summary>
    [Serializable]
    public class QuestData
    {
        [SerializeField, QuestLabel("备注")] string remark;
        [SerializeField, QuestLabel("名称")] LocKeyRef name = new();
        [SerializeField, QuestLabel("描述")] LocKeyRef desc = new();
        [SerializeField, QuestLabel("图标")] AssetReferenceSprite icon;

        [SerializeField, QuestLabel("领取触发（任一满足，留空=只看接受条件）")]
        List<IQuestTrigger> triggers = new();

        [SerializeField, QuestLabel("接受条件"), QuestRef(QuestRefKind.Cond)] long acceptCond;

        [SerializeField, QuestLabel("任务目标（按顺序）"), QuestRef(QuestRefKind.Obj)]
        List<long> objIds = new();

        [SerializeField, QuestLabel("任务奖励")] List<IQuestReward> rewards = new();

        [SerializeField, QuestLabel("目标按顺序完成")] bool objInOrder;

        /// <summary>没配触发时用的那一条：重扫时无条件命中，能不能领全看接受条件。</summary>
        static readonly IQuestTrigger[] PassiveOnly = { new AutoQuestTrigger() };

        public long Id { get; private set; }
        public string Remark => remark;
        public AssetReferenceSprite Icon => icon;

        /// <summary>领取条件ID，走 <see cref="CondManager"/>。</summary>
        public long AcceptCond => acceptCond;

        public IReadOnlyList<IQuestReward> Rewards => rewards;

        /// <summary>领取触发，多条之间是 OR。</summary>
        public IReadOnlyList<IQuestTrigger> Triggers => triggers.Count > 0 ? triggers : PassiveOnly;

        /// <summary>本任务的目标，按配置顺序，由 <see cref="Init"/> 解引用。</summary>
        public QuestObjConfigData[] Objs { get; private set; } = Array.Empty<QuestObjConfigData>();

        /// <summary>
        /// 目标要按 <see cref="Objs"/> 的顺序逐条完成。
        /// 顺序模式下同时只有当前那一条在监听事件，否则 <c>HoldItem</c> 这类状态型目标会因为
        /// 玩家身上早就有道具而提前达成、直接发奖，顺序就形同虚设。
        /// </summary>
        public bool ObjInOrder => objInOrder;

        public string Name => name.Get();
        public string Desc => desc.Get();

        /// <summary>名称的 Key 配了没有。校验要用它而不是 <see cref="Name"/> —— 编辑器里没跑多语言，取出来一定是空的。</summary>
        public bool HasName => name.IsValid();

        public void Init(long id, QuestConfig database)
        {
            Id = id;

            Objs = new QuestObjConfigData[objIds.Count];
            for (int i = 0; i < Objs.Length; i++)
            {
                if (!database.Objs.TryGetValue(objIds[i], out QuestObjConfigData obj))
                    throw new KeyNotFoundException($"[Quest] 任务 {Id} 引用的目标 {objIds[i]} 不在任务目标表里");

                Objs[i] = obj;
            }
        }

        public void Validate()
        {
            string owner = $"任务 {Id}";
            foreach (IQuestTrigger trigger in Triggers) trigger.Validate(owner);
            QuestRewards.Validate(rewards, owner);
        }

        public override string ToString() => $"QuestData {Id} ({remark})";
    }
}
