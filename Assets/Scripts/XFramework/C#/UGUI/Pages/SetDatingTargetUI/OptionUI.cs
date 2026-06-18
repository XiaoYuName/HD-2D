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
    private Action<string> OnValueChange;

    public string SelectedOption { get; private set; }
    public int SelectedOptionIndex { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        LeftButton = Get<CustomButton>("LeftButton");
        RightButton = Get<CustomButton>("RightButton");
        LabelStringEvent = Get<LocalizeStringEvent>("Content/LabelStringEvent");
        Bind(LeftButton,LeftButtonOnClick,"");
        Bind(RightButton,RightButtonOnClick,"");
    }

    public void ShowingSceneOptions(List<string> options, Action<string> onOptionSelected = null)
    {
        SelectedOptionIndex = 0;
        this.Options = options;
        OnValueChange = onOptionSelected;
        ShowSelectedOption(options[SelectedOptionIndex]);
    }

    private void LeftButtonOnClick()
    {
        SelectedOptionIndex++;
        if (SelectedOptionIndex >= Options.Count)
        {
            SelectedOptionIndex = 0;
        }
        ShowSelectedOption(Options[SelectedOptionIndex]);
    }

    private void RightButtonOnClick()
    {
        SelectedOptionIndex--;
        if (SelectedOptionIndex < 0)
        {
            SelectedOptionIndex = Options.Count - 1;
        }
        ShowSelectedOption(Options[SelectedOptionIndex]);
    }


    private void ShowSelectedOption(string option)
    {
        SelectedOption = option;
        string sceneId = option;
        var minSceneData = MinGameSceneDataManager.Instance.GetDataByID(sceneId);
        LabelStringEvent.StringReference.SetReference(minSceneData.sceneName.Table,minSceneData.sceneName.Value);
        LabelStringEvent.StringReference.RefreshString();
        OnValueChange?.Invoke(option);
    }
}
