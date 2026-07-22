using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 钓鱼系统存档字段（partial 扩展核心 <see cref="GameSaveData"/>）。
    /// 随存档读写，由常驻管理器 <see cref="Fish.FishGameManager"/> 在 SaveData/LoadData 中填充 / 还原。
    /// </summary>
    public partial class GameSaveData
    {
        [LabelText("钓鱼个人最佳记录")] public FishBestCatchSaveData FishBestCatch = new();
    }

    /// <summary>钓鱼「个人最佳记录」存档：记录各鱼种历史最大钓获长度/重量，供图鉴展示。</summary>
    [Serializable]
    public class FishBestCatchSaveData
    {
        public List<FishBestCatchEntry> Entries = new();
    }

    /// <summary>单条鱼种的最佳钓获记录。</summary>
    [Serializable]
    public class FishBestCatchEntry
    {
        public long ItemId;
        public float MaxLength;
        public float MaxWeight;
    }
}
