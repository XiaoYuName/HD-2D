using System;

namespace XFramework
{
    /// <summary>
    /// 一条任务目标的运行时实例。基类只规定四件事：是否达成、怎么显示进度、要不要订阅事件、进存档存什么。
    /// <b>进度用什么形式记由子类自己决定</b>：
    /// 单个计数的直接继承 <see cref="StateQuestObj"/> / <see cref="CountQuestObj"/>；
    /// 需要多个计数的（比如同时要 1 条鱼和 2 颗糖合并成一条目标）自己开字段，
    /// 重写 <see cref="IsComplete"/> 和 <see cref="ProgressText"/> 即可，基类不挡路。
    /// </summary>
    public abstract class QuestObjInfoBase
    {
        public QuestArgs Config;

        /// <summary>进度变化回调，由 <see cref="QuestInfo"/> 注入。</summary>
        public Action<QuestObjInfoBase> OnChanged;

        public abstract bool IsComplete { get; }

        /// <summary>给 UI 用的进度文本，多计数目标可以拼成 "鱼 1/1 糖 0/2"。</summary>
        public virtual string ProgressText => IsComplete ? "1/1" : "0/1";

        public void Init(QuestArgs config)
        {
            Config = config;
            OnInit();
        }

        /// <summary>读参数、校验写法。</summary>
        protected virtual void OnInit() { }

        /// <summary>由世界状态算出的目标在这里重算；事件累计型不用管。</summary>
        public virtual void Refresh() { }

        /// <summary>事件累计型在这里订阅自己那一种事件，挂/摘由 <see cref="QuestInfo"/> 统一驱动。</summary>
        public virtual void SubsEvents() { }

        public virtual void UnsubsEvents() { }

        /// <summary>要进存档的进度；由世界状态实时算出的目标返回 null（读档后重算即可）。</summary>
        public virtual int[] SaveState() => null;

        public virtual void LoadState(int[] state) { }

        protected void NotifyChanged() => OnChanged?.Invoke(this);

        public override string ToString() => $"{Config} [{ProgressText}]";
    }
}
