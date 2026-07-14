using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;
using Random = UnityEngine.Random;

public class CharacterManager : MonoSingleton<CharacterManager>,ISaveable
{
    [FoldoutGroup("Runtime"),ReadOnly,LabelText("角色背包配置表"),ShowInInspector]
    public List<CharacterBag> UserCharacterBags { get; private set; }

    #region Bindings
    
    public void Initialize()
    {
        DialogueFunctionHandler dialogueFunctionHandler = new ();
        Register(dialogueFunctionHandler);
        
        GoodwillFunctionHandler goodwillFunctionHandler = new ();
        Register(goodwillFunctionHandler);
        
        GiftGivingFunctionHandler giftGivingFunctionHandler = new ();
        Register(giftGivingFunctionHandler);
        
        KitchenFunctionHandler kitchenFunctionHandler = new ();
        Register(kitchenFunctionHandler);
        
        ClawMachineFunctionHandler clawMachineFunctionHandler  = new ();
        Register(clawMachineFunctionHandler);
        
        ExplosiveGamesFunctionHandler explosiveGamesFunctionHandler = new ();
        Register(explosiveGamesFunctionHandler);
        
        WitchPoisonFunctionHandler witchPoisonFunctionHandler = new ();
        Register(witchPoisonFunctionHandler);
        
        ExhibitionFunctionHandler exhibitionFunctionHandler = new ();
        Register(exhibitionFunctionHandler);
        
        ManuscriptFunctionHandler manuscriptFunctionHandler = new ();
        Register(manuscriptFunctionHandler);
        
        SupermarketFunctionHandler supermarketFunctionHandler  = new ();
        Register(supermarketFunctionHandler);
        
        FruitShopFunctionHandler fruitShopFunctionHandler = new ();
        Register(fruitShopFunctionHandler);
        
        FabricStoreFunctionHandler fabricStoreFunctionHandler = new ();
        Register(fabricStoreFunctionHandler);

        SexToyStoreFunctionHandler sexToyStoreFunctionHandler = new ();
        Register(sexToyStoreFunctionHandler);

        FishingFunctionHandler fishingFunctionHandler = new ();
        Register(fishingFunctionHandler);
        
        FishingBaitShopFunctionHandler fishingBaitShopFunctionHandler =  new ();
        Register(fishingBaitShopFunctionHandler);

        PhotographyFunctionHandler photographyFunctionHandler = new ();
        Register(photographyFunctionHandler);

        CoffeeShopFunctionHandler coffeeShopFunctionHandler = new ();
        Register(coffeeShopFunctionHandler);

        BarFunctionHandler barFunctionHandler = new ();
        Register(barFunctionHandler);

        FactoryProductionFunctionHandler factoryProductionFunctionHandler = new ();
        Register(factoryProductionFunctionHandler);
        
        ShopHelpEnterFunctionHandler shopHelpEnterFunctionHandler = new();
        Register(shopHelpEnterFunctionHandler);
    }

    public void Release()
    {
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameManager.IsInitialized)
        {
            GameManager.Instance.OnEnterGame -= Initialize;
            GameManager.Instance.OnExitGame -= Release;
        }
    }

    #endregion
    
    #region ISaveable

    public void Start()
    {
        GameManager.Instance.OnEnterGame += Initialize;
        GameManager.Instance.OnExitGame += Release;
        ((ISaveable)this).RegisterSaveable();
    }

    public string GUID => "CharacterManager";
    public void SaveData(GameSaveData data)
    {
        data.CharacterBags = new List<CharacterBag>(UserCharacterBags);
        data.NpcSpawnSaveDataList = CloneNpcSpawnSaveDataList(npcSpawnResults);
    }

    public void LoadData(GameSaveData GameSave)
    {
        if (GameSave != null)
        {
            UserCharacterBags = new List<CharacterBag>();
            if (GameSave.CharacterBags is { Count: > 0 })
            {
                UserCharacterBags = new List<CharacterBag>();
                for (int i = 0; i < GameSave.CharacterBags.Count; i++)
                {
                    CharacterBag characterBag = new CharacterBag();
                    characterBag.CharacterID = GameSave.CharacterBags[i].CharacterID;
                    characterBag.Favorability = GameSave.CharacterBags[i].Favorability;
                    characterBag.Feeling = GameSave.CharacterBags[i].Feeling;
                    UserCharacterBags.Add(characterBag);
                }
            }
            else
            {
                UserCharacterBags = new List<CharacterBag>();
                for (int i = 0; i < LubanManager.Instance.TbCharacterData.DataList.Count; i++)
                {
                    CharacterBag characterBag = new CharacterBag();
                    characterBag.CharacterID = LubanManager.Instance.TbCharacterData.DataList[i].ID;
                    characterBag.Favorability = 0;
                    characterBag.Feeling = 0;
                    UserCharacterBags.Add(characterBag);
                }
            }

            RestoreNpcSpawnSaveData(GameSave.NpcSpawnSaveDataList);
        }
    }

    #endregion

    #region CURD
    

    public CharacterData GetCharacterDataByID(long characterID )
    {
        return LubanManager.Instance.TbCharacterData.Get(characterID);
    }

    public NpcData GetNpcDataByID(long npcID)
    {
        return LubanManager.Instance.TbNpcData.Get(npcID);
    }

    public CharacterBag GetCharacterBag(long characterID)
    {
        if (UserCharacterBags.Any(t => t.CharacterID == characterID))
        {
            return UserCharacterBags.First(t => t.CharacterID == characterID);
        }

        return null;
    }

    /// <summary>
    /// 修改角色的好感度
    /// </summary>
    /// <param name="characterID"></param>
    /// <param name="value"></param>
    public void SetCharacterFavorability(long characterID,int value)
    {
        CharacterBag characterBag = UserCharacterBags.Find(x => x.CharacterID == characterID);
        if (characterBag != null)
        {
            characterBag.Favorability = value;
            OnCharacterChanged?.Invoke(UserCharacterBags);
            SaveGameManager.Instance.Save();
        }
    }

    /// <summary>
    /// 修改角色的心情值
    /// </summary>
    /// <param name="characterID"></param>
    /// <param name="value"></param>
    public void SetCharacterFeeling(long characterID, int value)
    {
        CharacterBag characterBag = UserCharacterBags.Find(x => x.CharacterID == characterID);
        if (characterBag != null)
        {
            characterBag.Feeling = value;
            OnCharacterChanged?.Invoke(UserCharacterBags);
            if (GameDataManager.IsInitialized && GameDataManager.Instance.PlayerData != null)
            {
                //PlayerDataChange(GameDataManager.Instance.PlayerData);
            }
            SaveGameManager.Instance.Save();
        }
    }

    #endregion

    #region Event
    /// <summary>
    /// 用户角色背包变化回调
    /// </summary>
    public  Action<List<CharacterBag>> OnCharacterChanged;

    public void RegisterAllCharacterBagChange(Action<List<CharacterBag>> action,bool invokeImmediately =true)
    {
        OnCharacterChanged += action;
        if (invokeImmediately)
        {
            OnCharacterChanged?.Invoke(UserCharacterBags);
        }
    }

    public void UnregisterAllCharacterBagChange(Action<List<CharacterBag>> action)
    {
        OnCharacterChanged -= action;
        
    }

    #endregion
    
    #region 角色功能

            
    private Dictionary<FunctionGroup,ICharacterFunctionHandler> handlers = new();
    
    public void Register(ICharacterFunctionHandler handler)
    {
        handlers[handler.FunctionType] = handler;
    }

    public void Execute(FunctionGroup functionType, NpcData npcData)
    {
        if (handlers.TryGetValue(functionType, out var handler))
        {
            handler.Execute(npcData);
        }
    }

    #endregion

    #region 随机场景角色

    /// <summary>
    /// 当前存档里的随机 NPC 结果缓存。
    /// 这里缓存的是“已经随机出来的结果”，不是配置规则本身。
    /// 这样玩家在同一天同一时段读档时，看到的随机 NPC 能保持一致，不会靠读档反复刷新。
    /// </summary>
    private List<NpcSpawnSaveData> npcSpawnResults = new();

    /// <summary>
    /// 读档后的第一次场景刷新允许复用 EveryEnter 缓存。
    /// EveryEnter 正常含义是“每次进入场景重新随机”，但如果玩家在场景内保存再读档，
    /// 第一次恢复画面时应该尽量维持保存时看到的 NPC，避免读档瞬间换人。
    /// </summary>
    private bool useSavedEveryEnterResultOnce;

    /// <summary>
    /// 获取当前场景最终应该显示的 NPC。
    /// 结果由两部分组成：
    /// 1. GameSceneData.ActiveNpcID 配出来的固定 NPC；
    /// 2. GameSceneData.NpcSpawnGroupID 关联随机组后，根据角色好感/心情随机出来的 NPC。
    /// </summary>
    /// <param name="sceneData">当前小场景配置。</param>
    /// <param name="playerData">当前玩家时间数据，用于判断星期、时段和随机缓存范围。</param>
    /// <returns>当前场景要实例化显示的 NPC 配置列表。</returns>
    public List<NpcData> GetSceneNpcDataList(GameSceneData sceneData, PlayerData playerData)
    {
        List<NpcData> result = new();
        if (sceneData == null || playerData == null) return result;

        // 每次计算前先清理过期缓存，避免存档里积累上一天或上个时段的随机结果。
        RemoveExpiredNpcSpawnSaveData(playerData);

        HashSet<long> usedNpcIDs = new();
        HashSet<long> usedCharacterIDs = new();

        AddFixedSceneNpc(sceneData, playerData, result, usedNpcIDs, usedCharacterIDs);
        AddRandomSceneNpc(sceneData, playerData, result, usedNpcIDs, usedCharacterIDs);

        return result;
    }

    /// <summary>
    /// 添加场景固定 NPC。
    /// 固定 NPC 仍然使用 NpcData 自己配置的 WeekType / AppearanceTime 做显示过滤。
    /// </summary>
    private void AddFixedSceneNpc(
        GameSceneData sceneData,
        PlayerData playerData,
        List<NpcData> result,
        HashSet<long> usedNpcIDs,
        HashSet<long> usedCharacterIDs)
    {
        if (sceneData.ActiveNpcID == null) return;

        foreach (long npcID in sceneData.ActiveNpcID)
        {
            if (npcID <= 0) continue;

            NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(npcID);
            if (npcData == null)
            {
                Debug.LogWarning($"场景固定 NPC 不存在，SceneID: {sceneData.ID}, NpcID: {npcID}");
                continue;
            }

            if (!IsNpcTimeMatched(npcData, playerData)) continue;
            TryAddSceneNpc(npcData, result, usedNpcIDs, usedCharacterIDs);
        }
    }

    /// <summary>
    /// 根据场景挂载的随机组追加随机 NPC。
    /// 每个随机组会先查存档缓存；缓存命中时直接用缓存结果，缓存没有命中时才重新随机。
    /// </summary>
    private void AddRandomSceneNpc(
        GameSceneData sceneData,
        PlayerData playerData,
        List<NpcData> result,
        HashSet<long> usedNpcIDs,
        HashSet<long> usedCharacterIDs)
    {
        if (sceneData.NpcSpawnGroupID == null) return;

        bool useSavedEveryEnterResult = useSavedEveryEnterResultOnce;

        foreach (long groupID in sceneData.NpcSpawnGroupID)
        {
            if (groupID <= 0) continue;

            NpcSpawnGroupData groupData = LubanManager.Instance.TbNpcSpawnGroupData.GetOrDefault(groupID);
            if (groupData == null)
            {
                Debug.LogWarning($"场景随机 NPC 组不存在，SceneID: {sceneData.ID}, GroupID: {groupID}");
                continue;
            }

            List<long> selectedNpcIDs = GetOrCreateNpcSpawnResult(
                sceneData.ID,
                groupData,
                playerData,
                usedNpcIDs,
                usedCharacterIDs,
                useSavedEveryEnterResult);

            foreach (long npcID in selectedNpcIDs)
            {
                NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(npcID);
                if (npcData == null) continue;

                TryAddSceneNpc(npcData, result, usedNpcIDs, usedCharacterIDs);
            }
        }

        // EveryEnter 的读档缓存只消费一次，第一次恢复画面之后就回到“每次进场重新随机”的语义。
        if (useSavedEveryEnterResultOnce)
        {
            useSavedEveryEnterResultOnce = false;
        }
    }

    /// <summary>
    /// 获取某个随机组的最终结果。
    /// 如果当前刷新范围内已有缓存，就直接复用；否则按规则表重新筛选并随机，然后写入缓存。
    /// </summary>
    private List<long> GetOrCreateNpcSpawnResult(
        long sceneID,
        NpcSpawnGroupData groupData,
        PlayerData playerData,
        HashSet<long> usedNpcIDs,
        HashSet<long> usedCharacterIDs,
        bool useSavedEveryEnterResult)
    {
        NpcSpawnSaveData saveData = FindNpcSpawnSaveData(sceneID, groupData, playerData, useSavedEveryEnterResult);
        if (saveData?.SelectedNpcIDs != null)
        {
            return new List<long>(saveData.SelectedNpcIDs);
        }

        List<NpcSpawnCandidate> candidates = GetNpcSpawnCandidates(groupData, playerData, usedNpcIDs, usedCharacterIDs);
        List<long> selectedNpcIDs = SelectNpcSpawnResult(groupData, candidates);
        SaveNpcSpawnResult(sceneID, groupData, playerData, selectedNpcIDs);
        return selectedNpcIDs;
    }

    /// <summary>
    /// 从规则表中筛选当前可参与随机的候选 NPC。
    /// 条件由三部分组成：
    /// 1. 规则属于当前随机组；
    /// 2. NPC 自身满足当前星期和时段；
    /// 3. 规则绑定的 UnlockConditionsID 满足通用解锁条件。
    ///
    /// 注意：
    /// 好感度、心情值已经不再由随机 NPC 表单独配置，
    /// 而是统一走 UnlockConditionsData 的 Character 条件。
    /// 这样随机 NPC 后续也可以自然支持玩家属性、物品、日期等其他解锁条件。
    /// </summary>
    private List<NpcSpawnCandidate> GetNpcSpawnCandidates(
        NpcSpawnGroupData groupData,
        PlayerData playerData,
        HashSet<long> usedNpcIDs,
        HashSet<long> usedCharacterIDs)
    {
        List<NpcSpawnCandidate> candidates = new();

        foreach (NpcSpawnRuleData ruleData in LubanManager.Instance.TbNpcSpawnRuleData.DataList)
        {
            if (ruleData.GroupID != groupData.ID) continue;

            NpcData npcData = LubanManager.Instance.TbNpcData.GetOrDefault(ruleData.NpcID);
            if (npcData == null)
            {
                Debug.LogWarning($"随机 NPC 规则引用了不存在的 NPC，RuleID: {ruleData.ID}, NpcID: {ruleData.NpcID}");
                continue;
            }

            if (usedNpcIDs.Contains(npcData.Id)) continue;
            if (usedCharacterIDs.Contains(npcData.CharacterData)) continue;
            if (!IsNpcTimeMatched(npcData, playerData)) continue;
            if (!IsNpcSpawnUnlockMatched(ruleData)) continue;

            candidates.Add(new NpcSpawnCandidate(ruleData, npcData));
        }

        return candidates
            .OrderByDescending(candidate => candidate.RuleData.Priority)
            .ThenBy(candidate => candidate.RuleData.ID)
            .ToList();
    }

    /// <summary>
    /// 根据随机组的选择模式，从候选 NPC 中选出最终结果。
    /// All：全部候选都会出现；
    /// RandomOne：在最高优先级候选里随机一个；
    /// RandomCount：按优先级从高到低抽取指定数量。
    /// </summary>
    private List<long> SelectNpcSpawnResult(NpcSpawnGroupData groupData, List<NpcSpawnCandidate> candidates)
    {
        if (candidates == null || candidates.Count <= 0) return new List<long>();

        switch (groupData.SelectMode)
        {
            case SelectMode.All:
                return candidates.Select(candidate => candidate.NpcData.Id).ToList();
            case SelectMode.RandomOne:
                return SelectRandomNpcByPriority(candidates, 1);
            case SelectMode.RandomCount:
                return SelectRandomNpcByPriority(candidates, GetRandomCount(groupData, candidates.Count));
            default:
                Debug.LogWarning($"未处理的 NPC 随机模式，GroupID: {groupData.ID}, SelectMode: {groupData.SelectMode}");
                return new List<long>();
        }
    }

    /// <summary>
    /// 按优先级随机抽取 NPC。
    /// 优先级越高越先进入抽取池；同一优先级内才随机。
    /// 这样可以让配置里的 Priority 真正控制“谁更优先出现”，而不是完全无差别随机。
    /// </summary>
    private List<long> SelectRandomNpcByPriority(List<NpcSpawnCandidate> candidates, int count)
    {
        List<long> selectedNpcIDs = new();
        List<NpcSpawnCandidate> candidatePool = new(candidates);
        HashSet<long> selectedCharacterIDs = new();

        while (candidatePool.Count > 0 && selectedNpcIDs.Count < count)
        {
            int maxPriority = candidatePool.Max(candidate => candidate.RuleData.Priority);
            List<NpcSpawnCandidate> priorityCandidates = candidatePool
                .Where(candidate => candidate.RuleData.Priority == maxPriority)
                .ToList();

            NpcSpawnCandidate selectedCandidate = priorityCandidates[Random.Range(0, priorityCandidates.Count)];
            selectedNpcIDs.Add(selectedCandidate.NpcData.Id);
            selectedCharacterIDs.Add(selectedCandidate.NpcData.CharacterData);

            candidatePool.RemoveAll(candidate =>
                candidate.NpcData.Id == selectedCandidate.NpcData.Id
                || selectedCharacterIDs.Contains(candidate.NpcData.CharacterData));
        }

        return selectedNpcIDs;
    }

    /// <summary>
    /// 解析随机数量。
    /// SelectCount 小于等于 0 时，表示“不限制数量”，最终最多抽取全部候选。
    /// </summary>
    private int GetRandomCount(NpcSpawnGroupData groupData, int candidateCount)
    {
        if (groupData.SelectCount <= 0) return candidateCount;
        return Mathf.Min(groupData.SelectCount, candidateCount);
    }

    /// <summary>
    /// 判断 NPC 自身是否满足当前星期和时段。
    /// 随机 NPC 仍复用 NpcData 上已有的 WeekType / AppearanceTime，避免随机表重复配置时间条件。
    /// </summary>
    private bool IsNpcTimeMatched(NpcData npcData, PlayerData playerData)
    {
        return npcData.WeekType.HasFlag(playerData.GetWeekType())
               && npcData.AppearanceTime.HasFlag(playerData.GetTimeType());
    }

    /// <summary>
    /// 判断随机 NPC 规则绑定的通用解锁条件是否满足。
    /// UnlockConditionsID 小于等于 0 时表示不需要额外条件，直接通过。
    /// </summary>
    private bool IsNpcSpawnUnlockMatched(NpcSpawnRuleData ruleData)
    {
        if (ruleData.UnlockConditionsID <= 0) return true;

        UnlockConditionsData unlockData =
            LubanManager.Instance.TbUnlockConditionsData.GetOrDefault(ruleData.UnlockConditionsID);

        if (unlockData == null)
        {
            Debug.LogWarning($"随机 NPC 规则引用了不存在的解锁条件，RuleID: {ruleData.ID}, UnlockConditionsID: {ruleData.UnlockConditionsID}");
            return false;
        }

        return IsUnlockConditionsMatched(unlockData);
    }

    /// <summary>
    /// 判断一条通用解锁条件是否满足。
    /// 该方法复用 UnlockConditionsData 的 flags 设计：
    /// 同一条配置可以同时要求玩家属性、物品、角色属性和日期条件，
    /// 只有所有被勾选的条件都满足时，才认为解锁通过。
    /// </summary>
    private bool IsUnlockConditionsMatched(UnlockConditionsData unlockData)
    {
        if (unlockData == null) return false;
        if (unlockData.UnlockConditionsType.HasFlag(UnlockConditionsType.None)) return true;

        if (unlockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Prop)
            && !IsPropUnlockMatched(unlockData))
        {
            return false;
        }

        if (unlockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Item)
            && !IsItemUnlockMatched(unlockData))
        {
            return false;
        }

        if (unlockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Character)
            && !IsCharacterUnlockMatched(unlockData))
        {
            return false;
        }

        if (unlockData.UnlockConditionsType.HasFlag(UnlockConditionsType.Date)
            && !IsDateUnlockMatched(unlockData))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断玩家属性条件是否满足。
    /// 例如金币、体力、行动点等属性达到配置数量时通过。
    /// </summary>
    private bool IsPropUnlockMatched(UnlockConditionsData unlockData)
    {
        if (!GameDataManager.IsInitialized || GameDataManager.Instance.PlayerData == null) return false;

        PropertyBag propertyBag = GameDataManager.Instance.GetProperty(unlockData.TbUlocakPropData.PropType);
        return propertyBag != null && propertyBag.Value >= unlockData.TbUlocakPropData.Value;
    }

    /// <summary>
    /// 判断物品条件是否满足。
    /// 当背包内指定物品数量达到配置数量时通过。
    /// </summary>
    private bool IsItemUnlockMatched(UnlockConditionsData unlockData)
    {
        if (!InventoryManager.IsInitialized) return false;

        return InventoryManager.Instance.GetItemCount(unlockData.TbUlocakItemData.ItemID)
               >= unlockData.TbUlocakItemData.Value;
    }

    /// <summary>
    /// 判断角色属性条件是否满足。
    /// CharacterPropType.Feeling 对应角色心情值；
    /// CharacterPropType.Goodwill 对应角色好感度。
    /// </summary>
    private bool IsCharacterUnlockMatched(UnlockConditionsData unlockData)
    {
        CharacterBag characterBag = GetCharacterBag(unlockData.TbUlockCharacterData.CharacterID);
        if (characterBag == null) return false;

        switch (unlockData.TbUlockCharacterData.CharacterType)
        {
            case CharacterPropType.Feeling:
                return characterBag.Feeling >= unlockData.TbUlockCharacterData.Value;
            case CharacterPropType.Goodwill:
                return characterBag.Favorability >= unlockData.TbUlockCharacterData.Value;
            default:
                Debug.LogWarning($"未处理的角色解锁属性类型: {unlockData.TbUlockCharacterData.CharacterType}");
                return false;
        }
    }

    /// <summary>
    /// 判断日期条件是否满足。
    /// 当前玩家周几和时间段都在配置 flags 内时通过。
    /// </summary>
    private bool IsDateUnlockMatched(UnlockConditionsData unlockData)
    {
        if (!GameDataManager.IsInitialized || GameDataManager.Instance.PlayerData == null) return false;

        PlayerData playerData = GameDataManager.Instance.PlayerData;
        return unlockData.TbUlockDateData.WeekFlag.HasFlag(playerData.GetWeekType())
               && unlockData.TbUlockDateData.TimeFlag.HasFlag(playerData.GetTimeType());
    }

    /// <summary>
    /// 向最终显示列表添加 NPC，并同步记录已使用的 NPC 和真实角色。
    /// 同一个 NpcData 或同一个 CharacterData 在同一场景内只显示一次，避免固定 NPC 和随机 NPC 重复刷出同一个人。
    /// </summary>
    private bool TryAddSceneNpc(
        NpcData npcData,
        List<NpcData> result,
        HashSet<long> usedNpcIDs,
        HashSet<long> usedCharacterIDs)
    {
        if (npcData == null) return false;
        if (usedNpcIDs.Contains(npcData.Id)) return false;
        if (usedCharacterIDs.Contains(npcData.CharacterData)) return false;

        result.Add(npcData);
        usedNpcIDs.Add(npcData.Id);
        usedCharacterIDs.Add(npcData.CharacterData);
        return true;
    }

    /// <summary>
    /// 在当前刷新范围内查找已经随机出的缓存结果。
    /// TimeSlot：同一天同一时段复用；
    /// Day：同一天复用；
    /// EveryEnter：正常不复用，只有读档后的第一次场景刷新会复用一次。
    /// </summary>
    private NpcSpawnSaveData FindNpcSpawnSaveData(
        long sceneID,
        NpcSpawnGroupData groupData,
        PlayerData playerData,
        bool useSavedEveryEnterResult)
    {
        if (groupData.RefreshType == RefreshType.EveryEnter && !useSavedEveryEnterResult)
        {
            return null;
        }

        return npcSpawnResults.FirstOrDefault(saveData =>
            IsSameNpcSpawnScope(saveData, sceneID, groupData, playerData));
    }

    /// <summary>
    /// 保存一组随机结果。
    /// 保存前会先移除同一刷新范围内的旧结果，保证同一个场景/随机组/刷新范围只保留一份缓存。
    /// </summary>
    private void SaveNpcSpawnResult(
        long sceneID,
        NpcSpawnGroupData groupData,
        PlayerData playerData,
        List<long> selectedNpcIDs)
    {
        npcSpawnResults.RemoveAll(saveData => IsSameNpcSpawnScope(saveData, sceneID, groupData, playerData));
        npcSpawnResults.Add(new NpcSpawnSaveData
        {
            SceneID = sceneID,
            GroupID = groupData.ID,
            Day = playerData.Day,
            Time = playerData.EnvironmentMode,
            SelectedNpcIDs = selectedNpcIDs != null ? new List<long>(selectedNpcIDs) : new List<long>()
        });
    }

    /// <summary>
    /// 判断一条缓存是否属于当前随机组的同一刷新范围。
    /// 注意：缓存数据自身不保存 RefreshType，刷新方式始终以当前表格配置为准。
    /// </summary>
    private bool IsSameNpcSpawnScope(
        NpcSpawnSaveData saveData,
        long sceneID,
        NpcSpawnGroupData groupData,
        PlayerData playerData)
    {
        if (saveData == null) return false;
        if (saveData.SceneID != sceneID || saveData.GroupID != groupData.ID) return false;
        if (saveData.Day != playerData.Day) return false;

        return groupData.RefreshType == RefreshType.Day
               || saveData.Time == playerData.EnvironmentMode;
    }

    /// <summary>
    /// 移除已经过期的随机缓存，避免存档越玩越大。
    /// 如果随机组配置已经被删除，对应缓存也会被清理掉。
    /// </summary>
    private void RemoveExpiredNpcSpawnSaveData(PlayerData playerData)
    {
        npcSpawnResults.RemoveAll(saveData =>
        {
            NpcSpawnGroupData groupData = LubanManager.Instance.TbNpcSpawnGroupData.GetOrDefault(saveData.GroupID);
            if (groupData == null) return true;
            if (saveData.Day != playerData.Day) return true;

            return groupData.RefreshType != RefreshType.Day
                   && saveData.Time != playerData.EnvironmentMode;
        });
    }

    /// <summary>
    /// 从存档恢复随机 NPC 缓存。
    /// 这里会深拷贝列表，避免存档对象和运行时对象共用同一份 List 引用。
    /// </summary>
    private void RestoreNpcSpawnSaveData(List<NpcSpawnSaveData> saveDataList)
    {
        npcSpawnResults = CloneNpcSpawnSaveDataList(saveDataList);
        useSavedEveryEnterResultOnce = true;
    }

    /// <summary>
    /// 克隆随机 NPC 缓存列表。
    /// 存档保存和读取都走这个方法，避免 SelectedNpcIDs 被外部逻辑误改。
    /// </summary>
    private List<NpcSpawnSaveData> CloneNpcSpawnSaveDataList(List<NpcSpawnSaveData> saveDataList)
    {
        List<NpcSpawnSaveData> clonedList = new();
        if (saveDataList == null) return clonedList;

        foreach (NpcSpawnSaveData saveData in saveDataList)
        {
            if (saveData == null) continue;

            clonedList.Add(new NpcSpawnSaveData
            {
                SceneID = saveData.SceneID,
                GroupID = saveData.GroupID,
                Day = saveData.Day,
                Time = saveData.Time,
                SelectedNpcIDs = saveData.SelectedNpcIDs != null
                    ? new List<long>(saveData.SelectedNpcIDs)
                    : new List<long>()
            });
        }

        return clonedList;
    }

    /// <summary>
    /// 随机 NPC 候选项。
    /// 运行时需要同时拿到规则数据和 NPC 表现数据，所以用这个小结构把两份数据绑在一起。
    /// </summary>
    private readonly struct NpcSpawnCandidate
    {
        public readonly NpcSpawnRuleData RuleData;
        public readonly NpcData NpcData;

        public NpcSpawnCandidate(NpcSpawnRuleData ruleData, NpcData npcData)
        {
            RuleData = ruleData;
            NpcData = npcData;
        }
    }

    #endregion
}


[System.Serializable]
public class CharacterBag
{
    [LabelText("角色ID")]
    public long CharacterID;
    [LabelText("好感度")]
    public float Favorability;
    [LabelText("心情值")]
    public float Feeling;
}

[Serializable]
public class NpcSpawnSaveData
{
    /// <summary>
    /// 场景ID。
    /// 同一个随机组挂到不同场景时，缓存需要按场景隔离。
    /// </summary>
    public long SceneID;

    /// <summary>
    /// 随机组ID，对应 NpcSpawnGroupData.ID。
    /// </summary>
    public long GroupID;

    /// <summary>
    /// 当前天数。
    /// Day 刷新模式只比较天数；TimeSlot 和 EveryEnter 也会先比较天数。
    /// </summary>
    public int Day;

    /// <summary>
    /// 当前时间段。
    /// TimeSlot 会用它保证同一时段读档结果一致；Day 模式会忽略这个字段。
    /// </summary>
    public EnvironmentMode Time;

    /// <summary>
    /// 已经随机出的 NPC 表现表 ID 列表。
    /// 保存的是 NpcData.Id，而不是 CharacterData.ID，因为同一真实角色可能有多个场景表现配置。
    /// </summary>
    public List<long> SelectedNpcIDs = new List<long>();
}
