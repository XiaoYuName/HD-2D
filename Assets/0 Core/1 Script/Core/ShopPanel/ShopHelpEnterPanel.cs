using UnityEngine;
using TMPro;
using UnityEngine.UI;
using XFramework;
using UnityEngine.Localization.Components;

public class ShopHelpEnterPanel : UIBase
{
    [SerializeField] GameEnterPanelConfig config;
    [SerializeField] TextMeshProUGUI apText, spText;
    [SerializeField] LocalizeStringEvent consumeText;
    [SerializeField] Button startButton, closeButton;
    [SerializeField] WarnTip warnTip;

    public override void Init()
    {
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(OnCloseButton);
    }

    public override void Open()
    {
        base.Open();
        RefreshUI();
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChange);
    }

    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(OnPlayerDataChange);
    }

    void RefreshUI()
    {
        int ap = config.GetConsume(UIPanelIdSet.ShopHelpPanel, PropertyType.ActionPointsValue);
        int sp = config.GetConsume(UIPanelIdSet.ShopHelpPanel, PropertyType.Strength);
        consumeText.SetVar(LocVarSet.MiniGame.ApConsumeCount, ap);
        consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, sp);
        consumeText.SetVar(LocVarSet.MiniGame.CoinCosumeCount, 0);
    }

    void OnPlayerDataChange(PlayerData data)
    {
        apText.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);
        spText.text = GameDataManager.Instance.GetPropertyText(PropertyType.Strength);
    }

    void OnStartButton()
    {
        if(!CheckCanStart())
            return;

        UISystem.Instance.OpenUI(UIPanelIdSet.ShopHelpPanel);
        Close();
    }

    // 仅校验不扣除：实际扣除由 ShopHelpGameManager.StartGame 在 ShopHelpPanel 打开时统一完成一次，避免重复扣费。
    // 判断逻辑统一交给 GameEnterPanelConfig，不满足时由其直接弹 warnTip。
    bool CheckCanStart() => config.HasEnough(UIPanelIdSet.ShopHelpPanel, warnTip);

    void OnCloseButton()
    {
        Close();
    }
}
