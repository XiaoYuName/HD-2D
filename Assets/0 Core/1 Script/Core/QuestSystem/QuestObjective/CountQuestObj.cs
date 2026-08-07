using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 靠事件累计的目标（打完几局、送几次礼、买几件……）：只增不减，**次数进存档**
    /// （世界状态里查不到「你打过几局」，只能自己记）。
    /// 子类在 <see cref="QuestObjInfoBase.SubsEvents"/> 里订自己那种事件，命中时调 <see cref="Advance"/>。
    /// </summary>
    public abstract class CountQuestObj : QuestObjInfoBase
    {
        /// <summary>已累计的次数，进存档。</summary>
        public int count;

        [JsonIgnore] public override bool IsComplete => count >= need;

        [JsonIgnore] public override string ProgressText => $"{Mathf.Min(count, need)}/{need}";

        protected void Advance(int delta)
        {
            if (delta <= 0 || count >= need) return;

            count = Mathf.Min(count + delta, need);
            NotifyChanged();
        }
    }
}
