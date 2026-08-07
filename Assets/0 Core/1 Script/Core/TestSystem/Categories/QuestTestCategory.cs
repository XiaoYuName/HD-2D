namespace TestSystem
{
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using XFramework;

    /// <summary>
    /// 任务测试：按配表生成每个任务的领取/交付按钮，另有一组走完整流程的检查。
    /// 目标进度全靠事件推，所以这里也用 <see cref="QuestEventBus"/> 上报事件来推进，而不是直接改进度。
    /// </summary>
    public sealed class QuestTestCategory : ITestCategory
    {
        public string Title => "任务测试";

        public void SetActions(TestActionList actionList)
        {
            actionList.Add("打印全部任务状态", DumpAll);
            actionList.Add("重扫可领取任务", TryAcceptPassive);

            if (!QuestManager.IsInitialized)
            {
                Debug.LogWarning("[Test] QuestManager 未初始化（需要挂到场景里的管理器 GameObject 上），只能用打印按钮。");
                return;
            }

            foreach (QuestDataConfig config in LubanManager.Instance.TbQuestData.DataList)
            {
                long questId = config.Id;
                string name = string.IsNullOrEmpty(config.Remark) ? questId.ToString() : config.Remark;

                actionList.Add($"领取 {name}", () => Accept(questId));
                actionList.Add($"交付 {name}", () => Complete(questId));
            }

            actionList.Add("上报：与NPC 10001 对话", () => QuestEventBus.ReportNpcTalked(10001));
            actionList.Add("上报：小游戏 1001 胜利", () => QuestEventBus.ReportMiniGameFinished(1001, 1));
            actionList.Add("上报：小游戏 1001 失败", () => QuestEventBus.ReportMiniGameFinished(1001, 2));
            actionList.Add("上报：给 10001 送礼 100001", () => QuestEventBus.ReportGiftGiven(10001, 100001, 1));
            actionList.Add("上报：购买道具 100001", () => QuestEventBus.ReportItemBought(100001, 1));
        }

        static void Accept(long questId)
        {
            if (!CheckManager()) return;

            QuestInfo info = QuestManager.Instance.AcceptQuest(questId);
            if (info != null) Debug.Log($"[Test] 已领取 {info}");
        }

        static void Complete(long questId)
        {
            if (!CheckManager()) return;

            if (QuestManager.Instance.CompleteQuest(questId))
            {
                Debug.Log($"[Test] 任务 {questId} 交付成功，奖励已发（超额奖励见下）");
                DumpRewards(questId);
            }
        }

        static void TryAcceptPassive()
        {
            if (!CheckManager()) return;

            // 等价于读档/天数变化后的那次扫描：没配触发、Auto、随机概率的任务在这里被捞出来判条件
            QuestManager.Instance.TryAcceptPassive();
            Debug.Log("[Test] 已重扫被动触发任务，结果见「打印全部任务状态」。");
        }

        static void DumpAll()
        {
            if (!CheckManager()) return;

            StringBuilder builder = new();
            builder.AppendLine("[Test] 任务状态：");

            foreach (QuestDataConfig config in LubanManager.Instance.TbQuestData.DataList)
            {
                QuestInfo info = QuestManager.Instance.GetQuest(config.Id);
                if (info == null)
                {
                    builder.AppendLine($"  {config.Id} {config.Remark}：未领取");
                    continue;
                }

                builder.AppendLine($"  {config.Id} {info.Name}：{info.StateText}"
                    + $"（超额{(info.ExceedAchieved ? "已" : "未")}达成）");
                AppendObjectives(builder, "目标", info.Objectives);
                AppendObjectives(builder, "超额", info.ExtraObjectives);
            }

            Debug.Log(builder.ToString());
        }

        static void AppendObjectives(StringBuilder builder, string title, IReadOnlyList<QuestObjInfoBase> objectives)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                // 顺带把多语言文案打出来，缺 Key 或占位符对不上在这里就能看见
                builder.AppendLine($"      {title}{i}: {objectives[i].GetDesc()}");
            }
        }

        static void DumpRewards(long questId)
        {
            QuestData data = QuestManager.Instance.GetQuestData(questId);
            foreach (IQuestReward reward in data.Rewards) Debug.Log($"[Test] 奖励：{reward.GetDesc()}");
            foreach (IQuestReward reward in data.ExtraRewards) Debug.Log($"[Test] 超额奖励：{reward.GetDesc()}");
        }

        static bool CheckManager()
        {
            if (QuestManager.IsInitialized) return true;

            Debug.LogWarning("[Test] QuestManager 未初始化，请确认它已挂到场景里的管理器 GameObject 上。");
            return false;
        }
    }
}
