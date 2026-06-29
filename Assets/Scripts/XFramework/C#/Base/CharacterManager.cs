using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using XFramework;
using Random = UnityEngine.Random;

public class CharacterManager : MonoSingleton<CharacterManager>, ISaveable
{
    [FoldoutGroup("Runtime"),ReadOnly,LabelText("角色背包配置表"),ShowInInspector]
    public List<CharacterBag> UserCharacterBags { get; private set; }

    #region Bindings
    
    public void Initialize()
    {
        DialogueFunctionHandler dialogueFunctionHandler = new DialogueFunctionHandler();
        Register(dialogueFunctionHandler);
        
        GoodwillFunctionHandler goodwillFunctionHandler = new GoodwillFunctionHandler();
        Register(goodwillFunctionHandler);
        
        GiftGivingFunctionHandler giftGivingFunctionHandler = new GiftGivingFunctionHandler();
        Register(giftGivingFunctionHandler);
        
        KitchenFunctionHandler kitchenFunctionHandler = new KitchenFunctionHandler();
        Register(kitchenFunctionHandler);
        
        ClawMachineFunctionHandler clawMachineFunctionHandler  = new ClawMachineFunctionHandler();
        Register(clawMachineFunctionHandler);
        
        ExplosiveGamesFunctionHandler explosiveGamesFunctionHandler = new ExplosiveGamesFunctionHandler();
        Register(explosiveGamesFunctionHandler);
        
        WitchPoisonFunctionHandler witchPoisonFunctionHandler = new WitchPoisonFunctionHandler();
        Register(witchPoisonFunctionHandler);
        
        ExhibitionFunctionHandler exhibitionFunctionHandler = new ExhibitionFunctionHandler();
        Register(exhibitionFunctionHandler);
        
        ManuscriptFunctionHandler manuscriptFunctionHandler = new ManuscriptFunctionHandler();
        Register(manuscriptFunctionHandler);
        
        SupermarketFunctionHandler supermarketFunctionHandler  = new SupermarketFunctionHandler();
        Register(supermarketFunctionHandler);
        
        FruitShopFunctionHandler fruitShopFunctionHandler = new FruitShopFunctionHandler();
        Register(fruitShopFunctionHandler);
        
        FabricStoreFunctionHandler fabricStoreFunctionHandler = new FabricStoreFunctionHandler();
        Register(fabricStoreFunctionHandler);

        SexToyStoreFunctionHandler sexToyStoreFunctionHandler = new SexToyStoreFunctionHandler();
        Register(sexToyStoreFunctionHandler);

        FishingFunctionHandler fishingFunctionHandler = new FishingFunctionHandler();
        Register(fishingFunctionHandler);
        
        FishingBaitShopFunctionHandler fishingBaitShopFunctionHandler =  new FishingBaitShopFunctionHandler();
        Register(fishingBaitShopFunctionHandler);

        PhotographyFunctionHandler photographyFunctionHandler = new PhotographyFunctionHandler();
        Register(photographyFunctionHandler);

        CoffeeShopFunctionHandler coffeeShopFunctionHandler = new CoffeeShopFunctionHandler();
        Register(coffeeShopFunctionHandler);

        BarFunctionHandler barFunctionHandler = new BarFunctionHandler();
        Register(barFunctionHandler);
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
        ISaveable saveable = this;
        SaveGameManager.Instance.RegisterSaveable(saveable);
    }

    public string GUID => "CharacterManager";

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.CharacterBags = UserCharacterBags;
    }

    public void LoadData(GameSaveData data)
    {
        if (data.CharacterBags is not { Count: > 0 })
        {
            UserCharacterBags = new List<CharacterBag>();
            // 角色配置已迁移到 Luban 表（TbCharacterData），无存档时按表初始化默认背包
            for (int i = 0; i < LubanManager.Instance.TbCharacterData.DataList.Count; i++)
            {
                CharacterBag characterBag = new CharacterBag();
                characterBag.CharacterID = LubanManager.Instance.TbCharacterData.DataList[i].ID;
                characterBag.Favorability = 0;
                characterBag.Feeling = 0;
                UserCharacterBags.Add(characterBag);
            }
        }
        else
        {
            UserCharacterBags = data.CharacterBags;
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

    public void BindAllCharacterBagChange(Action<List<CharacterBag>> action,bool invokeImmediately =true)
    {
        OnCharacterChanged += action;
        if (invokeImmediately)
        {
            OnCharacterChanged?.Invoke(UserCharacterBags);
        }
    }

    public void UnBindAllCharacterBagChange(Action<List<CharacterBag>> action)
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
