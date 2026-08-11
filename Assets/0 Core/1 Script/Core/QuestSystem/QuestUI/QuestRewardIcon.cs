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
            // 图标路径策划还没填全，空的就先留个空位，不要拿空 key 去 AA 里找
            if (string.IsNullOrEmpty(view.IconKey)) iconImage.ClearIcon();
            else iconImage.SetIcon(view.IconKey);

            amountText.text = view.AmountText;
        }

        void OnDestroy() => iconImage.ClearIcon();
    }
}
