using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

namespace XFramework
{
    [CreateAssetMenu(fileName = "SewingMachineGameData",menuName = "Configs/SewingMachineGameData")]
    public class SewingMachineGameData : OdinScriptableManager<SewingMachineGameData>
    {
        [LabelText("生成的物体列表"),FilePath]
        public List<string> Parents = new List<string>();
    }
}

