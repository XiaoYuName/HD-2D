using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 进度由世界状态算出的目标（持有道具、好感、天数……）：**不进存档**，
    /// 因为读档后订阅时会按当时的世界状态重算一遍；条件回退进度也跟着回退。
    /// 子类实现 <see cref="Subscribe"/>（订自己那种事件）和 <see cref="Evaluate"/>（当前已达成多少）。
    /// </summary>
    public abstract class StateQuestObj : QuestObjInfoBase
    {
        /// <summary>达成所需的量，布尔型目标保持 1。来自配置，不进存档。</summary>
        [JsonIgnore] protected int need = 1;

        [JsonIgnore] int current;

        [JsonIgnore] public override bool IsComplete => current >= need;

        [JsonIgnore] public override string ProgressText => $"{Mathf.Min(current, need)}/{need}";

        public sealed override void SubsEvents()
        {
            Subscribe();
            Recalc();
        }

        protected abstract void Subscribe();

        /// <summary>按当前世界状态重算，由子类的事件回调调用。</summary>
        protected void Recalc()
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
