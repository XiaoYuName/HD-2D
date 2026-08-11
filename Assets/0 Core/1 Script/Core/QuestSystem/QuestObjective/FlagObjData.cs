namespace XFramework
{
    /// <summary>
    /// **成没成**型目标（到第几天、做过某段对话、某任务完成了）：判定就是一个 bool，没有「几分之几」。
    ///
    /// 不存状态，<see cref="IsMet"/> 每次现问世界。这一点很关键：领任务之前就已经聊过那段对话、
    /// 前置任务早就做完了，订阅后那一次判定照样能捞到；要是只靠事件写一个 bool，事件早发过了就永远做不完。
    /// 实例侧见 <see cref="FlagObjInfo"/>。
    /// </summary>
    public abstract class FlagObjData : QuestObjData
    {
        /// <summary>当前满足了没有。</summary>
        public abstract bool IsMet();
    }
}
