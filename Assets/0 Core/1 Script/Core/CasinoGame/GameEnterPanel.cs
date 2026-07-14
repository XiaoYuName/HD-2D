using System.Collections.Generic;
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
        GameEnterPanelItemData gameConfig = config.DataDict[panelId];

        // gameIcon.SetIcon(gameConfig.IconPath);
        gameNameText.SetText(LocTableSet.GameEnterPanel, gameConfig.NameKey);
        gameDescText.SetText(LocTableSet.GameEnterPanel, gameConfig.DescKey);

        // 带占位符的文本，key="Consume" 内容按 PropertyType 显示："消耗体力 {sp}"/"金币 {coin}"/"消耗行动力 {ap}"；一行可同时配置多种资源消耗
        int sp = 0, coin = 0, ap = 0;
        foreach(KeyValuePair<PropertyType, int> consume in gameConfig.Consumes)
        {
            if(consume.Key == PropertyType.Strength) sp += consume.Value;
            else if(consume.Key == PropertyType.GameCoin) coin += consume.Value;
            else if(consume.Key == PropertyType.ActionPointsValue) ap += consume.Value;
        }
        consumeText.SetVar(LocVarSet.MiniGame.SpConsumeCount, sp);
        consumeText.SetVar(LocVarSet.MiniGame.CoinCosumeCount, coin);
        consumeText.SetVar(LocVarSet.MiniGame.ApConsumeCount, ap);
    }
    void StartGame()
    {
        // 校验并扣除进入消耗（判断/扣除逻辑统一交给 GameEnterPanelConfig）；不足则拦截并弹出对应提示，不进入小游戏
        if(!config.TryConsume(panelId, warnTip))
            return;

        UISystem.Instance.OpenUI(panelId);
        Close();
    }
}
