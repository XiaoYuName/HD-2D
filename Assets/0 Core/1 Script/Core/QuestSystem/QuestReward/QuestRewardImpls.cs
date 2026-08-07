using UnityEngine.Localization;

namespace XFramework
{
    /// <summary><c>Item:道具ID[:数量]</c></summary>
    public class ItemQuestReward : IQuestReward
    {
        readonly LocalizedString desc = new(LocTableSet.QuestSystem, QuestLocKey.Reward.Item);

        QuestArgs config;
        long itemId;
        int count;

        public void Init(QuestArgs args)
        {
            config = args;
            config.Require(1, QuestRewardUsage.Item);
            itemId = config.GetLong(0);
            count = config.GetInt(1, 1);
        }

        public bool Validate()
            => QuestRewardValidator.CheckItem(itemId, config)
             & QuestRewardValidator.CheckPositive(count, "数量", config);

        public void Reward() => InventoryManager.Instance.AddItem(itemId, count);

        public string GetDesc()
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(itemId);
            desc.SetVar(LocVarSet.QuestReward.ItemName, LanguageManager.Instance.GetLocalizedString(itemData.NameKey), false);
            desc.SetVar(LocVarSet.QuestReward.Value, count, false);
            return desc.GetLocalizedString();
        }
    }

    /// <summary>玩家属性奖励。写法 <c>属性名:数值</c>，子类只指定属性类型、文案 Key 和写法说明。</summary>
    public abstract class PlayerPropQuestReward : IQuestReward
    {
        protected abstract PropertyType PropType { get; }
        protected abstract string DescKey { get; }
        protected abstract string Usage { get; }

        LocalizedString desc;
        QuestArgs config;
        int value;

        public void Init(QuestArgs args)
        {
            config = args;
            config.Require(1, Usage);
            value = config.GetInt(0);
            desc = new LocalizedString(LocTableSet.QuestSystem, DescKey);
        }

        public bool Validate() => QuestRewardValidator.CheckPositive(value, "数值", config);

        public void Reward() => GameDataManager.Instance.AddProperty(PropType, value);

        public string GetDesc()
        {
            desc.SetVar(LocVarSet.QuestReward.Value, value, false);
            return desc.GetLocalizedString();
        }
    }

    /// <summary><c>Coin:数值</c></summary>
    public class CoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.Coin;
        protected override string DescKey => QuestLocKey.Reward.Coin;
        protected override string Usage => QuestRewardUsage.Coin;
    }

    /// <summary><c>GameCoin:数值</c></summary>
    public class GameCoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.GameCoin;
        protected override string DescKey => QuestLocKey.Reward.GameCoin;
        protected override string Usage => QuestRewardUsage.GameCoin;
    }

    /// <summary><c>Goodwill:NPC ID:数值</c></summary>
    public class GoodwillQuestReward : IQuestReward
    {
        readonly LocalizedString desc = new(LocTableSet.QuestSystem, QuestLocKey.Reward.Goodwill);

        QuestArgs config;
        long npcId;
        int value;

        public void Init(QuestArgs args)
        {
            config = args;
            config.Require(2, QuestRewardUsage.Goodwill);
            npcId = config.GetLong(0);
            value = config.GetInt(1);
        }

        public bool Validate()
            => QuestRewardValidator.CheckCharacter(npcId, config)
             & QuestRewardValidator.CheckPositive(value, "好感度", config);

        public void Reward() => CharacterManager.Instance.AddProperty(npcId, CharacterPropType.Goodwill, value);

        public string GetDesc()
        {
            CharacterData character = CharacterManager.Instance.GetCharacterDataByID(npcId);
            desc.SetVar(LocVarSet.QuestReward.CharacterName, LanguageManager.Instance.GetLocalizedString(character.Name), false);
            desc.SetVar(LocVarSet.QuestReward.Value, value, false);
            return desc.GetLocalizedString();
        }
    }
}
