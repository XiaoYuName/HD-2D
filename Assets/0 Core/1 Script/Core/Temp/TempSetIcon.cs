using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using XFramework;
public class TempSetIcon : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] string key;
    
    [SerializeField] string audioKey;
    const string FactoryVictoryClipSound = nameof(FactoryVictoryClipSound);

    [Button("Set")]
    void Set()
    {
        icon.SetIcon(key);
    }
    [Button]
    void TestAudio()
    {
        AudioManager.Instance.PlayAudio(audioKey);
    }
}
