using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestObjType"/> 分发到各自的目标实现。
    /// 加一种目标 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="QuestObjInfoBase"/> 子类 + 在这里注册。
    ///
    /// 这里不做任何空判：配置写错就当场抛出来，别拿 null 往下传，等到运行时才炸在别处。
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
            QuestObjType type = config.GetHead(QuestObjType.None);
            if (!Registry.TryGetValue(type, out Func<QuestObjInfoBase> creator))
            {
                throw new KeyNotFoundException($"[Quest] 任务 {config.QuestId} 的目标 \"{config.Raw}\" 类型 {type} 还没注册实现");
            }

            QuestObjInfoBase obj = creator();
            obj.Init(config);
            return obj;
        }

        public static QuestObjInfoBase[] CreateList(QuestArgs[] configs)
        {
            QuestObjInfoBase[] result = new QuestObjInfoBase[configs.Length];
            for (int i = 0; i < configs.Length; i++) result[i] = Create(configs[i]);
            return result;
        }

        /// <summary>
        /// 读档用：存档里的实例类型和配置对得上就沿用（保住累计进度），对不上就按配置新建。
        /// 沿用的实例会重新 <see cref="QuestObjInfoBase.Init"/> 一遍，所以配置改了立刻生效。
        /// </summary>
        public static QuestObjInfoBase[] CreateList(QuestArgs[] configs, QuestObjInfoBase[] saved)
        {
            QuestObjInfoBase[] result = new QuestObjInfoBase[configs.Length];
            for (int i = 0; i < configs.Length; i++)
            {
                QuestObjInfoBase fresh = Create(configs[i]);
                QuestObjInfoBase saveObj = i < saved.Length ? saved[i] : null;

                if (saveObj != null && saveObj.GetType() == fresh.GetType())
                {
                    saveObj.Init(configs[i]);
                    result[i] = saveObj;
                }
                else
                {
                    result[i] = fresh;
                }
            }
            return result;
        }

        public static void Register(QuestObjType type, Func<QuestObjInfoBase> creator) => Registry[type] = creator;
    }
}
