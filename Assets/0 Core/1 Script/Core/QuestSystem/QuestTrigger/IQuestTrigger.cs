namespace XFramework
{
    /// <summary>
    /// 一条领取触发。一种触发一个实现，参数直接是实现类自己的序列化字段（Odin 多态），
    /// 在 <see cref="QuestData"/> 的触发列表里选类型即可 —— 和任务目标、任务奖励同一套结构。
    /// 触发只负责「什么时候去检查」，能不能领还要看 <see cref="QuestData.AcceptCond"/>。
    /// </summary>
    public interface IQuestTrigger
    {
        QuestTriggerType Type { get; }

        /// <summary>校验参数，启动时调一次。</summary>
        bool Validate(string owner);

        /// <summary>
        /// 这次事件命中了吗。<paramref name="id"/> 是事件主体（场景ID / NPC ID / 小游戏ID），
        /// <paramref name="param"/> 是附带值（停留秒数 / 胜负结果），没有就传 0。
        /// </summary>
        bool IsHit(long id, int param);
    }
}
