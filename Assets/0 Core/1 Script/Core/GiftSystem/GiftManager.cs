using UnityEngine;

namespace XFramework
{
    /// <summary>送礼业务服务：校验礼物、扣除物品、发放角色与马吉属性奖励。</summary>
    public sealed class GiftManager : MonoSingleton<GiftManager>
    {
        public bool TryGiveGift(long characterID, ItemInfo item, out GiftItemData giftData)
        {
            giftData = LubanManager.Instance.TbGiftItemData.GetOrDefault(item.ID);
            if (giftData == null || !InventoryManager.Instance.ConsumeItem(item.Guid, 1))
            {
                giftData = null;
                return false;
            }

            CharacterManager.Instance.AddProperty(characterID, CharacterPropType.Goodwill, giftData.Goodwill);
            if (characterID == CharaIdSet1.Machi)
            {
                for (int i = 0; i < giftData.RewardProp.Count; i++)
                {
                    TbRewardPropData reward = giftData.RewardProp[i];
                    GameDataManager.Instance.AddProperty(reward.PropType, reward.Value);
                }
            }

            SaveGameManager.Instance.Save();
            return true;
        }
    }
}
