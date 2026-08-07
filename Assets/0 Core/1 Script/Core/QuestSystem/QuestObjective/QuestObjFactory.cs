using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public static class QuestObjFactory
    {
        static readonly Dictionary<QuestObjType, Func<QuestObjInfoBase>> Registry = new()
        {
            { QuestObjType.DayPassed, () => new DayPassedQuestObj() },
            { QuestObjType.Dialog, () => new DialogQuestObj() },
            { QuestObjType.CompleteQuest, () => new CompleteQuestQuestObj() },
            { QuestObjType.HoldItem, () => new HoldItemQuestObj() },
            { QuestObjType.NpcProp, () => new NpcPropQuestObj() },
            { QuestObjType.CompleteGame, () => new CompleteGameQuestObj() },
            { QuestObjType.DialogNpc, () => new DialogNpcQuestObj() },
            { QuestObjType.DialogNpcWithItem, () => new DialogNpcWithItemQuestObj() },
            { QuestObjType.GiveGift, () => new GiveGiftQuestObj() },
            { QuestObjType.BuyItem, () => new BuyItemQuestObj() },
        };

        public static QuestObjInfoBase Create(QuestArgs config)
        {
            if (config == null) return null;

            QuestObjType type = config.GetHead(QuestObjType.None);
            if (!Registry.TryGetValue(type, out Func<QuestObjInfoBase> creator))
            {
                Debug.LogError($"[Quest] 任务 {config.QuestId} 的目标类型 {type} 还没注册实现: {config}");
                return null;
            }

            QuestObjInfoBase obj = creator();
            obj.Init(config);
            return obj;
        }

        public static List<QuestObjInfoBase> CreateList(List<QuestArgs> configs)
        {
            List<QuestObjInfoBase> result = new(configs.Count);
            foreach (QuestArgs config in configs)
            {
                QuestObjInfoBase obj = Create(config);
                if (obj != null) result.Add(obj);
            }
            return result;
        }

        public static void Register(QuestObjType type, Func<QuestObjInfoBase> creator) => Registry[type] = creator;
    }
}
