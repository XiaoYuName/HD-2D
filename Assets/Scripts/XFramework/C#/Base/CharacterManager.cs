using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 角色管理器 - 框架存根版本
/// 如果项目需要角色系统，请实现完整版本
/// </summary>
public class CharacterManager : MonoSingleton<CharacterManager>, ISaveable
{
    [ShowInInspector, LabelText("角色背包")]
    public List<CharacterBag> UserCharacterBags = new List<CharacterBag>();

    #region ISaveable

    public string GUID => "CharacterManager";

    private void Start()
    {
        ((ISaveable)this).RegisterSaveable();
    }

    public void SaveData(GameSaveData data)
    {
        data.CharacterBags = new List<CharacterBag>(UserCharacterBags);
        data.CharacterSceneOverrides = new Dictionary<long, CharacterSceneOverride>();
    }

    public void LoadData(GameSaveData data)
    {
        if (data?.CharacterBags != null)
        {
            UserCharacterBags = new List<CharacterBag>(data.CharacterBags);
        }
        else
        {
            UserCharacterBags = new List<CharacterBag>();
        }
    }

    #endregion

    #region 场景NPC获取 - 框架存根版本

    /// <summary>
    /// 获取场景NPC列表 - 框架存根版本
    /// </summary>
    public List<NpcData> GetSceneNpcDataList(GameSceneData sceneData, PlayerData playerData)
    {
        // 框架存根版本：返回空列表
        // 根据项目需求实现NPC生成逻辑
        return new List<NpcData>();
    }

    #endregion

    #region 事件注册 - 框架存根版本

    private Action<List<CharacterBag>> onAllCharacterBagChange;

    public void RegisterAllCharacterBagChange(Action<List<CharacterBag>> callback)
    {
        onAllCharacterBagChange += callback;
        callback?.Invoke(UserCharacterBags);
    }

    public void UnregisterAllCharacterBagChange(Action<List<CharacterBag>> callback)
    {
        onAllCharacterBagChange -= callback;
    }

    #endregion

    #region 角色属性操作 - 框架存根版本

    /// <summary>
    /// 添加角色属性 - 框架存根版本
    /// </summary>
    public void AddProperty(long characterID, CharacterPropType propType, int value)
    {
        // 根据项目需求实现角色属性逻辑
        Debug.LogWarning($"CharacterManager.AddProperty stub: characterID={characterID}, propType={propType}, value={value}");
    }

    /// <summary>
    /// 解锁服装配件 - 框架存根版本
    /// </summary>
    public void UlockAccessories(long characterID, long clothingID, ClothingAccessoriesBag accessoriesBag)
    {
        // 根据项目需求实现服装解锁逻辑
        accessoriesBag.isUnlock = true;
        Debug.LogWarning($"CharacterManager.UlockAccessories stub: characterID={characterID}, clothingID={clothingID}");
    }

    #endregion
}

/// <summary>
/// 角色属性类型 - 框架存根版本
/// </summary>
public enum CharacterPropType
{
    Affection = 0,  // 好感度
    Mood = 1,       // 心情
}
