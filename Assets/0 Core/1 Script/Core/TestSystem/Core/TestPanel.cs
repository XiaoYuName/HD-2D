using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TestSystem
{
    /// <summary>测试面板：左侧竖排分类，右侧该分类的操作按钮，内容全部由 TestCategory 子类动态生成。</summary>
    public sealed class TestPanel : MonoBehaviour
    {
        [SerializeField] Transform categoryContainer;
        [SerializeField] Transform actionContainer;
        [SerializeField] TextMeshProUGUI categoryTitleText;
        [SerializeField] Button categoryButtonPrefab;
        [SerializeField] Button actionButtonPrefab;
        [SerializeField] Button closeButton;
        [SerializeField] int curCategoryIndex;
        
        readonly List<ITestCategory> categories = new ();
        readonly List<Image> categoryButtonImages = new ();
        readonly List<GameObject> actionButtons = new ();
        readonly TestActionList actionInfoList = new ();
        static readonly Color CategoryNormalColor = new Color32(0x33, 0x33, 0x37, 0xFF);
        static readonly Color CategorySeColor = new Color32(0x00, 0x78, 0xD4, 0xFF);

        void Awake()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            CreateCategoryButtons();
            SwitchCategory(0);
        }

        void CreateCategoryButtons()
        {
            categories.AddRange(TestCategorySet.GetAllCategories());
            for (int i = 0; i < categories.Count; i++)
            {
                int index = i;
                Button button = Instantiate(categoryButtonPrefab, categoryContainer);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TextMeshProUGUI>().text = categories[i].Title;
                button.onClick.AddListener(() => SwitchCategory(index));
                categoryButtonImages.Add(button.image);
            }
        }

        void SwitchCategory(int index)
        {
            curCategoryIndex = index;
            for (int i = 0; i < categoryButtonImages.Count; i++)
                categoryButtonImages[i].color = i == index ? CategorySeColor : CategoryNormalColor;

            ITestCategory category = categories[index];
            categoryTitleText.text = category.Title;
            for (int i = 0; i < actionButtons.Count; i++)
                Destroy(actionButtons[i]);
            actionButtons.Clear();

            actionInfoList.Clear();
            category.SetActions(actionInfoList);
            TestAttributeActionSet.AddActions(category.Title, actionInfoList);
            for (int i = 0; i < actionInfoList.Count; i++)
            {
                TestActionInfo actionInfo = actionInfoList[i];
                Button button = Instantiate(actionButtonPrefab, actionContainer);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TextMeshProUGUI>().text = actionInfo.Label;
                button.onClick.AddListener(() => actionInfo.OnClick());
                actionButtons.Add(button.gameObject);
            }
        }

#if UNITY_EDITOR
        /// <summary>预制体生成器用：显式写入 UI 引用。</summary>
        public void SetRef(
            Transform categoryContainer,
            Transform actionContainer,
            TextMeshProUGUI categoryTitleText,
            Button categoryButtonPrefab,
            Button actionButtonPrefab,
            Button closeButton)
        {
            this.categoryContainer = categoryContainer;
            this.actionContainer = actionContainer;
            this.categoryTitleText = categoryTitleText;
            this.categoryButtonPrefab = categoryButtonPrefab;
            this.actionButtonPrefab = actionButtonPrefab;
            this.closeButton = closeButton;
        }
#endif
    }
}
