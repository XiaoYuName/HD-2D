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
        if(app != null)
            app.SetContext(LanguageManager.Instance.GetLocalizedString(table, win ? "ShopHelpWinSpeech" : "ShopHelpLoseSpeech"));

        titleText.SetTextSafe(LocTableSet.Common, "SettleTitle");

        if(win)
        {
            contentText.SetTextWithVars(table, "ShopHelpSettleReward",
                (LocVarSet.ShopHelp.Coin, coin), (LocVarSet.ShopHelp.Favor, favor));
            if(rewardNoteText != null)
            {
                rewardNoteText.gameObject.SetActive(true);
                rewardNoteText.SetTextSafe(LocTableSet.Common, "RewardAutoSent");
            }
            if(failText != null)
                failText.gameObject.SetActive(false);
        }
        else
        {
            contentText.SetTextSafe(table, "ShopHelpLoseContent");
            if(failText != null)
            {
                failText.gameObject.SetActive(true);
                failText.SetTextSafe(LocTableSet.Common, "Fail");
            }
            if(rewardNoteText != null)
                rewardNoteText.gameObject.SetActive(false);
        }

        if(filledCountText != null)
            filledCountText.text = filledCount.ToString();

        // 入场缩放
        RectTransform root = window != null ? window : (RectTransform)transform;
        root.localScale = Vector3.zero;
        Tween.Scale(root, Vector3.one, 0.28f, Ease.OutBack);
    }

    public void Hide() => gameObject.SetActive(false);

#if UNITY_EDITOR
    // 由 ShopHelpPanel.BuildUI 调用，在本子物体下生成结算窗口并绑定引用。
    public void EditorBuild(string avatarPrefabGuid)
    {
        RectTransform rootRt = (RectTransform)transform;
        UIGen.Stretch(rootRt);

        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // 遮罩（全屏，拦截点击）
        Image mask = UIGen.Img("Mask", transform, new Color(0f, 0f, 0f, 0.55f));
        UIGen.Stretch(mask.rectTransform);

        // 窗口
        Image win = UIGen.Img("Window", transform, new Color(0.78f, 0.78f, 0.80f, 0.97f));
        UIGen.Center(win.rectTransform, 900f, 420f, 0f, 0f);
        window = win.rectTransform;

        // 左侧角色台词（AvatarPortraitPop 预制体）
        GameObject avatarGo = UIGen.InstantiatePrefab(avatarPrefabGuid, win.transform);
        if(avatarGo != null)
        {
            RectTransform art = (RectTransform)avatarGo.transform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(60f, -30f);
            app = avatarGo.GetComponent<AvatarPortraitPop>();
        }

        // 标题
        titleText = UIGen.Loc("Title", win.transform, "SettleTitle", 40, new Color(0.2f, 0.16f, 0.1f), TextAlignmentOptions.Center);
        UIGen.Center(titleText.GetComponent<RectTransform>(), 400f, 60f, 120f, 150f);

        // 失败标签（仅失败时显示，默认隐藏）
        failText = UIGen.Loc("FailLabel", win.transform, "Fail", 34, new Color(0.75f, 0.15f, 0.15f), TextAlignmentOptions.Center);
        UIGen.Center(failText.GetComponent<RectTransform>(), 300f, 50f, 120f, 95f);
        failText.gameObject.SetActive(false);

        // 奖励/失败文案
        contentText = UIGen.Loc("Content", win.transform, "ShopHelpSettleReward", 30, new Color(0.2f, 0.2f, 0.2f), TextAlignmentOptions.Center);
        UIGen.Center(contentText.GetComponent<RectTransform>(), 520f, 60f, 120f, 30f);

        // 奖励发放提示（仅胜利时显示，默认隐藏）
        rewardNoteText = UIGen.Loc("RewardNote", win.transform, "RewardAutoSent", 22, new Color(0.35f, 0.35f, 0.35f), TextAlignmentOptions.Center);
        UIGen.Center(rewardNoteText.GetComponent<RectTransform>(), 520f, 40f, 120f, -10f);
        rewardNoteText.gameObject.SetActive(false);

        // 已完成货架数量（只显示当前数量）
        filledCountText = UIGen.Text("FilledCount", win.transform, "0", 26, new Color(0.2f, 0.2f, 0.2f), TextAlignmentOptions.Center);
        UIGen.Center(filledCountText.GetComponent<RectTransform>(), 200f, 40f, 120f, -50f);

        // 按钮
        replayButton = UIGen.Button("ReplayButton", win.transform, "ShopHelpReplay", new Color(0.96f, 0.86f, 0.42f), Color.black);
        UIGen.Center((RectTransform)replayButton.transform, 200f, 66f, 40f, -140f);
        backButton = UIGen.Button("BackButton", win.transform, "ShopHelpBack", new Color(0.95f, 0.95f, 0.95f), Color.black);
        UIGen.Center((RectTransform)backButton.transform, 200f, 66f, 280f, -140f);

        gameObject.SetActive(false);
        EditorUtility.SetDirty(this);
    }
#endif
}
