namespace XFramework
{
    /// <summary>
    /// 任务系统的多语言 Key，统一走 <see cref="LocTableSet.QuestSystem"/> 表。
    /// 文案在 <c>Assets/0 Core/1 Script/Data/QuestSystem/</c> 下的 CSV 里，别在业务代码里写魔法字符串。
    /// 任务自己的名称/描述 Key 由策划填在 QuestDataConfig.xlsx 的 NameKey/DescKey 列，不在这里列举。
    /// </summary>
    public static class QuestLocKey
    {
        /// <summary>奖励描述，文案在 QuestRewardDataLoc.csv。</summary>
        public static class Reward
        {
            const string Prefix = "QuestReward/";

            public const string Item = Prefix + nameof(Item);
            public const string Coin = Prefix + nameof(Coin);
            public const string GameCoin = Prefix + nameof(GameCoin);
            public const string Goodwill = Prefix + nameof(Goodwill);
        }

        /// <summary>目标描述，文案在 QuestDataLoc.csv。同一种目标按参数会有几种说法，所以 Key 比枚举多。</summary>
        public static class Obj
        {
            const string Prefix = "QuestObj/";

            public const string DayPassed = Prefix + nameof(DayPassed);
            public const string Dialog = Prefix + nameof(Dialog);
            public const string CompleteQuest = Prefix + nameof(CompleteQuest);
            public const string HoldItem = Prefix + nameof(HoldItem);
            public const string NpcProp = Prefix + nameof(NpcProp);

            public const string CompleteGame = Prefix + nameof(CompleteGame);
            public const string CompleteGameWin = Prefix + nameof(CompleteGameWin);
            public const string CompleteGameLose = Prefix + nameof(CompleteGameLose);

            public const string DialogNpc = Prefix + nameof(DialogNpc);
            public const string DialogNpcWithItem = Prefix + nameof(DialogNpcWithItem);

            public const string GiveGift = Prefix + nameof(GiveGift);
            /// <summary>礼物ID 填 0（任意礼物）时用这条。</summary>
            public const string GiveGiftAny = Prefix + nameof(GiveGiftAny);

            public const string BuyItem = Prefix + nameof(BuyItem);
            /// <summary>道具ID 填 0（任意道具）时用这条。</summary>
            public const string BuyItemAny = Prefix + nameof(BuyItemAny);
        }

        /// <summary>角色属性名，给 <c>NpcProp</c> 目标的描述填 <see cref="QuestLocVar.PropName"/> 用。</summary>
        public static class Prop
        {
            const string Prefix = "QuestProp/";

            public static string Of(CharacterPropType type) => Prefix + type;
        }

        /// <summary>任务 UI 的通用文案，文案在 QuestCommonLoc.csv。</summary>
        public static class Common
        {
            const string Prefix = "QuestCommon/";

            public const string QuestList = Prefix + nameof(QuestList);
            public const string NoQuest = Prefix + nameof(NoQuest);

            public const string StateTitle = Prefix + nameof(StateTitle);
            public const string NotAccepted = Prefix + nameof(NotAccepted);
            public const string InProgress = Prefix + nameof(InProgress);
            public const string ReadyToComplete = Prefix + nameof(ReadyToComplete);
            public const string Completed = Prefix + nameof(Completed);
            public const string QuestCompleted = Prefix + nameof(QuestCompleted);

            public const string ObjTitle = Prefix + nameof(ObjTitle);
            public const string ExtraObjTitle = Prefix + nameof(ExtraObjTitle);
            public const string RewardTitle = Prefix + nameof(RewardTitle);
            public const string ExtraRewardTitle = Prefix + nameof(ExtraRewardTitle);

            public const string ExceedAchieved = Prefix + nameof(ExceedAchieved);
            public const string ExceedNotAchieved = Prefix + nameof(ExceedNotAchieved);

            public const string Accept = Prefix + nameof(Accept);
            public const string Submit = Prefix + nameof(Submit);

            /// <summary>任务状态 → 显示文案。</summary>
            public static string Of(QuestState state) => state switch
            {
                QuestState.InProgress => InProgress,
                QuestState.ReadyToComplete => ReadyToComplete,
                QuestState.Completed => Completed,
                _ => NotAccepted,
            };
        }
    }

    /// <summary>
    /// 任务系统文案里的占位符名，和 CSV 里 <c>{Value}</c> 这种写法一一对应。
    /// 目标和奖励共用同一套名字，同一个东西在两边别起两个名。
    /// </summary>
    public static class QuestLocVar
    {
        /// <summary>数值：奖励数量、目标所需的量。</summary>
        public const string Value = nameof(Value);

        /// <summary>进度文本，形如 "1/2"。</summary>
        public const string Progress = nameof(Progress);

        public const string ItemName = nameof(ItemName);
        public const string CharacterName = nameof(CharacterName);
        public const string QuestName = nameof(QuestName);
        public const string PropName = nameof(PropName);
    }

    /// <summary>
    /// 任务文案里要塞进去的那几个名字，统一在这里取。
    /// 不做空判：ID 在 <see cref="QuestConfigValidator"/> 里已经查过，查不到就该在启动时炸出来。
    /// </summary>
    public static class QuestLocText
    {
        public static string Get(string key)
            => LanguageManager.Instance.GetLocalizedString(LocTableSet.QuestSystem, key);

        public static string ItemName(long itemId)
            => LanguageManager.Instance.GetLocalizedString(InventoryManager.Instance.GetItemData(itemId).NameKey);

        public static string CharacterName(long npcId)
            => LanguageManager.Instance.GetLocalizedString(CharacterManager.Instance.GetCharacterDataByID(npcId).Name);
    }
}
