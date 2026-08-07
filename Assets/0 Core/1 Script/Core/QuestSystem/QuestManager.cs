using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务系统主控。
    /// 领取 = 触发（瞬时事件，<see cref="QuestTrigger"/>）+ 条件（持续状态，<see cref="CondManager"/>）都满足；
    /// 领取后由目标（<see cref="QuestObjInfoBase"/>）推进，全达成后进入待交付，交付时发奖。
    /// </summary>
    public class QuestManager : MonoSingleton<QuestManager>, ISaveable
    {
        #region 配置访问

        IReadOnlyDictionary<long, QuestData> QuestDataDict => LubanManager.Instance.TbQuestData.DataMap;
        IReadOnlyList<QuestData> QuestDataList => LubanManager.Instance.TbQuestData.DataList;

        public QuestData GetQuestData(long questId) => QuestDataDict.GetValueOrDefault(questId);

        /// <summary>已解析好的奖励，给 UI 展示用。</summary>
        public List<IQuestReward> GetRewards(long questId, bool extra = false)
            => (extra ? extraRewardCache : rewardCache).GetValueOrDefault(questId);

        /// <summary>表里那几列字符串只在启动时解析一次，之后事件里不再碰字符串。</summary>
        readonly Dictionary<long, List<QuestTrigger>> triggerCache = new();
        readonly Dictionary<long, List<QuestArgs>> objCache = new();
        readonly Dictionary<long, List<QuestArgs>> extraObjCache = new();
        readonly Dictionary<long, List<IQuestReward>> rewardCache = new();
        readonly Dictionary<long, List<IQuestReward>> extraRewardCache = new();

        /// <summary>触发类型 → 用到它的任务ID。事件来了只遍历相关任务，不扫全表。</summary>
        readonly Dictionary<QuestTriggerType, List<long>> triggerIndex = new();

        #endregion

        /// <summary>已领取的任务（含已完成的）。</summary>
        [ShowInInspector, LabelText("任务列表")]
        readonly Dictionary<long, QuestInfo> quests = new();

        /// <summary>区域停留的检查间隔，秒。只在当前场景确实有停留触发时才起协程。</summary>
        const float StayCheckInterval = 0.5f;

        long currentSceneId;
        Coroutine stayRoutine;

        /// <summary>重扫时的遍历副本，避免回调里改动 quests 炸迭代器。</summary>
        readonly List<QuestInfo> refreshBuffer = new();

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
            BuildConfigCache();
            SubsEvents();
            ((ISaveable)this).RegisterSaveable();
            GameSceneManager.Instance.RegisterSceneChange(OnSceneChanged);
        }

        protected override void OnDestroy()
        {
            UnsubsEvents();
            foreach (QuestInfo info in quests.Values) info.Deactivate();
            if (GameSceneManager.IsInitialized) GameSceneManager.Instance.UnregisterSceneChange(OnSceneChanged);
            base.OnDestroy();
        }

        /// <summary>启动时把所有任务的触发/目标解析并校验一遍，配置手误当场报错。</summary>
        void BuildConfigCache()
        {
            triggerCache.Clear();
            objCache.Clear();
            extraObjCache.Clear();
            rewardCache.Clear();
            extraRewardCache.Clear();
            triggerIndex.Clear();

            foreach (QuestData config in QuestDataList)
            {
                List<QuestTrigger> triggers = QuestTrigger.ParseList(config.AcceptTrigger, config.Id);
                triggerCache[config.Id] = triggers;
                objCache[config.Id] = QuestArgs.SplitList(config.QuestObjData, config.Id);
                extraObjCache[config.Id] = QuestArgs.SplitList(config.ExtraQuestObjData, config.Id);

                List<IQuestReward> rewards = QuestRewardFactory.CreateList(config.Reward, config.Id);
                List<IQuestReward> extraRewards = QuestRewardFactory.CreateList(config.ExtraReward, config.Id);
                QuestRewardValidator.ValidateAll(rewards);
                QuestRewardValidator.ValidateAll(extraRewards);
                rewardCache[config.Id] = rewards;
                extraRewardCache[config.Id] = extraRewards;

                // 没配触发 = 只看条件，按 None 入索引，重扫时会捞到
                if (triggers.Count == 0)
                {
                    IndexTrigger(QuestTriggerType.None, config.Id);
                    continue;
                }
                foreach (QuestTrigger trigger in triggers) IndexTrigger(trigger.Type, config.Id);
            }
        }

        void IndexTrigger(QuestTriggerType type, long questId)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> list)) triggerIndex[type] = list = new List<long>();
            if (!list.Contains(questId)) list.Add(questId);
        }

        #endregion

        #region 事件订阅

        void SubsEvents()
        {
            QuestEventBus.EnterZone += OnEnterZone;
            QuestEventBus.ExitZone += OnExitZone;
            QuestEventBus.ZoneStay += OnZoneStay;
            QuestEventBus.NpcClicked += OnNpcClicked;
            QuestEventBus.NpcTalked += OnNpcTalked;
            QuestEventBus.GameFinished += OnGameFinished;
            QuestEventBus.DialogueFinished += OnDialogueFinished;
            QuestEventBus.GiftGiven += OnGiftGiven;
            QuestEventBus.ItemBought += OnItemBought;
        }

        void UnsubsEvents()
        {
            QuestEventBus.EnterZone -= OnEnterZone;
            QuestEventBus.ExitZone -= OnExitZone;
            QuestEventBus.ZoneStay -= OnZoneStay;
            QuestEventBus.NpcClicked -= OnNpcClicked;
            QuestEventBus.NpcTalked -= OnNpcTalked;
            QuestEventBus.GameFinished -= OnGameFinished;
            QuestEventBus.DialogueFinished -= OnDialogueFinished;
            QuestEventBus.GiftGiven -= OnGiftGiven;
            QuestEventBus.ItemBought -= OnItemBought;
        }

        // 目标进度由目标自己订阅推进，这里只管「领取触发」和「状态型目标重扫」
        void OnEnterZone(long sceneId) => AfterEvent(QuestTriggerType.EnterZone, sceneId);
        void OnExitZone(long sceneId) => AfterEvent(QuestTriggerType.ExitZone, sceneId);
        void OnZoneStay(long sceneId, int seconds) => TryAcceptByTrigger(QuestTriggerType.EnterZoneStay, sceneId, seconds);
        void OnNpcClicked(long npcId) => AfterEvent(QuestTriggerType.ClickNpc, npcId);
        void OnNpcTalked(long npcId) => AfterEvent(QuestTriggerType.DialogNpc, npcId);
        void OnDialogueFinished(long dialogueId) => RefreshAll();
        void OnItemBought(long itemId, int count) => RefreshAll();
        void OnGiftGiven(long npcId, long itemId, int count) => RefreshAll();

        void OnGameFinished(long gameId, int result)
        {
            TryAcceptByTrigger(QuestTriggerType.GameEnd, gameId);
            TryAcceptByTrigger(QuestTriggerType.GameResult, gameId, result);
            RefreshAll();
        }

        void AfterEvent(QuestTriggerType type, long id, int param = 0)
        {
            TryAcceptByTrigger(type, id, param);
            RefreshAll();
        }

        #endregion

        #region 场景停留

        void OnSceneChanged(SceneData sceneData)
        {
            long newSceneId = sceneData?.SceneID ?? 0;
            if (newSceneId == currentSceneId) return;

            StopStayRoutine();
            if (currentSceneId > 0) QuestEventBus.ReportExitZone(currentSceneId);

            currentSceneId = newSceneId;
            if (currentSceneId <= 0) return;

            QuestEventBus.ReportEnterZone(currentSceneId);
            if (HasPendingStayTrigger(currentSceneId)) stayRoutine = StartCoroutine(ZoneStayLoop(currentSceneId));
        }

        /// <summary>当前场景有没有还没领、且靠停留触发的任务。没有就不起协程。</summary>
        bool HasPendingStayTrigger(long sceneId)
        {
            if (!triggerIndex.TryGetValue(QuestTriggerType.EnterZoneStay, out List<long> questIds)) return false;

            foreach (long questId in questIds)
            {
                if (quests.ContainsKey(questId)) continue;
                foreach (QuestTrigger trigger in triggerCache[questId])
                {
                    if (trigger.Type == QuestTriggerType.EnterZoneStay && trigger.SceneId == sceneId) return true;
                }
            }
            return false;
        }

        IEnumerator ZoneStayLoop(long sceneId)
        {
            WaitForSeconds wait = new(StayCheckInterval);
            float elapsed = 0f;
            int reported = 0;

            while (true)
            {
                yield return wait;
                elapsed += StayCheckInterval;

                int seconds = Mathf.FloorToInt(elapsed);
                if (seconds == reported) continue;
                reported = seconds;

                QuestEventBus.ReportZoneStay(sceneId, seconds);
                if (!HasPendingStayTrigger(sceneId)) break; // 该领的都领了，收工
            }
            stayRoutine = null;
        }

        void StopStayRoutine()
        {
            if (stayRoutine == null) return;
            StopCoroutine(stayRoutine);
            stayRoutine = null;
        }

        #endregion

        #region 查询

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

        /// <summary>重扫状态型目标 + 被动触发（Auto / 未配触发 / 随机）。时间推进、读档、条件可能变化时调它。</summary>
        public void RefreshAll()
        {
            // 回调里可能有人接新任务，改动 quests，所以先拷一份再遍历
            refreshBuffer.Clear();
            refreshBuffer.AddRange(quests.Values);

            foreach (QuestInfo info in refreshBuffer)
            {
                if (info.State != QuestState.InProgress) continue;
                info.Refresh();
                if (info.State == QuestState.InProgress && info.IsAllObjComplete) MarkReadyToComplete(info);
            }
            refreshBuffer.Clear();

            TryAcceptByTrigger(QuestTriggerType.None, 0);
            TryAcceptByTrigger(QuestTriggerType.Auto, 0);
            TryAcceptByTrigger(QuestTriggerType.RandomChance, 0);
        }

        void TryAcceptByTrigger(QuestTriggerType type, long id, int param = 0)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> questIds)) return;

            for (int i = 0; i < questIds.Count; i++)
            {
                long questId = questIds[i];
                if (quests.ContainsKey(questId)) continue;

                foreach (QuestTrigger trigger in triggerCache[questId])
                {
                    if (trigger.Type != type || !IsTriggerHit(trigger, id, param)) continue;
                    if (CondManager.IsMatched(GetQuestData(questId).AcceptCond)) AcceptQuest(questId);
                    break;
                }
            }
        }

        static bool IsTriggerHit(QuestTrigger trigger, long id, int param) => trigger.Type switch
        {
            QuestTriggerType.None or QuestTriggerType.Auto => true,
            QuestTriggerType.RandomChance => trigger.RollChance(),
            QuestTriggerType.EnterZone or QuestTriggerType.ExitZone => trigger.SceneId == id,
            QuestTriggerType.EnterZoneStay => trigger.SceneId == id && param >= trigger.Seconds,
            QuestTriggerType.ClickNpc or QuestTriggerType.DialogNpc => trigger.NpcId == id,
            QuestTriggerType.GameEnd => trigger.GameId == id,
            QuestTriggerType.GameResult => trigger.GameId == id && (trigger.Result == 0 || trigger.Result == param),
            _ => false,
        };

        /// <summary>直接领取（跳过触发与条件判定），给剧情/调试用。</summary>
        public QuestInfo AcceptQuest(long questId)
        {
            if (quests.TryGetValue(questId, out QuestInfo exist)) return exist;

            QuestInfo info = BuildQuestInfo(questId);
            if (info == null) return null;

            quests.Add(questId, info);
            info.Activate();
            info.Refresh();
            OnQuestAccepted?.Invoke(info);

            if (info.IsAllObjComplete) MarkReadyToComplete(info);
            return info;
        }

        QuestInfo BuildQuestInfo(long questId)
        {
            if (GetQuestData(questId) == null) return null;

            return new QuestInfo(questId,
                QuestObjFactory.CreateList(objCache[questId]),
                QuestObjFactory.CreateList(extraObjCache[questId]))
            {
                OnProgressChanged = OnObjectiveProgress,
            };
        }

        void OnObjectiveProgress(QuestInfo info)
        {
            OnQuestProgress?.Invoke(info);
            if (info.State == QuestState.InProgress && info.IsAllObjComplete) MarkReadyToComplete(info);
        }

        #endregion

        #region 完成

        void MarkReadyToComplete(QuestInfo info)
        {
            if (info.State != QuestState.InProgress) return;

            info.ExceedAchieved = info.IsAllExtraComplete;
            info.State = QuestState.ReadyToComplete;
            info.Deactivate();
            OnQuestReadyToComplete?.Invoke(info);
        }

        /// <summary>交付任务并发奖。目标没达成会拒绝。</summary>
        public bool CompleteQuest(long questId)
        {
            if (!quests.TryGetValue(questId, out QuestInfo info))
            {
                Debug.LogError($"[Quest] 任务 {questId} 还没领取，不能交付");
                return false;
            }
            if (info.State == QuestState.Completed) return false;
            if (info.State != QuestState.ReadyToComplete)
            {
                Debug.LogWarning($"[Quest] 任务 {questId} 目标未达成，不能交付");
                return false;
            }

            QuestRewardFactory.Grant(rewardCache.GetValueOrDefault(questId));
            if (info.ExceedAchieved) QuestRewardFactory.Grant(extraRewardCache.GetValueOrDefault(questId));

            info.State = QuestState.Completed;
            info.Deactivate();
            OnQuestCompleted?.Invoke(info);

            // 「完成N个任务」这类目标依赖完成状态，得立刻重扫
            RefreshAll();
            SaveGameManager.Instance.Save();
            return true;
        }

        #endregion

        #region ISaveable

        public string GUID => "QuestManager";

        public void SaveData(GameSaveData data)
        {
            QuestSaveData save = new();
            foreach (QuestInfo info in quests.Values)
            {
                QuestEntrySaveData entry = new()
                {
                    QuestId = info.ID,
                    State = info.State,
                    AcceptDay = info.AcceptDay,
                    ExceedAchieved = info.ExceedAchieved,
                };
                foreach (QuestObjInfoBase obj in info.Objectives) entry.ObjStates.Add(obj.SaveState());
                foreach (QuestObjInfoBase obj in info.ExtraObjectives) entry.ExtraObjStates.Add(obj.SaveState());
                save.Quests.Add(entry);
            }
            data.Quest = save;
        }

        public void LoadData(GameSaveData data)
        {
            foreach (QuestInfo info in quests.Values) info.Deactivate();
            quests.Clear();
            StopStayRoutine();
            currentSceneId = 0;

            if (triggerCache.Count == 0) BuildConfigCache();

            if (data?.Quest?.Quests != null)
            {
                foreach (QuestEntrySaveData entry in data.Quest.Quests)
                {
                    QuestInfo info = BuildQuestInfo(entry.QuestId);
                    if (info == null)
                    {
                        Debug.LogWarning($"[Quest] 存档里的任务 {entry.QuestId} 已从配置移除，跳过");
                        continue;
                    }

                    info.State = entry.State;
                    info.AcceptDay = entry.AcceptDay;
                    info.ExceedAchieved = entry.ExceedAchieved;
                    ApplyStates(info.Objectives, entry.ObjStates);
                    ApplyStates(info.ExtraObjectives, entry.ExtraObjStates);

                    quests.Add(info.ID, info);
                    if (info.State == QuestState.InProgress) info.Activate();
                }
            }

            RefreshAll();
        }

        /// <summary>按下标回填进度；配置增删目标时多退少补，不炸档。</summary>
        static void ApplyStates(List<QuestObjInfoBase> objectives, List<int[]> states)
        {
            if (states == null) return;
            int count = Mathf.Min(objectives.Count, states.Count);
            for (int i = 0; i < count; i++) objectives[i].LoadState(states[i]);
        }

        #endregion
    }
}
