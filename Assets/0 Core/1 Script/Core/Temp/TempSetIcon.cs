using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

public class TempSetIcon : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] string key;

    [Button("Set")]
    void Set()
    {
        icon.SetIcon(key);
    }
}
