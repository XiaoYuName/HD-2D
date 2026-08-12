namespace TestSystem
{
    using System.Text;
    using UnityEngine;
    using XFramework;

    /// <summary>
    /// 任务测试：按配表生成每个任务的接受按钮，另有一组走完整流程的检查。
    /// 目标进度全靠事件推，所以这里也用 <see cref="QuestEventBus"/> 上报事件来推进，而不是直接改进度。
    /// 交付已改成自动的：目标全达成就自己完成发奖，所以这里没有交付按钮。
    /// </summary>
    public sealed class QuestTestCategory : ITestCategory
    {
        public string Title => "任务测试";

        public void SetActions(TestActionList actionList)
        {
            actionList.Add("打开任务面板", OpenPanel);
            actionList.Add("打印全部任务状态", DumpAll);
            actionList.Add("打印任务类别", DumpCategories);
            actionList.Add("重扫可接受任务", TryAcceptPassive);

            if (!QuestManager.IsInitialized)
            {
                Debug.LogWarning("[Test] QuestManager 未初始化（需要挂到场景里的管理器 GameObject 上），只能用打印按钮。");
                return;
            }

            foreach (QuestDataConfig config in LubanManager.Instance.TbQuestData.DataList)
            {
                long questId = config.Id;
                string name = string.IsNullOrEmpty(config.Remark) ? questId.ToString() : config.Remark;

                actionList.Add($"接受 {name}", () => Accept(questId));
            }

            actionList.Add("上报：与NPC 10001 对话", () => QuestEventBus.ReportNpcTalked(10001));
            actionList.Add("上报：暴走冲刺 胜利", () => QuestEventBus.ReportMiniGameFinished(MiniGameType.CrashSprint, MiniGameResult.Win));
            actionList.Add("上报：暴走冲刺 失败", () => QuestEventBus.ReportMiniGameFinished(MiniGameType.CrashSprint, MiniGameResult.Lose));
            actionList.Add("上报：给 10001 送礼 100001", () => QuestEventBus.ReportGiftGiven(10001, 100001, 1));
            actionList.Add("上报：购买道具 100001", () => QuestEventBus.ReportItemBought(100001, 1));
        }

        static void OpenPanel()
        {
            if (!CheckManager()) return;

            UISystem.Instance.OpenUI<QuestPanel>(UIPanelIdSet.QuestPanel);
        }

        static void Accept(long questId)
        {
            if (!CheckManager()) return;

            QuestInfo info = QuestManager.Instance.AcceptQuest(questId);
            if (info != null) Debug.Log($"[Test] 已接受 {info}");
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
                if (!QuestManager.Instance.IsQuestAccepted(config.Id))
                {
                    builder.AppendLine($"  {config.Id} {config.Remark}：未接受");
                    continue;
                }

                QuestInfo info = QuestManager.Instance.GetQuest(config.Id);

                builder.AppendLine($"  {config.Id} {info.Name}：{info.StateText}");

                // 顺带把多语言文案打出来，缺 Key 或占位符对不上在这里就能看见
                foreach (QuestObjStateInfo obj in info.Objectives)
                {
                    QuestObjConfigData objConfig = obj.Config;
                    string extra = objConfig.HasExtra ? (obj.ExceedAchieved ? "，超额已达成" : "，超额未达成") : string.Empty;
                    builder.AppendLine($"      目标{obj.Id}: {obj.Desc}{(obj.IsComplete ? " [完成]" : string.Empty)}{extra}");
                    AppendRewards(builder, "        目标奖励", objConfig.Rewards);
                    if (objConfig.HasExtra) AppendRewards(builder, "        超额奖励", objConfig.ExtraRewards);
                }

                AppendRewards(builder, "      任务奖励", info.Data.Rewards);
            }

            Debug.Log(builder.ToString());
        }

        static void DumpCategories()
        {
            if (!CheckManager()) return;

            StringBuilder builder = new();
            builder.AppendLine("[Test] 任务类别：");

            foreach (QuestCategory category in QuestManager.Instance.GetCategories())
            {
                bool done = QuestManager.Instance.IsCategoryCompleted(category);
                bool rewarded = QuestManager.Instance.IsCategoryRewarded(category.Id);
                builder.AppendLine($"  {category.Id} {category.Remark}："
                    + $"{(done ? "全部完成" : "未完成")}，类别奖励{(rewarded ? "已发" : "未发")}");
                builder.AppendLine($"      任务：{string.Join(", ", category.QuestIds)}");
                AppendRewards(builder, "      类别奖励", category.Rewards);
            }

            Debug.Log(builder.ToString());
        }

        static void AppendRewards(StringBuilder builder, string title, IQuestReward[] rewards)
        {
            if (rewards.Length == 0) return;

            builder.Append(title).Append("：");
            for (int i = 0; i < rewards.Length; i++)
            {
                if (i > 0) builder.Append('、');
                builder.Append(rewards[i].GetDesc());
            }
            builder.AppendLine();
        }

        static bool CheckManager()
        {
            if (QuestManager.IsInitialized) return true;

            Debug.LogWarning("[Test] QuestManager 未初始化，请确认它已挂到场景里的管理器 GameObject 上。");
            return false;
        }
    }
}
