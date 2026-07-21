using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;

namespace XFramework.Fish
{
    public class FishGameEnterPanel : UIBase
    {
        [SerializeField] GameEnterPanelConfig config;
        [SerializeField] TimeSlotConfig envModeConfig;
        
        [SerializeField] Button startButton, galleryButton, closeButton;
        [SerializeField] TextMeshProUGUI apText;
        [SerializeField] Image timeIcon;
        [SerializeField] LocalizeStringEvent timeText;
        [SerializeField] LocalizeStringEvent consumeText;
        [SerializeField] TextMeshProUGUI baitCountText;
        [SerializeField] WarnTip warnTip;
        
        public override void Init()
        {
            startButton.onClick.AddListener(OnStartButton);
            closeButton.onClick.AddListener(Close);
            galleryButton.onClick.AddListener(OnGalleryButton);
        }
        public override void Open()
        {
            base.Open();
            GameDataManager.Instance.RegisterPlayerDataDayChange(OnTimePerChange);
            GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChange);
            consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, config.GetConsume(UIPanelIdSet.FishGamePanel, PropertyType.Strength), false);
            consumeText.SetVar(LocVarSet.MiniGame.CoinConsumeCount, config.GetConsume(UIPanelIdSet.FishGamePanel, PropertyType.GameCoin), false);
            consumeText.SetVar(LocVarSet.MiniGame.ApConsumeCount, config.GetConsume(UIPanelIdSet.FishGamePanel, PropertyType.ActionPointsValue));
            baitCountText.text = InventoryManager.Instance.GetItemCount(ItemIdSet.Bait).ToString();
        }

        public override void Close()
        {
            base.Close();
            timeIcon.ClearIcon();
            GameDataManager.Instance.UnregisterPlayerDataDayChange(OnTimePerChange);
            GameDataManager.Instance.UnregisterPlayerDataChange(OnPlayerDataChange);
        }
        
        void OnTimePerChange(PlayerData playerData)
        {
            timeIcon.SetIcon(envModeConfig.GetIconPath(playerData.TimeSlot));
            timeText.SetText(LocTableSet.MainUI, envModeConfig.GetNameKey(playerData.TimeSlot));
        }
        void OnPlayerDataChange(PlayerData playerData)
        {
            apText.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);
        }
        void OnStartButton()
        {
            // 恢复行动力
            if(!CheckCanStart())
                return;
            
            UISystem.Instance.OpenUI(UIPanelIdSet.FishGamePanel);
        }
        void OnGalleryButton()
        {
            UISystem.Instance.OpenUI(UIPanelIdSet.FishGalleryPanel);
        }

        bool CheckCanStart()
        {
            if(InventoryManager.Instance.GetItemCount(ItemIdSet.Bait) == 0)
            {
                warnTip.Show(LocTableSet.Fish, LocVarSet.Fish.NotEnoughBait);
                // 鱼饵不足
                return false;
            }

            if(!config.TryConsume(UIPanelIdSet.FishGamePanel, warnTip))
                return false;
            return true;
        }
    }
}
