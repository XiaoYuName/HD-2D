using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class OptionUI : UIBase
{
    private List<string> Options = new List<string>();
    private CustomButton LeftButton;
    private CustomButton RightButton;
    private LocalizeStringEvent LabelStringEvent;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        LeftButton = Get<CustomButton>("LeftButton");
        RightButton = Get<CustomButton>("RightButton");
        LabelStringEvent = Get<LocalizeStringEvent>("Content/LabelStringEvent");
    }

    public void ShowingSceneOptions(List<string> options, Action<string> onOptionSelected)
    {
        
    }
}
