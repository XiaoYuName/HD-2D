using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 一个已领取任务的运行时实例，同时也是存档单位 —— 目标对象直接存进去（多态由序列化器的
    /// <c>TypeNameHandling.Auto</c> 处理），读档后重新绑回配置。
    ///
    /// 目标各自结算奖励（见 <see cref="QuestObjStateInfo"/>），本类只管「全达成了没有」和状态迁移。
    /// 状态迁移只从 <see cref="SwitchState"/> 走，达成判定只从 <see cref="CheckReadyCompleteState"/> 走。
    /// </summary>
    public class QuestInfo
    {
        public long id;
        public QuestState state = QuestState.InProgress;
        public QuestObjStateInfo[] objectives = Array.Empty<QuestObjStateInfo>();

        [JsonIgnore] public long Id => id;
        [JsonIgnore] public QuestState State => state;
        [JsonIgnore] public QuestObjStateInfo[] Objectives => objectives;

        /// <summary>任一目标进度变化，由 <see cref="QuestManager"/> 注入。</summary>
        [JsonIgnore] public Action<QuestInfo> OnProgressChanged;

        /// <summary>状态迁移，由 <see cref="QuestManager"/> 注入并转成对外事件。</summary>
        [JsonIgnore] public Action<QuestInfo> OnStateChanged;

        /// <summary>
        /// 某条目标达成（它自己的奖励已经发了），由 <see cref="QuestManager"/> 注入去弹奖励提示。
        /// 带上任务本身，弹窗要显示是哪个任务的第几条目标。
        /// </summary>
        [JsonIgnore] public Action<QuestInfo, QuestObjStateInfo> OnObjectiveCompleted;

        [JsonIgnore] public bool IsActive { get; private set; }

        [JsonIgnore] public QuestData Data => QuestManager.Instance.GetQuestData(id);

        [JsonIgnore] public string Name => Data.Name;
        [JsonIgnore] public string Desc => Data.Desc;
        [JsonIgnore] public string IconKey => Data.IconKey;
        [JsonIgnore] public AssetReferenceSprite Icon => Data.Icon;

        /// <summary>状态文案，给 UI 直接用。</summary>
        [JsonIgnore] public string StateText => QuestLocText.Get(QuestLocKey.Common.Of(state));

        public static QuestInfo Create(QuestData data)
        {
            QuestObjStateInfo[] objectives = new QuestObjStateInfo[data.Objs.Length];
            for (int i = 0; i < objectives.Length; i++) objectives[i] = QuestObjStateInfo.Create(data.Objs[i]);

            QuestInfo info = new() { id = data.Id };
            info.Set(objectives);
            return info;
        }

        /// <summary>挂上目标列表与回调，新建和读档都走这里。</summary>
        public void Set(QuestObjStateInfo[] newObjectives)
        {
            objectives = newObjectives;

            foreach (QuestObjStateInfo obj in objectives)
            {
                obj.OnChanged = RelayProgress;
                obj.OnCompleted = OnObjCompleted;
            }
        }

        void RelayProgress(QuestObjStateInfo _) => OnProgressChanged?.Invoke(this);

        void OnObjCompleted(QuestObjStateInfo obj)
        {
            OnObjectiveCompleted?.Invoke(this, obj);
            OnProgressChanged?.Invoke(this);

            // 顺序任务：上一条完成了才轮到下一条监听事件。下一条可能一挂上就达成，会顺着递归连锁完成
            if (IsActive && Data.ObjInOrder) ActivateCurrent();

            CheckReadyCompleteState();
        }

        #region 订阅生命周期

        /// <summary>
        /// 让目标挂上各自的事件。订阅本身不会推进度（状态型目标的进度是现算的），
        /// 所以订完统一判一次达成 —— 领任务时就已经满足的情况在这里被捞到。
        ///
        /// 顺序任务只挂当前那一条：要是把后面的也挂上，<c>HoldItem</c> 这类状态型目标会因为
        /// 玩家身上早就有道具而提前达成并发奖，顺序就白配了。
        /// </summary>
        public void Activate()
        {
            if (IsActive)
            {
                Debug.LogError($"[Quest] 任务 {id} 已经在监听事件了，重复 Activate");
                return;
            }
            IsActive = true;

            if (Data.ObjInOrder) ActivateCurrent();
            else foreach (QuestObjStateInfo obj in objectives) obj.Activate();

            CheckReadyCompleteState();
        }

        void ActivateCurrent() => CurrentObj?.Activate();

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            foreach (QuestObjStateInfo obj in objectives) obj.Deactivate();
        }

        #endregion

        #region 状态

        /// <summary>目标全达成就进待交付。交付由 <see cref="QuestManager"/> 接着自动做掉。</summary>
        public void CheckReadyCompleteState()
        {
            if (state != QuestState.InProgress || !IsAllObjComplete) return;

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

        /// <summary>第一条还没完成的目标；全完成了返回 null。顺序任务里它就是「当前该做的那条」。</summary>
        [JsonIgnore]
        public QuestObjStateInfo CurrentObj
        {
            get
            {
                foreach (QuestObjStateInfo obj in objectives)
                {
                    if (!obj.IsComplete) return obj;
                }
                return null;
            }
        }

        /// <summary>没有配目标的任务视为「一领就能交」。</summary>
        [JsonIgnore]
        public bool IsAllObjComplete
        {
            get
            {
                foreach (QuestObjStateInfo obj in objectives)
                {
                    if (!obj.IsComplete) return false;
                }
                return true;
            }
        }

        public override string ToString()
            => $"Quest {id} [{state}] {string.Join<QuestObjStateInfo>(" | ", objectives)}";
    }
}
