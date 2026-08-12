using UnityEngine;

namespace XFramework.QuestSystem.UI
{
    /// <summary>
    /// 任务系统 → 弹窗的接线处：把 <see cref="QuestManager"/> 的对外事件接到各个弹窗上，
    /// 于是任务系统核心一个 UI 类型都不认识，「哪件事弹哪个窗」只看这一处。
    ///
    /// 为什么要有这么个组件：弹窗是「用完就关」的，平时没有实例挂着去订阅，
    /// 得有个常驻的东西代订。挂在 <see cref="QuestManager"/> 同一个物体上，
    /// 直接 <c>GetComponent</c> 拿管理器，不看两边 <c>Awake</c> 谁先谁后，也不用静态事件。
    /// 常驻打开的面板（<c>QuestPanel</c>）照旧自己订 <see cref="QuestManager"/> 的事件。
    /// </summary>
    [RequireComponent(typeof(QuestManager))]
    public class QuestUI : MonoBehaviour
    {
        QuestManager quest;

        void Awake()
        {
            quest = GetComponent<QuestManager>();

            quest.OnQuestAccepted += QuestAcceptPop.Show;
            quest.OnObjectiveRewarded += QuestRewardPop.ShowObjective;
            quest.OnQuestCompleted += ShowQuestReward;
            quest.OnCategoryCompleted += QuestRewardPop.ShowCategory;
        }

        void OnDestroy()
        {
            quest.OnQuestAccepted -= QuestAcceptPop.Show;
            quest.OnObjectiveRewarded -= QuestRewardPop.ShowObjective;
            quest.OnQuestCompleted -= ShowQuestReward;
            quest.OnCategoryCompleted -= QuestRewardPop.ShowCategory;
        }

        /// <summary>任务奖励弹窗要的是配置（名字 ＋ 奖励），运行时实例上取。</summary>
        static void ShowQuestReward(QuestInfo info) => QuestRewardPop.ShowQuest(info.Data);
    }
}
