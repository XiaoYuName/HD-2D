using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class OptionUI : UIBase
{
    private List<long> Options = new List<long>();
    private CustomButton LeftButton;
    private CustomButton RightButton;
    private LocalizeStringEvent LabelStringEvent;
    private Action<long> OnValueChange;

    public long SelectedOption { get; private set; }
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

    public void ShowingSceneOptions(List<long> options, Action<long> onOptionSelected = null)
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


    private void ShowSelectedOption(long option)
    {
        SelectedOption = option;
        long sceneId = option;
        GameSceneData SceneData = GameSceneManager.Instance.GetGameSceneData(sceneId);
        LabelStringEvent.StringReference.SetReference(SceneData.SceneName.Table,SceneData.SceneName.Value);
        LabelStringEvent.StringReference.RefreshString();
        OnValueChange?.Invoke(option);
    }
}
