using TMPro;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 超额那一块里的一行：「目标1」＋「完成条件：…」＋ 完成标记 ＋ 超额奖励。
    /// 只有配了超额的目标才有这一行，编号指回它挂在第几条目标上。
    /// </summary>
    public class QuestObjExtraRow : MonoBehaviour
    {
        [SerializeField] TMP_Text indexText;
        [SerializeField] TMP_Text descText;
        [SerializeField] GameObject completeMark;
        [SerializeField] QuestRewardRow rewardRow;
        [SerializeField] CanvasGroup rootGroup;

        /// <param name="index">这条超额挂在第几条目标上，从 1 数。</param>
        public void SetData(QuestObjStateInfo info, int index)
        {
            // 主目标已经完成而超额没达成 = 永远错过了，整行灰掉让玩家看见错过了什么
            rootGroup.alpha = info.IsComplete && !info.ExceedAchieved ? 0.4f : 1f;

            indexText.text = QuestLocText.ObjIndex(index);
            descText.text = QuestLocText.ExtraCond(info.ExtraDesc);
            completeMark.SetActive(info.ExceedAchieved);
            rewardRow.SetData(info.Config.ExtraRewards);
        }
    }
}
