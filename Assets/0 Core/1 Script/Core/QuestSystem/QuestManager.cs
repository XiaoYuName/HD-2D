using System;
using System.Collections;
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
    /// 没有任何轮询：目标进度靠各自订阅的事件推，本类只在事件到达时做「还没领的任务能不能领」这一件扫描。
    /// </summary>
    public class QuestManager : MonoSingleton<QuestManager>, ISaveable
    {
        #region 配置访问

        IReadOnlyDictionary<long, QuestData> QuestDataDict => LubanManager.Instance.TbQuestData.DataMap;
        IReadOnlyList<QuestData> QuestDataList => LubanManager.Instance.TbQuestData.DataList;

        public QuestData GetQuestData(long questId) => QuestDataDict.GetValueOrDefault(questId);

        /// <summary>已解析好的奖励，给 UI 展示用。</summary>
        public List<IQuestReward> GetRewards(long questId, bool extra)
            => (extra ? extraRewardCache : rewardCache).GetValueOrDefault(questId);

        /// <summary>表里那几列字符串只在启动时解析一次，之后事件里不再碰字符串。</summary>
        readonly Dictionary<long, List<IQuestTrigger>> triggerCache = new();
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

        /// <summary>启动时把所有任务的触发/目标/奖励解析并校验一遍，配置手误当场报错。</summary>
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
                List<IQuestTrigger> triggers = QuestTriggerFactory.CreateList(config.AcceptTrigger, config.Id);
                triggerCache[config.Id] = triggers;
                foreach (IQuestTrigger trigger in triggers) IndexTrigger(trigger.Type, config.Id);

                objCache[config.Id] = QuestArgs.SplitList(config.QuestObjData, config.Id);
                extraObjCache[config.Id] = QuestArgs.SplitList(config.ExtraQuestObjData, config.Id);

                List<IQuestReward> rewards = QuestRewardFactory.CreateList(config.Reward, config.Id);
                List<IQuestReward> extraRewards = QuestRewardFactory.CreateList(config.ExtraReward, config.Id);
                QuestConfigValidator.ValidateRewards(rewards);
                QuestConfigValidator.ValidateRewards(extraRewards);
                rewardCache[config.Id] = rewards;
                extraRewardCache[config.Id] = extraRewards;

                ValidateObjectives(config.Id);
            }
        }

        /// <summary>目标是每次领取才实例化的，所以启动时先造一份临时的把配置查一遍。</summary>
        void ValidateObjectives(long questId)
        {
            QuestConfigValidator.ValidateObjectives(QuestObjFactory.CreateList(objCache[questId]), objCache[questId]);
            QuestConfigValidator.ValidateObjectives(QuestObjFactory.CreateList(extraObjCache[questId]), extraObjCache[questId]);
        }

        void IndexTrigger(QuestTriggerType type, long questId)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> list)) triggerIndex[type] = list = new List<long>();
            if (!list.Contains(questId)) list.Add(questId);
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
            if (GameDataManager.IsInitialized) GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);
        }

        void OnEnterZone(long sceneId) => TryAcceptByTrigger(QuestTriggerType.EnterZone, sceneId, 0);
        void OnExitZone(long sceneId) => TryAcceptByTrigger(QuestTriggerType.ExitZone, sceneId, 0);
        void OnZoneStay(long sceneId, int seconds) => TryAcceptByTrigger(QuestTriggerType.EnterZoneStay, sceneId, seconds);
        void OnNpcClicked(long npcId) => TryAcceptByTrigger(QuestTriggerType.ClickNpc, npcId, 0);
        void OnNpcTalked(long npcId) => TryAcceptByTrigger(QuestTriggerType.DialogNpc, npcId, 0);

        // 领取条件（天数/对话/道具…）可能刚刚被满足，这两个事件后把被动触发的任务重扫一遍
        void OnDialogueFinished(long dialogueId) => TryAcceptPassive();
        void OnDayChanged(PlayerData _) => TryAcceptPassive();

        void OnMiniGameFinished(long gameId, int result)
        {
            TryAcceptByTrigger(QuestTriggerType.MiniGameEnd, gameId, 0);
            TryAcceptByTrigger(QuestTriggerType.MiniGameResult, gameId, result);
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
                foreach (IQuestTrigger trigger in triggerCache[questId])
                {
                    if (trigger is EnterZoneStayQuestTrigger stay && stay.SceneId == sceneId) return true;
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

        /// <summary>重扫被动触发（未配触发 / Auto / 随机）。读档、天数变化、对话结束后调。</summary>
        public void TryAcceptPassive()
        {
            TryAcceptByTrigger(QuestTriggerType.None, 0, 0);
            TryAcceptByTrigger(QuestTriggerType.Auto, 0, 0);
            TryAcceptByTrigger(QuestTriggerType.RandomChance, 0, 0);
        }

        void TryAcceptByTrigger(QuestTriggerType type, long id, int param)
        {
            if (!triggerIndex.TryGetValue(type, out List<long> questIds)) return;

            for (int i = 0; i < questIds.Count; i++)
            {
                long questId = questIds[i];
                if (quests.ContainsKey(questId)) continue;

                foreach (IQuestTrigger trigger in triggerCache[questId])
                {
                    if (trigger.Type != type || !trigger.IsHit(id, param)) continue;
                    if (CondManager.IsMatched(GetQuestData(questId).AcceptCond)) AcceptQuest(questId);
                    break;
                }
            }
        }

        /// <summary>直接领取（跳过触发与条件判定），给剧情/调试用。</summary>
        public QuestInfo AcceptQuest(long questId)
        {
            if (quests.TryGetValue(questId, out QuestInfo exist)) return exist;
            if (GetQuestData(questId) == null) return null;

            QuestInfo info = new(questId,
                QuestObjFactory.CreateList(objCache[questId]),
                QuestObjFactory.CreateList(extraObjCache[questId]))
            {
                OnProgressChanged = OnObjectiveProgress,
            };

            quests.Add(questId, info);
            OnQuestAccepted?.Invoke(info);

            // 订阅时目标会按当前状态先算一次，可能当场就达成
            info.Activate();
            if (info.State == QuestState.InProgress && info.IsAllObjComplete) MarkReadyToComplete(info);
            return info;
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

            // 「完成某任务」目标和以任务完成为门槛的领取条件都靠这个事件推进
            QuestEventBus.ReportQuestCompleted(questId);
            TryAcceptPassive();

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
            StopStayRoutine();
            currentSceneId = 0;

            if (triggerCache.Count == 0) BuildConfigCache();

            if (data?.Quest?.Quests != null)
            {
                foreach (QuestInfo info in data.Quest.Quests)
                {
                    if (GetQuestData(info.ID) == null)
                    {
                        Debug.LogWarning($"[Quest] 存档里的任务 {info.ID} 已从配置移除，跳过");
                        continue;
                    }

                    // 目标对象直接从存档里恢复，但要按当前配置重新 Init：
                    // 配置改了数量/目标条数都能跟上，累计进度保留，状态型目标订阅时自然重算
                    info.Bind(
                        QuestObjFactory.CreateList(objCache[info.ID], info.Objectives),
                        QuestObjFactory.CreateList(extraObjCache[info.ID], info.ExtraObjectives));
                    info.OnProgressChanged = OnObjectiveProgress;

                    quests.Add(info.ID, info);
                    if (info.State == QuestState.InProgress) info.Activate();
                }
            }

            TryAcceptPassive();
        }

        #endregion
    }
}
