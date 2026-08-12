using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 一条任务奖励。一种奖励一个实现，参数直接是实现类自己的序列化字段（Odin 多态），
    /// 在任务／目标／类别的奖励列表里选类型即可 —— 和任务目标、领取触发同一套结构。
    /// </summary>
    public interface IQuestReward
    {
        QuestRewardType Type { get; }

        /// <summary>
        /// 校验参数指向的东西真的存在，走 <see cref="QuestConfigValidator"/>。
        /// 由 <see cref="QuestManager"/> 初始化阶段统一调，所以发放和显示时不用再做空判。
        /// </summary>
        bool Validate(string owner);

        void Reward();

        /// <summary>给 UI 用的多语言描述，文案在 Data/QuestSystem/QuestRewardDataLoc.csv。</summary>
        string GetDesc();

        /// <summary>给面板画图标用，见 <see cref="QuestRewardView"/>。</summary>
        QuestRewardView GetView();
    }

    /// <summary>奖励列表的发放与校验，任务、目标、类别三处共用。</summary>
    public static class QuestRewards
    {
        public static void Grant(IReadOnlyList<IQuestReward> rewards)
        {
            for (int i = 0; i < rewards.Count; i++) rewards[i].Reward();
        }

        public static void Validate(IReadOnlyList<IQuestReward> rewards, string owner)
        {
            for (int i = 0; i < rewards.Count; i++) rewards[i].Validate(owner);
        }
    }
}
