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
            itemId = config.GetLong(0, 0);
            count = config.GetInt(1, 1);
        }

        public bool Validate()
            => QuestConfigValidator.CheckItem(itemId, config)
             & QuestConfigValidator.CheckPositive(count, "数量", config);

        public void Reward() => InventoryManager.Instance.AddItem(itemId, count);

        public string GetDesc()
        {
            desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
            desc.SetVar(QuestLocVar.Value, count, false);
            return desc.GetLocalizedString();
        }
    }

    /// <summary>玩家属性奖励。写法 <c>属性名:数值</c>，子类只指定属性类型和文案 Key。</summary>
    public abstract class PlayerPropQuestReward : IQuestReward
    {
        protected abstract PropertyType PropType { get; }
        protected abstract string DescKey { get; }

        LocalizedString desc;
        QuestArgs config;
        int value;

        public void Init(QuestArgs args)
        {
            config = args;
            value = config.GetInt(0, 0);
            desc = new LocalizedString(LocTableSet.QuestSystem, DescKey);
        }

        public bool Validate() => QuestConfigValidator.CheckPositive(value, "数值", config);

        public void Reward() => GameDataManager.Instance.AddProperty(PropType, value);

        public string GetDesc()
        {
            desc.SetVar(QuestLocVar.Value, value, false);
            return desc.GetLocalizedString();
        }
    }

    /// <summary><c>Coin:数值</c></summary>
    public class CoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.Coin;
        protected override string DescKey => QuestLocKey.Reward.Coin;
    }

    /// <summary><c>GameCoin:数值</c></summary>
    public class GameCoinQuestReward : PlayerPropQuestReward
    {
        protected override PropertyType PropType => PropertyType.GameCoin;
        protected override string DescKey => QuestLocKey.Reward.GameCoin;
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
            npcId = config.GetLong(0, 0);
            value = config.GetInt(1, 0);
        }

        public bool Validate()
            => QuestConfigValidator.CheckCharacter(npcId, config)
             & QuestConfigValidator.CheckPositive(value, "好感度", config);

        public void Reward() => CharacterManager.Instance.AddProperty(npcId, CharacterPropType.Goodwill, value);

        public string GetDesc()
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            desc.SetVar(QuestLocVar.Value, value, false);
            return desc.GetLocalizedString();
        }
    }
}
