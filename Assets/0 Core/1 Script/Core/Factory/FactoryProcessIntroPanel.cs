using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using XFramework;
using UnityEngine.Localization.Components;

// 加工确认弹窗：主面板点「加工」先弹出本窗（展示已选周边 + 当前体力 + 本次消耗），
// 本窗点「开始加工」后才真正打开下压小游戏 FactoryProcessGamePanel，并关闭本窗。
public class FactoryProcessIntroPanel : UIBase
{
    [SerializeField] TextMeshProUGUI spValueText;
    [SerializeField] FactoryComposedItemCellUI itemCell;
    [SerializeField] FactoryGameConfig config;
    [SerializeField] LocalizeStringEvent consumeSpLse;
    [SerializeField] Button startButton, closeButton;
    [SerializeField] WarnTip warnTip;

    FactoryMoldItemInfo material;
    Action onClosed;

    public override void Init()
    {

    }

    void Awake()
    {
        startButton.onClick.AddListener(OnStartButton);
        closeButton.onClick.AddListener(CloseCurUI);
    }
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChaneg);
        consumeSpLse.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Intro.ConsumeStaminaFmt,
            (LocVarSet.FactoryMain.Consume, config.StartSpCost));
    }
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(OnPlayerDataChaneg);
    }

    // 由主面板在点「加工」时调用：带入本局选中的生产资料，及小游戏关闭后要回调主面板的刷新
    public void Set(FactoryMoldItemInfo material, Action onClosed)
    {
        this.material = material;
        this.onClosed = onClosed;
        itemCell.Set(material);
    }

    void OnPlayerDataChaneg(PlayerData data)
    {
        spValueText.text = GameDataManager.Instance.GetPropertyText(PropertyType.Strength);
    }

    // 确认开始加工：先查体力，不够则提示并中止；够则扣体力，打开下压小游戏并带入本局批次，关闭本确认弹窗
    void OnStartButton()
    {
        if(!GameDataManager.Instance.HasProperty(PropertyType.Strength, config.StartSpCost))
        {
            warnTip.Show(LocTableSet.Factory, FactoryLocKeySet.Process.NotEnoughStamina);
            return;
        }
        GameDataManager.Instance.RemoveProperty(PropertyType.Strength, config.StartSpCost);

        FactoryProcessGamePanel panel = UISystem.Instance.OpenUI<FactoryProcessGamePanel>(UIPanelIdSet.FactoryProcessGamePanel);
        panel.SetCraftBatch(new List<FactoryMoldItemInfo> { material });
        panel.SetOnClosed(onClosed);
        Close();
    }
    void CloseCurUI()
    {
        Close();
    }
}
