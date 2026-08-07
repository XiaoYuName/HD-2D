using System;
using Newtonsoft.Json;
using UnityEngine.Localization;

namespace XFramework
{
    public abstract class QuestObjInfoBase
    {
        /// <summary>所属任务，报错定位用。</summary>
        public long QuestId;

        /// <summary>达成所需的量，布尔型目标保持 1。来自配置，不进存档。</summary>
        [JsonIgnore] protected int need = 1;

        /// <summary>进度变化回调，由 <see cref="QuestInfo"/> 注入。</summary>
        [JsonIgnore] public Action<QuestObjInfoBase> OnChanged;

        [JsonIgnore] public abstract bool IsComplete { get; }

        /// <summary>给 UI 用的进度文本，多计数目标可以拼成 "鱼 1/1 糖 0/2"。</summary>
        [JsonIgnore] public virtual string ProgressText => IsComplete ? "1/1" : "0/1";

        /// <summary>本目标的文案 Key，见 <see cref="QuestLocKey.Obj"/>。同种目标按参数可以给不同的说法。</summary>
        [JsonIgnore] protected abstract string DescKey { get; }

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

        /// <summary>给 UI 用的目标描述，如「持有 鱼 ×2（1/2）」。</summary>
        public string GetDesc()
        {
            LocalizedString desc = new(LocTableSet.QuestSystem, DescKey);
            desc.SetVar(QuestLocVar.Value, need, false);
            desc.SetVar(QuestLocVar.Progress, ProgressText, false);
            SetDescVars(desc);
            return desc.GetLocalizedString();
        }

        /// <summary>把自己那几个占位符（道具名、角色名……）塞进文案，<c>Value</c> 和 <c>Progress</c> 基类已经填好。</summary>
        protected virtual void SetDescVars(LocalizedString desc) { }

        public override string ToString() => $"{GetType().Name} [{ProgressText}]";
    }
}
