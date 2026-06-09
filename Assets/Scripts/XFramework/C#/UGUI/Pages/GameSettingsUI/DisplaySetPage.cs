using TMPro;
using UnityEngine;
using XFramework;

public class DisplaySetPage : UIBase
{
    private TMP_Dropdown m_windowsDropdown;
    private TMP_Dropdown m_resolutionDropdown;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        m_windowsDropdown = Get<TMP_Dropdown>("UIMask/DisplayFarme/Dropdown");
        m_resolutionDropdown = Get<TMP_Dropdown>("UUIMask/ResolutionSettings/Dropdown");
        
        m_windowsDropdown.ClearOptions();
        // for (int i = 0; i < (int)WindowType.Borderless; i++)
        // {
        //    // string str = WindowType
        //     m_windowsDropdown.options.Add(new TMP_Dropdown.OptionData(((WindowType)i)).ToString());
        // }
        //
        // m_windowsDropdown.onValueChanged.AddListener(OnWindowTypeChanged);
    }
}
