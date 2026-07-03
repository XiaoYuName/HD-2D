using UnityEngine;
using XFramework;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using Sirenix.OdinInspector;

public class CasinoGameEnterPanel : UIBase
{
    [SerializeField] Image gameIcon;
    [SerializeField] LocalizeStringEvent gameNameText, gameDescText;
    [SerializeField] LocalizeStringEvent consumeText;// 如：消耗体力-30 消耗金币-50
    [SerializeField] CasinoGameConfig config;
    [SerializeField] Button closeButton;
    [SerializeField] Button startGameButton;
    [SerializeField] int curGameId;
    [SerializeField] WarnTip warnTip;

    [Button]
    void Set()
    {
        consumeText.SetText(LocTableSet.CasinoGame, "Consume");
    }
    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
        startGameButton.onClick.AddListener(StartGame);
    }
    /// <summary>
    /// 打开后传入要展示的 GameId，并刷新界面
    /// 用法：UISystem.Instance.OpenUI<CasinoGameEnterPanel>("CasinoGameEnterPanel").ShowData(gameId);
    /// </summary>
    public void ShowData(int gameId)
    {
        curGameId = gameId;
        RefreshUI();
    }

    void RefreshUI()
    {
        // 根据传入的curGameId 获取游戏配置
        CasinoGameItemData gameConfig = config.DataDict[curGameId];

        // gameIcon.SetIcon(gameConfig.IconResPath);

        // 多语言：把 config 里的 Name/Desc 当作 StringTable 的 key
        gameNameText.SetText(LocTableSet.CasinoGame, gameConfig.Name);
        gameDescText.SetText(LocTableSet.CasinoGame, gameConfig.Desc);

        // 带占位符的文本，例如表里 key="Consume" 内容为 "消耗体力 {sp} 金币 {coin}"
        consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, gameConfig.ConsumeSp);
        consumeText.SetVar(LocVarSet.MiniGame.CoinCosumeCount, gameConfig.ConsumeCoin);
    }
    void StartGame()
    {
        if (!CanStartGame())
            return;
            
        UISystem.Instance.OpenUI(config.DataDict[curGameId].PanelId);
        Close();
    }

    bool CanStartGame()
    {
        // 获取当前游戏配置
        CasinoGameItemData gameConfig = config.DataDict[curGameId];

        // 检测玩家是否满足条件
        if (!PlayerInfo.St.Stats.CanConsumeSp(gameConfig.ConsumeSp))
        {
            warnTip.ShowTip(LocTableSet.CasinoGame, LocVarSet.MiniGame.NotEnoughStamina);
            return false;
        }
        if (!InventoryManager.Instance.HasGameCoin(gameConfig.ConsumeCoin))
        {
            warnTip.ShowTip(LocTableSet.CasinoGame, LocVarSet.MiniGame.NotEnoughGameCoin);
            return false;
        }

        return true;
    }
}
