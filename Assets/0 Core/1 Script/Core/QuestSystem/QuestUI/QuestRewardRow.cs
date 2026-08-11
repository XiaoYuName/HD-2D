using UnityEngine;

namespace XFramework
{
    /// <summary>一排奖励图标。三层奖励（类别 / 任务 / 目标）都挂这个，只是父节点不同。</summary>
    public class QuestRewardRow : MonoBehaviour
    {
        [SerializeField] QuestRewardIcon iconTemplate;

        QuestUIPool<QuestRewardIcon> pool;

        public void SetData(IQuestReward[] rewards)
        {
            pool ??= new QuestUIPool<QuestRewardIcon>(iconTemplate);
            pool.Resize(rewards.Length);

            for (int i = 0; i < rewards.Length; i++) pool.Items[i].SetData(rewards[i].GetView());
            gameObject.SetActive(rewards.Length > 0);
        }
    }
}
