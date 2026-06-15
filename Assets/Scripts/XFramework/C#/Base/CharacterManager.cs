using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public class CharacterManager : MonoSingleton<CharacterManager>,ISaveable
{
    [FoldoutGroup("Configs"),LabelText("角色配置表")]
    public CharacterDataManager CharacterData;

    [FoldoutGroup("Runtime"),ReadOnly,LabelText("角色背包配置表"),ShowInInspector]
    public List<CharacterBag> UserCharacterBags { get; private set; }

    #region ISaveable

    public void Start()
    {
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