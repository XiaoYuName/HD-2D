using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class GameAudioSetPage : UIBase
{
    private Slider m_MasterVolumeSlider;
    private TextMeshProUGUI m_MasterVolumeText;
    
    private Slider m_BGMVolumeSlider;
    private TextMeshProUGUI m_BGMVolumeText;
    
    private Slider m_MusicVolumeSlider;
    private TextMeshProUGUI m_MusicVolumeText;
    
    private Slider m_HumanVolumeSlider;
    private TextMeshProUGUI m_HumanVolumeText;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        m_MasterVolumeSlider = Get<Slider>("UIMask/MasterVolumeFarme/Slider");
        m_MasterVolumeText = Get<TextMeshProUGUI>("UIMask/MasterVolumeFarme/ValueTex");
        m_MasterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChange);
        m_MasterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
        
        m_BGMVolumeSlider = Get<Slider>("UIMask/BGMVolumeFarme/Slider");
        m_BGMVolumeText = Get<TextMeshProUGUI>("UIMask/BGMVolumeFarme/ValueTex");
        m_BGMVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChange);
        m_BGMVolumeSlider.value = PlayerPrefs.GetFloat("BGMItemVolume", 0.5f);
        
        m_MusicVolumeSlider = Get<Slider>("UIMask/MusicVolumeFarme/Slider");
        m_MusicVolumeText = Get<TextMeshProUGUI>("UIMask/MusicVolumeFarme/ValueTex");
        m_MusicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChange);
        m_MusicVolumeSlider.value = PlayerPrefs.GetFloat("MusicItemVolume", 0.5f);
        
        m_HumanVolumeSlider = Get<Slider>("UIMask/HumanVolumeFarme/Slider");
        m_HumanVolumeText = Get<TextMeshProUGUI>("UIMask/HumanVolumeFarme/ValueTex");
        m_HumanVolumeSlider.onValueChanged.AddListener(OnHumanVolumeChange);
        m_HumanVolumeSlider.value = PlayerPrefs.GetFloat("HumanItemVolume", 0.5f);
    }
    
    private void OnMasterVolumeChange(float value)
    {
        m_MasterVolumeText.text = $"{value*100 :N0}";
        AudioManager.Instance.SetAudioVolume(AudioMixerGroupType.Master,value);
    }
    
    private void OnBGMVolumeChange(float value)
    {
        m_BGMVolumeText.text = $"{value*100:N0}";
        AudioManager.Instance.SetAudioVolume(AudioMixerGroupType.BGMItem,value);
    }
    
    private void OnMusicVolumeChange(float value)
    {
        m_MusicVolumeText.text = $"{value*100:N0}";
        AudioManager.Instance.SetAudioVolume(AudioMixerGroupType.MusicItem,value);
    }

    private void OnHumanVolumeChange(float value)
    {
        m_HumanVolumeText.text = $"{value*100:N0}";
        AudioManager.Instance.SetAudioVolume(AudioMixerGroupType.HumanItem,value);
    }
}
