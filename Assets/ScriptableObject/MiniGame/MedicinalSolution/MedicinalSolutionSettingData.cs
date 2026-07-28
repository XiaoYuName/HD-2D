using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    [CreateAssetMenu(fileName = "MedicinalSolutionSettingData",menuName = "Configs/MiniGame/MedicinalSolutionSettingData")]
    public class MedicinalSolutionSettingData : OdinScriptableManager<MedicinalSolutionSettingData>
    {
        [LabelText("模具配置")]
        public List<ClothingPaintTubeMoldData>  ClothingPaintTubeMoldList = new List<ClothingPaintTubeMoldData>();
        
        [LabelText("颜色配置")]
        public List<ClothingPaintTubeColorData> ClothingPaintTubeColorList = new List<ClothingPaintTubeColorData>();
    }

    [System.Serializable]
    public class ClothingPaintTubeMoldData
    {
        [LabelText("模具类型")]
        public ClothingPaintTubeMoldType Type;
        [LabelText("模具Icon")]
        public Sprite Icon;
    }

    [System.Serializable]
    public class ClothingPaintTubeColorData
    {
        [LabelText("颜色类型")]
        public PaintTubeColorType Type;
        [LabelText("绘画颜色")]
        public Color Color;
    }
}

