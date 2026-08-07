using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 靠事件累计的目标（打完几局、送几次礼、买几件……）：只增不减，进度进存档。
    /// 子类在 <see cref="QuestObjInfoBase.SubsEvents"/> 里订自己那一种事件，命中时调 <see cref="Advance"/>。
    /// </summary>
    public abstract class CountQuestObj : QuestObjInfoBase
    {
        /// <summary>达成所需的次数。</summary>
        protected int need = 1;

        int count;

        public override bool IsComplete => count >= need;

        public override string ProgressText => $"{count}/{need}";

        public override int[] SaveState() => new[] { count };

        public override void LoadState(int[] state)
        {
            if (state is { Length: > 0 }) count = Mathf.Clamp(state[0], 0, need);
        }

        protected void Advance(int delta = 1)
        {
            if (delta <= 0 || count >= need) return;

            count = Mathf.Min(count + delta, need);
            NotifyChanged();
        }
    }
}
