using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "GameSceneDataManager", menuName = "Configs/GameSceneDataManager")]
public class GameSceneDataManager : OdinScriptableObject<GameSceneData>
{
    
}

[System.Serializable]
public class GameSceneData : OdinDataItem<GameSceneData>
{
    [HorizontalGroup("基本字段"),LabelText("场景ID")]
    public string scene_id;
    [HorizontalGroup("基本字段"),LabelText("场景名称")]
    public string scene_name;
    [LabelText("地图Icon")]
    public Sprite word_icon;

    [LabelText("小场景列表"),ValueDropdown("GetMinSceneItemID")] 
    public List<string> min_sceneList = new List<string>();
    
    
    public override string GetID()
    {
        return scene_id;
    }

    public IEnumerable GetMinSceneItemID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        
        return MinGameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.scene_id))
            .Select(temp => new ValueDropdownItem(temp.scene_name,temp.scene_id));
    }
}


