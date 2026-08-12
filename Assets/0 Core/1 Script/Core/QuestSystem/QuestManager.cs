using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务系统主控。
    /// 接受 = 触发（瞬时事件，<see cref="IQuestTrigger"/>）+ 条件（持续状态，<see cref="CondManager"/>）都满足。
    /// 接受后每条目标（<see cref="QuestObjStateInfo"/>）自己订阅事件推进，达成即结算自己的奖励；
    /// 目标全达成后任务自动交付并发任务奖励；类别下的任务全完成再发一次类别奖励。
    /// </summary>
    public class QuestManager : MonoSingleton<QuestManager>, ISaveable
    {
        /// <summary>全部配置（任务、目标、类别、条件、奖励显示）都在这一个资产里。</summary>
        QuestDatabaseData database;

        IReadOnlyDictionary<long, QuestData> questDataDict;
        IReadOnlyDictionary<long, QuestObjConfigData> objDataDict;
        IReadOnlyDictionary<long, QuestCategory> categoryDict;

        QuestAcceptScanner scanner;
        QuestZoneTracker zoneTracker;

        /// <summary>已接受的任务（含已完成的）。</summary>
        [ShowInInspector, LabelText("任务列表")]
        readonly Dictionary<long, QuestInfo> questInfoDict = new();

        /// <summary>类别奖励已发过的类别，防止重复发。</summary>
        readonly HashSet<long> rewardedCategories = new();

        #region 对外事件

        /// <summary>任务被接受</summary>
        public event Action<QuestInfo> OnQuestAccepted;
        /// <summary>任务目标进度变化</summary>
        public event Action<QuestInfo> OnQuestProgress;
        /// <summary>任务目标全达成，进入待交付（随即自动交付）</summary>
        public event Action<QuestInfo> OnQuestReadyToComplete;
        /// <summary>任务完成（奖励已发）</summary>
        public event Action<QuestInfo> OnQuestCompleted;
        /// <summary>任务类别完成，类别奖励已发</summary>
        public event Action<QuestCategory> OnCategoryCompleted;
        /// <summary>某条目标达成、它自己的奖励（含超额）已发。第三个参数是这条目标在任务里排第几，从 1 数。</summary>
        public event Action<QuestInfo, QuestObjStateInfo, int> OnObjectiveRewarded;

        #endregion

        #region 生命周期

        void Start()
        {
            database = QuestDatabaseProvider.Database;
            database.Init();

            questDataDict = database.Quests;
            objDataDict = database.Objs;
            categoryDict = database.Categories;
            QuestConfigValidator.ValidateAll(database);

            scanner = new QuestAcceptScanner(questDataDict, IsQuestAccepted, questId => AcceptQuest(questId));
            zoneTracker = new QuestZoneTracker(this, scanner);

            SubsEvents();
            ((ISaveable)this).RegisterSaveable();
            zoneTracker.Start();
        }

        protected override void OnDestroy()
        {
            UnsubsEvents();
            foreach (QuestInfo info in questInfoDict.Values) info.Deactivate();
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

        void OnEnterZone(long mapSceneId, long sceneId)
            => scanner.ByZone(QuestTriggerType.EnterZone, new QuestZoneArgs(mapSceneId, sceneId));

        void OnExitZone(long mapSceneId, long sceneId)
            => scanner.ByZone(QuestTriggerType.ExitZone, new QuestZoneArgs(mapSceneId, sceneId));

        void OnZoneStay(long mapSceneId, long sceneId, int seconds)
            => scanner.ByZone(QuestTriggerType.EnterZoneStay, new QuestZoneArgs(mapSceneId, sceneId, seconds));
        void OnNpcClicked(long npcId) => scanner.ByTrigger(QuestTriggerType.ClickNpc, npcId, 0);
        void OnNpcTalked(long npcId) => scanner.ByTrigger(QuestTriggerType.DialogNpc, npcId, 0);

        // 接受条件（天数/对话/道具…）可能刚刚被满足，这两个事件后把被动触发的任务重扫一遍
        void OnDialogueFinished(long dialogueId) => scanner.Passive();
        void OnDayChanged(PlayerData _) => scanner.Passive();

        void OnMiniGameFinished(MiniGameType game, MiniGameResult result)
        {
            scanner.ByTrigger(QuestTriggerType.MiniGameEnd, (long)game, 0);
            scanner.ByTrigger(QuestTriggerType.MiniGameResult, (long)game, (int)result);
        }

        #endregion

        #region 查询

        // Get 一律「取不到就抛」：这些 ID 都是配表里互相引用出来的，取不到就是没配好，
        // 早点炸在启动阶段好过悄悄返回 null、拖到后面某处 NullReference。
        // 要先问「在不在」的，用下面的 Contain / IsXxx，别拿 Get 的返回值当存在性判断。

        public QuestData GetQuestData(long questId) => Require(questDataDict, questId, "任务");

        public bool ContainQuestData(long questId) => questDataDict.ContainsKey(questId);

        public QuestObjConfigData GetQuestObjConfigData(long objId) => Require(objDataDict, objId, "任务目标");

        public bool ContainQuestObjConfigData(long objId) => objDataDict.ContainsKey(objId);

        public QuestCategory GetCategory(long categoryId) => Require(categoryDict, categoryId, "任务类别");

        public bool ContainCategory(long categoryId) => categoryDict.ContainsKey(categoryId);

        /// <summary>面板左侧 Tab 用，按配表顺序。</summary>
        public IEnumerable<QuestCategory> GetCategories() => categoryDict.Values;

        /// <summary>全部任务配置，调试/测试面板遍历用。</summary>
        public IEnumerable<QuestData> GetQuestDatas() => questDataDict.Values;

        /// <summary>只能对已接受的任务调，先用 <see cref="IsQuestAccepted"/> 问一声。</summary>
        public QuestInfo GetQuest(long questId)
        {
            if (questInfoDict.TryGetValue(questId, out QuestInfo info)) return info;

            throw new KeyNotFoundException($"[Quest] 任务 {questId} 还没接受，先用 {nameof(IsQuestAccepted)} 判断");
        }

        public bool IsQuestAccepted(long questId) => questInfoDict.ContainsKey(questId);

        public bool IsQuestCompleted(long questId)
            => questInfoDict.TryGetValue(questId, out QuestInfo info) && info.State == QuestState.Completed;

        static TValue Require<TValue>(IReadOnlyDictionary<long, TValue> dict, long id, string what)
        {
            if (dict.TryGetValue(id, out TValue value)) return value;

            throw new KeyNotFoundException($"[Quest] {what} {id} 在配置表里不存在");
        }

        public bool IsCategoryRewarded(long categoryId) => rewardedCategories.Contains(categoryId);

        /// <summary>类别下的任务是否全部完成。</summary>
        public bool IsCategoryCompleted(QuestCategory category)
        {
            foreach (long questId in category.QuestIds)
            {
                if (!IsQuestCompleted(questId)) return false;
            }
            return true;
        }

        /// <summary>当前进行中（含待交付）的任务。</summary>
        public IEnumerable<QuestInfo> GetActiveQuests()
        {
            foreach (QuestInfo info in questInfoDict.Values)
            {
                if (info.State is QuestState.InProgress or QuestState.ReadyToComplete) yield return info;
            }
        }

        #endregion

        #region 接受

        /// <summary>
        /// 重扫被动触发（未配触发 / Auto / 随机）的任务。
        /// 平时由读档、天数变化、对话结束、任务完成自动触发，外部系统改了接受条件后也可以手动调一次。
        /// </summary>
        public void TryAcceptPassive() => scanner.Passive();

        /// <summary>直接接受（跳过触发与条件判定），给剧情/调试用。</summary>
        public QuestInfo AcceptQuest(long questId)
        {
            if (IsQuestAccepted(questId))
            {
                Debug.LogError($"[Quest] 任务 {questId} 已经接受过了，不能重复接受");
                return GetQuest(questId);
            }
            if (!ContainQuestData(questId))
            {
                Debug.LogError($"[Quest] 任务 {questId} 在任务表里不存在，不能接受");
                return null;
            }

            QuestInfo info = QuestInfo.Create(GetQuestData(questId));
            info.OnProgressChanged = OnObjectiveProgress;
            info.OnStateChanged = OnQuestStateChanged;
            info.OnObjectiveCompleted = OnObjectiveCompleted;

            questInfoDict.Add(questId, info);
            OnQuestAccepted?.Invoke(info);

            // 接受时可能已经满足全部目标，Activate 末尾会判一次
            info.Activate();
            return info;
        }

        void OnObjectiveProgress(QuestInfo info) => OnQuestProgress?.Invoke(info);

        /// <summary>
        /// 目标奖励是目标自己在达成那一刻发的（<see cref="QuestObjStateInfo.CheckComplete"/>），
        /// 这里只转成对外事件，弹窗（含超额那份）由表现层自己接。
        /// </summary>
        void OnObjectiveCompleted(QuestInfo info, QuestObjStateInfo obj)
        {
            // 弹窗要显示「这是第几条目标」，编号就按任务里的排列顺序来
            OnObjectiveRewarded?.Invoke(info, obj, Array.IndexOf(info.Objectives, obj) + 1);
        }

        /// <summary>状态迁移在 <see cref="QuestInfo.SwitchState"/> 里完成，这里只负责转成对外事件和自动交付。</summary>
        void OnQuestStateChanged(QuestInfo info)
        {
            if (info.State == QuestState.ReadyToComplete)
            {
                OnQuestReadyToComplete?.Invoke(info);
                CompleteQuest(info.Id);
            }
            else if (info.State == QuestState.Completed)
            {
                OnQuestCompleted?.Invoke(info);
            }
        }

        #endregion

        #region 完成

        /// <summary>
        /// 交付任务并发任务奖励。目标全达成后由 <see cref="OnQuestStateChanged"/> 自动调用，
        /// 想改回手动交付（策划案里的「点交付」）就把那处调用去掉，从 UI 调这里。
        /// </summary>
        public bool CompleteQuest(long questId)
        {
            if (!IsQuestAccepted(questId))
            {
                Debug.LogError($"[Quest] 任务 {questId} 还没接受，不能交付");
                return false;
            }

            QuestInfo info = GetQuest(questId);
            if (info.State != QuestState.ReadyToComplete)
            {
                Debug.LogError($"[Quest] 任务 {questId} 当前是 {info.State}，只有 ReadyToComplete 才能交付");
                return false;
            }

            // 奖励先发，再切到已完成 —— OnQuestCompleted 的语义是「奖励已发」，弹窗接的就是它
            QuestRewards.Grant(GetQuestData(questId).Rewards);
            info.SwitchState(QuestState.Completed);

            // 「完成某任务」目标和以任务完成为门槛的接受条件都靠这个事件推进
            QuestEventBus.ReportQuestCompleted(questId);
            TryRewardCategories();
            scanner.Passive();

            SaveGameManager.Instance.Save();
            return true;
        }

        /// <summary>任务完成后扫一遍类别，凑齐的发一次类别奖励。</summary>
        void TryRewardCategories()
        {
            foreach (QuestCategory category in categoryDict.Values)
            {
                if (rewardedCategories.Contains(category.Id) || !IsCategoryCompleted(category)) continue;

                rewardedCategories.Add(category.Id);
                QuestRewards.Grant(category.Rewards);
                OnCategoryCompleted?.Invoke(category);
            }
        }

        #endregion

        #region ISaveable

        public string GUID => "QuestManager";

        public void SaveData(GameSaveData data)
        {
            QuestSaveData save = new();
            foreach (QuestInfo info in questInfoDict.Values) save.Quests.Add(info);
            save.RewardedCategories.AddRange(rewardedCategories);
            data.Quest = save;
        }

        public void LoadData(GameSaveData data)
        {
            foreach (QuestInfo info in questInfoDict.Values) info.Deactivate();
            questInfoDict.Clear();
            rewardedCategories.Clear();
            zoneTracker.Reset();

            foreach (long categoryId in data.Quest.RewardedCategories) rewardedCategories.Add(categoryId);

            foreach (QuestInfo info in data.Quest.Quests)
            {
                if (!ContainQuestData(info.Id))
                {
                    Debug.LogWarning($"[Quest] 存档里的任务 {info.Id} 已从配置移除，跳过");
                    continue;
                }

                info.Set(LoadObjectives(GetQuestData(info.Id), info.Objectives));
                info.OnProgressChanged = OnObjectiveProgress;
                info.OnStateChanged = OnQuestStateChanged;
                info.OnObjectiveCompleted = OnObjectiveCompleted;

                questInfoDict.Add(info.Id, info);
                if (info.State == QuestState.InProgress) info.Activate();
            }

            scanner.Passive();
        }

        /// <summary>
        /// 目标对象直接从存档里恢复。存档里只有进度，需求量/参数这些都从配置现取，
        /// 所以配置改了数量、改了目标条数都能跟上，累计次数和已结算标记保留。
        /// </summary>
        static QuestObjStateInfo[] LoadObjectives(QuestData questData, QuestObjStateInfo[] saved)
        {
            QuestObjStateInfo[] result = new QuestObjStateInfo[questData.Objs.Length];
            for (int i = 0; i < result.Length; i++)
            {
                QuestObjConfigData objData = questData.Objs[i];
                QuestObjStateInfo savedObj = Array.Find(saved, o => o != null && o.Id == objData.Id);

                if (savedObj == null)
                {
                    result[i] = QuestObjStateInfo.Create(objData);
                    continue;
                }

                savedObj.Set(objData.CreateTarget(savedObj.target), objData.CreateExtra(savedObj.extra));
                result[i] = savedObj;
            }
            return result;
        }

        #endregion
    }
}
