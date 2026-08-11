using UnityEngine.Localization;

namespace XFramework
{
    /// <summary>
    /// **现在有多少**型目标（持有道具数、好感度）：要 <see cref="Need"/> 那么多，
    /// 当前量 <see cref="GetAmount"/> 每次现问世界，所以卖掉道具、好感掉了，进度跟着回退。
    /// 实例侧见 <see cref="AmountObjInfo"/>。
    /// </summary>
    public abstract class AmountObjData : QuestObjData
    {
        /// <summary>要达到的量。</summary>
        public abstract int Need { get; }

        /// <summary>当前的量。</summary>
        public abstract int GetAmount();

        protected override void SetTemplateVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.Value, Need, false);
            base.SetTemplateVars(desc);
        }
    }
}
