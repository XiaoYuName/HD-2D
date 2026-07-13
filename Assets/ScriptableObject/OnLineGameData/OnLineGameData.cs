using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "OnLineGameData", menuName = "Configs/OnLineGameData")]
public class OnLineGameData : OdinScriptableManager<OnLineGameData>
{
    [TitleGroup("界面列表")]
    [LabelText("界面设置")]
    public List<OnLineTypeMenuData>  OnLineTypeMenuData = new List<OnLineTypeMenuData>();
}

[System.Serializable]
public class OnLineTypeMenuData
{
    [HorizontalGroup("类型"),LabelText("界面类型")]
    public OnLinePageType onLinePageType = OnLinePageType.None;
    
    [HorizontalGroup("类型"),LabelText("界面路径"),FilePath]
    public string onLinePagePath;
    
    [LabelText("按钮名称")]
    public LocalSelectedData labelNameString;
}
