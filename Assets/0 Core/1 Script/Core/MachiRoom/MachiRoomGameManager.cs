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
        bool canHandleTimeSlotChange;
        public string GUID => "MachiRoomGameManager";
        public MachiRoomGameConfig Config => config;
        public MachiRoomCreationInfo CreationInfo => curCeationInfo;
        public IReadOnlyDictionary<long, ManuscriptItemData> ManuscriptItemDataTable => manuscriptItemDataTable;
        /// <summary>当前评分是否够格发奖，奖励就是当前这张画稿物品本身。</summary>
        public bool IsRewardEarned => curCeationInfo.Score >= config.RewardMinScore;
        const string DraftImagePath = "Assets/AddressableAssets/Remote/Texture2D/UI/MachiRoom/Draft/";
        const string FinalImagePath = "Assets/AddressableAssets/Remote/Texture2D/UI/MachiRoom/FinalImage/";
        const long StudioSceneId = 10020;
        void Start()
        {
            ((ISaveable)this).RegisterSaveable();
            GameDataManager.Instance.RegisterPlayerDataTimeSlotChangeNoInvoke(OnTimeSlotChanged);
            canHandleTimeSlotChange = true;
            
            manuscriptItemDataTable = LubanManager.Instance.TbManuscriptItemData.DataMap;
            manuscriptItemDataList = LubanManager.Instance.TbManuscriptItemData.DataList;
        }

        protected override void OnDestroy()
        {
            canHandleTimeSlotChange = false;
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
            if (isMachiInStudioForced)
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

        MachiRoomDraftActionResult GetRushPaintingResult()
        {
            if (curCeationInfo.State != MachiRoomCreationState.Painting)
                return MachiRoomDraftActionResult.InvalidState;
            if (!IsMachiInStudio())
                return MachiRoomDraftActionResult.MachiNotInStudio;
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

        void OnTimeSlotChanged(TimeSlot timeSlot)
        {
            if (!canHandleTimeSlotChange)
                return;

            if (curCeationInfo.State != MachiRoomCreationState.Painting || !IsMachiInStudio())
                return;

            int inspiration = GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value;
            AddPaintingProgress(
                inspiration / config.NaturalProgressInspirationDivisor,
                inspiration);
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
                };
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
    }

    [Serializable]
    public class MachiRoomCreationInfo
    {
        public MachiRoomCreationState State;
        public long ManuscriptItemId;
        public int DraftInspirationCost;
        public float Progress;
        public int Score;
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
    }
}
