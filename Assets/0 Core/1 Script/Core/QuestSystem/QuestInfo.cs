using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 一个已领取任务的运行时实例。不直接进存档（含多态目标对象），
    /// 存档只落 <see cref="QuestEntrySaveData"/> 里的状态与各目标自报的进度。
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
        public Action<QuestInfo> OnProgressChanged;

        public bool IsActive { get; set; }

        public QuestData Config => QuestManager.Instance.GetQuestData(ID);

        public QuestInfo() { }

        public QuestInfo(long questId, List<QuestObjInfoBase> objectives, List<QuestObjInfoBase> extraObjectives)
        {
            ID = questId;
            AcceptDay = GameDataManager.Instance.PlayerData.Day;
            Objectives = objectives;
            ExtraObjectives = extraObjectives;

            foreach (QuestObjInfoBase obj in Objectives) obj.OnChanged = RelayProgress;
            foreach (QuestObjInfoBase obj in ExtraObjectives) obj.OnChanged = RelayProgress;
        }

        void RelayProgress(QuestObjInfoBase _) => OnProgressChanged?.Invoke(this);

        #region 订阅生命周期

        /// <summary>让累计型目标挂上各自的事件。只在任务进行中期间保持。</summary>
        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            foreach (QuestObjInfoBase obj in Objectives) obj.SubsEvents();
            foreach (QuestObjInfoBase obj in ExtraObjectives) obj.SubsEvents();
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
        public bool IsAllObjComplete => IsAllComplete(Objectives);
        public bool IsAllExtraComplete => ExtraObjectives.Count > 0 && IsAllComplete(ExtraObjectives);

        static bool IsAllComplete(List<QuestObjInfoBase> list)
        {
            foreach (QuestObjInfoBase obj in list)
            {
                if (!obj.HasComplete()) return false;
            }
            return true;
        }

        /// <summary>重算所有目标；事件累计型是空实现。有变化的目标会自己回调 OnProgressChanged。</summary>
        public void Refresh()
        {
            foreach (QuestObjInfoBase obj in Objectives) obj.Refresh();
            foreach (QuestObjInfoBase obj in ExtraObjectives) obj.Refresh();
        }

        public override string ToString() => $"Quest {ID} [{State}] {string.Join(" | ", Objectives)}";
    }
}
