using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

namespace XFramework
{
    [CreateAssetMenu(fileName = "SewingMachineGameData",menuName = "Configs/SewingMachineGameData")]
    public class SewingMachineGameData : OdinScriptableManager<SewingMachineGameData>
    {
        [TitleGroup("基础设定")]
        [LabelText("生成数量")]
        public int GenerateNumber;
    }
}

