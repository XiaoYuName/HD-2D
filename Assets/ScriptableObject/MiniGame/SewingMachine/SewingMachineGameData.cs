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

        [LabelText("刮刮乐完成度")]
        [Range(0f, 1f)]
        public float ScratchCompleteRatio = 0.8f;
    }
}

