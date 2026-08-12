using UnityEngine.Localization;

namespace XFramework
{
    /// <summary>
    /// 任务系统的多语言 Key，统一走 <see cref="LocTableSet.QuestSystem"/> 表。
    /// 文案在 <c>Assets/0 Core/1 Script/Data/QuestSystem/</c> 下的 CSV 里，别在业务代码里写魔法字符串。
    /// 任务自己的名称/描述 Key 由任务编辑器选择，下面的前缀用于约束不同表格可选择的范围。
    /// </summary>
    public static class QuestLocKey
    {
        /// <summary>任务配置数据使用的 Key 分组。</summary>
        public static class Prefix
        {
            public const string Quest = "Quest/";
            public const string Objective = "QuestObj/";
            public const string Category = "QuestCategory/";
            public const string RewardName = "QuestRewardName/";
        }

        /// <summary>奖励描述，文案在 QuestRewardDataLoc.csv。</summary>
        public static class Reward
        {
            const string Prefix = "QuestReward/";

            public const string Item = Prefix + nameof(Item);
            public const string Coin = Prefix + nameof(Coin);
            public const string GameCoin = Prefix + nameof(GameCoin);
            public const string Affection = Prefix + nameof(Affection);
        }

        // 目标描述的 Key 不在这里逐项列举，统一放在 QuestObj/* 下，由任务编辑器选择。

        /// <summary>角色属性名，给 <c>CharacterProp</c> 目标的描述填 <see cref="QuestLocVar.PropName"/> 用。</summary>
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

            /// <summary>行首的「任务1」，超额那一块靠它指回是第几条目标。</summary>
            public const string ObjIndex = Prefix + nameof(ObjIndex);

            #region 奖励弹窗

            /// <summary>弹窗底部的常规提示语。</summary>
            public const string RewardPopTips = Prefix + nameof(RewardPopTips);

            /// <summary>队列里还压着几条，压着的时候顶掉 <see cref="RewardPopTips"/>。</summary>
            public const string RewardPopMore = Prefix + nameof(RewardPopMore);

            public const string Confirm = Prefix + nameof(Confirm);

            /// <summary>弹窗里的来源行：这份奖励是哪一层发的。</summary>
            public const string ObjRewardTitle = Prefix + nameof(ObjRewardTitle);

            /// <summary>弹窗里的「任务：{QuestName}」。</summary>
            public const string RewardPopQuest = Prefix + nameof(RewardPopQuest);

            /// <summary>弹窗里的「类别：{QuestName}」。</summary>
            public const string RewardPopCategory = Prefix + nameof(RewardPopCategory);

            /// <summary>弹窗里的「目标{Value}：{Desc}」，指明这份奖励是哪条目标发的。</summary>
            public const string RewardPopObj = Prefix + nameof(RewardPopObj);

            #endregion

            /// <summary>超额行的「完成条件：{Desc}」。</summary>
            public const string ExtraCond = Prefix + nameof(ExtraCond);

            public const string ObjTitle = Prefix + nameof(ObjTitle);
            public const string ExtraObjTitle = Prefix + nameof(ExtraObjTitle);
            public const string RewardTitle = Prefix + nameof(RewardTitle);
            public const string ExtraRewardTitle = Prefix + nameof(ExtraRewardTitle);

            /// <summary>类别整体完成才发的那份奖励的标题。</summary>
            public const string CategoryRewardTitle = Prefix + nameof(CategoryRewardTitle);
            public const string CategoryRewarded = Prefix + nameof(CategoryRewarded);
            public const string NoCategory = Prefix + nameof(NoCategory);

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

        /// <summary>嵌进外层文案的一整段描述，如「完成条件：{Desc}」。</summary>
        public const string Desc = nameof(Desc);

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

        /// <summary>只带一个占位符的文案。</summary>
        public static string Get(string key, string varName, object value)
        {
            LocalizedString text = new(LocTableSet.QuestSystem, key);
            text.SetVar(varName, value, false);
            return text.GetLocalizedString();
        }

        /// <summary>行首的「任务1」，index 从 1 数。</summary>
        public static string ObjIndex(int index)
            => Get(QuestLocKey.Common.ObjIndex, QuestLocVar.Value, index);

        /// <summary>把超额条件的描述套进「完成条件：…」。</summary>
        public static string ExtraCond(string desc)
            => Get(QuestLocKey.Common.ExtraCond, QuestLocVar.Desc, desc);

        public static string ItemName(long itemId)
            => LanguageManager.Instance.GetLocalizedString(InventoryManager.Instance.GetItemData(itemId).NameKey);

        public static string CharacterName(long npcId)
            => LanguageManager.Instance.GetLocalizedString(CharacterManager.Instance.GetCharacterDataByID(npcId).Name);
    }
}
