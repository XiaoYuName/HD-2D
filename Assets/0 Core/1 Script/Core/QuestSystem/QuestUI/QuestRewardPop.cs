using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 获得任务奖励的提示弹窗，挂在 UITop 上，任何界面开着都能弹。
    ///
    /// 一条奖励从上到下摆四行：来源标题（目标/任务/类别奖励）、来源名（任务名或类别名）、
    /// 目标行（第几条目标 ＋ 目标描述，只有目标奖励才有）、奖励图标。
    /// 超额奖励不再单独弹一次，而是和目标奖励并排放在同一条里，右边那一组带「超额奖励」小标题 ——
    /// 一次目标达成对应一个弹窗，玩家不用点两下才知道超额那份是跟着哪条目标来的。
    ///
    /// 三层奖励（目标 / 任务 / 类别）都往这里推，**一次一条排队展示** —— 同一帧连着完成几条目标、
    /// 交掉一个任务、又凑齐一个类别，会攒成一列依次点过去，而不是互相把弹窗内容顶掉。
    ///
    /// 文本都走美术预制体上现成的 <see cref="LocalizeStringEvent"/>，不直接写 <c>TMP_Text.text</c> ——
    /// 那样会被组件在切语言时覆盖掉。
    /// </summary>
    public class QuestRewardPop : UIBase
    {
        [SerializeField] LocalizeStringEvent titleEvent;
        [SerializeField] LocalizeStringEvent sourceNameEvent;
        [SerializeField] LocalizeStringEvent objDescEvent;
        [SerializeField] LocalizeStringEvent tipsEvent;
        [SerializeField] QuestRewardRow rewardRow;
        [SerializeField] Button confirmButton;

        [Header("超额")]
        [SerializeField] GameObject extraRoot;
        [SerializeField] LocalizeStringEvent extraTitleEvent;
        [SerializeField] QuestRewardRow extraRewardRow;

        static readonly IQuestReward[] NoReward = Array.Empty<IQuestReward>();

        /// <summary>一条待展示的奖励：标题 ＋ 来源名 ＋ 目标行 ＋ 两组奖励。</summary>
        struct Entry
        {
            /// <summary>标题：这份奖励是哪一层发的。</summary>
            public string TitleKey;

            /// <summary>「任务：xx」/「类别：xx」那一行的文案 Key，空 = 整行收起。</summary>
            public string SourceNameKey;
            public string SourceName;

            /// <summary>这是第几条目标，从 1 数；0 = 不是目标奖励，目标行收起。</summary>
            public int ObjIndex;
            public string ObjDesc;

            public IQuestReward[] Rewards;

            /// <summary>超额奖励，只有目标奖励且超额达成时才有。</summary>
            public IQuestReward[] ExtraRewards;
        }

        readonly Queue<Entry> pending = new();

        #region 对外入口

        /// <summary>目标达成：任务名 ＋ 是第几条目标 ＋ 目标奖励，超额达成了连超额奖励一起摆在同一条里。</summary>
        /// <param name="index">这是任务里的第几条目标，从 1 数。</param>
        public static void ShowObjective(QuestInfo quest, QuestObjStateInfo obj, int index)
        {
            Enqueue(new Entry
            {
                TitleKey = QuestLocKey.Common.ObjRewardTitle,
                SourceNameKey = QuestLocKey.Common.RewardPopQuest,
                SourceName = quest.Name,
                ObjIndex = index,
                ObjDesc = obj.Desc,
                Rewards = obj.Config.Rewards,
                ExtraRewards = obj.ExceedAchieved ? obj.Config.ExtraRewards : NoReward,
            });
        }

        /// <summary>任务交付：任务整体的那份奖励。</summary>
        public static void ShowQuest(QuestData data)
        {
            Enqueue(new Entry
            {
                TitleKey = QuestLocKey.Common.RewardTitle,
                SourceNameKey = QuestLocKey.Common.RewardPopQuest,
                SourceName = data.Name,
                Rewards = data.Rewards,
            });
        }

        /// <summary>类别下的任务全完成：类别奖励。</summary>
        public static void ShowCategory(QuestCategory category)
        {
            Enqueue(new Entry
            {
                TitleKey = QuestLocKey.Common.CategoryRewardTitle,
                SourceNameKey = QuestLocKey.Common.RewardPopCategory,
                SourceName = category.Name,
                Rewards = category.Rewards,
            });
        }

        /// <summary>
        /// 推一条奖励进队列并把弹窗顶到最前。两组奖励都空就直接忽略，不弹空窗。
        /// 先入队再 Open，免得 Open 的时候队里还没内容、闪一下空的。
        /// </summary>
        static void Enqueue(Entry entry)
        {
            if (IsEmpty(entry.Rewards) && IsEmpty(entry.ExtraRewards)) return;

            QuestRewardPop pop = UISystem.Instance.GetUI<QuestRewardPop>(UIPanelIdSet.QuestRewardPop);
            if (pop == null) return;

            pop.pending.Enqueue(entry);

            // 已经开着的时候 OpenUI 不会再走 Open()，玩家看完当前这条才轮到新入队的，不打断他
            if (pop.isOpen) return;

            UISystem.Instance.OpenUI<QuestRewardPop>(UIPanelIdSet.QuestRewardPop);
        }

        static bool IsEmpty(IQuestReward[] rewards) => rewards == null || rewards.Length == 0;

        #endregion

        public override void Init()
        {
            confirmButton.onClick.AddListener(ShowNext);

            // 超额那一组的小标题是固定文案，开窗前设一次就够
            extraTitleEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.ExtraRewardTitle);
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

            titleEvent.SetText(LocTableSet.QuestSystem, entry.TitleKey);
            SetSourceName(entry);
            SetObjDesc(entry);
            rewardRow.SetData(entry.Rewards ?? NoReward);
            SetExtra(entry);
            SetTips();
        }

        /// <summary>「任务：xx」/「类别：xx」，没来源名就整行收起。</summary>
        void SetSourceName(Entry entry)
        {
            bool has = !string.IsNullOrEmpty(entry.SourceNameKey);
            sourceNameEvent.gameObject.SetActive(has);
            if (!has) return;

            // 先塞变量（不刷新）再切 Key：SetText 内部会立刻 RefreshString，
            // 变量晚一步注册的话这次刷新就会因为找不到 {QuestName} 抛 FormattingException
            sourceNameEvent.SetVar(QuestLocVar.QuestName, entry.SourceName, false);
            sourceNameEvent.SetText(LocTableSet.QuestSystem, entry.SourceNameKey);
        }

        /// <summary>「目标2：持有 鱼 ×2（2/2）」，只有目标奖励才有这一行。</summary>
        void SetObjDesc(Entry entry)
        {
            bool has = entry.ObjIndex > 0;
            objDescEvent.gameObject.SetActive(has);
            if (!has) return;

            objDescEvent.SetVar(QuestLocVar.Value, entry.ObjIndex, false);
            objDescEvent.SetVar(QuestLocVar.Desc, entry.ObjDesc, false);
            objDescEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopObj);
        }

        /// <summary>超额那一组挂在奖励行右边，没超额就整块收起，剩下的一组奖励靠父节点的布局自己回到居中。</summary>
        void SetExtra(Entry entry)
        {
            bool has = !IsEmpty(entry.ExtraRewards);
            extraRoot.SetActive(has);
            if (!has) return;

            extraRewardRow.SetData(entry.ExtraRewards);
        }

        /// <summary>后面还压着奖励就把条数亮出来，否则回到「奖品已经放入背包」。</summary>
        void SetTips()
        {
            if (pending.Count == 0)
            {
                tipsEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopTips);
                return;
            }

            tipsEvent.SetVar(QuestLocVar.Value, pending.Count, false);
            tipsEvent.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopMore);
        }

        public override void Close()
        {
            // 没看完的不留到下次开窗，免得隔了半天又冒出来一串
            pending.Clear();
            base.Close();
        }
    }
}
