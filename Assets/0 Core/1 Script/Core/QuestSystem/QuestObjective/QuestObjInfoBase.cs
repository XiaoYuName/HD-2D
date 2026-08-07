using System;
using Newtonsoft.Json;

namespace XFramework
{
    public abstract class QuestObjInfoBase
    {
        /// <summary>所属任务，报错定位用。</summary>
        public long QuestId;

        /// <summary>进度变化回调，由 <see cref="QuestInfo"/> 注入。</summary>
        [JsonIgnore] public Action<QuestObjInfoBase> OnChanged;

        [JsonIgnore] public abstract bool IsComplete { get; }

        /// <summary>给 UI 用的进度文本，多计数目标可以拼成 "鱼 1/1 糖 0/2"。</summary>
        [JsonIgnore] public virtual string ProgressText => IsComplete ? "1/1" : "0/1";

        /// <summary>读参数并校验写法（段数、是不是数字），创建时和读档后各调一次。</summary>
        public abstract void Init(QuestArgs config);

        /// <summary>
        /// 校验参数指向的东西真的存在，走 <see cref="QuestConfigValidator"/>。
        /// 只在 <see cref="QuestManager"/> 初始化阶段对每条配置查一次，所以运行时不再做空判兜底。
        /// </summary>
        public abstract bool Validate(QuestArgs config);

        /// <summary>
        /// 订阅自己关心的那一种事件，并**按当前状态先算一次**（有的目标领取时就已经达成了）。
        /// 挂/摘由 <see cref="QuestInfo.Activate"/> / <see cref="QuestInfo.Deactivate"/> 统一驱动。
        /// </summary>
        public abstract void SubsEvents();

        public abstract void UnsubsEvents();

        protected void NotifyChanged() => OnChanged?.Invoke(this);

        public override string ToString() => $"{GetType().Name} [{ProgressText}]";
    }
}
