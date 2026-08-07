namespace XFramework
{
    public enum QuestState
    {
        None,
        /// <summary>触发与条件都满足，可以领取</summary>
        Available,
        /// <summary>已领取，目标未达成</summary>
        InProgress,
        /// <summary>目标全达成，等交付领奖</summary>
        ReadyToComplete,
        /// <summary>已交付</summary>
        Completed,
    }
}
