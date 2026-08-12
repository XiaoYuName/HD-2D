using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 任务面板：左侧类别页签，右侧当前类别的说明＋类别奖励，下面每个任务一张卡，卡里每条目标一行。
    /// 全事件驱动，不做轮询刷新 —— 领取/进度/完成/类别完成四个事件各自只重画需要重画的部分。
    /// </summary>
    public class QuestPanel : UIBase
    {
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text categoryDescText;
        [SerializeField] TMP_Text categoryRewardTitleText;
        [SerializeField] QuestRewardRow categoryRewardRow;
        [SerializeField] TMP_Text emptyTipText;

        [SerializeField] QuestCategoryTab tabTemplate;
        [SerializeField] QuestCard cardTemplate;

        QuestUIPool<QuestCategoryTab> tabPool;
        QuestUIPool<QuestCard> cardPool;

        readonly List<QuestCategory> categories = new();
        QuestCategory current;

        public override void Init()
        {
            tabPool = new QuestUIPool<QuestCategoryTab>(tabTemplate);
            cardPool = new QuestUIPool<QuestCard>(cardTemplate);

            Bind(closeButton, Close, string.Empty);
        }

        public override void Open()
        {
            base.Open();
            SubsEvents();
            BuildCategories();
        }

        public override void Close()
        {
            UnsubsEvents();
            base.Close();
        }

        protected override void OnDestroy()
        {
            UnsubsEvents();
            base.OnDestroy();
        }

        #region 事件

        bool isSubscribed;

        void SubsEvents()
        {
            if (isSubscribed) return;
            isSubscribed = true;

            QuestManager.Instance.OnQuestAccepted += OnQuestChanged;
            QuestManager.Instance.OnQuestProgress += OnQuestChanged;
            QuestManager.Instance.OnQuestCompleted += OnQuestChanged;
            QuestManager.Instance.OnCategoryCompleted += OnCategoryCompleted;
            LanguageManager.Instance.AddOnLanguageChanged(RefreshCurrent);
        }

        void UnsubsEvents()
        {
            if (!isSubscribed) return;
            isSubscribed = false;

            QuestManager.Instance.OnQuestAccepted -= OnQuestChanged;
            QuestManager.Instance.OnQuestProgress -= OnQuestChanged;
            QuestManager.Instance.OnQuestCompleted -= OnQuestChanged;
            QuestManager.Instance.OnCategoryCompleted -= OnCategoryCompleted;
            LanguageManager.Instance.RemoveOnLanguageChanged(RefreshCurrent);
        }
        [Button]
        void Test()
        {
            Init();
            Open();
        }
        // 变的那个任务在不在当前类别都无所谓：整页重画一次比逐卡定位便宜得多，卡片数量是个位数
        void OnQuestChanged(QuestInfo _) => RefreshCurrent();
        void OnCategoryCompleted(QuestCategory _) => RefreshCurrent();

        #endregion

        void BuildCategories()
        {
            titleText.text = QuestLocText.Get(QuestLocKey.Common.QuestList);
            categoryRewardTitleText.text = QuestLocText.Get(QuestLocKey.Common.CategoryRewardTitle);

            RefreshCurrent();
        }

        void RefreshCurrent()
        {
            // 页签列表每次重算：领到新任务可能让一个没露过面的类别冒出来
            CollectOpenedCategories();

            if (categories.Count == 0)
            {
                tabPool.Clear();
                cardPool.Clear();
                current = null;
                ShowEmpty(QuestLocKey.Common.NoCategory);
                return;
            }

            if (current == null || !categories.Contains(current)) current = categories[0];

            tabPool.Resize(categories.Count);
            for (int i = 0; i < categories.Count; i++)
            {
                tabPool.Items[i].SetData(categories[i], categories[i] == current, SelectCategory);
            }

            categoryDescText.text = current.Desc;
            categoryRewardRow.SetData(current.Rewards);

            BuildCards();
        }

        /// <summary>
        /// 只列出**已经领到过任务**的类别：还没碰过的类别连页签都不出现，不提前把后面的内容摊给玩家。
        /// 已完成的任务也算数 —— 做完了类别不该凭空消失。
        /// </summary>
        void CollectOpenedCategories()
        {
            categories.Clear();
            foreach (QuestCategory category in QuestManager.Instance.GetCategories())
            {
                if (HasAcceptedQuest(category)) categories.Add(category);
            }
        }

        static bool HasAcceptedQuest(QuestCategory category)
        {
            foreach (long questId in category.QuestIds)
            {
                if (QuestManager.Instance.IsQuestAccepted(questId)) return true;
            }
            return false;
        }

        void SelectCategory(QuestCategory category)
        {
            current = category;
            RefreshCurrent();
        }

        void BuildCards()
        {
            IReadOnlyList<long> questIds = current.QuestIds;
            if (questIds.Count == 0)
            {
                cardPool.Clear();
                ShowEmpty(QuestLocKey.Common.NoQuest);
                return;
            }

            emptyTipText.gameObject.SetActive(false);
            cardPool.Resize(questIds.Count);

            for (int i = 0; i < questIds.Count; i++)
            {
                long questId = questIds[i];

                // 没领取的任务也画（灰着），这时没有运行时实例，传 null 让卡片走「按配置预览」那条路
                QuestInfo info = QuestManager.Instance.IsQuestAccepted(questId)
                    ? QuestManager.Instance.GetQuest(questId)
                    : null;

                cardPool.Items[i].SetData(QuestManager.Instance.GetQuestData(questId), info);
            }
        }

        void ShowEmpty(string key)
        {
            emptyTipText.gameObject.SetActive(true);
            emptyTipText.text = QuestLocText.Get(key);
        }
    }
}
