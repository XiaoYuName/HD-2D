using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 状态条件判定（<see cref="QuestStoryCondData"/>）。剧情系统与任务系统共用。
    /// 判定本身无状态，全部走静态方法，不依赖场景里挂没挂这个组件。
    /// </summary>
    public class CondManager : MonoSingleton<CondManager>
    {
        /// <summary>条件表里各项都是 AND：全部满足才算通过。空条件（ID&lt;=0）视为无门槛。</summary>
        public static bool IsMatched(long condId)
        {
            if (condId <= 0) return true;

            QuestStoryCondData cond = GetCond(condId);
            if (cond == null) return false;
            return IsMatched(cond);
        }

        public static bool IsMatched(QuestStoryCondData cond)
        {
            if (cond == null) return false;
            return IsItemOwnMatched(cond.ItemOwn)
                   && IsNpcPropMatched(cond.NpcProp)
                   && IsPlotPreMatched(cond.PlotPre)
                   && IsDlgPreMatched(cond.DlgPre)
                   && IsTimeMatched(cond.Day, cond.TimeSlot)
                   && IsQuestPreMatched(cond.QuestPre);
        }

        public static QuestStoryCondData GetCond(long condId)
        {
            QuestStoryCondData cond = LubanManager.Instance.TbQuestStoryCondData.GetOrDefault(condId);
            if (cond == null) Debug.LogError($"[Cond] 找不到条件配置 {condId}");
            return cond;
        }

        #region 分项判定

        /// <summary>道具持有：ItemID + 数量。</summary>
        public static bool IsItemOwnMatched(List<TbUlocakItemData> itemOwn)
        {
            if (itemOwn == null) return true;
            foreach (TbUlocakItemData need in itemOwn)
            {
                if (need.ItemID <= 0) continue;
                if (InventoryManager.Instance.GetItemCount(need.ItemID) < need.Value) return false;
            }
            return true;
        }

        /// <summary>NPC 数值达标：角色ID + 属性类型 + 数值。</summary>
        public static bool IsNpcPropMatched(List<TbUlockCharacterData> npcProp)
        {
            if (npcProp == null) return true;
            foreach (TbUlockCharacterData need in npcProp)
            {
                if (need.CharacterID <= 0) continue;
                CharacterBag bag = CharacterManager.Instance.GetCharacterBag(need.CharacterID);
                if (bag == null || bag.GetPropertyValue(need.CharacterType) < need.Value) return false;
            }
            return true;
        }

        /// <summary>对话前置：这些对话都得播过。</summary>
        public static bool IsDlgPreMatched(List<long> dlgPre)
        {
            if (dlgPre == null) return true;
            foreach (long dlgId in dlgPre)
            {
                if (dlgId <= 0) continue;
                if (!DramaManager.Instance.HasDialogue(dlgId)) return false;
            }
            return true;
        }

        /// <summary>剧情前置。剧情模块尚无「已完成」记录，见 <see cref="IsPlotFinished"/>。</summary>
        public static bool IsPlotPreMatched(List<long> plotPre)
        {
            if (plotPre == null) return true;
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
        public static bool IsPlotFinished(long plotId)
        {
            Debug.LogWarning($"[Cond] 剧情前置 {plotId} 暂未接入（剧情模块完成记录尚未实现），按不满足处理");
            return false;
        }

        /// <summary>天数达到 + 时间段命中。Day 填 0 表示不限；TimeSlot 填 All 表示不限。</summary>
        public static bool IsTimeMatched(int needDay, ShowRuleTimeType needSlot)
        {
            PlayerData player = GameDataManager.Instance.PlayerData;
            if (needDay > 0 && player.Day < needDay) return false;
            if (needSlot != 0 && needSlot != ShowRuleTimeType.All
                              && !needSlot.HasFlag(player.GetTimeType())) return false;
            return true;
        }

        /// <summary>前置任务全部完成。</summary>
        public static bool IsQuestPreMatched(List<long> questPre)
        {
            if (questPre == null) return true;
            foreach (long questId in questPre)
            {
                if (questId <= 0) continue;
                if (!QuestManager.Instance.IsQuestCompleted(questId)) return false;
            }
            return true;
        }

        #endregion
    }
}
