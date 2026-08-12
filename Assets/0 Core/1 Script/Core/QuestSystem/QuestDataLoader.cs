using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 把 Luban / ScriptableObject 配置合并为统一的运行时数据。
    /// 目标要先于任务读完，因为任务只存目标 ID；<see cref="QuestManager"/> 不关心实际数据源。
    /// </summary>
    public static class QuestDataLoader
    {
        public static Dictionary<long, QuestObjConfigData> LoadObjs()
        {
            Dictionary<long, QuestObjConfigData> result = new();
            if (QuestDatabaseProvider.UseLuban)
            {
                IReadOnlyList<QuestObjConfig> configs = LubanManager.Instance.TbQuestObjData.DataList;
                foreach (QuestObjConfig config in configs) result.Add(config.Id, new QuestObjConfigData(config));
            }

            if (QuestDatabaseProvider.UseScriptableObject)
            {
                foreach (QuestObjectiveDefinition config in QuestDatabaseProvider.Database.Objectives)
                {
                    if (config.enabled) result[config.id] = new QuestObjConfigData(config);
                }
            }
            return result;
        }

        public static Dictionary<long, QuestData> LoadQuests(IReadOnlyDictionary<long, QuestObjConfigData> objDict)
        {
            Dictionary<long, QuestData> result = new();
            if (QuestDatabaseProvider.UseLuban)
            {
                IReadOnlyList<QuestDataConfig> configs = LubanManager.Instance.TbQuestData.DataList;
                foreach (QuestDataConfig config in configs) result.Add(config.Id, new QuestData(config, objDict));
            }

            if (QuestDatabaseProvider.UseScriptableObject)
            {
                foreach (QuestDefinition config in QuestDatabaseProvider.Database.Quests)
                {
                    if (config.enabled) result[config.id] = new QuestData(config, objDict);
                }
            }
            return result;
        }

        public static Dictionary<long, QuestCategory> LoadCategories()
        {
            Dictionary<long, QuestCategory> result = new();
            if (QuestDatabaseProvider.UseLuban)
            {
                IReadOnlyList<QuestCategoryData> configs = LubanManager.Instance.TbQuestCategoryData.DataList;
                foreach (QuestCategoryData config in configs) result.Add(config.Id, new QuestCategory(config));
            }

            if (QuestDatabaseProvider.UseScriptableObject)
            {
                foreach (QuestCategoryDefinition config in QuestDatabaseProvider.Database.Categories)
                {
                    if (config.enabled) result[config.id] = new QuestCategory(config);
                }
            }
            return result;
        }
    }
}
