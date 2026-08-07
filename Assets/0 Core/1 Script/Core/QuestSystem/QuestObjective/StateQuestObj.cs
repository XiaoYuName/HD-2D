using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 进度由世界状态算出的目标（持有道具、好感、天数……）：**不存任何状态**，
    /// <see cref="IsComplete"/> 每次现算，所以读档、订阅、UI 刷新都不会和世界状态脱节。
    /// 子类订自己那种变化事件，回调里 <see cref="QuestObjInfoBase.NotifyChanged"/> 一下就行。
    /// </summary>
    public abstract class StateQuestObj : QuestObjInfoBase
    {
        [JsonIgnore] public override bool IsComplete => Evaluate() >= need;

        [JsonIgnore] public override string ProgressText => $"{Mathf.Min(Evaluate(), need)}/{need}";

        /// <summary>当前已达成的量；布尔型目标返回 0 或 1。</summary>
        protected abstract int Evaluate();
    }
}
