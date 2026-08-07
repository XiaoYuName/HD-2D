namespace XFramework
{
    /// <summary>
    /// 一条任务奖励。一种 <see cref="QuestRewardType"/> 一个实现，各写各的发放逻辑，
    /// 由 <see cref="QuestRewardFactory"/> 按类型分发 —— 和任务目标同一套结构。
    /// 配置写法：<c>类型:参数:参数</c>，多条用 <c>/</c> 分隔，例 <c>Coin:100/Goodwill:10001:10</c>。
    /// </summary>
    public interface IQuestReward
    {
        /// <summary>读参数并校验写法（段数、类型），启动时调一次。</summary>
        void Init(QuestArgs config);

        /// <summary>
        /// 校验参数指向的东西真的存在，走 <see cref="QuestRewardValidator"/>。
        /// 由 <see cref="QuestManager"/> 初始化阶段统一调，所以发放和显示时不用再做空判。
        /// </summary>
        bool Validate();

        void Reward();

        /// <summary>给 UI 用的多语言描述，文案在 Data/QuestSystem/QuestRewardDataLoc.csv。</summary>
        string GetDesc();
    }
}
