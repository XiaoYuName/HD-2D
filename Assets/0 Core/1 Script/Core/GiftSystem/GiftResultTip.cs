using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>送礼结果提示组件，挂在面板的结果文本上。</summary>
    public sealed class GiftResultTip : MonoBehaviour
    {
        const string SuccessKey = "GiftGivingPanel_Success";
        const string FailureKey = "GiftGivingPanel_Failure";
        const string GiftNameVar = "GiftName";
        const string GoodwillVar = "Goodwill";

        [SerializeField] TMP_Text text;

        readonly LocalizedString successLoc = new(LocTableSet.GitfSystem, SuccessKey);
        string giftName;
        int goodwill;
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

        public void Show(string itemName, GiftItemData giftData)
        {
            giftName = itemName;
            goodwill = giftData.Goodwill;
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
            state = TipState.None;
            text.text = string.Empty;
        }

        public void RefreshLocalization()
        {
            switch (state)
            {
                case TipState.Success:
                    successLoc.SetVar(GiftNameVar, giftName, false);
                    successLoc.SetVar(GoodwillVar, FormatSigned(goodwill), false);
                    text.text = successLoc.GetLocalizedString();
                    break;
                case TipState.Failure:
                    text.text = LanguageManager.Instance.GetLocalizedString(
                        LocTableSet.GitfSystem,
                        FailureKey);
                    break;
            }
        }

        static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
