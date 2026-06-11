using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "CharacterDataManager", menuName = "Configs/CharacterDataManager")]
public class CharacterDataManager : OdinScriptableObject<CharacterData>
{
    
}

[System.Serializable]
public class CharacterData : OdinDataItem<CharacterData>
{
    [BoxGroup("基本属性"),VerticalGroup("基本属性/属性"),LabelText("角色ID")]
    public string CharacterID;
    [BoxGroup("基本属性"),VerticalGroup("基本属性/属性"),LabelText("角色名称")]
    public string CharacterName;
    [BoxGroup("基本属性"),VerticalGroup("基本属性/属性"),LabelText("备注")]
    public string Remark;

    [BoxGroup("立绘"),VerticalGroup("立绘/属性"),LabelText("场景立绘"),PreviewField(ObjectFieldAlignment.Left)]
    public Sprite CharacterSceneIcon;
    [BoxGroup("立绘"),VerticalGroup("立绘/属性"),LabelText("对话立绘"),PreviewField(ObjectFieldAlignment.Left)]
    public Sprite DialogueTexture;
    
    [BoxGroup("设定"),VerticalGroup("设定/时机"),LabelText("出现设定")]
    public List<ShowingData> ShowingDataList;
    
    public override string GetID()
    {
        return CharacterID;
    }

    
}

[Flags]
public enum ShowingWeek
{
    [LabelText("周一")]
    Monday = 1,
    [LabelText("周二")]
    Tuesday = 1 << 2,
    [LabelText("周三")]
    Wednesday = 1 << 3,
    [LabelText("周四")]
    Thursday = 1 << 4,
    [LabelText("周五")]
    Friday = 1 << 5,
    [LabelText("周六")]
    Saturday = 1 << 6,
    [LabelText("周日")]
    Sunday = 1 << 7
}

[Flags]
public enum ShowingTime
{
    [LabelText("白天")]
    Day = 1,
    [LabelText("夜晚")]
    Night = 1 << 1
}

[Flags]
public enum FunctionType
{
    [LabelText("对话")]
    Dialogue = 1,
    [LabelText("任务")]
    Task = 1 << 1,
    [LabelText("送礼")]
    GiftGiving = 1 << 2,
}

[System.Serializable]
public class ShowingData
{
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("出现周几")]
    public ShowingWeek ShowWeek;
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("出现时间")]
    public ShowingTime ShowTime;
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("功能")]
    public FunctionType Functions;
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("出现场景"),ValueDropdown("GetMinSceneID")]
    public string SceneID;
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("出现位置")]
    public Vector3 Position;
    
    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }

        return MinGameSceneDataManager.Instance.DataList.Where(t => t != null && !string.IsNullOrEmpty(t.scene_id))
            .Select(t => new ValueDropdownItem(t.Remark, t.scene_id));
    }
}
