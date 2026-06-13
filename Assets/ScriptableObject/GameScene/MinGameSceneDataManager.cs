using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;

[CreateAssetMenu(fileName = "MinGameSceneDataManager", menuName = "Configs/MinGameSceneDataManager")]
public class MinGameSceneDataManager : OdinScriptableObject<MinSceneData>
{
    
}

[System.Serializable]
public class MinSceneData : OdinDataItem<MinSceneData>
{
    [HorizontalGroup("基本属性"),LabelText("场景ID")]
    public string scene_id;
    
    [HorizontalGroup("基本属性"),LabelText("场景名称")]
    public string scene_name;

    [LabelText("场景描述")]
    public string scene_description;
    
    [HorizontalGroup("基本属性"),LabelText("备注")]
    public string Remark;
    [LabelText("场景资源路径"),FilePath,VerticalGroup("AddressableKey")]
    public string scenePath;
    
    [LabelText("场景图标"),FilePath]
    public string SceneTexturePath;

    public override string GetID()
    {
        return scene_id;
    }
    
}
