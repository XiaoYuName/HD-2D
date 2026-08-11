namespace XFramework
{
    /// <summary>
    /// 任务目标配置表的一行（QuestObjConfig.xlsx）：一条主目标 ＋ 可选的超额目标 ＋ 各自的奖励。
    /// 同一行可以被多个任务引用，所以整局只有一份，里面的 <see cref="QuestObjData"/> 也跟着共用。
    ///
    /// 运行时进度不在这里 —— 每次领取生成一份 <see cref="QuestObjStateInfo"/>。
    /// </summary>
    public class QuestObjConfigData
    {
        public readonly long Id;
        public readonly string Remark;

        /// <summary>目标达成时发的奖励。</summary>
        public readonly IQuestReward[] Rewards;

        /// <summary>超额条件同时达成时追加的奖励。</summary>
        public readonly IQuestReward[] ExtraRewards;

        /// <summary>主目标的静态数据。</summary>
        public readonly QuestObjData TargetData;

        /// <summary>超额目标的静态数据，没配超额时为 null。</summary>
        public readonly QuestObjData ExtraData;

        readonly QuestArgs objArgs;
        readonly QuestArgs extraArgs;

        /// <summary>有没有配超额条件。</summary>
        public bool HasExtra => ExtraData != null;

        public QuestObjConfigData(QuestObjConfig config)
        {
            Id = config.Id;
            Remark = config.Remark;

            string owner = $"目标 {Id}";
            objArgs = QuestArgs.Split(config.QuestObjData, owner);
            extraArgs = QuestArgs.Split(config.ExtraCompleteCond, owner);

            QuestConfigValidator.ValidateUsage(objArgs, QuestObjUsage.Map);
            QuestConfigValidator.ValidateUsage(extraArgs, QuestObjUsage.Map);

            TargetData = QuestObjFactory.Create(objArgs);
            ExtraData = extraArgs == null ? null : QuestObjFactory.Create(extraArgs);

            TargetData.DescKey = config.DescKey;
            if (ExtraData != null) ExtraData.DescKey = config.ExtraDescKey;

            Rewards = QuestData.ParseRewards(config.Reward, owner);
            ExtraRewards = QuestData.ParseRewards(config.ExtraReward, owner);
        }

        public QuestObjInfoBase CreateTarget() => TargetData.CreateInfo();

        /// <summary>没配超额条件时返回 null。</summary>
        public QuestObjInfoBase CreateExtra() => ExtraData?.CreateInfo();

        /// <summary>读档用：存档实例和当前配置对得上就沿用（保住累计次数），对不上按配置新建。</summary>
        public QuestObjInfoBase CreateTarget(QuestObjInfoBase saved) => Reuse(CreateTarget(), saved);

        public QuestObjInfoBase CreateExtra(QuestObjInfoBase saved)
            => ExtraData == null ? null : Reuse(CreateExtra(), saved);

        static QuestObjInfoBase Reuse(QuestObjInfoBase fresh, QuestObjInfoBase saved)
            => saved == null || saved.GetType() != fresh.GetType() ? fresh : saved;

        /// <summary>查参数指向的道具/角色/任务在不在表里。</summary>
        public void Validate()
        {
            TargetData.Validate(objArgs);
            ExtraData?.Validate(extraArgs);

            QuestConfigValidator.CheckDescKey(TargetData, QuestFieldName.DescKey, objArgs);
            if (ExtraData != null)
            {
                QuestConfigValidator.CheckDescKey(ExtraData, QuestFieldName.ExtraDescKey, extraArgs);
            }

            QuestConfigValidator.ValidateRewards(Rewards);
            QuestConfigValidator.ValidateRewards(ExtraRewards);
        }

        public override string ToString() => $"QuestObjConfigData {Id} ({Remark})";
    }
}
