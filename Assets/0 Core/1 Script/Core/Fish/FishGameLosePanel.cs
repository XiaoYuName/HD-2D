using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace XFramework.Fish
{
    public class FishGameLosePanel : UIBase
    {
        [SerializeField] Button continueButton, upgradeButton, quitButtn;
        [SerializeField] TextMeshProUGUI fishLvText, fishXpText;
        [SerializeField] Image exProgressBar;

        public override void Init()
        {
            continueButton.onClick.AddListener(OnContinueButton);
            upgradeButton.onClick.AddListener(OnUpgradeButton);
            quitButtn.onClick.AddListener(OnQuitButton);
        }

        public override void Open()
        {
            base.Open();

            FishGameManager.Instance.OnProgressChanged += RefreshProgress;
            RefreshProgress();
        }

        public override void Close()
        {
            FishGameManager.Instance.OnProgressChanged -= RefreshProgress;
            base.Close();
        }

        void RefreshProgress()
        {
            FishGameManager manager = FishGameManager.Instance;
            fishLvText.text = FishGameManager.LvPrefix + manager.Level;

            int expToNext = manager.GetExpToNext(manager.Level);
            if(expToNext <= 0)
            {
                fishXpText.text = "MAX";
                exProgressBar.fillAmount = 1f;
                return;
            }

            int currentExp = Mathf.Clamp(manager.Exp, 0, expToNext);
            fishXpText.text = $"{currentExp}/{expToNext}";
            exProgressBar.fillAmount = currentExp / (float)expToNext;
        }

        void OnContinueButton()
        {
            UISystem.Instance.GetUI<FishGamePanel>(UIPanelIdSet.FishGamePanel)?.ContinueFishing();
            Close();
        }

        void OnUpgradeButton()
        {
            UISystem.Instance.OpenUI(UIPanelIdSet.FishUpgradePanel);
            Close();
        }

        void OnQuitButton()
        {
            UISystem.Instance.CloseUI(UIPanelIdSet.FishGamePanel);
            Close();
        }
    }
}
