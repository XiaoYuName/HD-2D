using System;
using Newtonsoft.Json;

namespace XFramework
{
    /// <summary>
    /// 一条目标的**动态部分**，随任务一起进存档：只存进度，别的一概不存。
    ///
    /// 事件订阅也在这一侧 —— 回调要作用于「我这一份进度」，天生是每个实例一份。
    /// 具体实现是各 <see cref="QuestObjData"/> 子类里的嵌套 <c>Info</c>，那里能直接读到静态参数。
    ///
    /// 静态数据不靠 id 反查 —— 是主目标还是超额，看它挂在 <see cref="QuestObjStateInfo.target"/> 还是
    /// <see cref="QuestObjStateInfo.extra"/> 上就已经说明了，所以 <see cref="Data"/> 由
    /// <see cref="QuestObjStateInfo.Set"/> 现场注入（新建和读档都会走到），存档里不留任何归属字段。
    /// </summary>
    public abstract class QuestObjInfoBase
    {
        /// <summary>本目标的静态数据，整局共用一份。</summary>
        [JsonIgnore] public QuestObjData Data { get; set; }

        /// <summary>进度变化回调，由 <see cref="QuestObjStateInfo"/> 注入。</summary>
        [JsonIgnore] public Action<QuestObjInfoBase> OnChanged;

        [JsonIgnore] public abstract bool IsComplete { get; }

        /// <summary>进度片段（"1/2"）—— 只有动态侧算得出来。完整描述见 <see cref="GetDesc"/>。</summary>
        [JsonIgnore] public abstract string ProgressText { get; }

        /// <summary>
        /// 订自己关心的事件，回调里推进／重算本实例的进度。
        /// 挂/摘由 <see cref="QuestObjStateInfo.Activate"/> / <see cref="QuestObjStateInfo.Deactivate"/> 成对驱动，
        /// 那边已经拦了重复调用，这里不再自查。
        /// </summary>
        public abstract void SubsEvents();

        public abstract void UnsubsEvents();

        public void NotifyChanged() => OnChanged?.Invoke(this);

        /// <summary>给 UI 用的目标描述，如「持有 鱼 ×2（1/2）」。</summary>
        public string GetDesc() => Data.GetDesc(ProgressText);

        public override string ToString() => $"{GetType().Name} [{ProgressText}]";
    }
}
