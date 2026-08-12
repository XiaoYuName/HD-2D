using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XFramework.QuestSystem.UI
{
    public class QuestAcceptPop : UIBase
    {
        [SerializeField] Image icon;
        [SerializeField] LocalizeStringEvent titleText;
        [SerializeField] LocalizeStringEvent questNameText;
        [SerializeField] LocalizeStringEvent objDescText;
        [SerializeField] LocalizeStringEvent tipsText;
        [SerializeField] Button confirmButton;

        readonly Queue<QuestInfo> pending = new();

        /// <summary>由 <see cref="QuestUI"/> 接到 <see cref="QuestManager.OnQuestAccepted"/> 上。</summary>
        public static void Show(QuestInfo info)
        {
            QuestAcceptPop pop = UISystem.Instance.GetUI<QuestAcceptPop>(UIPanelIdSet.QuestAcceptPop);
            pop.pending.Enqueue(info);
            if (!pop.isOpen) UISystem.Instance.OpenUI<QuestAcceptPop>(UIPanelIdSet.QuestAcceptPop);
        }

        public override void Init()
        {
            confirmButton.onClick.AddListener(ShowNext);
            titleText.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.Accept);
            tipsText.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.AcceptPopTips);
        }

        public override void Open()
        {
            base.Open();
            PlayerInputManager.Instance.OnClick += Close;
            ShowNext();
        }

        void ShowNext()
        {
            if (pending.Count == 0)
            {
                Close();
                return;
            }

            QuestInfo info = pending.Dequeue();
            icon.SetIcon(info.Icon);

            questNameText.SetVar(QuestLocVar.QuestName, info.Name, false);
            questNameText.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopQuest);

            bool hasObjective = info.Objectives.Length > 0;
            objDescText.gameObject.SetActive(hasObjective);
            if (!hasObjective) return;

            objDescText.SetVar(QuestLocVar.Value, 1, false);
            objDescText.SetVar(QuestLocVar.Desc, info.Objectives[0].Desc, false);
            objDescText.SetText(LocTableSet.QuestSystem, QuestLocKey.Common.RewardPopObj);
        }

        public override void Close()
        {
            PlayerInputManager.Instance.OnClick -= Close;
            pending.Clear();
            base.Close();
        }
    }
}
