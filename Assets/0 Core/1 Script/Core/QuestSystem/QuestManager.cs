using UnityEngine;
using System;

namespace XFramework
{
    public class QuestManager : MonoSingleton<QuestManager>, ISaveable
    {
        
        public string GUID => "QuestManager";
        public void LoadData(GameSaveData data)
        {

        }
        public void SaveData(GameSaveData data)
        {

        }
    }
}

[Serializable]
public class QuestSaveData
{
    
}
