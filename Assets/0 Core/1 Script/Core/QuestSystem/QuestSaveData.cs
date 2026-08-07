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

    [Serializable]
    public class QuestSaveData
    {
        public List<QuestEntrySaveData> Quests = new();
    }

    /// <summary>
    /// 单个任务的存档。只落状态和各目标自报的进度（<see cref="QuestObjInfoBase.SaveState"/>），
    /// 运行时目标对象由配置重建 —— 配置改了目标条数也不会读坏档，多出来的从 0 开始，少掉的直接丢弃。
    /// 进度用 int[] 而不是单个 int，是为了让多计数的目标也能自己决定存什么。
    /// </summary>
    [Serializable]
    public class QuestEntrySaveData
    {
        public long QuestId;
        public QuestState State;
        public int AcceptDay;
        public bool ExceedAchieved;
        public List<int[]> ObjStates = new();
        public List<int[]> ExtraObjStates = new();
    }
}
