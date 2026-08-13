using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    public sealed class MachiRoomGameManager : MonoSingleton<MachiRoomGameManager>, ISaveable
    {
        [LabelText("玩法配置"), SerializeField] MachiRoomGameConfig config;
        [SerializeField] MachiRoomCreationInfo curCeationInfo;
        [LabelText("测试：强制马吉在工作室"), SerializeField] bool isMachiInStudioForced;

        IReadOnlyDictionary<long, ManuscriptItemData> manuscriptItemDataTable;
        IReadOnlyList<ManuscriptItemData> manuscriptItemDataList;
        Action<MachiRoomCreationInfo> onCreationChanged;
        public string GUID => "MachiRoomGameManager";
        public MachiRoomGameConfig Config => config;
        public MachiRoomCreationInfo CreationInfo => curCeationInfo;
        public IReadOnlyDictionary<long, ManuscriptItemData> ManuscriptItemDataTable => manuscriptItemDataTable;
        /// <summary>当前评分是否够格发奖，奖励就是当前这张画稿物品本身。</summary>
        public bool IsRewardEarned => curCeationInfo.Score >= config.SuccessMinScore;
        const string DraftImagePath = "Assets/AddressableAssets/Remote/Texture2D/UI/MachiRoom/Draft/";
        const string FinalImagePath = "Assets/AddressableAssets/Remote/Texture2D/UI/MachiRoom/FinalImage/";
        public const long StudioSceneId = 10020;
        const int TimeSlotCountPerDay = 4;

        void Start()
        {
            ((ISaveable)this).RegisterSaveable();
            GameDataManager.Instance.RegisterPlayerDataTimeSlotChangeNoInvoke(OnTimeSlotChanged);
            manuscriptItemDataTable = LubanManager.Instance.TbManuscriptItemData.DataMap;
            manuscriptItemDataList = LubanManager.Instance.TbManuscriptItemData.DataList;
        }

        protected override void OnDestroy()
        {
            GameDataManager.Instance?.UnregisterPlayerDataTimeSlotChange(OnTimeSlotChanged);
            base.OnDestroy();
        }

        public ManuscriptItemData GetManuscriptItemData(long itemId)
        {
            return manuscriptItemDataTable[itemId];
        }

        /// <summary>测试用：强制认为马吉在工作室，跳过星期/时段规则；改动后立刻刷新已打开的面板。</summary>
        public bool IsMachiInStudioForced
        {
            get => isMachiInStudioForced;
            set
            {
                isMachiInStudioForced = value;
                onCreationChanged?.Invoke(curCeationInfo);
            }
        }

        /// <summary>马吉是否在工作室：按工作室场景配的固定 NPC 的星期/时段出现规则判定，与玩家当前所在场景无关。</summary>
        public bool IsMachiInStudio()
        {
            if (isMachiInStudioForced || IsMachiCalledToStudio)
                return true;

            GameSceneData studioScene = LubanManager.Instance.TbGameSceneData.GetOrDefault(StudioSceneId);
            PlayerData playerData = GameDataManager.Instance.PlayerData;
            
            ShowRuleWeekType weekType = playerData.GetWeekType();
            ShowRuleTimeType timeType = playerData.GetTimeType();
            for (int i = 0; i < studioScene.ActiveNpcID.Count; i++)
            {
                NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(studioScene.ActiveNpcID[i]);
                if (npcData == null || npcData.CharacterData != CharaIdSet1.Machi)
                    continue;
                if (npcData.WeekType.HasFlag(weekType) && npcData.AppearanceTime.HasFlag(timeType))
                    return true;
            }

            return false;
        }

        /// <summary>本时段马吉是否已被催稿叫回工作室；状态存在角色系统的驻场覆盖里，过时段自动失效。</summary>
        public bool IsMachiCalledToStudio =>
            CharacterManager.Instance.IsCharacterOverriddenToScene(CharaIdSet1.Machi, StudioSceneId);

        /// <summary>NPC 交互「催稿」：在工作室内直接开面板；在工作室外先让马吉说一句，关掉对话后她自己回工作室（玩家留在原地）。</summary>
        public void OnRushInteract()
        {
            if (GameSceneManager.Instance.GameSceneData?.SceneID == StudioSceneId)
            {
                UISystem.Instance.OpenUI(UIPanelIdSet.MachiRoomGamePanel);
                return;
            }

            long[] dialogueIds = config.RushCallDialogueIds;
        }

        /// <summary>把马吉瞬移回工作室，本时段有效。</summary>
        public void CallMachiToStudio()
        {
            if (IsMachiInStudio())
                return;

            CharacterManager.Instance.SetCharacterSceneOverride(CharaIdSet1.Machi, StudioSceneId);
            TriggerCreationChanged();
        }

        public bool CanStartDraft(int inspirationCost)
        {
            return CanStartDraft(inspirationCost, out _);
        }

        public bool CanStartDraft(
            int inspirationCost,
            out MachiRoomDraftActionResult result)
        {
            result = GetStartDraftResult(inspirationCost);
            return result == MachiRoomDraftActionResult.Success;
        }

        public void StartDraft(int inspirationCost)
        {
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, inspirationCost);
            SetDraft(GetRandomDraft(inspirationCost), inspirationCost);
        }

        public bool CanAbandonDraft()
        {
            return curCeationInfo.State == MachiRoomCreationState.DraftReady
                && !GetManuscriptItemData(curCeationInfo.ManuscriptItemId).IsSpecial;
        }

        public void AbandonDraft()
        {
            if (!CanAbandonDraft())
                return;

            curCeationInfo = new MachiRoomCreationInfo();
            TriggerCreationChanged();
        }

        /// <summary>催稿进程中能否放弃出图：特殊稿件不允许放弃，与草稿阶段一致。</summary>
        public bool CanAbandonPainting()
        {
            return curCeationInfo.State == MachiRoomCreationState.Painting
                && !GetManuscriptItemData(curCeationInfo.ManuscriptItemId).IsSpecial;
        }

        /// <summary>放弃出图：丢弃当前进度回到空闲态，已消耗的灵感/行动力不返还。</summary>
        public void AbandonPainting()
        {
            if (!CanAbandonPainting())
                return;

            curCeationInfo = new MachiRoomCreationInfo();
            TriggerCreationChanged();
        }

        public bool CanRerollDraft()
        {
            return CanRerollDraft(out _);
        }

        public bool CanRerollDraft(out MachiRoomDraftActionResult result)
        {
            result = GetRerollDraftResult();
            return result == MachiRoomDraftActionResult.Success;
        }

        public void StartRerollDraft()
        {
            int inspirationCost = curCeationInfo.DraftInspirationCost;
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, inspirationCost);
            SetDraft(GetRandomDraft(inspirationCost), inspirationCost);
        }

        public bool CanStartPainting()
        {
            return curCeationInfo.State == MachiRoomCreationState.DraftReady && IsMachiInStudio();
        }

        public void StartPainting()
        {
            if (!CanStartPainting())
                return;

            curCeationInfo.State = MachiRoomCreationState.Painting;
            // 开画当前所在时段不算自然增长，从下一个时段开始
            curCeationInfo.LastNaturalProgressSlotIndex = GetTimeSlotIndex();
            TriggerCreationChanged();
        }

        public bool CanRushPainting()
        {
            return CanRushPainting(out _);
        }

        public bool CanRushPainting(out MachiRoomDraftActionResult result)
        {
            result = GetRushPaintingResult();
            return result == MachiRoomDraftActionResult.Success;
        }

        /// <summary>压力是否已满：满压后不能再催稿。</summary>
        public bool IsPressureFull()
        {
            var pressureData = GameDataManager.Instance.GetPropertyData(PropertyType.MachiPressure);
            return pressureData.NumberLimit > 0
                && GameDataManager.Instance.GetProperty(PropertyType.MachiPressure).Value
                    >= pressureData.NumberLimit;
        }

        MachiRoomDraftActionResult GetRushPaintingResult()
        {
            if (curCeationInfo.State != MachiRoomCreationState.Painting)
                return MachiRoomDraftActionResult.InvalidState;
            if (!IsMachiInStudio())
                return MachiRoomDraftActionResult.MachiNotInStudio;
            if (IsPressureFull())
                return MachiRoomDraftActionResult.PressureFull;
            if (!GameDataManager.Instance.HasProperty(PropertyType.ActionPointsValue, config.RushActionPointCost))
                return MachiRoomDraftActionResult.ActionPointNotEnough;
            if (GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value <= 0)
                return MachiRoomDraftActionResult.InspirationNotEnough;
            return MachiRoomDraftActionResult.Success;
        }

        public void StartRushPainting()
        {
            if (!CanRushPainting())
                return;

            // 先结清消耗（行动力、灵感、压力），再推进度，避免进度打满触发结算时消耗还没落账
            int inspiration = GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value;
            GameDataManager.Instance.RemoveProperty(
                PropertyType.ActionPointsValue,
                config.RushActionPointCost);
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, config.RushInspirationCost);
            GameDataManager.Instance.AddProperty(PropertyType.MachiPressure, config.RushPressureAdd);

            // 进度按扣减前的灵感 ÷ 2，评分判定用扣减后的灵感
            AddPaintingProgress(
                inspiration / config.RushProgressInspirationDivisor,
                GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value);
        }

        public void CollectArtwork()
        {
            if (curCeationInfo.State != MachiRoomCreationState.Completed)
                return;

            curCeationInfo = new ();
            TriggerCreationChanged();
        }

        public void AddCreationChangedListener(Action<MachiRoomCreationInfo> callback)
        {
            onCreationChanged += callback;
            callback?.Invoke(curCeationInfo);
        }

        public void RemoveCreationChangedListener(Action<MachiRoomCreationInfo> callback)
        {
            onCreationChanged -= callback;
        }

        /// <summary>把天数+时段折成单调递增的序号，用来判定是否真的跨过了一个时段。</summary>
        static int GetTimeSlotIndex()
        {
            // 新建存档时 PlayerData 可能还没建立
            PlayerData playerData = GameDataManager.Instance.PlayerData;
            if (playerData == null)
                return -1;
            return playerData.Day * TimeSlotCountPerDay + (int)playerData.TimeSlot;
        }

        void OnTimeSlotChanged(TimeSlot timeSlot)
        {
            if (curCeationInfo.State != MachiRoomCreationState.Painting)
                return;

            // 同一时段内的重复回调（反注册回调、面板重开等）不再重复增长
            int slotIndex = GetTimeSlotIndex();
            if (slotIndex == curCeationInfo.LastNaturalProgressSlotIndex)
                return;
            curCeationInfo.LastNaturalProgressSlotIndex = slotIndex;

            if (!IsMachiInStudio())
            {
                TriggerCreationChanged();
                return;
            }

            // 与催稿一致：压力满了不再推进度
            if (IsPressureFull())
            {
                TriggerCreationChanged();
                return;
            }

            // 没灵感就不自然增长（与催稿一致，只是不消耗行动力）
            int inspiration = GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value;
            if (inspiration <= 0)
            {
                TriggerCreationChanged();
                return;
            }

            // 与催稿一致：先结清灵感/压力消耗，再推进度
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, config.RushInspirationCost);
            GameDataManager.Instance.AddProperty(PropertyType.MachiPressure, config.RushPressureAdd);

            // 进度按扣减前的灵感算，评分判定用扣减后的灵感
            AddPaintingProgress(
                inspiration / config.NaturalProgressInspirationDivisor,
                GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value);
        }

        MachiRoomDraftActionResult GetStartDraftResult(int inspirationCost)
        {
            if (curCeationInfo.State != MachiRoomCreationState.Idle)
                return MachiRoomDraftActionResult.InvalidState;
            if (!IsMachiInStudio())
                return MachiRoomDraftActionResult.MachiNotInStudio;
            if (!config.IsDraftInspirationCost(inspirationCost))
                return MachiRoomDraftActionResult.InvalidInspirationCost;
            if (!GameDataManager.Instance.HasProperty(PropertyType.MachiInspire, inspirationCost))
                return MachiRoomDraftActionResult.InspirationNotEnough;
            return MachiRoomDraftActionResult.Success;
        }

        MachiRoomDraftActionResult GetRerollDraftResult()
        {
            if (curCeationInfo.State != MachiRoomCreationState.DraftReady)
                return MachiRoomDraftActionResult.InvalidState;
            if (GetManuscriptItemData(curCeationInfo.ManuscriptItemId).IsSpecial)
                return MachiRoomDraftActionResult.SpecialDraftLocked;
            if (!IsMachiInStudio())
                return MachiRoomDraftActionResult.MachiNotInStudio;
            if (!GameDataManager.Instance.HasProperty(
                    PropertyType.MachiInspire,
                    curCeationInfo.DraftInspirationCost))
                return MachiRoomDraftActionResult.InspirationNotEnough;
            return MachiRoomDraftActionResult.Success;
        }

        ManuscriptItemData GetRandomDraft(int inspirationCost)
        {
            float totalWeight = 0f;
            for (int i = 0; i < manuscriptItemDataList.Count; i++)
                totalWeight += config.GetDraftProbability(manuscriptItemDataList[i], inspirationCost);

            float randomWeight = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < manuscriptItemDataList.Count; i++)
            {
                randomWeight -= config.GetDraftProbability(manuscriptItemDataList[i], inspirationCost);
                if (randomWeight <= 0f)
                    return manuscriptItemDataList[i];
            }

            return manuscriptItemDataList[^1];
        }

        void SetDraft(ManuscriptItemData manuscriptData, int inspirationCost)
        {
            curCeationInfo.State = MachiRoomCreationState.DraftReady;
            curCeationInfo.ManuscriptItemId = manuscriptData.ItemID;
            curCeationInfo.DraftInspirationCost = inspirationCost;
            curCeationInfo.Progress = 0f;
            curCeationInfo.Score = manuscriptData.BaseScore;
            TriggerCreationChanged();
        }

        void AddPaintingProgress(float progress, int inspiration)
        {
            if (progress <= 0f)
                return;

            curCeationInfo.Progress = Mathf.Min(
                config.CompleteProgress,
                curCeationInfo.Progress + progress);

            if (inspiration <= config.LowInspirationThreshold)
                curCeationInfo.Score = Mathf.Max(
                    0,
                    curCeationInfo.Score - config.LowInspirationScorePenalty);

            if (curCeationInfo.Progress >= config.CompleteProgress)
                EndPainting();
            else
                TriggerCreationChanged();
        }

        void EndPainting()
        {
            curCeationInfo.State = MachiRoomCreationState.Completed;
            // 奖励就是当前这张画稿本身，评分达标直接进物品栏
            if (IsRewardEarned)
                InventoryManager.Instance.AddItem(curCeationInfo.ManuscriptItemId, 1);

            TriggerCreationChanged();
        }

        void TriggerCreationChanged()
        {
            SaveGameManager.Instance.Save();
            onCreationChanged?.Invoke(curCeationInfo);
        }
        public string GetDraftImagePath()
        {
            return DraftImagePath + manuscriptItemDataTable[curCeationInfo.ManuscriptItemId].DraftImageName;
        }
        public string GetFinalImagePath()
        {
            return FinalImagePath + manuscriptItemDataTable[curCeationInfo.ManuscriptItemId].FinalImageName;
        }
        #region Save
        public void SaveData(GameSaveData data)
        {
            data.MachiRoomCreation = new MachiRoomCreationSaveData
            {
                State = curCeationInfo.State,
                ManuscriptItemId = curCeationInfo.ManuscriptItemId,
                DraftInspirationCost = curCeationInfo.DraftInspirationCost,
                Progress = curCeationInfo.Progress,
                Score = curCeationInfo.Score,
                LastNaturalProgressSlotIndex = curCeationInfo.LastNaturalProgressSlotIndex,
            };
        }

        public void LoadData(GameSaveData data)
        {
            MachiRoomCreationSaveData saveData = data.MachiRoomCreation;
            curCeationInfo = saveData == null
                ? new MachiRoomCreationInfo()
                : new MachiRoomCreationInfo
                {
                    State = saveData.State,
                    ManuscriptItemId = saveData.ManuscriptItemId,
                    DraftInspirationCost = saveData.DraftInspirationCost,
                    Progress = saveData.Progress,
                    Score = saveData.Score,
                    LastNaturalProgressSlotIndex = saveData.LastNaturalProgressSlotIndex,
                };
            // 「叫回工作室」的状态存在 CharacterManager 的驻场覆盖里，这里不用管
            onCreationChanged?.Invoke(curCeationInfo);
        }
        #endregion
    }

    public enum MachiRoomCreationState
    {
        Idle,
        DraftReady,
        Painting,
        Completed,
    }

    public enum MachiRoomDraftActionResult
    {
        Success,
        InvalidState,
        MachiNotInStudio,
        InvalidInspirationCost,
        InspirationNotEnough,
        SpecialDraftLocked,
        ActionPointNotEnough,
        PressureFull,
    }

    [Serializable]
    public class MachiRoomCreationInfo
    {
        public MachiRoomCreationState State;
        public long ManuscriptItemId;
        public int DraftInspirationCost;
        public float Progress;
        public int Score;
        /// <summary>已结算过自然增长的时段序号（天数*4+时段），用于保证一个时段只长一次。</summary>
        public int LastNaturalProgressSlotIndex = -1;
    }

    public partial class GameSaveData
    {
        [LabelText("马吉画室创作数据")]
        public MachiRoomCreationSaveData MachiRoomCreation = new();
    }

    [Serializable]
    public class MachiRoomCreationSaveData
    {
        public MachiRoomCreationState State;
        public long ManuscriptItemId;
        public int DraftInspirationCost;
        public float Progress;
        public int Score;
        public int LastNaturalProgressSlotIndex = -1;
    }
}
