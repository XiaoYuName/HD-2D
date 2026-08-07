namespace XFramework
{
    /// <summary>
    /// 一个任务的运行时配置：表里那几列字符串在启动时解析成对象，之后事件里不再碰字符串，
    /// 也不再持有 Luban 的 <c>QuestDataConfig</c> —— 需要的几个标量在构造时就抄过来了。
    ///
    /// 目标不同于触发和奖励：它每次领取都要一份带自己进度的新实例，所以这里只能留着解析好的
    /// 参数按需造，<see cref="objArgs"/> 也因此不对外暴露，外面一律走 <see cref="CreateObjectives()"/>。
    /// </summary>
    public class QuestData
    {
        // 解析完就不再变，直接用 readonly 字段，不额外包一层只读属性
        public readonly long Id;
        public readonly string Remark;
        public readonly string NameKey;
        public readonly string DescKey;

        /// <summary>领取条件ID，走 <see cref="CondManager"/>。</summary>
        public readonly long AcceptCond;

        /// <summary>领取触发，多条之间是 OR。</summary>
        public readonly IQuestTrigger[] Triggers;

        public readonly IQuestReward[] Rewards;
        public readonly IQuestReward[] ExtraRewards;

        readonly QuestArgs[] objArgs;
        readonly QuestArgs[] extraObjArgs;

        public string Name => LanguageManager.Instance.GetLocalizedString(LocTableSet.QuestSystem, NameKey);
        public string Desc => LanguageManager.Instance.GetLocalizedString(LocTableSet.QuestSystem, DescKey);

        public QuestData(QuestDataConfig config)
        {
            Id = config.Id;
            Remark = config.Remark;
            NameKey = config.NameKey;
            DescKey = config.DescKey;
            AcceptCond = config.AcceptCond;

            QuestArgs[] triggerArgs = QuestArgs.SplitList(config.AcceptTrigger, Id);
            QuestConfigValidator.ValidateUsage(triggerArgs, QuestTriggerUsage.Map);
            Triggers = QuestTriggerFactory.CreateList(triggerArgs);

            objArgs = QuestArgs.SplitList(config.QuestObjData, Id);
            extraObjArgs = QuestArgs.SplitList(config.ExtraQuestObjData, Id);
            QuestConfigValidator.ValidateUsage(objArgs, QuestObjUsage.Map);
            QuestConfigValidator.ValidateUsage(extraObjArgs, QuestObjUsage.Map);

            QuestArgs[] rewardArgs = QuestArgs.SplitList(config.Reward, Id);
            QuestArgs[] extraRewardArgs = QuestArgs.SplitList(config.ExtraReward, Id);
            QuestConfigValidator.ValidateUsage(rewardArgs, QuestRewardUsage.Map);
            QuestConfigValidator.ValidateUsage(extraRewardArgs, QuestRewardUsage.Map);
            Rewards = QuestRewardFactory.CreateList(rewardArgs);
            ExtraRewards = QuestRewardFactory.CreateList(extraRewardArgs);
        }

        public QuestObjInfoBase[] CreateObjectives() => QuestObjFactory.CreateList(objArgs);
        public QuestObjInfoBase[] CreateExtraObjectives() => QuestObjFactory.CreateList(extraObjArgs);

        /// <summary>读档用：类型和配置对得上的沿用存档实例（保住累计进度），对不上按配置新建。</summary>
        public QuestObjInfoBase[] CreateObjectives(QuestObjInfoBase[] saved)
            => QuestObjFactory.CreateList(objArgs, saved);

        public QuestObjInfoBase[] CreateExtraObjectives(QuestObjInfoBase[] saved)
            => QuestObjFactory.CreateList(extraObjArgs, saved);

        /// <summary>查参数指向的道具/角色/任务在不在表里。目标现造一份临时的来查，查完丢掉。</summary>
        public void Validate()
        {
            QuestConfigValidator.ValidateObjectives(CreateObjectives(), objArgs);
            QuestConfigValidator.ValidateObjectives(CreateExtraObjectives(), extraObjArgs);
            QuestConfigValidator.ValidateRewards(Rewards);
            QuestConfigValidator.ValidateRewards(ExtraRewards);
        }

        public override string ToString() => $"QuestData {Id} ({Remark})";
    }
}
