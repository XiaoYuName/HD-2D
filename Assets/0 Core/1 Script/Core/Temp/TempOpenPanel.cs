using UnityEngine;
using XFramework;
using Sirenix.OdinInspector;

public class TempOpenPanel : MonoBehaviour
{
    [SerializeField] string id;

    [Button]
    void TestOpen()
    {
        UISystem.Instance.OpenUI(id);
    }
}
