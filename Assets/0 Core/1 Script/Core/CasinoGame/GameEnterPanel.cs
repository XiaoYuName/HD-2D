using UnityEngine;
using XFramework;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using Sirenix.OdinInspector;

public class GameEnterPanel : UIBase
{
    [SerializeField] Image gameIcon;
    [SerializeField] LocalizeStringEvent gameNameText, gameDescText;
    [SerializeField] LocalizeStringEvent consumeText;// 如：消耗体力-30 消耗金币-50
    [SerializeField] GameEnterPanelConfig config;
    [SerializeField] Button closeButton;
    [SerializeField] Button startGameButton;
    [SerializeField] string panelId;
    [SerializeField] WarnTip warnTip;

    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
        startGameButton.onClick.AddListener(StartGame);
    }
    // 打开后传入要展示的 GameId，并刷新界面
    public void ShowData(string panelId)
    {
        this.panelId = panelId;
        RefreshUI();
    }

    void RefreshUI()
    {
        // 根据传入的curGameId 获取游戏配置
        GameEnterItemData gameConfig = config.DataDict[panelId];

        // gameIcon.SetIcon(gameConfig.IconResPath);

        // 多语言：把 config 里的 Name/Desc 当作 StringTable 的 key
        gameNameText.SetText(LocTableSet.CasinoGame, gameConfig.Name);
        gameDescText.SetText(LocTableSet.CasinoGame, gameConfig.Desc);

        // 带占位符的文本，例如表里 key="Consume" 内容为 "消耗体力 {sp} 金币 {coin}"
        // consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, gameConfig.ConsumeSp);
        // consumeText.SetVar(LocVarSet.MiniGame.CoinCosumeCount, gameConfig.ConsumeCoin);
    }
    void StartGame()
    {
        if (!CanStartGame())
            return;
            
        UISystem.Instance.OpenUI(config.DataDict[panelId].PanelId);
        Close();
    }

    bool CanStartGame()
    {
        // 获取当前游戏配置
        GameEnterItemData gameConfig = config.DataDict[panelId];

        // // 检测玩家是否满足条件
        // if (GameDataManager.Instance.GetProperty(PropertyType.Strength).Value < gameConfig.ConsumeSp)
        // {
        //     warnTip.ShowTip(LocTableSet.CasinoGame, LocVarSet.MiniGame.NotEnoughStamina);
        //     return false;
        // }
        // if (!GameDataManager.Instance.HasProperty(PropertyType.GameCoin,gameConfig.ConsumeCoin))
        // {
        //     warnTip.ShowTip(LocTableSet.CasinoGame, LocVarSet.MiniGame.NotEnoughGameCoin);
        //     return false;
        // }

        return true;
    }
}
