using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>任务系统存档字段（partial 扩展核心 <see cref="GameSaveData"/>）。</summary>
    public partial class GameSaveData
    {
        [LabelText("任务数据")] public QuestSaveData Quest = new();
    }

    /// <summary>
    /// 任务存档。直接存 <see cref="QuestInfo"/>（含多态的目标对象），
    /// 序列化器开了 <c>TypeNameHandling.Auto</c>，子类类型会写进 json 的 <c>$type</c> 里。
    /// 由配置决定的字段都标了 <c>[JsonIgnore]</c>，读档后重新 Init —— 配置改了立刻生效。
    /// </summary>
    [Serializable]
    public class QuestSaveData
    {
        public List<QuestInfo> Quests = new();

        /// <summary>类别奖励已发过的类别ID，只发一次。</summary>
        public List<long> RewardedCategories = new();
    }
}
