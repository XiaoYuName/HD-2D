using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>面板左侧的一个类别页签。</summary>
    public class QuestCategoryTab : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text labelText;
        [SerializeField] GameObject selectedMark;

        public void SetData(QuestCategory category, bool selected, Action<QuestCategory> onClick)
        {
            labelText.text = category.Name;
            selectedMark.SetActive(selected);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick(category));
        }
    }
}
