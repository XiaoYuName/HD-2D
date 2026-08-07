using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一条领取触发的配置，来自 <see cref="QuestData.AcceptTrigger"/>：<c>EnterZoneStay:101:5</c>。
    /// 触发只负责「什么时候去检查」，能不能领还要看 <see cref="QuestData.AcceptCond"/>。
    /// </summary>
    public class QuestTrigger
    {
        public QuestTriggerType Type;
        public QuestArgs Args;

        public long SceneId => Args.GetLong(0);
        public long NpcId => Args.GetLong(0);
        public long GameId => Args.GetLong(0);
        public long PlotId => Args.GetLong(0);
        public int Permille => Args.GetInt(0);
        public int Seconds => Args.GetInt(1);
        public int Result => Args.GetInt(1);

        /// <summary>没有事件驱动、靠重扫触发的类型（读档、任意事件后都会重扫一遍）。</summary>
        public bool IsPassive => Type is QuestTriggerType.None or QuestTriggerType.Auto or QuestTriggerType.RandomChance;

        public bool RollChance()
            => Type != QuestTriggerType.RandomChance || Random.Range(0, 1000) < Permille;

        public static List<QuestTrigger> ParseList(string text, long questId)
        {
            List<QuestTrigger> result = new();
            foreach (QuestArgs args in QuestArgs.SplitList(text, questId))
            {
                QuestTriggerType type = args.GetHead(QuestTriggerType.None);
                if (type == QuestTriggerType.None) continue;

                Validate(type, args);
                result.Add(new QuestTrigger { Type = type, Args = args });
            }
            return result;
        }

        static void Validate(QuestTriggerType type, QuestArgs args)
        {
            switch (type)
            {
                case QuestTriggerType.EnterZone:
                    args.Require(1, "EnterZone:场景ID");
                    break;
                case QuestTriggerType.ExitZone:
                    args.Require(1, "ExitZone:场景ID");
                    break;
                case QuestTriggerType.EnterZoneStay:
                    args.Require(2, "EnterZoneStay:场景ID:停留秒数");
                    break;
                case QuestTriggerType.ClickNpc:
                    args.Require(1, "ClickNpc:NPC ID");
                    break;
                case QuestTriggerType.DialogNpc:
                    args.Require(1, "DialogNpc:NPC ID");
                    break;
                case QuestTriggerType.GameEnd:
                    args.Require(1, "GameEnd:游戏ID");
                    break;
                case QuestTriggerType.GameResult:
                    args.Require(2, "GameResult:游戏ID:结果（1 胜 / 2 负）");
                    break;
                case QuestTriggerType.RandomChance:
                    args.Require(1, "RandomChance:千分比");
                    break;
                case QuestTriggerType.PlotEnd:
                    args.Require(1, "PlotEnd:剧情ID");
                    Debug.LogWarning($"[Quest] 任务 {args.QuestId} 用了 PlotEnd 触发，剧情完成记录尚未实现，不会触发");
                    break;
            }
        }

        public override string ToString() => Args?.Raw ?? Type.ToString();
    }
}
