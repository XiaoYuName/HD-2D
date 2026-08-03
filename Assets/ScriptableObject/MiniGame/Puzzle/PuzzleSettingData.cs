using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    [CreateAssetMenu(fileName = "PuzzleSettingData", menuName = "Configs/MiniGame/拼图配置")]
    public class PuzzleSettingData : OdinScriptableManager<PuzzleSettingData>
    {
        [TitleGroup("拼图小游戏配置")]
        [LabelText("拼图")]
        public List<PuzzleData> PuzzleGroups = new List<PuzzleData>();
    }

    [System.Serializable]
    public class PuzzleData
    {
        [LabelText("原图素材")]
        public Sprite OriginalSprite;
        [LabelText("拼图图片素材")]
        public List<Sprite> PuzzleSprites;
    }
}

