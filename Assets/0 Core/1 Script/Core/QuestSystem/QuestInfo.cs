using System;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一个已领取任务的运行时实例，同时也是存档单位 —— 目标对象直接存进去（多态由序列化器的
    /// <c>TypeNameHandling.Auto</c> 处理），读档后按配置重新 Init 一遍。
    /// 状态迁移只从 <see cref="SwitchState"/> 走，达成判定只从 <see cref="CheckReadyCompleteState"/> 走。
    /// </summary>
    public class QuestInfo
    {
        // 存档字段：Newtonsoft 只序列化 public 成员，所以这几个是 public；
        // 下面的只读属性标了 JsonIgnore，否则会被写出一份读不回来的重复数据。
        public long id;
        public QuestState state = QuestState.InProgress;
        public bool exceedAchieved;
        public QuestObjInfoBase[] objectives = Array.Empty<QuestObjInfoBase>();
        public QuestObjInfoBase[] extraObjectives = Array.Empty<QuestObjInfoBase>();

        [JsonIgnore] public long Id => id;
        [JsonIgnore] public QuestState State => state;

        /// <summary>超额目标是否达成，在进入 ReadyToComplete 那一刻定格。</summary>
        [JsonIgnore] public bool ExceedAchieved => exceedAchieved;

        [JsonIgnore] public QuestObjInfoBase[] Objectives => objectives;
        [JsonIgnore] public QuestObjInfoBase[] ExtraObjectives => extraObjectives;

        /// <summary>任一目标进度变化，由 <see cref="QuestManager"/> 注入。</summary>
        [JsonIgnore] public Action<QuestInfo> OnProgressChanged;

        /// <summary>状态迁移，由 <see cref="QuestManager"/> 注入并转成对外事件。</summary>
        [JsonIgnore] public Action<QuestInfo> OnStateChanged;

        [JsonIgnore] public bool IsActive { get; private set; }

        [JsonIgnore] public QuestData Data => QuestManager.Instance.GetQuestData(id);

        [JsonIgnore] public string Name => Data.Name;
        [JsonIgnore] public string Desc => Data.Desc;

        /// <summary>状态文案，给 UI 直接用。</summary>
        [JsonIgnore] public string StateText => QuestLocText.Get(QuestLocKey.Common.Of(state));

        public static QuestInfo Create(long questId, QuestObjInfoBase[] objectives, QuestObjInfoBase[] extraObjectives)
        {
            QuestInfo info = new() { id = questId };
            info.Bind(objectives, extraObjectives);
            return info;
        }

        /// <summary>挂上目标列表与进度回调，新建和读档都走这里。</summary>
        public void Bind(QuestObjInfoBase[] newObjectives, QuestObjInfoBase[] newExtraObjectives)
        {
            objectives = newObjectives;
            extraObjectives = newExtraObjectives;

            foreach (QuestObjInfoBase obj in objectives) obj.OnChanged = RelayProgress;
            foreach (QuestObjInfoBase obj in extraObjectives) obj.OnChanged = RelayProgress;
        }

        void RelayProgress(QuestObjInfoBase _)
        {
            OnProgressChanged?.Invoke(this);
            CheckReadyCompleteState();
        }

        #region 订阅生命周期

        /// <summary>
        /// 让目标挂上各自的事件。订阅本身不会推进度（状态型目标的进度是现算的），
        /// 所以订完统一判一次达成 —— 领任务时就已经满足的情况在这里被捞到。
        /// </summary>
        public void Activate()
        {
            if (IsActive)
            {
                Debug.LogError($"[Quest] 任务 {id} 已经在监听事件了，重复 Activate");
                return;
            }
            IsActive = true;

            foreach (QuestObjInfoBase obj in objectives) obj.SubsEvents();
            foreach (QuestObjInfoBase obj in extraObjectives) obj.SubsEvents();

            CheckReadyCompleteState();
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            foreach (QuestObjInfoBase obj in objectives) obj.UnsubsEvents();
            foreach (QuestObjInfoBase obj in extraObjectives) obj.UnsubsEvents();
        }

        #endregion

        #region 状态

        /// <summary>目标全达成就进待交付，超额目标在这一刻定格。</summary>
        public void CheckReadyCompleteState()
        {
            if (state != QuestState.InProgress || !IsAllObjComplete) return;

            exceedAchieved = IsAllExtraComplete;
            SwitchState(QuestState.ReadyToComplete);
        }

        /// <summary>唯一的状态迁移入口：离开进行中就退订事件，已完成的任务不会再吃事件。</summary>
        public void SwitchState(QuestState targetState)
        {
            if (state == targetState)
            {
                Debug.LogError($"[Quest] 任务 {id} 重复切到 {targetState}");
                return;
            }

            state = targetState;
            if (state != QuestState.InProgress) Deactivate();
            OnStateChanged?.Invoke(this);
        }

        #endregion

        /// <summary>没有配目标的任务视为「一领就能交」。</summary>
        [JsonIgnore] public bool IsAllObjComplete => IsAllComplete(objectives);

        [JsonIgnore] public bool IsAllExtraComplete => extraObjectives.Length > 0 && IsAllComplete(extraObjectives);

        static bool IsAllComplete(QuestObjInfoBase[] list)
        {
            foreach (QuestObjInfoBase obj in list)
            {
                if (!obj.IsComplete) return false;
            }
            return true;
        }

        public override string ToString()
            => $"Quest {id} [{state}] {string.Join<QuestObjInfoBase>(" | ", objectives)}";
    }
}
