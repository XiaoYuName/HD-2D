using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 任务配置校验，分两处跑：
    /// <list type="bullet">
    /// <item><see cref="ValidateAll"/>：游戏启动时，查参数指向的道具/角色/任务在不在表里 —— 要等全部配置读完才能互相查。</item>
    /// <item><see cref="CheckStructure"/>：编辑器里点「校验配置」，只查结构（ID、引用、文案），不碰运行时管理器。</item>
    /// </list>
    /// 填错的当场 LogError 指出是哪个任务哪一条，所以运行时一律不做空判兜底。
    ///
    /// 目标、触发、奖励共用这一个类，是因为 CheckItem / CheckCharacter 这些检查几边完全一样，拆开只会各改一半。
    /// </summary>
    public static class QuestConfigValidator
    {
        /// <summary>启动时的存在性校验。逐条的活交给各自的 Validate，因为参数是它们的私有字段。</summary>
        public static void ValidateAll(QuestConfig database)
        {
            foreach (QuestObjConfigData data in database.ObjDict.Values) data.Validate();
            foreach (QuestData data in database.QuestDict.Values) data.Validate();

            foreach (QuestCategory category in database.CategoryDict.Values)
            {
                category.Validate();
                foreach (long questId in category.QuestIds)
                {
                    if (!database.QuestDict.ContainsKey(questId))
                        Debug.LogError($"[Quest] 任务类别 {category.Id} 引用的任务 {questId} 在任务表里不存在");
                }
            }
        }

        #region 各种检查

        public static bool CheckItem(long itemId, string owner)
        {
            if (InventoryManager.Instance.GetItemData(itemId) != null) return true;

            LogError(owner, $"道具 {itemId} 在道具表里不存在");
            return false;
        }

        public static bool CheckCharacter(long npcId, string owner)
        {
            if (CharacterManager.Instance.GetCharacterDataByID(npcId) != null) return true;

            LogError(owner, $"角色 {npcId} 在角色表里不存在");
            return false;
        }

        public static bool CheckQuest(long questId, string owner)
        {
            if (QuestManager.Instance.ContainQuestData(questId)) return true;

            LogError(owner, $"任务 {questId} 在任务表里不存在");
            return false;
        }

        /// <summary>数量/数值必须是正数，配 0 或负数多半是手误。</summary>
        public static bool CheckPositive(int value, string fieldName, string owner)
        {
            if (value > 0) return true;

            LogError(owner, $"{fieldName} 是 {value}，必须为正数");
            return false;
        }

        /// <summary>描述文案不能不填 —— 空的话 UI 上就是一行空白。</summary>
        public static bool CheckDesc(QuestObjData data, string owner)
        {
            if (data.HasDesc) return true;

            LogError(owner, "没配目标描述，界面上会是一行空白");
            return false;
        }

        /// <summary>小游戏类型不能留 None，否则任何一局结算都对不上。</summary>
        public static bool CheckMiniGame(MiniGameType game, string owner)
        {
            if (game != MiniGameType.None) return true;

            LogError(owner, $"{QuestFieldName.GameType} 没填，可填的类型见 MiniGameType 枚举");
            return false;
        }

        public static bool CheckId(long id, string fieldName, string owner)
        {
            if (id > 0) return true;

            LogError(owner, $"{fieldName} 是 {id}，必须是正的 ID");
            return false;
        }

        #endregion

        /// <summary>
        /// 编辑器用的结构校验：只看配置自己（ID、互相引用、文案、有没有漏选类型），
        /// 不查道具/角色是否存在 —— 那些要靠运行时管理器，等进游戏由 <see cref="ValidateAll"/> 报。
        /// </summary>
        public static void CheckStructure(QuestConfig database)
        {
            List<string> problems = new();

            foreach (KeyValuePair<long, QuestObjConfigData> pair in database.ObjDict)
            {
                QuestObjConfigData data = pair.Value;
                string owner = $"目标 {pair.Key}";

                if (data.TargetData == null) problems.Add($"{owner} 没选主目标类型");
                else if (!data.TargetData.HasDesc) problems.Add($"{owner} 没配目标描述");
                if (data.ExtraData != null && !data.ExtraData.HasDesc) problems.Add($"{owner} 的超额目标没配描述");

                CheckRewardViews(data.Rewards, owner, database, problems);
                CheckRewardViews(data.ExtraRewards, owner + " 的超额奖励", database, problems);
            }

            foreach (KeyValuePair<long, QuestData> pair in database.QuestDict)
            {
                QuestData data = pair.Value;
                string owner = $"任务 {pair.Key}";

                if (!data.HasName) problems.Add($"{owner} 没配名称");
                if (data.AcceptCond > 0 && !database.CondDict.ContainsKey(data.AcceptCond))
                    problems.Add($"{owner} 引用的接受条件 {data.AcceptCond} 不存在");

                foreach (IQuestTrigger trigger in data.Triggers)
                {
                    if (trigger == null) problems.Add($"{owner} 的触发列表里有没选类型的空项");
                }
                CheckRewardViews(data.Rewards, owner, database, problems);
            }

            foreach (KeyValuePair<long, QuestCategory> pair in database.CategoryDict)
            {
                QuestCategory data = pair.Value;
                string owner = $"类别 {pair.Key}";

                foreach (long questId in data.QuestIds)
                {
                    if (!database.QuestDict.ContainsKey(questId)) problems.Add($"{owner} 引用的任务 {questId} 不存在");
                }
                CheckRewardViews(data.Rewards, owner, database, problems);
            }

            if (problems.Count == 0)
            {
                Debug.Log("[Quest] 校验通过：结构没问题（道具/角色的存在性要进游戏后才查）。");
                return;
            }

            Debug.LogError($"[Quest] 校验发现 {problems.Count} 个问题：\n  " + string.Join("\n  ", problems));
        }

        /// <summary>目标ID 引用要在任务里查，这里顺带把「奖励类型没配显示」也报出来。</summary>
        static void CheckRewardViews(IReadOnlyList<IQuestReward> rewards, string owner,
            QuestConfig database, List<string> problems)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                IQuestReward reward = rewards[i];
                if (reward == null)
                {
                    problems.Add($"{owner} 的奖励列表里有没选类型的空项");
                    continue;
                }

                // 道具奖励的图标名字都在道具表里，不需要奖励显示配置
                if (reward.Type == QuestRewardType.Item) continue;
                if (!database.RewardViewDict.ContainsKey(reward.Type))
                    problems.Add($"{owner} 用了 {reward.Type} 奖励，但没配对应的奖励显示");
            }
        }

        static void LogError(string owner, string reason) => Debug.LogError($"[Quest] {owner}：{reason}");
    }
}
