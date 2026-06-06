using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
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

    [LabelText("场景资源路径"),FilePath]
    public string scenePath;
    
    [LabelText("UI资源"),ValueDropdown("GetPageID")]
    public string page_id;

    public override string GetID()
    {
        return scene_id;
    }


    public IEnumerable GetPageID()
    {
        if (PageConfiguration.Instance == null)
        {
            return new List<string>();
        }

        var PageData = PageConfiguration.Instance as PageConfiguration;
        return PageData.Pages.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.PathID)).Select(temp => temp.PathID);
    }
}
