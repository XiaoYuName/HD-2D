using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务配置校验：查参数指向的东西是不是真的存在。
    /// 在 <see cref="QuestManager"/> 初始化阶段把全表跑一遍，策划填错的 ID 当场 LogError 指出是哪个任务哪一条
    /// —— 所以运行时取配置一律不做空判兜底，查不到就是配置错了，该被看见。
    ///
    /// 目标和奖励共用这一个类，是因为 CheckItem / CheckCharacter 这些检查两边完全一样，拆两份只会各改一半。
    /// </summary>
    public static class QuestConfigValidator
    {
        public static void ValidateObjectives(List<QuestObjInfoBase> objectives, List<QuestArgs> configs)
        {
            if (objectives == null || configs == null) return;

            int count = Mathf.Min(objectives.Count, configs.Count);
            for (int i = 0; i < count; i++) objectives[i].Validate(configs[i]);
        }

        public static void ValidateRewards(List<IQuestReward> rewards)
        {
            if (rewards == null) return;
            foreach (IQuestReward reward in rewards) reward.Validate();
        }

        #region 各种检查

        public static bool CheckItem(long itemId, QuestArgs config)
        {
            if (InventoryManager.Instance.GetItemData(itemId) != null) return true;

            LogError(config, $"道具 {itemId} 在道具表里不存在");
            return false;
        }

        public static bool CheckCharacter(long npcId, QuestArgs config)
        {
            if (CharacterManager.Instance.GetCharacterDataByID(npcId) != null) return true;

            LogError(config, $"角色 {npcId} 在角色表里不存在");
            return false;
        }

        public static bool CheckQuest(long questId, QuestArgs config)
        {
            if (QuestManager.Instance.GetQuestData(questId) != null) return true;

            LogError(config, $"任务 {questId} 在任务表里不存在");
            return false;
        }

        /// <summary>数量/数值必须是正数，配 0 或负数多半是手误。</summary>
        public static bool CheckPositive(int value, string fieldName, QuestArgs config)
        {
            if (value > 0) return true;

            LogError(config, $"{fieldName} 是 {value}，必须为正数");
            return false;
        }

        public static bool CheckId(long id, string fieldName, QuestArgs config)
        {
            if (id > 0) return true;

            LogError(config, $"{fieldName} 是 {id}，必须是正的 ID");
            return false;
        }

        #endregion

        static void LogError(QuestArgs config, string reason)
            => Debug.LogError($"[Quest] 任务 {config.QuestId} 的 \"{config.Raw}\" {reason}");
    }
}
