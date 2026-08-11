using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 一张任务卡：图标／名称／描述／状态 ＋ 任务奖励 ＋ 目标列表 ＋ 超额那一块。
    /// 没领取的任务也画（灰着，目标行按配置显示"要做什么"），玩家能提前看到线索。
    ///
    /// 超额单独成一块放在目标列表下面，而不是挤在每条目标行的尾巴上 —— 一条目标一行，读起来才不串。
    /// </summary>
    public class QuestCard : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descText;
        [SerializeField] TMP_Text stateText;
        [SerializeField] QuestRewardRow rewardRow;
        [SerializeField] QuestObjRow objRowTemplate;

        [Header("超额")]
        [SerializeField] GameObject extraRoot;
        [SerializeField] TMP_Text extraTitleText;
        [SerializeField] QuestObjExtraRow extraRowTemplate;

        QuestUIPool<QuestObjRow> objPool;
        QuestUIPool<QuestObjExtraRow> extraPool;

        /// <summary>配了超额的目标在 objectives 里的下标，复用避免每次刷新都新建。</summary>
        readonly List<int> extraIndices = new();

        public void SetData(QuestData data, QuestInfo info)
        {
            iconImage.SetIcon(QuestAssetPath.Icon(data.IconKey));
            nameText.text = data.Name;
            descText.text = data.Desc;
            stateText.text = QuestLocText.Get(QuestLocKey.Common.Of(info?.State ?? QuestState.None));
            rewardRow.SetData(data.Rewards);

            objPool ??= new QuestUIPool<QuestObjRow>(objRowTemplate);
            extraPool ??= new QuestUIPool<QuestObjExtraRow>(extraRowTemplate);

            // 没领取的任务没有运行时实例，临时造一份只为了拿描述文案，不订阅事件、不进存档
            QuestObjStateInfo[] objectives = info != null ? info.Objectives : CreatePreview(data);
            objPool.Resize(objectives.Length);

            // 顺序任务里还没轮到的目标灰掉，玩家能看出「这几条要一条条来」
            int currentIndex = data.ObjInOrder ? FirstIncompleteIndex(objectives) : -1;
            for (int i = 0; i < objectives.Length; i++)
            {
                objPool.Items[i].SetData(objectives[i], i + 1, currentIndex >= 0 && i > currentIndex);
            }

            SetExtraData(objectives);
        }

        void SetExtraData(QuestObjStateInfo[] objectives)
        {
            extraIndices.Clear();
            for (int i = 0; i < objectives.Length; i++)
            {
                if (objectives[i].Config.HasExtra) extraIndices.Add(i);
            }

            // 一条超额都没配就整块收起来，不留一个空标题
            extraRoot.SetActive(extraIndices.Count > 0);
            extraTitleText.text = QuestLocText.Get(QuestLocKey.Common.ExtraRewardTitle);

            extraPool.Resize(extraIndices.Count);
            for (int i = 0; i < extraIndices.Count; i++)
            {
                int objIndex = extraIndices[i];
                extraPool.Items[i].SetData(objectives[objIndex], objIndex + 1);
            }
        }

        static int FirstIncompleteIndex(QuestObjStateInfo[] objectives)
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                if (!objectives[i].IsComplete) return i;
            }
            return objectives.Length;
        }

        static QuestObjStateInfo[] CreatePreview(QuestData data)
        {
            QuestObjStateInfo[] result = new QuestObjStateInfo[data.Objs.Length];
            for (int i = 0; i < result.Length; i++) result[i] = QuestObjStateInfo.Create(data.Objs[i]);
            return result;
        }

        void OnDestroy() => iconImage.ClearIcon();
    }
}
