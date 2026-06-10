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
    private Vector2Int m_resolution;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        m_windowsDropdown = Get<TMP_Dropdown>("UIMask/DisplayFarme/Dropdown");
        m_resolutionDropdown = Get<TMP_Dropdown>("UIMask/ResolutionSettings/Dropdown");
        m_windowsDropdown.onValueChanged.AddListener(OnWindowTypeChanged);
        m_windowType = WindowType.Fullscreen;
        m_resolutionDropdown.ClearOptions();
        for (int i = 0; i < GameDataManager.Instance.GameSettingsData.ResolutionTypeList.Count; i++)
        {
            ResolutionType type = GameDataManager.Instance.GameSettingsData.ResolutionTypeList[i];
            string option = $"{type.Resolution.x}x{type.Resolution.y}";
            m_resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }
        m_resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        m_resolution = GameDataManager.Instance.GameSettingsData.ResolutionTypeList[0].Resolution;
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        LanguageManager.Instance.AddOnLanguageChanged(RefreshWindowTypeOptions);
        RefreshWindowTypeOptions();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        LanguageManager.Instance.RemoveOnLanguageChanged(RefreshWindowTypeOptions);
        m_windowsDropdown.ClearOptions();
    }

    private void OnWindowTypeChanged(int index)
    {
        m_windowType = (WindowType)index;
        ResolutionManager.Instance.ChangeWindowMode(m_windowType,m_resolution.x,m_resolution.y);
    }
    
    private void OnResolutionChanged(int index)
    {
        m_resolution = GameDataManager.Instance.GameSettingsData.ResolutionTypeList[index].Resolution;
        ResolutionManager.Instance.ChangeWindowMode(m_windowType,m_resolution.x,m_resolution.y);
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
}
