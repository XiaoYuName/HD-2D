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
/// 胜/败时由主面板 <see cref="Show"/> 激活并填充：左侧 AvatarPortraitPop 台词、标题、奖励/失败文案、再来一局与返回按钮。
/// </summary>
public class ShopHelpSettlePanel : MonoBehaviour
{
    [Title("Ref")]
    [LabelText("角色台词(AvatarPortraitPop)")][SerializeField] AvatarPortraitPop app;
    [LabelText("标题")][SerializeField] LocalizeStringEvent titleText;
    [LabelText("奖励/失败文案")][SerializeField] LocalizeStringEvent contentText;
    [LabelText("窗口(缩放动画根)")][SerializeField] RectTransform window;
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
        replayButton.onClick.AddListener(() => OnReplay?.Invoke());
        backButton.onClick.AddListener(() => OnBack?.Invoke());
    }

    /// <summary>展示结算：胜利显示金币/好感度奖励，失败显示失败文案；canReplay 控制再来一局是否可点。</summary>
    public void Show(bool win, int coin, int favor, bool canReplay)
    {
        HookOnce();
        gameObject.SetActive(true);

        string table = LocTableSet.ShopHelpPanel;
        if(app != null)
            app.SetContext(LanguageManager.Instance.GetLocalizedString(table, win ? "ShopHelpWinSpeech" : "ShopHelpLoseSpeech"));

        titleText.SetTextSafe(table, "SettleTitle");

        if(win)
            contentText.SetTextWithVars(table, "ShopHelpSettleReward",
                (LocVarSet.ShopHelp.Coin, coin), (LocVarSet.ShopHelp.Favor, favor));
        else
            contentText.SetTextSafe(table, "ShopHelpLoseContent");

        replayButton.interactable = canReplay;

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

        // 奖励/失败文案
        contentText = UIGen.Loc("Content", win.transform, "ShopHelpSettleReward", 30, new Color(0.2f, 0.2f, 0.2f), TextAlignmentOptions.Center);
        UIGen.Center(contentText.GetComponent<RectTransform>(), 520f, 60f, 120f, 30f);

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
