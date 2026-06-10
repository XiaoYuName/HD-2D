using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XFramework;

public class DisplaySetPage : UIBase
{
    private TMP_Dropdown m_windowsDropdown;
    private TMP_Dropdown m_resolutionDropdown;
    private WindowType m_windowType;
    private int m_resolutionIndex;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        m_windowsDropdown = Get<TMP_Dropdown>("UIMask/DisplayFarme/Dropdown");
        m_resolutionDropdown = Get<TMP_Dropdown>("UIMask/ResolutionSettings/Dropdown");
        
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        m_windowType = ResolutionManager.Instance.SelectedWindowType;
        m_resolutionIndex = ResolutionManager.Instance.SelectedWindowResolutionIndex;
        LanguageManager.Instance.AddOnLanguageChanged(RefreshWindowTypeOptions);
        RefreshWindowTypeOptions();
        RefreshResolutionOptions();
        m_windowsDropdown.value = (int)m_windowType;
        m_resolutionDropdown.value = m_resolutionIndex;
        m_windowsDropdown.onValueChanged.AddListener(OnWindowTypeChanged);
        m_resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        m_windowsDropdown.onValueChanged.RemoveListener(OnWindowTypeChanged);
        m_resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        LanguageManager.Instance.RemoveOnLanguageChanged(RefreshWindowTypeOptions);
        m_windowsDropdown.ClearOptions();
        m_resolutionDropdown.ClearOptions();
    }

    private void OnWindowTypeChanged(int index)
    {
        m_windowType = (WindowType)index;
        ResolutionManager.Instance.ChangeWindowMode(m_windowType,m_resolutionIndex);
    }
    
    private void OnResolutionChanged(int index)
    {
        m_resolutionIndex = index;
        ResolutionManager.Instance.ChangeWindowMode(m_windowType,m_resolutionIndex);
    }

    private void RefreshWindowTypeOptions()
    {
        int selectedIndex = m_windowsDropdown.value;

        m_windowsDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (WindowType type in Enum.GetValues(typeof(WindowType)))
        {
            options.Add(new TMP_Dropdown.OptionData(
                LanguageManager.Instance.GetLocalizedString(
                    "GameSettingsUI",
                    $"GameSettingsUI/DisplaySetPage/WindowType/{type}")));
        }

        m_windowsDropdown.AddOptions(options);
        m_windowsDropdown.SetValueWithoutNotify(
            Mathf.Clamp(selectedIndex, 0, m_windowsDropdown.options.Count - 1));
        m_windowsDropdown.RefreshShownValue();
    }

    private void RefreshResolutionOptions()
    {
        int selectedIndex = m_resolutionDropdown.value;

        m_resolutionDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();
        for (int i = 0; i < Screen.resolutions.Length; i++)
        {
            string option = $"{Screen.resolutions[i]}";
            options.Add(new TMP_Dropdown.OptionData(option));
        }
        m_resolutionDropdown.AddOptions(options);
        m_resolutionDropdown.SetValueWithoutNotify(
            Mathf.Clamp(selectedIndex, 0, m_resolutionDropdown.options.Count - 1));
        m_resolutionDropdown.RefreshShownValue();
    }
}
