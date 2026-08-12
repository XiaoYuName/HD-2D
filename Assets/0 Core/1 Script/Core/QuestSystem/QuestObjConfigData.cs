using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一条任务目标的配置：一条主目标 ＋ 可选的超额目标 ＋ 各自的奖励。
    /// 同一条可以被多个任务引用，所以整局只有这一份，里面的 <see cref="QuestObjData"/> 也跟着共用。
    ///
    /// 运行时进度不在这里 —— 每次接受生成一份 <see cref="QuestObjStateInfo"/>。
    /// </summary>
    [Serializable]
    public class QuestObjConfigData
    {
        [SerializeField, QuestLabel("备注")] string remark;

        [SerializeField, QuestLabel("主目标")] QuestObjData target;
        [SerializeField, QuestLabel("目标奖励")] List<IQuestReward> rewards = new();

        [SerializeField, QuestLabel("超额目标（不配=没有超额）")] QuestObjData extra;
        [SerializeField, QuestLabel("超额奖励")] List<IQuestReward> extraRewards = new();

        public long Id { get; private set; }
        public string Remark => remark;

        /// <summary>主目标的静态数据。</summary>
        public QuestObjData TargetData => target;

        /// <summary>超额目标的静态数据，没配超额时为 null。</summary>
        public QuestObjData ExtraData => extra;

        /// <summary>目标达成时发的奖励。</summary>
        public IReadOnlyList<IQuestReward> Rewards => rewards;

        /// <summary>超额条件同时达成时追加的奖励。</summary>
        public IReadOnlyList<IQuestReward> ExtraRewards => extraRewards;

        /// <summary>有没有配超额条件。</summary>
        public bool HasExtra => extra != null;

        public void Init(long id) => Id = id;

        public QuestObjInfoBase CreateTarget() => target.CreateInfo();

        /// <summary>没配超额条件时返回 null。</summary>
        public QuestObjInfoBase CreateExtra() => extra?.CreateInfo();

        /// <summary>读档用：存档实例和当前配置对得上就沿用（保住累计次数），对不上按配置新建。</summary>
        public QuestObjInfoBase CreateTarget(QuestObjInfoBase saved) => Reuse(CreateTarget(), saved);

        public QuestObjInfoBase CreateExtra(QuestObjInfoBase saved)
            => extra == null ? null : Reuse(CreateExtra(), saved);

        static QuestObjInfoBase Reuse(QuestObjInfoBase fresh, QuestObjInfoBase saved)
            => saved == null || saved.GetType() != fresh.GetType() ? fresh : saved;

        /// <summary>查参数指向的道具/角色/任务在不在表里。</summary>
        public void Validate()
        {
            string owner = $"目标 {Id}";

            target.Validate(owner);
            QuestConfigValidator.CheckDesc(target, owner);
            QuestRewards.Validate(rewards, owner);

            if (extra == null) return;

            extra.Validate(owner + " 的超额目标");
            QuestConfigValidator.CheckDesc(extra, owner + " 的超额目标");
            QuestRewards.Validate(extraRewards, owner + " 的超额目标");
        }

        public override string ToString() => $"QuestObjConfigData {Id} ({remark})";
    }
}
