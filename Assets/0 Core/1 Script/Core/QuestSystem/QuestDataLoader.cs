using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 把 Luban 表读成运行时的 <see cref="QuestData"/> 字典。
    /// 和 <see cref="LubanManager"/> 的耦合只有这一处，<see cref="QuestManager"/> 不直接碰表。
    /// </summary>
    public static class QuestDataLoader
    {
        public static Dictionary<long, QuestData> Load()
        {
            IReadOnlyList<QuestDataConfig> configs = LubanManager.Instance.TbQuestData.DataList;

            Dictionary<long, QuestData> result = new(configs.Count);
            foreach (QuestDataConfig config in configs) result.Add(config.Id, new QuestData(config));
            return result;
        }
    }
}
