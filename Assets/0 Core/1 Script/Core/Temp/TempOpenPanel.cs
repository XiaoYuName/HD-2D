using UnityEngine;
using Sirenix.OdinInspector;

namespace XFramework
{
    public class TempOpenPanel : MonoBehaviour
    {
        [SerializeField] string id;

        [Button]
        void TestOpen()
        {
            UISystem.Instance.OpenUI(id);
        }
    }
}
