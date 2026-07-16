using UnityEngine;
using XFramework;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;


public class FishGameEnterPanel : UIBase
{
    [SerializeField] GameEnterPanelConfig config;
    [SerializeField] EnvironmentModeConfig envModeConfig;
    
    [SerializeField] Button startButton, upgadeButton, galleryButton, closeButton;
    [SerializeField] TextMeshProUGUI apText;
    [SerializeField] Image timeIcon;
    [SerializeField] LocalizeStringEvent timeText;
    [SerializeField] LocalizeStringEvent consumeText;
    [SerializeField] TextMeshProUGUI baitCountText;
    [SerializeField] WarnTip warnTip;
    
    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
        startButton.onClick.AddListener(OnStartButton);
        upgadeButton.onClick.AddListener(OnUpgadeButton);
        galleryButton.onClick.AddListener(OnGalleryButton);
    
    }
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataDayChange(OnTimePerChange);
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChange);
        consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, config.GetConsume(UIPanelIdSet.FishGamePanel, PropertyType.Strength));
        baitCountText.text = InventoryManager.Instance.GetItemCount(ItemIdSet.Bait).ToString();
    }

    public override void Close()
    {
        base.Close();
        timeIcon.ClearIcon();
        GameDataManager.Instance.UnregisterPlayerDataDayChange(OnTimePerChange);
    }
    
    void OnTimePerChange(PlayerData playerData)
    {
        timeIcon.SetIcon(envModeConfig.GetIconPath(GameDataManager.Instance.CurEnvMode));
        timeText.SetText(LocTableSet.MainUI, envModeConfig.GetNameKey(GameDataManager.Instance.CurEnvMode));
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
    void OnUpgadeButton()
    {
        UISystem.Instance.OpenUI(UIPanelIdSet.FishUpgradePanel);
    }
    void OnGalleryButton()
    {
        UISystem.Instance.OpenUI(UIPanelIdSet.FishGalleryPanel);
    }

    bool CheckCanStart()
    {
        if(InventoryManager.Instance.GetItemCount(ItemIdSet.Bait) == 0)
        {
            warnTip.Show(LocTableSet.Fish, LocVarSet.FishGame.NotEnoughBait);
            // 鱼饵不足
            return false;
        }


        if(config.TryConsume(UIPanelIdSet.FishGamePanel, warnTip))
            return false;
        return true;
    }
}
