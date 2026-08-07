using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 由世界状态实时算出的目标（持有道具、好感、天数、前置任务……）：
    /// 不订阅事件、不进存档，条件回退进度也跟着回退。子类只实现 <see cref="Evaluate"/>。
    /// </summary>
    public abstract class StateQuestObj : QuestObjInfoBase
    {
        /// <summary>达成所需的量，布尔型目标保持 1。</summary>
        protected int need = 1;

        int current;

        public override bool IsComplete => current >= need;

        public override string ProgressText => $"{Mathf.Min(current, need)}/{need}";

        public override void Refresh()
        {
            int value = Mathf.Max(0, Evaluate());
            if (value == current) return;

            current = value;
            NotifyChanged();
        }

        /// <summary>当前已达成的量；布尔型目标返回 0 或 1。</summary>
        protected abstract int Evaluate();
    }
}
