using UnityEngine;

namespace XFramework
{
    [QuestTypeInfo("发道具")]
    public class ItemQuestReward : IQuestReward
    {
        [SerializeField, QuestLabel("道具"), QuestRef(QuestRefKind.Item)] long itemId;
        [SerializeField, QuestLabel("数量"), QuestMin(1)] int count = 1;

        public QuestRewardType Type => QuestRewardType.Item;

        public bool Validate(string owner)
            => QuestConfigValidator.CheckItem(itemId, owner)
             & QuestConfigValidator.CheckPositive(count, QuestFieldName.Count, owner);

        public void Reward() => InventoryManager.Instance.AddItem(itemId, count);

        public string GetDesc()
            => QuestLocText.Get(QuestLocKey.Reward.Item, new LocVars()
                .Set(QuestLocVar.ItemName, QuestLocText.ItemName(itemId))
                .Set(QuestLocVar.Value, count));

        // 道具的图标在道具表里，不重复配到奖励显示配置
        public QuestRewardView GetView()
        {
            ItemData data = InventoryManager.Instance.GetItemData(itemId);
            return new QuestRewardView(
                GamePathTools.CombinationItemIconPath(data.IconName), QuestLocText.ItemName(itemId), count);
        }
    }

    /// <summary>玩家属性奖励，子类只指定属性类型和文案 Key。</summary>
    public abstract class PlayerPropQuestReward : IQuestReward
    {
        [SerializeField, QuestLabel("数值"), QuestMin(1)] int value = 1;

        protected abstract PropertyType PropType { get; }
        protected abstract string DescKey { get; }

        public abstract QuestRewardType Type { get; }

        public bool Validate(string owner) => QuestConfigValidator.CheckPositive(value, QuestFieldName.Value, owner);

        public void Reward() => GameDataManager.Instance.AddProperty(PropType, value);

        public string GetDesc() => QuestLocText.Get(DescKey, QuestLocVar.Value, value);

        public QuestRewardView GetView() => QuestRewardView.OfType(Type, value);
    }

    [QuestTypeInfo("发金币")]
    public class CoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.Coin;
        protected override string DescKey => QuestLocKey.Reward.Coin;

        public override QuestRewardType Type => QuestRewardType.Coin;
    }

    [QuestTypeInfo("发游戏币")]
    public class GameCoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.GameCoin;
        protected override string DescKey => QuestLocKey.Reward.GameCoin;

        public override QuestRewardType Type => QuestRewardType.GameCoin;
    }

    [QuestTypeInfo("加某 NPC 的好感度")]
    public class AffectionQuestReward : IQuestReward
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] long npcId;
        [SerializeField, QuestLabel("好感度"), QuestMin(1)] int value = 1;

        public QuestRewardType Type => QuestRewardType.Affection;

        public bool Validate(string owner)
            => QuestConfigValidator.CheckCharacter(npcId, owner)
             & QuestConfigValidator.CheckPositive(value, QuestFieldName.Affection, owner);

        public void Reward() => CharacterManager.Instance.AddProperty(npcId, CharacterPropType.Goodwill, value);

        public string GetDesc()
            => QuestLocText.Get(QuestLocKey.Reward.Affection, new LocVars()
                .Set(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId))
                .Set(QuestLocVar.Value, value));

        public QuestRewardView GetView() => QuestRewardView.OfType(QuestRewardType.Affection, value);
    }
}
