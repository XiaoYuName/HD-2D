using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 状态条件判定（<see cref="QuestStoryCondData"/>）。剧情系统与任务系统共用。
    /// </summary>
    public class CondManager : MonoSingleton<CondManager>
    {
        IReadOnlyDictionary<long, QuestStoryCondData> CondDict => LubanManager.Instance.TbQuestStoryCondData.DataMap;
        /// <summary>条件表里各项都是 AND：全部满足才算通过。空条件（ID&lt;=0）视为无门槛。</summary>
        public bool IsMatched(long condId)
        {
            if (condId <= 0) return true;

            // 任务系统的条件已经全部搬到任务数据库里；剧情那边还在 Luban 表，所以两条路都留着
            QuestConfig config = QuestConfigProvider.Config;
            if (config != null && config.GetCond(condId, out QuestCondData soCond)) return IsMatched(soCond);

            // 配错 ID 只当作不满足并报错：这里是任务领取的扫描路径，抛异常会把同一批别的任务一起带走
            if (!CondDict.TryGetValue(condId, out QuestStoryCondData cond))
            {
                Debug.LogError($"[Cond] 条件 {condId} 在 QuestStoryCondData 里不存在，按不满足处理");
                return false;
            }

            return IsMatched(cond);
        }

        public bool IsMatched(QuestStoryCondData cond)
        {
            return IsItemOwnMatched(cond.ItemOwn)
                   && IsNpcPropMatched(cond.NpcProp)
                   && IsPlotPreMatched(cond.PlotPre)
                   && IsDlgPreMatched(cond.DlgPre)
                   && IsTimeMatched(cond.Day, cond.TimeSlot)
                   && IsQuestPreMatched(cond.QuestPre);
        }

        public bool IsMatched(QuestCondData cond)
        {
            return IsItemOwnMatched(cond.items)
                   && IsNpcPropMatched(cond.characterProps)
                   && IsPlotPreMatched(cond.plotPrerequisites)
                   && IsDlgPreMatched(cond.dialoguePrerequisites)
                   && IsTimeMatched(cond.day, cond.timeSlot)
                   && IsQuestPreMatched(cond.questPrerequisites);
        }

        public QuestStoryCondData GetCond(long condId) => CondDict[condId];

        #region 分项判定

        /// <summary>道具持有：ItemID + 数量。</summary>
        public bool IsItemOwnMatched(List<TbUlocakItemData> itemOwn)
        {
            foreach (TbUlocakItemData need in itemOwn)
            {
                if (InventoryManager.Instance.GetItemCount(need.ItemID) < need.Value)
                    return false;
            }
            return true;
        }

        public bool IsItemOwnMatched(List<QuestItemRequirement> itemOwn)
        {
            foreach (QuestItemRequirement need in itemOwn)
            {
                if (InventoryManager.Instance.GetItemCount(need.itemId) < need.count) return false;
            }
            return true;
        }

        /// <summary>NPC 数值达标：角色ID + 属性类型 + 数值。</summary>
        public bool IsNpcPropMatched(List<TbUlockCharacterData> npcProp)
        {
            foreach (TbUlockCharacterData need in npcProp)
            {
                CharacterBag bag = CharacterManager.Instance.GetCharacterBag(need.CharacterID);
                if (bag.GetPropertyValue(need.CharacterType) < need.Value)
                    return false;
            }
            return true;
        }

        public bool IsNpcPropMatched(List<QuestCharacterRequirement> npcProp)
        {
            foreach (QuestCharacterRequirement need in npcProp)
            {
                CharacterBag bag = CharacterManager.Instance.GetCharacterBag(need.npcId);
                if (bag.GetPropertyValue(need.propType) < need.value) return false;
            }
            return true;
        }

        /// <summary>对话前置：这些对话都得播过。</summary>
        public bool IsDlgPreMatched(List<long> dlgPre)
        {
            foreach (long dlgId in dlgPre)
            {
                if (!DramaManager.Instance.HasDialogue(dlgId))
                    return false;
            }
            return true;
        }

        /// <summary>剧情前置。剧情模块尚无「已完成」记录，见 <see cref="IsPlotFinished"/>。</summary>
        public bool IsPlotPreMatched(List<long> plotPre)
        {
            foreach (long plotId in plotPre)
            {
                if (plotId <= 0) continue;
                if (!IsPlotFinished(plotId)) return false;
            }
            return true;
        }

        /// <summary>
        /// 剧情模块是否已完成。占位：DramaManager 目前只记录到对话粒度，没有剧情模块完成记录，
        /// 剧情系统补上之后把这里换成真实查询即可，其余代码不用动。
        /// </summary>
        public bool IsPlotFinished(long plotId)
        {
            Debug.LogWarning($"[Cond] 剧情前置 {plotId} 暂未接入（剧情模块完成记录尚未实现），按不满足处理");
            return false;
        }

        /// <summary>天数达到 + 时间段命中。Day 填 0 表示不限；TimeSlot 填 All 表示不限。</summary>
        public bool IsTimeMatched(int needDay, ShowRuleTimeType needSlot)
        {
            PlayerData player = GameDataManager.Instance.PlayerData;
            if (needDay > 0 && player.Day < needDay) return false;
            if (needSlot != 0 && needSlot != ShowRuleTimeType.All
                              && !needSlot.HasFlag(player.GetTimeType())) return false;
            return true;
        }

        /// <summary>前置任务全部完成。</summary>
        public bool IsQuestPreMatched(List<long> questPre)
        {
            foreach (long questId in questPre)
            {
                if (!QuestManager.Instance.IsQuestCompleted(questId)) return false;
            }
            return true;
        }

        #endregion
    }
}
