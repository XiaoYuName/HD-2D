using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>送礼结果提示组件，挂在面板的结果文本上。</summary>
    public sealed class GiftResultTip : MonoBehaviour
    {
        const string SuccessKey = "GiftGivingPanel/Success";
        const string FailureKey = "GiftGivingPanel/Failure";
        const string GiftNameVar = "GiftName";
        const string GoodwillVar = "Goodwill";

        [SerializeField] TMP_Text text;

        readonly LocalizedString successLoc = new(LocTableSet.GiftSystem, SuccessKey);
        string giftName;
        int goodwill;
        GiftItemData giftData;
        bool showMachiReward;
        TipState state;

        enum TipState
        {
            None,
            Success,
            Failure
        }

        [Button("SetRef")]
        void SetRef()
        {
            text = GetComponent<TMP_Text>();
        }

        public void Show(string itemName, GiftItemData data, bool isMachi)
        {
            giftName = itemName;
            goodwill = data.Goodwill;
            giftData = data;
            showMachiReward = isMachi;
            state = TipState.Success;
            RefreshLocalization();
        }

        public void ShowFailure()
        {
            state = TipState.Failure;
            RefreshLocalization();
        }

        public void Clear()
        {
            giftData = null;
            showMachiReward = false;
            state = TipState.None;
            text.text = string.Empty;
        }

        public void RefreshLocalization()
        {
            switch (state)
            {
                case TipState.Success:
                    successLoc.SetVar(GiftNameVar, giftName, false);
                    successLoc.SetVar(GoodwillVar, GiftTextUtility.FormatSigned(goodwill), false);
                    StringBuilder builder = new(successLoc.GetLocalizedString());
                    if (showMachiReward)
                    {
                        for (int i = 0; i < giftData.RewardProp.Count; i++)
                        {
                            TbRewardPropData reward = giftData.RewardProp[i];
                            if (!GiftTextUtility.IsMachiProperty(reward.PropType))
                            {
                                continue;
                            }

                            builder.AppendLine();
                            builder.Append(GiftTextUtility.GetPropertyName(reward.PropType))
                                .Append(' ')
                                .Append(GiftTextUtility.FormatSigned(reward.Value));
                        }
                    }
                    text.text = builder.ToString();
                    break;
                case TipState.Failure:
                    text.text = LanguageManager.Instance.GetLocalizedString(
                        LocTableSet.GiftSystem,
                        FailureKey);
                    break;
            }
        }

    }

    static class GiftTextUtility
    {
        public static bool IsMachiProperty(PropertyType propertyType) =>
            propertyType is PropertyType.MachiInspire or PropertyType.MachiPressure;

        public static string GetPropertyName(PropertyType propertyType)
        {
            string machiRoomKey = propertyType switch
            {
                PropertyType.MachiInspire => "MachiRoom/Inspiration",
                PropertyType.MachiPressure => "MachiRoom/Pressure",
                _ => null,
            };
            if (machiRoomKey != null)
            {
                return LanguageManager.Instance.GetLocalizedString(LocTableSet.MachiRoom, machiRoomKey);
            }

            PropertyData propertyData = GameDataManager.Instance.GetPropertyData(propertyType);
            return propertyData?.Name == null
                ? propertyType.ToString()
                : LanguageManager.Instance.GetLocalizedString(propertyData.Name);
        }

        public static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
