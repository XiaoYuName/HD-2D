using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务系统主控。
    /// 领取 = 触发（瞬时事件，<see cref="IQuestTrigger"/>）+ 条件（持续状态，<see cref="CondManager"/>）都满足；
    /// 领取后由目标（<see cref="QuestObjInfoBase"/>）自己订阅事件推进，全达成后进入待交付，交付时发奖。
    ///
    /// 没有任何轮询。配置读取在 <see cref="QuestDataLoader"/>，领取判定在 <see cref="QuestAcceptScanner"/>，
    /// 进出区域/区域停留在 <see cref="QuestZoneTracker"/>，本类只负责把事件转发进去、管任务实例的生命周期和存档。
    /// </summary>
    public class QuestManager : MonoSingleton<QuestManager>, ISaveable
    {
        /// <summary>全部任务的运行时配置，表里那几列字符串启动时就解析成对象了。</summary>
        IReadOnlyDictionary<long, QuestData> questDataDict;

        QuestAcceptScanner scanner;
        QuestZoneTracker zoneTracker;

        /// <summary>已领取的任务（含已完成的）。</summary>
        [ShowInInspector, LabelText("任务列表")]
        readonly Dictionary<long, QuestInfo> quests = new();

        #region 对外事件

        /// <summary>任务被领取</summary>
        public event Action<QuestInfo> OnQuestAccepted;
        /// <summary>任务目标进度变化</summary>
        public event Action<QuestInfo> OnQuestProgress;
        /// <summary>任务目标全达成，进入待交付</summary>
        public event Action<QuestInfo> OnQuestReadyToComplete;
        /// <summary>任务交付完成（奖励已发）</summary>
        public event Action<QuestInfo> OnQuestCompleted;

        #endregion

        #region 生命周期

        void Start()
        {
            questDataDict = QuestDataLoader.Load();
            QuestConfigValidator.ValidateAll(questDataDict);
            scanner = new QuestAcceptScanner(questDataDict, IsQuestAccepted, questId => AcceptQuest(questId));
            zoneTracker = new QuestZoneTracker(this, scanner);

            SubsEvents();
            ((ISaveable)this).RegisterSaveable();
            zoneTracker.Start();
        }

        protected override void OnDestroy()
        {
            UnsubsEvents();
            foreach (QuestInfo info in quests.Values) info.Deactivate();
            zoneTracker.Stop();
            base.OnDestroy();
        }

        #endregion

        #region 事件订阅

        // 目标进度由目标自己订阅推进，这里只管「还没领的任务现在能不能领」
        void SubsEvents()
        {
            QuestEventBus.EnterZone += OnEnterZone;
            QuestEventBus.ExitZone += OnExitZone;
            QuestEventBus.ZoneStay += OnZoneStay;
            QuestEventBus.NpcClicked += OnNpcClicked;
            QuestEventBus.NpcTalked += OnNpcTalked;
            QuestEventBus.MiniGameFinished += OnMiniGameFinished;
            QuestEventBus.DialogueFinished += OnDialogueFinished;
            GameDataManager.Instance.RegisterPlayerDataDayChange(OnDayChanged);
        }

        void UnsubsEvents()
        {
            QuestEventBus.EnterZone -= OnEnterZone;
            QuestEventBus.ExitZone -= OnExitZone;
            QuestEventBus.ZoneStay -= OnZoneStay;
            QuestEventBus.NpcClicked -= OnNpcClicked;
            QuestEventBus.NpcTalked -= OnNpcTalked;
            QuestEventBus.MiniGameFinished -= OnMiniGameFinished;
            QuestEventBus.DialogueFinished -= OnDialogueFinished;
            GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);
        }

        void OnEnterZone(long sceneId) => scanner.ByTrigger(QuestTriggerType.EnterZone, sceneId, 0);
        void OnExitZone(long sceneId) => scanner.ByTrigger(QuestTriggerType.ExitZone, sceneId, 0);
        void OnZoneStay(long sceneId, int seconds) => scanner.ByTrigger(QuestTriggerType.EnterZoneStay, sceneId, seconds);
        void OnNpcClicked(long npcId) => scanner.ByTrigger(QuestTriggerType.ClickNpc, npcId, 0);
        void OnNpcTalked(long npcId) => scanner.ByTrigger(QuestTriggerType.DialogNpc, npcId, 0);

        // 领取条件（天数/对话/道具…）可能刚刚被满足，这两个事件后把被动触发的任务重扫一遍
        void OnDialogueFinished(long dialogueId) => scanner.Passive();
        void OnDayChanged(PlayerData _) => scanner.Passive();

        void OnMiniGameFinished(long gameId, int result)
        {
            scanner.ByTrigger(QuestTriggerType.MiniGameEnd, gameId, 0);
            scanner.ByTrigger(QuestTriggerType.MiniGameResult, gameId, result);
        }

        #endregion

        #region 查询

        public QuestData GetQuestData(long questId) => questDataDict.GetValueOrDefault(questId);

        public QuestInfo GetQuest(long questId) => quests.GetValueOrDefault(questId);

        public bool IsQuestCompleted(long questId)
            => quests.TryGetValue(questId, out QuestInfo info) && info.State == QuestState.Completed;

        public bool IsQuestAccepted(long questId) => quests.ContainsKey(questId);

        /// <summary>当前进行中（含待交付）的任务。</summary>
        public IEnumerable<QuestInfo> GetActiveQuests()
        {
            foreach (QuestInfo info in quests.Values)
            {
                if (info.State is QuestState.InProgress or QuestState.ReadyToComplete) yield return info;
            }
        }

        #endregion

        #region 领取

        /// <summary>
        /// 重扫被动触发（未配触发 / Auto / 随机）的任务。
        /// 平时由读档、天数变化、对话结束、任务交付自动触发，外部系统改了领取条件后也可以手动调一次。
        /// </summary>
        public void TryAcceptPassive() => scanner.Passive();

        /// <summary>直接领取（跳过触发与条件判定），给剧情/调试用。</summary>
        public QuestInfo AcceptQuest(long questId)
        {
            if (quests.TryGetValue(questId, out QuestInfo exist))
            {
                Debug.LogError($"[Quest] 任务 {questId} 已经领取过了，不能重复领取");
                return exist;
            }

            QuestData data = GetQuestData(questId);
            if (data == null)
            {
                Debug.LogError($"[Quest] 任务 {questId} 在任务表里不存在，不能领取");
                return null;
            }

            QuestInfo info = QuestInfo.Create(questId, data.CreateObjectives(), data.CreateExtraObjectives());
            info.OnProgressChanged = OnObjectiveProgress;
            info.OnStateChanged = OnQuestStateChanged;

            quests.Add(questId, info);
            OnQuestAccepted?.Invoke(info);

            // 领取时可能已经满足全部目标，Activate 末尾会判一次
            info.Activate();
            return info;
        }

        void OnObjectiveProgress(QuestInfo info) => OnQuestProgress?.Invoke(info);

        /// <summary>状态迁移在 <see cref="QuestInfo.SwitchState"/> 里完成，这里只负责转成对外事件。</summary>
        void OnQuestStateChanged(QuestInfo info)
        {
            if (info.State == QuestState.ReadyToComplete) OnQuestReadyToComplete?.Invoke(info);
            else if (info.State == QuestState.Completed) OnQuestCompleted?.Invoke(info);
        }

        #endregion

        #region 完成

        /// <summary>交付任务并发奖。目标没达成会拒绝。</summary>
        public bool CompleteQuest(long questId)
        {
            if (!quests.TryGetValue(questId, out QuestInfo info))
            {
                Debug.LogError($"[Quest] 任务 {questId} 还没领取，不能交付");
                return false;
            }
            if (info.State != QuestState.ReadyToComplete)
            {
                Debug.LogError($"[Quest] 任务 {questId} 当前是 {info.State}，只有 ReadyToComplete 才能交付");
                return false;
            }

            QuestData data = questDataDict[questId];
            QuestRewardFactory.Grant(data.Rewards);
            if (info.ExceedAchieved) QuestRewardFactory.Grant(data.ExtraRewards);

            info.SwitchState(QuestState.Completed);

            // 「完成某任务」目标和以任务完成为门槛的领取条件都靠这个事件推进
            QuestEventBus.ReportQuestCompleted(questId);
            scanner.Passive();

            SaveGameManager.Instance.Save();
            return true;
        }

        #endregion

        #region ISaveable

        public string GUID => "QuestManager";

        public void SaveData(GameSaveData data)
        {
            QuestSaveData save = new();
            foreach (QuestInfo info in quests.Values) save.Quests.Add(info);
            data.Quest = save;
        }

        public void LoadData(GameSaveData data)
        {
            foreach (QuestInfo info in quests.Values) info.Deactivate();
            quests.Clear();
            zoneTracker.Reset();

            foreach (QuestInfo info in data.Quest.Quests)
            {
                QuestData questData = GetQuestData(info.Id);
                if (questData == null)
                {
                    Debug.LogWarning($"[Quest] 存档里的任务 {info.Id} 已从配置移除，跳过");
                    continue;
                }

                // 目标对象直接从存档里恢复，但要按当前配置重新 Init：
                // 配置改了数量/目标条数都能跟上，累计进度保留，状态型目标的进度本来就是现算的
                info.Bind(
                    questData.CreateObjectives(info.Objectives),
                    questData.CreateExtraObjectives(info.ExtraObjectives));
                info.OnProgressChanged = OnObjectiveProgress;
                info.OnStateChanged = OnQuestStateChanged;

                quests.Add(info.Id, info);
                if (info.State == QuestState.InProgress) info.Activate();
            }

            scanner.Passive();
        }

        #endregion
    }
}
