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

        [TitleGroup("基础设定")]
        [LabelText("随机旋转范围(角度)")]
        [MinMaxSlider(-180f, 180f, true)]
        public Vector2 RotationRange = new Vector2(-15f, 15f);

        [TitleGroup("基础设定")]
        [LabelText("距离父物体边界的内边距")]
        public float BorderPadding = 0f;
    }
}

