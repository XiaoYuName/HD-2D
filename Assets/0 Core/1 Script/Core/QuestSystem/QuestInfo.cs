using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace XFramework
{
    /// <summary>
    /// 一个已领取任务的运行时实例，同时也是存档单位 —— 目标对象直接存进去（多态由序列化器的
    /// <c>TypeNameHandling.Auto</c> 处理），读档后按配置重新 Init 一遍。
    /// </summary>
    public class QuestInfo
    {
        public long ID;
        public QuestState State = QuestState.InProgress;
        /// <summary>领取时的游戏天数，做限时任务/排序用。</summary>
        public int AcceptDay;
        /// <summary>超额目标是否达成，在进入 ReadyToComplete 那一刻定格。</summary>
        public bool ExceedAchieved;

        public List<QuestObjInfoBase> Objectives = new();
        public List<QuestObjInfoBase> ExtraObjectives = new();

        /// <summary>任一目标进度变化，由 <see cref="QuestManager"/> 注入。</summary>
        [JsonIgnore] public Action<QuestInfo> OnProgressChanged;

        [JsonIgnore] public bool IsActive { get; set; }

        [JsonIgnore] public QuestData Config => QuestManager.Instance.GetQuestData(ID);

        public QuestInfo() { }

        public QuestInfo(long questId, List<QuestObjInfoBase> objectives, List<QuestObjInfoBase> extraObjectives)
        {
            ID = questId;
            AcceptDay = GameDataManager.Instance.PlayerData.Day;
            Bind(objectives, extraObjectives);
        }

        /// <summary>读档后重新挂上目标列表与回调。</summary>
        public void Bind(List<QuestObjInfoBase> objectives, List<QuestObjInfoBase> extraObjectives)
        {
            Objectives = objectives;
            ExtraObjectives = extraObjectives;

            foreach (QuestObjInfoBase obj in Objectives) obj.OnChanged = RelayProgress;
            foreach (QuestObjInfoBase obj in ExtraObjectives) obj.OnChanged = RelayProgress;
        }

        void RelayProgress(QuestObjInfoBase _) => OnProgressChanged?.Invoke(this);

        #region 订阅生命周期

        /// <summary>
        /// 让目标挂上各自的事件。只在任务进行中期间保持。
        /// 订阅时目标会按当前状态先算一次，所以可能在这个循环里就完成并触发 <see cref="Deactivate"/> ——
        /// 用 <see cref="IsActive"/> 兜住，别再往下订。
        /// </summary>
        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;

            foreach (QuestObjInfoBase obj in Objectives)
            {
                if (!IsActive) return;
                obj.SubsEvents();
            }
            foreach (QuestObjInfoBase obj in ExtraObjectives)
            {
                if (!IsActive) return;
                obj.SubsEvents();
            }
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            foreach (QuestObjInfoBase obj in Objectives) obj.UnsubsEvents();
            foreach (QuestObjInfoBase obj in ExtraObjectives) obj.UnsubsEvents();
        }

        #endregion

        /// <summary>没有配目标的任务视为「一领就能交」。</summary>
        [JsonIgnore] public bool IsAllObjComplete => IsAllComplete(Objectives);

        [JsonIgnore] public bool IsAllExtraComplete => ExtraObjectives.Count > 0 && IsAllComplete(ExtraObjectives);

        static bool IsAllComplete(List<QuestObjInfoBase> list)
        {
            foreach (QuestObjInfoBase obj in list)
            {
                if (!obj.HasComplete()) return false;
            }
            return true;
        }

        public override string ToString() => $"Quest {ID} [{State}] {string.Join(" | ", Objectives)}";
    }
}
