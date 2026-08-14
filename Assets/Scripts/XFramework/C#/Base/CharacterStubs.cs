using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// 角色背包 - 框架存根版本
/// 如果项目不需要角色系统，可以删除此文件
/// </summary>
[Serializable]
public class CharacterBag
{
    [LabelText("角色ID")]
    public long CharacterID;

    [LabelText("角色名称")]
    public string CharacterName;

    [LabelText("服装背包")]
    public List<ClothingBag> ClothingBags = new List<ClothingBag>();
}

/// <summary>
/// 服装背包 - 框架存根版本
/// </summary>
[Serializable]
public class ClothingBag
{
    [LabelText("服装ID")]
    public long clothingID;

    [LabelText("服装配件列表")]
    public List<ClothingAccessoriesBag> Accessories = new List<ClothingAccessoriesBag>();
}

/// <summary>
/// 服装配件背包 - 框架存根版本
/// </summary>
[Serializable]
public class ClothingAccessoriesBag
{
    [LabelText("配件ID")]
    public long AccessoriesID;

    [LabelText("是否解锁")]
    public bool isUnlock;
}

/// <summary>
/// 角色场景覆盖 - 框架存根版本
/// </summary>
[Serializable]
public class CharacterSceneOverride
{
    [LabelText("角色ID")]
    public long CharacterID;

    [LabelText("场景ID")]
    public long SceneID;
}

/// <summary>
/// NPC生成保存数据 - 框架存根版本
/// </summary>
[Serializable]
public class NpcSpawnSaveData
{
    [LabelText("NPC ID")]
    public long NpcID;

    [LabelText("场景ID")]
    public long SceneID;
}
