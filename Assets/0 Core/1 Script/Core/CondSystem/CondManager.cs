using UnityEngine;

namespace XFramework
{
    public class CondManager : MonoSingleton<CondManager>
    {
        
        public bool IsCondMet(long id)
        {
            return true;
        }

        public bool IsCondMet()
        {
            return true;
        }
    }
}