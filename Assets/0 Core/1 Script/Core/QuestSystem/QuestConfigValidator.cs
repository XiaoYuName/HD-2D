using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务配置校验，两层都在游戏启动时跑完：
    /// <list type="bullet">
    /// <item><see cref="ValidateUsage"/>：段数够不够、类型名认不认识 —— 在实例化之前查，正确写法来自各 Usage 字典。</item>
    /// <item><see cref="ValidateAll"/>：参数指向的道具/角色/任务在不在表里 —— 要等全部任务读完才能互相查。</item>
    /// </list>
    /// 策划填错的当场 LogError 指出是哪个任务哪一条，所以运行时一律不做空判兜底。
    ///
    /// 目标和奖励共用这一个类，是因为 CheckItem / CheckCharacter 这些检查两边完全一样，拆两份只会各改一半。
    /// </summary>
    public static class QuestConfigValidator
    {
        /// <summary>查写法：类型名认不认识、参数够不够。</summary>
        public static void ValidateUsage<T>(QuestArgs[] configs, IReadOnlyDictionary<T, QuestUsage> usages)
            where T : struct, Enum
        {
            foreach (QuestArgs config in configs) ValidateUsage(config, usages);
        }

        /// <summary>没配（null）就跳过，可选列用得上。</summary>
        public static void ValidateUsage<T>(QuestArgs config, IReadOnlyDictionary<T, QuestUsage> usages)
            where T : struct, Enum
        {
            if (config == null) return;

            T type = config.GetHead(default(T));
            if (!usages.TryGetValue(type, out QuestUsage usage))
            {
                LogError(config, $"类型 {type} 没有登记写法，检查 {typeof(T).Name} 与对应的 Usage 字典");
                return;
            }
            config.Require(usage.LeastArgs, usage.Text);
        }

        /// <summary>
        /// 查 ID 存在性。要等三张表全部读完，因为任务之间会互相引用（<c>CompleteQuest:任务ID</c>）。
        /// 逐条的活交给各自的 Validate，因为解析后的参数是它们的私有数据。
        /// </summary>
        public static void ValidateAll(
            IReadOnlyDictionary<long, QuestData> questDataDict,
            IReadOnlyDictionary<long, QuestObjConfigData> objDataDict,
            IReadOnlyDictionary<long, QuestCategory> categoryDict)
        {
            foreach (QuestObjConfigData data in objDataDict.Values) data.Validate();
            foreach (QuestData data in questDataDict.Values) data.Validate();

            foreach (QuestCategory category in categoryDict.Values)
            {
                category.Validate();
                foreach (long questId in category.QuestIds)
                {
                    if (!questDataDict.ContainsKey(questId))
                    {
                        Debug.LogError($"[Quest] 任务类别 {category.Id} 引用的任务 {questId} 在任务表里不存在");
                    }
                }
            }
        }

        public static void ValidateRewards(IQuestReward[] rewards)
        {
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
            if (QuestManager.Instance.ContainQuestData(questId)) return true;

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

        /// <summary>描述文案不能不填 —— 现在没有类型自带的默认说法了，空的话 UI 上就是一行空白。</summary>
        public static bool CheckDescKey(QuestObjData data, string fieldName, QuestArgs config)
        {
            if (data.HasDescKey) return true;

            LogError(config, $"没配 {fieldName}，界面上会是一行空白");
            return false;
        }

        /// <summary>小游戏类型不能留空/写 None，否则任何一局结算都对不上。</summary>
        public static bool CheckMiniGame(MiniGameType game, QuestArgs config)
        {
            if (game != MiniGameType.None) return true;

            LogError(config, $"{QuestFieldName.GameType} 没填，可填的类型见 MiniGameType 枚举");
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
            => Debug.LogError($"[Quest] {config.Owner} 的 \"{config.Raw}\" {reason}");
    }
}
