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

    [Button]
    void Set()
    {
        consumeText.SetText(LocalizeTableSet.CasinoGame, "Consume");
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

        gameIcon.SetIcon(gameConfig.IconResPath);

        // 多语言：把 config 里的 Name/Desc 当作 StringTable 的 key
        gameNameText.SetText(LocalizeTableSet.CasinoGame, gameConfig.Name);
        gameDescText.SetText(LocalizeTableSet.CasinoGame, gameConfig.Desc);

        // 带占位符的文本，例如表里 key="Consume" 内容为 "消耗体力 {sp} 金币 {coin}"
        consumeText.SetVar(LocalizeVarSet.MiniGame.SpConsumeCount, gameConfig.ConsumeSp);
        consumeText.SetVar(LocalizeVarSet.MiniGame.CoinCosumeCount, gameConfig.ConsumeCoin);
    }
    void StartGame()
    {
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
            return false;
        }
        if (!PlayerInfo.St.Bag.HasMoney(gameConfig.ConsumeCoin))
        {
            return false;
        }

        return true;
    }
}
