using PrimeTween;
using UnityEngine;
using UnityEngine.Localization.Components;
using TMPro;
using UnityEngine.UI;

namespace XFramework.Fish
{
    public class FishGameWinPanel : UIBase
    {
        [SerializeField] FishResultCellUI fishResultCellUI;
        [SerializeField] TextMeshProUGUI fishLvText, fishXpText;
        [SerializeField] Image exProgressBar;
        [SerializeField] Button continueBtn, quitBtn;

        const float ExpAnimSegmentDuration = 0.5f;    // 经验条每一段（每跨一级算一段）动画时长
        Sequence expAnimSeq;

        public override void Init()
        {
            continueBtn.onClick.AddListener(OnContinueButton);
            quitBtn.onClick.AddListener(OnQuitButton);
        }

        /// <summary>
        /// prevLevel/prevExp：本次结算前的等级与等级内经验；newLevel/newExp：结算后的等级与等级内经验（可能跨多级）。
        /// exProgressBar 会从 prevExp 动态涨到 newExp，每跨一级清零重涨一段。
        /// </summary>
        public void Set(ItemInfo fishItem, float l, float w, int prevLevel, int prevExp, int newLevel, int newExp)
        {
            fishResultCellUI.Set(fishItem, l, w);
            PlayExpGainAnim(prevLevel, prevExp, newLevel, newExp);
        }

        void PlayExpGainAnim(int prevLevel, int prevExp, int newLevel, int newExp)
        {
            expAnimSeq.Stop();
            fishLvText.text = FishGameManager.LvPrefix + prevLevel;

            var mg = FishGameManager.Instance;
            var seq = Sequence.Create();
            int lvl = prevLevel;
            int fromExp = prevExp;

            // 每跨过一级：先把经验条涨满，再切等级文本、经验条清零，继续涨下一级
            while (lvl < newLevel)
            {
                int need = mg.GetExpToNext(lvl);
                if (need <= 0)
                    break;
                seq = seq.Chain(BuildSegmentTween(fromExp, need, need));
                int nextLvl = lvl + 1;
                seq = seq.ChainCallback(() =>
                {
                    fishLvText.text = FishGameManager.LvPrefix + nextLvl;
                    exProgressBar.fillAmount = 0f;
                });
                lvl = nextLvl;
                fromExp = 0;
            }

            int finalNeed = mg.GetExpToNext(newLevel);
            seq = seq.Chain(BuildSegmentTween(fromExp, finalNeed > 0 ? newExp : 0, finalNeed));
            expAnimSeq = seq;
        }

        // 单段经验条动画：从 fromExp/need 涨到 toExp/need；need<=0 视为满级，固定显示满条 + MAX
        Tween BuildSegmentTween(int fromExp, int toExp, int need)
        {
            float fromFrac = need > 0 ? fromExp / (float)need : 1f;
            float toFrac = need > 0 ? toExp / (float)need : 1f;
            return Tween.Custom(exProgressBar, fromFrac, toFrac, ExpAnimSegmentDuration, (bar, v) =>
            {
                bar.fillAmount = v;
                fishXpText.text = need > 0 ? $"{Mathf.RoundToInt(v * need)}/{need}" : "MAX";
            });
        }

        public override void Open()
        {
            base.Open();
        }
        public override void Close()
        {
            expAnimSeq.Stop();
            base.Close();
        }

        void OnContinueButton()
        {
            UISystem.Instance.GetUI<FishGamePanel>(UIPanelIdSet.FishGamePanel)?.ContinueFishing();
            Close();
        }
        public void OnQuitButton()
        {
            UISystem.Instance.CloseUI(nameof(FishGamePanel));
            Close();
        }
    }
}
