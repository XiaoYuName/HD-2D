namespace XFramework
{
    /// <summary>
    /// 一个任务类别的配置（QuestCategoryData.xlsx 一行），面板左侧一个 Tab 就是一个类别。
    /// 类别下的任务全部完成时发一次类别奖励，发过的记在存档里。
    /// 一个任务允许挂在多个类别下，所以这里只存任务ID，不反向独占。
    /// </summary>
    public class QuestCategory
    {
        public readonly long Id;
        public readonly string Remark;
        public readonly string NameKey;
        public readonly string DescKey;
        public readonly string IconKey;
        public readonly long[] QuestIds;
        public readonly IQuestReward[] Rewards;

        public string Name => QuestLocText.Get(NameKey);
        public string Desc => QuestLocText.Get(DescKey);

        public QuestCategory(QuestCategoryData config)
        {
            Id = config.Id;
            Remark = config.Remark;
            NameKey = config.NameKey;
            DescKey = config.DescKey;
            IconKey = config.IconKey;

            QuestIds = new long[config.QuestId.Count];
            for (int i = 0; i < QuestIds.Length; i++) QuestIds[i] = config.QuestId[i];

            Rewards = QuestData.ParseRewards(config.Reward, $"任务类别 {Id}");
        }

        public void Validate() => QuestConfigValidator.ValidateRewards(Rewards);

        public override string ToString() => $"QuestCategory {Id} ({Remark})";
    }
}
