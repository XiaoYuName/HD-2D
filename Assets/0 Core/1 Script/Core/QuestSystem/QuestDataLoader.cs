using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 把三张 Luban 表读成运行时配置。目标要先于任务读完，因为任务只存目标ID。
    /// 和 <see cref="LubanManager"/> 的耦合只有这一处，<see cref="QuestManager"/> 不直接碰表。
    /// </summary>
    public static class QuestDataLoader
    {
        public static Dictionary<long, QuestObjConfigData> LoadObjs()
        {
            IReadOnlyList<QuestObjConfig> configs = LubanManager.Instance.TbQuestObjData.DataList;

            Dictionary<long, QuestObjConfigData> result = new(configs.Count);
            foreach (QuestObjConfig config in configs) result.Add(config.Id, new QuestObjConfigData(config));
            return result;
        }

        public static Dictionary<long, QuestData> LoadQuests(IReadOnlyDictionary<long, QuestObjConfigData> objDict)
        {
            IReadOnlyList<QuestDataConfig> configs = LubanManager.Instance.TbQuestData.DataList;

            Dictionary<long, QuestData> result = new(configs.Count);
            foreach (QuestDataConfig config in configs) result.Add(config.Id, new QuestData(config, objDict));
            return result;
        }

        public static Dictionary<long, QuestCategory> LoadCategories()
        {
            IReadOnlyList<QuestCategoryData> configs = LubanManager.Instance.TbQuestCategoryData.DataList;

            Dictionary<long, QuestCategory> result = new(configs.Count);
            foreach (QuestCategoryData config in configs) result.Add(config.Id, new QuestCategory(config));
            return result;
        }
    }
}
