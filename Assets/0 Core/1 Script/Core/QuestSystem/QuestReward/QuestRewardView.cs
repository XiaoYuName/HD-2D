using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 一条奖励在 UI 上的样子：一个图标 + 一个数量。面板不认识奖励的具体类型，只画这个。
    /// </summary>
    public readonly struct QuestRewardView
    {
        /// <summary>AA 图标路径，已经拼好，UI 直接 SetIcon。</summary>
        public readonly string IconKey;
        public readonly AssetReferenceSprite Icon;

        /// <summary>已本地化的奖励名，做 tooltip 用。</summary>
        public readonly string Name;

        public readonly int Amount;

        public QuestRewardView(string iconKey, string name, int amount)
        {
            Icon = null;
            IconKey = iconKey;
            Name = name;
            Amount = amount;
        }

        public QuestRewardView(AssetReferenceSprite icon, string name, int amount)
        {
            IconKey = null;
            Icon = icon;
            Name = name;
            Amount = amount;
        }

        /// <summary>数量文本，1 个时不显示。</summary>
        public string AmountText => Amount > 1 ? Amount.ToString() : string.Empty;

        /// <summary>玩家属性类奖励（金币/游戏币/好感度）的图标和名字在任务数据库的「奖励显示」里，按类型取。</summary>
        public static QuestRewardView OfType(QuestRewardType type, int amount)
        {
            if (QuestConfigProvider.Config.GetRewardView(type, out QuestRewardPresentation data))
                return new QuestRewardView(data.Icon, data.Name, amount);

            // 没配显示就退成「类型名 + 无图标」，缺哪一条由「校验配置」报出来
            return new QuestRewardView((string)null, type.ToString(), amount);
        }
    }
}
