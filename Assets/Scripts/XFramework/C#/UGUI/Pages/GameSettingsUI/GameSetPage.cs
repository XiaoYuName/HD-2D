using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class GameSetPage : UIBase,IReset
{
    private TMP_Dropdown m_Dropdown;
    private Slider m_TextSlider;
    private TextMeshProUGUI m_TextSpeedText;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        m_Dropdown = Get<TMP_Dropdown>("UIMask/LanguageFarme/Dropdown");
        m_TextSlider = Get<Slider>("UIMask/TextSpeedFarme/Slider");
        m_TextSpeedText = Get<TextMeshProUGUI>("UIMask/TextSpeedFarme/ValueTex");

        m_Dropdown.ClearOptions();
        foreach (var languageType in GameDataManager.Instance.GameSettingsData.LanguageTypeList) 
        {
            m_Dropdown.options.Add(new TMP_Dropdown.OptionData(languageType.LanguageName));
        }
        m_Dropdown.onValueChanged.RemoveAllListeners();
        m_Dropdown.onValueChanged.AddListener(OnLanguageTypeChanged);
        m_Dropdown.value = LanguageManager.Instance.LanguageIndex;
            
        m_TextSlider.onValueChanged.RemoveAllListeners();
        m_TextSlider.onValueChanged.AddListener(OnTextSpeedChanged);
    }
    
    public void OnLanguageTypeChanged(int index)
    {
        LanguageManager.Instance.SetLocalization(index);
    }
    
    public void OnTextSpeedChanged(float value)
    {
        m_TextSpeedText.text = value.ToString("N0");
    }

    public void ResetData()
    {
        LanguageManager.Instance.SetLocalization(0);
        m_Dropdown.value = 0;
    }
}
