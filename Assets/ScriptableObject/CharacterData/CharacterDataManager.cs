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

    [BoxGroup("资源"),VerticalGroup("资源/预制体"),LabelText("Cubism预制体"),FilePath]
    public string CubismPrefab;
    
    [BoxGroup("设定"),VerticalGroup("设定/时机"),LabelText("出现设定")]
    public List<ShowingData> ShowingDataList;
    
    [FoldoutGroup("约会"),LabelText("约会数据")]
    public List<DatingDramaData> DatingDramaList;
    
    public override string GetID()
    {
        return CharacterID;
    }
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
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("是否自定义")]
    public ShowingModel ShowingModel;

    [BoxGroup("交互"),VerticalGroup("交互/对话"),LabelText("闲聊内容"),ShowIf("IsDialogue")]
    public List<DramaData> NormalDramaData;
    
    
   
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("出现场景"),ShowIf("ShowingModel",ShowingModel.Custom)]
    public List<CustomSceneData> CustomSceneList;
    [BoxGroup("出现时机"),VerticalGroup("出现时机/属性"),LabelText("场景配置"),HideIf("ShowingModel",ShowingModel.Custom)]
    public SceneData FixedSceneData;
    
    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        
        return MinGameSceneDataManager.Instance.DataList.Where(t => t != null && !string.IsNullOrEmpty(t.scene_id))
            .Select(t => new ValueDropdownItem(t.scene_description, t.scene_id));
    }
    
    public bool IsDialogue()
    {
        return Functions.HasFlag(FunctionType.Dialogue);
    }
    
}

[System.Serializable]
public class CustomSceneData
{
    [LabelText("属性")]
    public PropertyType PropertyType;
    [LabelText("范围")]
    public Vector2 Radius;
    [LabelText("场景配置")]
    public List<SceneData> SceneList;
}

[System.Serializable]
public class SceneData
{
    [LabelText("场景ID"),HorizontalGroup("Row"),ValueDropdown("GetMinSceneID")]
    public string SceneID;
    
    [LabelText("出现位置"),HorizontalGroup("Row")]
    public Vector3 Position;
    
    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }

        return MinGameSceneDataManager.Instance.DataList.Where(t => t != null && !string.IsNullOrEmpty(t.scene_id))
            .Select(t => new ValueDropdownItem(t.scene_description, t.scene_id));
    }
}

[System.Serializable]
public class DatingDramaData
{
    [LabelText("场景ID"),ValueDropdown("GetMinSceneID")]
    public string SceneID;
    [LabelText("约会剧情列表")]
    public List<DramaData> DramaList;
    
    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }

        return MinGameSceneDataManager.Instance.DataList.Where(t => t != null && !string.IsNullOrEmpty(t.scene_id))
            .Select(t => new ValueDropdownItem(t.scene_description, t.scene_id));
    }
}
