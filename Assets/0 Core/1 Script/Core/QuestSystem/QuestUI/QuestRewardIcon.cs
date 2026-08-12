using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>一个奖励图标格。类别、任务、目标三层的奖励都用它，画的是 <see cref="QuestRewardView"/>。</summary>
    public class QuestRewardIcon : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text amountText;

        public void SetData(QuestRewardView view)
        {
            if (view.Icon != null && view.Icon.RuntimeKeyIsValid())
                iconImage.SetIcon(view.Icon.RuntimeKey.ToString());
            else if (!string.IsNullOrEmpty(view.IconKey)) iconImage.SetIcon(view.IconKey);
            else iconImage.ClearIcon();

            amountText.text = view.AmountText;
        }

        void OnDestroy() => iconImage.ClearIcon();
    }
}
