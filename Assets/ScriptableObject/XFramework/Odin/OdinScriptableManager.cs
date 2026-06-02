using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    public class OdinScriptableManager<T> : SerializedScriptableObject
    {
        public static OdinScriptableManager<T> Instance;
    
        void OnEnable() => Instance = this;

        void OnDisable() => Instance = null;

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}

