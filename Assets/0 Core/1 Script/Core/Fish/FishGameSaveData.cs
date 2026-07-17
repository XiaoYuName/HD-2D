using System;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 钓鱼系统存档字段（partial 扩展核心 <see cref="GameSaveData"/>）。
    /// 由常驻管理器 FishGameManager 在 SaveData/LoadData 中读写。
    /// </summary>
    public partial class GameSaveData
    {
        [LabelText("钓鱼进度")] public FishGameSaveData FishGame = new();
    }

    /// <summary>钓鱼进度存档：等级、当前等级内累计经验、当前装备鱼竿。</summary>
    [Serializable]
    public class FishGameSaveData
    {
        public int Level = 1;
        public int Exp;                 // 当前等级内累计经验
        public long CurrentRodId = 140000;   // 默认竹鱼竿
    }
}
