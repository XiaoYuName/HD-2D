using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 按 <see cref="QuestObjType"/> 分发到各自的目标实现。
    /// 加一种目标 = <c>__enums__.xlsx</c> 加一项 + 写个 <see cref="QuestObjData"/> 子类 + 在这里注册。
    ///
    /// 这里不做任何空判：配置写错就当场抛出来，别拿 null 往下传，等到运行时才炸在别处。
    /// </summary>
    public static class QuestObjFactory
    {
        static readonly Dictionary<QuestObjType, Func<QuestObjData>> Registry = new()
        {
            { QuestObjType.DayPassed, () => new DayPassedObjData() },
            { QuestObjType.Dialog, () => new DialogObjData() },
            { QuestObjType.CompleteQuest, () => new CompleteQuestObjData() },
            { QuestObjType.HoldItem, () => new HoldItemObjData() },
            { QuestObjType.CharacterProp, () => new CharacterPropObjData() },
            { QuestObjType.CompleteGame, () => new CompleteGameObjData() },
            { QuestObjType.DialogNpc, () => new DialogNpcObjData() },
            { QuestObjType.DialogNpcWithItem, () => new DialogNpcWithItemObjData() },
            { QuestObjType.GiveGift, () => new GiveGiftObjData() },
            { QuestObjType.BuyItem, () => new BuyItemObjData() },
        };

        /// <summary>整局一份，只在读表时造，之后所有引用这条目标的任务共用。</summary>
        public static QuestObjData Create(QuestArgs config)
        {
            QuestObjType type = config.GetHead(QuestObjType.None);
            if (!Registry.TryGetValue(type, out Func<QuestObjData> creator))
            {
                throw new KeyNotFoundException($"[Quest] {config.Owner} 的 \"{config.Raw}\" 类型 {type} 还没注册实现");
            }

            QuestObjData data = creator();
            data.Init(config);
            return data;
        }

        public static QuestObjData Create(QuestObjectiveSpec config)
        {
            if (!Registry.TryGetValue(config.type, out Func<QuestObjData> creator))
                throw new KeyNotFoundException($"[Quest] 目标类型 {config.type} 还没注册实现");

            QuestObjData data = creator();
            data.Init(config);
            return data;
        }

        public static void Register(QuestObjType type, Func<QuestObjData> creator) => Registry[type] = creator;
    }
}
