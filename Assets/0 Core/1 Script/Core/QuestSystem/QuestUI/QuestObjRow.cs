using TMPro;
using UnityEngine;

namespace XFramework
{
    /// <summary>任务卡片里的一行目标：「目标1」＋ 描述（自带进度）＋ 完成标记 ＋ 目标奖励。</summary>
    public class QuestObjRow : MonoBehaviour
    {
        [SerializeField] TMP_Text indexText;
        [SerializeField] TMP_Text descText;
        [SerializeField] GameObject completeMark;
        [SerializeField] QuestRewardRow rewardRow;
        [SerializeField] CanvasGroup rootGroup;

        /// <param name="index">第几条目标，从 1 数 —— 超额那一块靠这个编号指回来。</param>
        /// <param name="locked">顺序任务里还没轮到这一条，整行灰掉。</param>
        public void SetData(QuestObjStateInfo info, int index, bool locked)
        {
            rootGroup.alpha = locked ? 0.35f : 1f;

            indexText.text = QuestLocText.ObjIndex(index);
            descText.text = info.Desc;
            completeMark.SetActive(info.IsComplete);
            rewardRow.SetData(info.Config.Rewards);
        }
    }
}
