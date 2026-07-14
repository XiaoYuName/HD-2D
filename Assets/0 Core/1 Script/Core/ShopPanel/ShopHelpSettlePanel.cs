using System;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 商店帮忙小游戏的自制结算面板：内嵌在 <see cref="ShopHelpPanel"/> 层级下的子物体（非 UISystem 面板，避免注册缺失导致弹不出）。
/// 胜/败时由主面板 <see cref="Show"/> 激活并填充：左侧 AvatarPortraitPop 台词、标题、奖励/失败文案、已完成货架数量、再来一局与返回按钮。
/// </summary>
public class ShopHelpSettlePanel : MonoBehaviour
{
    [Title("Ref")]
    [LabelText("角色台词(AvatarPortraitPop)")][SerializeField] AvatarPortraitPop app;
    [LabelText("标题")][SerializeField] LocalizeStringEvent titleText;
    [LabelText("奖励/失败文案")][SerializeField] LocalizeStringEvent contentText;
    [LabelText("失败文案(仅失败时显示)")][SerializeField] LocalizeStringEvent failText;
    [LabelText("奖励发放提示(仅胜利时显示)")][SerializeField] LocalizeStringEvent rewardNoteText;
    [LabelText("已完成货架数量文本")][SerializeField] TMP_Text filledCountText;
    [LabelText("窗口(缩放动画根)")][SerializeField] RectTransform window;
    [LabelText("再来一局条件不满足提示(WarnTip)")][SerializeField] WarnTip warnTip;
    [Title("Button")]
    [LabelText("再来一局")][SerializeField] Button replayButton;
    [LabelText("返回")][SerializeField] Button backButton;

    public event Action OnReplay;
    public event Action OnBack;

    bool hooked;

    void HookOnce()
    {
        if(hooked)
            return;
        hooked = true;
        replayButton.onClick.AddListener(OnReplayClicked);
        backButton.onClick.AddListener(() => OnBack?.Invoke());

        // 「返回」文案改走通用表（Common），按钮文案组件由 UIGen.Button 生成时挂在子物体上
        LocalizeStringEvent backLabel = backButton.GetComponentInChildren<LocalizeStringEvent>();
        backLabel.SetTextSafe(LocTableSet.Common, "Back");
    }

    // 点击「再来一局」：进入消耗是否足够统一交给 GameEnterPanelConfig（经 AssetKeys 加载）判断，不足则弹 WarnTip 并拦截，不再预先禁用按钮。
    void OnReplayClicked()
    {
        GameEnterPanelConfig config = AssetsManager.Instance.LoadAssets<GameEnterPanelConfig>(AssetKeys.GameEnterPanelConfigPath);
        bool enough = config.HasEnough(UIPanelIdSet.ShopHelpPanel, warnTip);
        AssetsManager.Instance.FreeAsset(AssetKeys.GameEnterPanelConfigPath);

        if(enough)
            OnReplay?.Invoke();
    }

    /// <summary>展示结算：胜利显示金币/好感度奖励与「奖励已发放」提示，失败显示失败文案与「失败」标签；filledCount 为本局已摆上货架的数量（只显示当前数量）。</summary>
    public void Show(bool win, int coin, int favor, int filledCount)
    {
        HookOnce();
        gameObject.SetActive(true);

        string table = LocTableSet.ShopHelpPanel;
        app.SetContext(LanguageManager.Instance.GetLocalizedString(table, win ? "ShopHelpWinSpeech" : "ShopHelpLoseSpeech"));

        titleText.SetTextSafe(LocTableSet.Common, "SettleTitle");

        if(win)
        {
            contentText.SetTextWithVars(table, "ShopHelpSettleReward",
                (LocVarSet.ShopHelp.Coin, coin), (LocVarSet.ShopHelp.Favor, favor));
            rewardNoteText.gameObject.SetActive(true);
            failText.gameObject.SetActive(false);
        }
        else
        {
            contentText.SetTextSafe(table, "ShopHelpLoseContent");
            failText.gameObject.SetActive(true);
            failText.SetTextSafe(LocTableSet.Common, "Fail");
            rewardNoteText.gameObject.SetActive(false);
        }

        filledCountText.text = filledCount.ToString();

        // 入场缩放
        RectTransform root = window != null ? window : (RectTransform)transform;
        root.localScale = Vector3.zero;
        Tween.Scale(root, Vector3.one, 0.28f, Ease.OutBack, useUnscaledTime: true);
    }

    public void Hide() => gameObject.SetActive(false);
}
