using System;
using System.ComponentModel;
using UnityEngine;
using XFramework;
using Object = System.Object;


public partial class SROptions
{
    #region Character

    public string characterid;

    public int favorability;
    
    [Category("Character"),DisplayName("角色ID")]
    public string CharacterID
    {
        get { return characterid;}
        set { characterid = value; }
    }
    
    [Category("Character"),NumberRange(1,999),DisplayName("好感度")]
    public int Favorability
    {
        get { return favorability; }
        set { favorability = value; }
    }
    
    [Category("Character"), DisplayName("设置角色好感度")]
    public void AddCharacterFavorability()
    {
        CharacterManager.Instance.SetCharacterFavorability(CharacterID,Favorability);
    }

    #endregion
    

    [Category("Save"), DisplayName("存档游戏")]
    public void Save()
    {
        SaveGameManager.Instance.Save();
    }



}	
