using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 获得任务奖励的提示弹窗，挂在 UITop 上，任何界面开着都能弹。
    ///
    /// 三层奖励（目标 / 任务 / 类别）都往这里推，**一次一条排队展示** —— 同一帧连着完成几条目标、
    /// 交掉一个任务、又凑齐一个类别，会攒成一列依次点过去，而不是互相把弹窗内容顶掉。
    ///
    /// 三处文本都走美术预制体上现成的 <see cref="LocalizeStringEvent"/>，不直接写 <c>TMP_Text.text</c> ——
    /// 那样会被组件在切语言时覆盖掉。
    /// </summary>
    public class QuestRewardPop : UIBase
    {
        [SerializeField] LocalizeStringEvent titleEvent;
        [SerializeField] LocalizeStringEvent tipsEvent;
        [SerializeField] QuestRewardRow rewardRow;
        [SerializeField] Button confirmButton;

        /// <summary>一条待展示的奖励：来源文案 Key ＋ 那一组奖励。</summary>
        readonly struct Entry
        {
            public readonly string SourceKey;
            public readonly IQuestReward[] Rewards;

            public Entry(string sourceKey, IQuestReward[] rewards)
            {
                SourceKey = sourceKey;
                Rewards = rewards;
            }
        }

        readonly Queue<Entry> pending = new();

        /// <summary>
        /// 推一条奖励进队列并把弹窗顶到最前。空奖励直接忽略，不弹空窗。
        /// 先入队再 Open，免得 Open 的时候队里还没内容、闪一下空的。
        /// </summary>
        public static void Show(string sourceKey, IQuestReward[] rewards)
        {
            if (rewards == null || rewards.Length == 0) return;

            QuestRewardPop pop = UISystem.Instance.GetUI<QuestRewardPop>(UIPanelIdSet.QuestRewardPop);
            if (pop == null) return;

            pop.pending.Enqueue(new Entry(sourceKey, rewards));

            // 已经开着的时候 OpenUI 不会再走 Open()，玩家看完当前这条才轮到新入队的，不打断他
            if (pop.isOpen) return;

            UISystem.Instance.OpenUI<QuestRewardPop>(UIPanelIdSet.QuestRewardPop);
        }

        public override void Init()
        {
            confirmButton.onClick.AddListener(ShowNext);
        }

        public override void Open()
        {
            base.Open();
            ShowNext();
        }

        /// <summary>取下一条来显示；队列空了就关窗。</summary>
        void ShowNext()
        {
            if (pending.Count == 0)
            {
                Close();
                return;
            }

            Entry entry = pending.Dequeue();

            titleEvent.SetText(LocTableSet.QuestSystem, entry.SourceKey);
            rewardRow.SetData(entry.Rewards);
            SetTips();
        }

        /// <summary>后面还压着奖励就把条数亮出来，否则回到「奖品已经放入背包」。</summary>
        void SetTips()
        {
            if (pending.Count == 0)
            {
                tipsEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopTips);
                return;
            }

            tipsEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopMore);
            tipsEvent.SetVar(QuestLocVar.Value, pending.Count);
        }

        public override void Close()
        {
            // 没看完的不留到下次开窗，免得隔了半天又冒出来一串
            pending.Clear();
            base.Close();
        }
    }
}
