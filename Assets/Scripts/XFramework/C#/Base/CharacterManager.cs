using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;
using Random = UnityEngine.Random;

public class CharacterManager : MonoSingleton<CharacterManager>,ISaveable
{
    [FoldoutGroup("Configs"),LabelText("角色配置表")]
    public CharacterDataManager CharacterData;

    [FoldoutGroup("Runtime"),ReadOnly,LabelText("角色背包配置表"),ShowInInspector]
    public List<CharacterBag> UserCharacterBags { get; private set; }
    

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (GameManager.IsInitialized)
        {
            GameManager.Instance.OnEnterGame -= Initialize;
            GameManager.Instance.OnExitGame -= Release;
        }
    }

    public void Initialize()
    {
        GameDataManager.Instance.BindUserDayChange(UserDayChange);
    }

    public void Release()
    {
        if(GameDataManager.IsInitialized)
            GameDataManager.Instance.UnBindUserDayChange(UserDayChange);
    }


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
    public GameSaveData GenerateSaveData()
    {
        GameSaveData gameSaveData = new GameSaveData();
        gameSaveData.CharacterBags = UserCharacterBags;
        return gameSaveData;
    }

    public void RestoreData(GameSaveData GameSave)
    {
        if (GameSave != null)
        {
            if (GameSave.CharacterBags is not { Count: > 0 })
            {
                UserCharacterBags = new List<CharacterBag>();
                for (int i = 0; i < CharacterData.DataList.Count; i++)
                {
                    CharacterBag characterBag = new CharacterBag();
                    characterBag.CharacterID = CharacterData.DataList[i].CharacterID;
                    characterBag.Favorability = 0;
                    characterBag.Feeling = 0;
                    UserCharacterBags.Add(characterBag);
                }
            }
            else
            {
                UserCharacterBags = GameSave.CharacterBags;
            }
        }
    }

    #endregion

    #region CURD

    public void SetCharacterFavorability(string characterID,int value)
    {
        CharacterBag characterBag = UserCharacterBags.Find(x => x.CharacterID == characterID);
        if (characterBag != null)
        {
            characterBag.Favorability = value;
            OnCharacterChanged?.Invoke(characterBag);
            SaveGameManager.Instance.Save(GameDataManager.Instance.CurrentUser);
        }
    }

    #endregion

    #region Event
    /// <summary>
    /// 用户角色背包变化回调
    /// </summary>
    public event Action<CharacterBag> OnCharacterChanged;

    #endregion

    #region 非固定NPC

    [FoldoutGroup("NPC_Data"),LabelText("自定义角色展示位置"),ShowInInspector]
    private List<CustomCharacterData> CustomCharacterData = new List<CustomCharacterData>();

    
    
    
    
    private void UserDayChange(User user)
    {
        var characterDataList = CharacterManager.Instance.CharacterData.DataList;
        var configDataList = CharacterDataManager.Instance.DataList;
        ShowingWeek curWeek = user.Week switch
        {
            1 => ShowingWeek.Monday,
            2 => ShowingWeek.Tuesday,
            3 => ShowingWeek.Wednesday,
            4 => ShowingWeek.Thursday,
            5 => ShowingWeek.Friday,
            6 => ShowingWeek.Saturday,
            7 => ShowingWeek.Sunday,
            _ => ShowingWeek.Monday,
        };
        
        ShowingTime curTime = user.EnvironmentMode switch
        {
            EnvironmentMode.Morning => ShowingTime.Morning,
            EnvironmentMode.Noon => ShowingTime.Noon,
            EnvironmentMode.Evening => ShowingTime.Evening,
            EnvironmentMode.Midnight => ShowingTime.Midnight,
            _ => ShowingTime.Morning,
        };
        
        
        //string targetSceneId = minSceneData.scene_id;

        int count = characterDataList.Count;

        for (int i = 0; i < count; i++)
        {
            var characterData = characterDataList[i];
            var showingDataList = configDataList[i].ShowingDataList;
            var characterBag = UserCharacterBags.Find(x => x.CharacterID == characterData.CharacterID);

            if (characterBag == null)
            {
                break;
            }


            for (int j = 0; j < showingDataList.Count; j++)
            {
                var showingData = showingDataList[j];
                if (!showingData.ShowWeek.HasFlag(curWeek))
                {
                    continue;
                }
                
                if(!showingData.ShowTime.HasFlag(curTime))
                    continue;

                if (showingData.ShowingModel != ShowingModel.Custom)
                    continue;

                for (int k = 0; k < showingData.CustomSceneList.Count; k++)
                {
                    if((showingData.CustomSceneList[k].PropertyType == PropertyType.Feeling))
                    {
                        if (characterBag.Feeling >= showingData.CustomSceneList[k].Radius.x &&
                            characterBag.Feeling <= showingData.CustomSceneList[k].Radius.y)
                        {
                            SceneData sceneData = showingData.CustomSceneList[k].SceneList[Random.Range(0, showingData.CustomSceneList[k].SceneList.Count)];
                            
                            CustomCharacterData customCharacterData = new CustomCharacterData();
                            customCharacterData.CharacterData = characterData;
                            customCharacterData.CustomSceneData = sceneData;
                            CustomCharacterData.Add(customCharacterData);
                        }
                    }
                
                }
                
                
                break;
            }
        }
        OnCustomCharacterDataChanged?.Invoke(CustomCharacterData);
    }

    #region Event
    public Action<List<CustomCharacterData>> OnCustomCharacterDataChanged;

    public void BindCustomCharacterDataChange(Action<List<CustomCharacterData>> callback)
    {
        OnCustomCharacterDataChanged += callback;
        callback?.Invoke(CustomCharacterData);
    }

    public void UnBindCustomCharacterDataChange(Action<List<CustomCharacterData>> callback)
    {
        OnCustomCharacterDataChanged -= callback;
    }


    #endregion

    

    #endregion

}


[System.Serializable]
public class CharacterBag
{
    [LabelText("角色ID")]
    public string CharacterID;
    [LabelText("好感度")]
    public float Favorability;
    [LabelText("心情值")]
    public float Feeling;
}

[System.Serializable]
public class CustomCharacterData
{
    [LabelText("角色数据")]
    public CharacterData CharacterData;
    [LabelText("角色显示数据")]
    public ShowingData ShowingData;
    [LabelText("出现场景")]
    public SceneData CustomSceneData;
}