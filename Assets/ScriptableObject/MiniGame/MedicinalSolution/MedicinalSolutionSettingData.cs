using System;
using System.Collections.Generic;
using Assets.Scripts.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace XFramework
{
    [CreateAssetMenu(fileName = "MedicinalSolutionSettingData",menuName = "Configs/MiniGame/MedicinalSolutionSettingData")]
    public class MedicinalSolutionSettingData : OdinScriptableManager<MedicinalSolutionSettingData>
    {
        [LabelText("模具配置")]
        public List<ClothingPaintTubeMoldData>  ClothingPaintTubeMoldList = new List<ClothingPaintTubeMoldData>();
        
        [LabelText("颜色配置")]
        public List<ClothingPaintTubeColorData> ClothingPaintTubeColorList = new List<ClothingPaintTubeColorData>();
        
        [LabelText("游戏配置")]
        public List<MedicinalSolutionData> MiniGameSolutionList = new List<MedicinalSolutionData>();

        [LabelText("随机生成"),Button("随机生成")]
        public void RandomSolution(int count)
        {
            MiniGameSolutionList.Clear();
            for (int i = 0; i < count; i++)
            {
                MedicinalSolutionData solutionData = new MedicinalSolutionData();
                solutionData.MoldClass = RandomUtil.NextEnum<ClothingPaintTubeMoldType>();

                int randomColorSize = Random.Range(1, 3);
                List<PaintTubeColorType>  colorList = new List<PaintTubeColorType>();
                foreach (PaintTubeColorType type in Enum.GetValues(typeof(PaintTubeColorType)))
                {
                    colorList.Add(type);
                }
                solutionData.PaintTubeColorList = RandomUtil.Take<PaintTubeColorType>(colorList,randomColorSize);
                solutionData.Ml = (int)RandomUtil.NextDiscrete(100, 500, 5);
                MiniGameSolutionList.Add(solutionData);
            }
        }

        public ClothingPaintTubeColorData GetColorData(PaintTubeColorType type)
        {
            return ClothingPaintTubeColorList.Find(item => item.Type == type);
        }

        public Sprite GetColorIcon(PaintTubeColorType type)
        {
            return GetColorData(type)?.Icon;
        }

        public Color GetColorValue(PaintTubeColorType type)
        {
            return GetColorData(type)?.Color ?? Color.white;
        }

        public ClothingPaintTubeMoldData GetMoldData(ClothingPaintTubeMoldType type)
        {
            return ClothingPaintTubeMoldList.Find(item => item.Type == type);
        }

        public Sprite GetMoldIcon(ClothingPaintTubeMoldType type)
        {
            return GetMoldData(type)?.Icon;
        }
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
        [LabelText("图标")]
        public Sprite Icon;
    }

    [System.Serializable]
    public class MedicinalSolutionData
    {
        [LabelText("所需模具")]
        public ClothingPaintTubeMoldType MoldClass;
        [LabelText("所需颜色")]
        public List<PaintTubeColorType>  PaintTubeColorList;
        [LabelText("所需毫升")]
        public int Ml;
    }
}

