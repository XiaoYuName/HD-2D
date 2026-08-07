using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务奖励配置校验。在 <see cref="QuestManager"/> 初始化阶段跑一遍，
    /// 策划填错的 ID 当场 LogError 指出是哪个任务哪条奖励 —— 所以运行时取配置不再做空判兜底。
    /// </summary>
    public static class QuestRewardValidator
    {
        /// <summary>校验一整列奖励。返回是否全部合法。</summary>
        public static bool ValidateAll(List<IQuestReward> rewards)
        {
            if (rewards == null) return true;

            bool ok = true;
            foreach (IQuestReward reward in rewards)
            {
                if (!reward.Validate()) ok = false;
            }
            return ok;
        }

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

        /// <summary>数量/数值必须是正数，配 0 或负数多半是手误。</summary>
        public static bool CheckPositive(int value, string fieldName, QuestArgs config)
        {
            if (value > 0) return true;

            LogError(config, $"{fieldName} 是 {value}，必须为正数");
            return false;
        }

        static void LogError(QuestArgs config, string reason)
            => Debug.LogError($"[Quest] 任务 {config.QuestId} 的奖励 \"{config.Raw}\" {reason}");
    }
}
