using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    public sealed class MachiRoomGameManager : MonoSingleton<MachiRoomGameManager>, ISaveable
    {
        [LabelText("玩法配置"), SerializeField] MachiRoomGameConfig config;
        [ShowInInspector, ReadOnly] MachiRoomCreationInfo creationInfo = new();
        
        IReadOnlyDictionary<long, ManuscriptItemData> manuscriptItemDataTable;
        IReadOnlyList<ManuscriptItemData> manuscriptItemDataList;
        Action<MachiRoomCreationInfo> onCreationChanged;
        bool canHandleTimeSlotChange;
        public string GUID => "MachiRoomGameManager";
        public MachiRoomGameConfig Config => config;
        public MachiRoomCreationInfo CreationInfo => creationInfo;
        public IReadOnlyDictionary<long, ManuscriptItemData> ManuscriptItemDataTable => manuscriptItemDataTable;
        const string k_NamespaceName = "MachiRoom";

        const string IconPath = "Assets/AddressableAssets/Remote/Texture2D/UI/MachiRoom/Draft/";
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

        public bool IsMachiInStudio()
        {
            return true;
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
            return creationInfo.State == MachiRoomCreationState.DraftReady
                && !GetManuscriptItemData(creationInfo.ManuscriptItemId).IsSpecial;
        }

        public void AbandonDraft()
        {
            if (!CanAbandonDraft())
                return;

            creationInfo = new MachiRoomCreationInfo();
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
            int inspirationCost = creationInfo.DraftInspirationCost;
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, inspirationCost);
            SetDraft(GetRandomDraft(inspirationCost), inspirationCost);
        }

        public bool CanStartPainting()
        {
            return creationInfo.State == MachiRoomCreationState.DraftReady && IsMachiInStudio();
        }

        public void StartPainting()
        {
            if (!CanStartPainting())
                return;

            creationInfo.State = MachiRoomCreationState.Painting;
            TriggerCreationChanged();
        }

        public bool CanRushPainting()
        {
            return creationInfo.State == MachiRoomCreationState.Painting
                && IsMachiInStudio()
                && GameDataManager.Instance.HasProperty(
                    PropertyType.ActionPointsValue,
                    config.RushActionPointCost)
                && GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value > 0;
        }

        public void StartRushPainting()
        {
            if (!CanRushPainting())
                return;

            int inspiration = GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value;
            GameDataManager.Instance.RemoveProperty(PropertyType.MachiInspire, config.RushInspirationCost);
            GameDataManager.Instance.AddProperty(PropertyType.MachiPressure, config.RushPressureAdd);

            AddPaintingProgress(
                inspiration / config.RushProgressInspirationDivisor,
                GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value);

            GameDataManager.Instance.RemoveProperty(
                PropertyType.ActionPointsValue,
                config.RushActionPointCost);
        }

        public void CollectArtwork()
        {
            if (creationInfo.State != MachiRoomCreationState.Completed)
                return;

            creationInfo = new MachiRoomCreationInfo();
            TriggerCreationChanged();
        }

        public void AddCreationChangedListener(Action<MachiRoomCreationInfo> callback)
        {
            onCreationChanged += callback;
            callback?.Invoke(creationInfo);
        }

        public void RemoveCreationChangedListener(Action<MachiRoomCreationInfo> callback)
        {
            onCreationChanged -= callback;
        }

        void OnTimeSlotChanged(TimeSlot timeSlot)
        {
            if (!canHandleTimeSlotChange)
                return;

            if (creationInfo.State != MachiRoomCreationState.Painting || !IsMachiInStudio())
                return;

            int inspiration = GameDataManager.Instance.GetProperty(PropertyType.MachiInspire).Value;
            AddPaintingProgress(
                inspiration / config.NaturalProgressInspirationDivisor,
                inspiration);
        }

        MachiRoomDraftActionResult GetStartDraftResult(int inspirationCost)
        {
            if (creationInfo.State != MachiRoomCreationState.Idle)
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
            if (creationInfo.State != MachiRoomCreationState.DraftReady)
                return MachiRoomDraftActionResult.InvalidState;
            if (GetManuscriptItemData(creationInfo.ManuscriptItemId).IsSpecial)
                return MachiRoomDraftActionResult.SpecialDraftLocked;
            if (!IsMachiInStudio())
                return MachiRoomDraftActionResult.MachiNotInStudio;
            if (!GameDataManager.Instance.HasProperty(
                    PropertyType.MachiInspire,
                    creationInfo.DraftInspirationCost))
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
            creationInfo.State = MachiRoomCreationState.DraftReady;
            creationInfo.ManuscriptItemId = manuscriptData.ItemID;
            creationInfo.DraftInspirationCost = inspirationCost;
            creationInfo.Progress = 0f;
            creationInfo.Score = manuscriptData.BaseScore;
            creationInfo.RewardItemId = 0;
            TriggerCreationChanged();
        }

        void AddPaintingProgress(float progress, int inspiration)
        {
            if (progress <= 0f)
                return;

            creationInfo.Progress = Mathf.Min(
                config.CompleteProgress,
                creationInfo.Progress + progress);

            if (inspiration <= config.LowInspirationThreshold)
                creationInfo.Score = Mathf.Max(
                    0,
                    creationInfo.Score - config.LowInspirationScorePenalty);

            if (creationInfo.Progress >= config.CompleteProgress)
                EndPainting();
            else
                TriggerCreationChanged();
        }

        void EndPainting()
        {
            creationInfo.State = MachiRoomCreationState.Completed;
            if (creationInfo.Score >= config.RewardMinScore)
            {
                creationInfo.RewardItemId = creationInfo.ManuscriptItemId;
                InventoryManager.Instance.AddItem(creationInfo.RewardItemId, 1);
            }

            TriggerCreationChanged();
        }

        void TriggerCreationChanged()
        {
            SaveGameManager.Instance.Save();
            onCreationChanged?.Invoke(creationInfo);
        }
        public string GetIconPath()
        {
            return IconPath + manuscriptItemDataTable[creationInfo.ManuscriptItemId].IconName;
        }
        public void SaveData(GameSaveData data)
        {
            data.MachiRoomCreation = new MachiRoomCreationSaveData
            {
                State = creationInfo.State,
                ManuscriptItemId = creationInfo.ManuscriptItemId,
                DraftInspirationCost = creationInfo.DraftInspirationCost,
                Progress = creationInfo.Progress,
                Score = creationInfo.Score,
                RewardItemId = creationInfo.RewardItemId,
            };
        }

        public void LoadData(GameSaveData data)
        {
            MachiRoomCreationSaveData saveData = data.MachiRoomCreation;
            creationInfo = saveData == null
                ? new MachiRoomCreationInfo()
                : new MachiRoomCreationInfo
                {
                    State = saveData.State,
                    ManuscriptItemId = saveData.ManuscriptItemId,
                    DraftInspirationCost = saveData.DraftInspirationCost,
                    Progress = saveData.Progress,
                    Score = saveData.Score,
                    RewardItemId = saveData.RewardItemId,
                };
            onCreationChanged?.Invoke(creationInfo);
        }
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
    }

    [Serializable]
    public class MachiRoomCreationInfo
    {
        public MachiRoomCreationState State;
        public long ManuscriptItemId;
        public int DraftInspirationCost;
        public float Progress;
        public int Score;
        public long RewardItemId;
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
        public long RewardItemId;
    }
}
