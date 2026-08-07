using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestObjType"/> 分发到各自的目标实现。
    /// 加一种目标 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="QuestObjInfoBase"/> 子类 + 在这里注册。
    /// </summary>
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

        /// <summary>
        /// 读档用：存档里的实例类型和配置对得上就沿用（保住进度），对不上就按配置新建。
        /// 沿用的实例会重新 <see cref="QuestObjInfoBase.Init"/> 一遍，所以配置改了立刻生效。
        /// </summary>
        public static List<QuestObjInfoBase> CreateList(List<QuestArgs> configs, List<QuestObjInfoBase> saved)
        {
            List<QuestObjInfoBase> result = new(configs.Count);
            for (int i = 0; i < configs.Count; i++)
            {
                QuestObjInfoBase fresh = Create(configs[i]);
                if (fresh == null) continue;

                QuestObjInfoBase saveObj = saved != null && i < saved.Count ? saved[i] : null;
                if (saveObj != null && saveObj.GetType() == fresh.GetType())
                {
                    saveObj.Init(configs[i]);
                    result.Add(saveObj);
                }
                else
                {
                    result.Add(fresh);
                }
            }
            return result;
        }

        public static void Register(QuestObjType type, Func<QuestObjInfoBase> creator) => Registry[type] = creator;
    }
}
