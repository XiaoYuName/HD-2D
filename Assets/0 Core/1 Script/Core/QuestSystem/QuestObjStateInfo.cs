using System;
using Newtonsoft.Json;

namespace XFramework
{
    /// <summary>
    /// 已领取任务里的一条目标，同时也是存档单位。
    ///
    /// 达成那一刻就地结算：定格超额结果、发目标奖励、退订事件。之后 <see cref="IsComplete"/> 恒为真 ——
    /// 持有类目标把道具卖了也不会把已发的奖励收回去。
    /// </summary>
    public class QuestObjStateInfo
    {
        public long id;
        public QuestObjInfoBase target;
        /// <summary>超额目标实例，没配超额时为 null。</summary>
        public QuestObjInfoBase extra;
        public bool completed;
        public bool exceedAchieved;

        [JsonIgnore] public long Id => id;
        [JsonIgnore] public bool IsComplete => completed;
        [JsonIgnore] public bool ExceedAchieved => exceedAchieved;

        /// <summary>本目标那一行配置（奖励、主目标／超额的静态数据都在里面）。</summary>
        [JsonIgnore] public QuestObjConfigData Config => QuestManager.Instance.GetQuestObjConfigData(id);

        /// <summary>完整描述，进度已经拼在里面（「持有 鱼 ×2（1/2）」），UI 直接显示这一条。</summary>
        [JsonIgnore] public string Desc => target.GetDesc();

        /// <summary>超额条件的描述，没配超额时为空。</summary>
        [JsonIgnore] public string ExtraDesc => extra == null ? string.Empty : extra.GetDesc();

        /// <summary>本目标完成，由 <see cref="QuestInfo"/> 注入。</summary>
        [JsonIgnore] public Action<QuestObjStateInfo> OnCompleted;

        /// <summary>进度变化（还没完成），由 <see cref="QuestInfo"/> 注入。</summary>
        [JsonIgnore] public Action<QuestObjStateInfo> OnChanged;

        [JsonIgnore] bool isActive;

        public static QuestObjStateInfo Create(QuestObjConfigData config)
        {
            QuestObjStateInfo info = new() { id = config.Id };
            info.Set(config.CreateTarget(), config.CreateExtra());
            return info;
        }

        /// <summary>
        /// 挂上目标实例，新建和读档都走这里。
        /// 静态数据在这儿直接注入：是主目标还是超额，看的就是它挂在哪个字段上，
        /// 所以实例自己不用存归属，也不用每次拿配置都回头查一遍字典。
        /// </summary>
        public void Set(QuestObjInfoBase newTarget, QuestObjInfoBase newExtra)
        {
            QuestObjConfigData config = Config;

            target = newTarget;
            extra = newExtra;

            target.Data = config.TargetData;
            target.OnChanged = OnTargetChanged;

            if (extra == null) return;

            extra.Data = config.ExtraData;
            extra.OnChanged = OnExtraChanged;
        }

        #region 订阅生命周期

        /// <summary>已完成的目标不再订阅，订完立刻判一次（领取时可能就已经满足）。</summary>
        public void Activate()
        {
            if (isActive || completed) return;
            isActive = true;

            target.SubsEvents();
            extra?.SubsEvents();

            CheckComplete();
        }

        public void Deactivate()
        {
            if (!isActive) return;
            isActive = false;

            target.UnsubsEvents();
            extra?.UnsubsEvents();
        }

        #endregion

        void OnTargetChanged(QuestObjInfoBase _)
        {
            if (!CheckComplete()) OnChanged?.Invoke(this);
        }

        // 超额本身不推进主目标，只是让 UI 上的超额进度跟着动
        void OnExtraChanged(QuestObjInfoBase _) => OnChanged?.Invoke(this);

        /// <summary>达成就地结算并退订。返回是否在这次调用里完成。</summary>
        bool CheckComplete()
        {
            if (completed || !target.IsComplete) return false;

            completed = true;
            exceedAchieved = extra != null && extra.IsComplete;

            Deactivate();

            QuestObjConfigData config = Config;
            QuestRewardFactory.Grant(config.Rewards);
            if (exceedAchieved) QuestRewardFactory.Grant(config.ExtraRewards);

            OnCompleted?.Invoke(this);
            return true;
        }

        public override string ToString() => $"Obj {id} [{(completed ? "完成" : target.ProgressText)}]";
    }
}
