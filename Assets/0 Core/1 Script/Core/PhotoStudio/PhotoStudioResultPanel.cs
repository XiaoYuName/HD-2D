using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 拍摄结算面板：展示本局照片质量分数、灵感/女主好感奖励、自动入包的照片道具，
// 提供「再来一局」（消耗行动值，重新进入配置）与「返回」两个出口。
// 由 PhotoStudioManager 在进入 Result 状态时 Show(result) 调用。
public sealed class PhotoStudioResultPanel : MonoBehaviour
{
    [Header("文本")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI scoreValueText;        // 分数数值
    [SerializeField] TextMeshProUGUI inspirationValueText;  // 灵感 +X
    [SerializeField] TextMeshProUGUI affinityValueText;     // 女主好感 +X
    [SerializeField] LocalizeStringEvent commentLocalize;   // 点评气泡多语言（可选）

    [Header("照片/道具")]
    [SerializeField] Image photoReviewImage;                // 左侧照片预览
    [SerializeField] Image itemIconImage;                   // 入包道具图标
    [SerializeField] TextMeshProUGUI itemCountText;         // 道具数量 x1

    [Header("按钮")]
    [SerializeField] Button replayButton;                   // 再来一局
    [SerializeField] Button returnButton;                   // 返回

    public event Action OnReplay;
    public event Action OnReturn;

    void Awake()
    {
        replayButton.onClick.AddListener(Replay);
        returnButton.onClick.AddListener(Return);
    }

    // 由 PhotoStudioManager 调用：刷新数值显示并发放奖励
    public void Show(PhotoStudioPhotoResult result)
    {
        gameObject.SetActive(true);
        RefreshDisplay(result);
        GrantRewards(result);
    }

    void RefreshDisplay(PhotoStudioPhotoResult result)
    {
        // 灵感/好感/压力 均为 积分/10（只入不舍，即向下取整）
        int amount = RewardAmount(result.qualityScore);

        scoreValueText.text = result.qualityScore.ToString();
        inspirationValueText.text = $"+{amount}";
        affinityValueText.text = $"+{amount}";
        itemCountText.text = $"x{1}";

        // 照片预览
        // Sprite sprite = tier != null ? tier.reviewSprite : null;
        // if(sprite == null)
        //     sprite = result.characterSprite;
        // photoReviewImage.sprite = sprite;
        // photoReviewImage.enabled = sprite != null;

        // 点评气泡
        // string commentKey = tier != null ? tier.commentKey : null;
        // commentLocalize.StringReference.SetReference(LocalizeTableSet.PhotoStudio, commentKey);
        // commentLocalize.RefreshString();
    }

    // 拍摄结果反馈：发放照片素材 + 女主灵感/好感/压力变化（积分/10，只入不舍）+ 后续系统接入
    void GrantRewards(PhotoStudioPhotoResult result)
    {
        PhotoQualityTierConfig tier = result.qualityTierConfig;
        int amount = RewardAmount(result.qualityScore);

        // 1. 获得对应品质的照片素材，存入素材库（背包）。照片素材道具ID随品质不同，配在质量档位上
        long photoItemId = tier != null ? tier.photoItemId : 0;
        if(photoItemId != 0)
            InventoryManager.Instance.AddItem(photoItemId, 1);
        else
            Debug.Log($"[PhotoStudioResultPanel] 获得照片素材（品质={result.qualityTier}）x1，但该品质未配置照片素材道具ID，暂不入库。");

        // 2. 高品质照片（传奇及以上）触发专属拍摄剧情
        // TODO: 接入拍摄剧情系统，按 result.qualityTier 判断是否达到触发档位
        if(result.qualityTier >= PhotoQualityTier.Perfect)
            Debug.Log($"[PhotoStudioResultPanel] 高品质照片（{result.qualityTier}）应触发专属拍摄剧情。（待接入剧情系统）");

        // 3. 女主获得 灵感值 +amount（待接入女主属性系统）
        Debug.Log($"[PhotoStudioResultPanel] 女主灵感值 +{amount}。（待接入女主属性系统）");

        // 4. 女主获得 好感度 +amount（待接入女主属性系统）
        Debug.Log($"[PhotoStudioResultPanel] 女主好感度 +{amount}。（待接入女主属性系统）");

        // 5. 女主失去 压力值 -amount（待接入女主属性系统）
        Debug.Log($"[PhotoStudioResultPanel] 女主压力值 -{amount}。（待接入女主属性系统）");

        // 6. 摄影师 NPC 进度更新，解锁新道具、姿势、场景
        // TODO: 接入摄影师 NPC 进度系统
    }

    // 积分/10，只入不舍（向下取整）
    static int RewardAmount(int score) => Mathf.Max(0, score) / 10;

    [Button("再来一局")]
    public void Replay()
    {
        gameObject.SetActive(false);
        OnReplay?.Invoke();
    }

    [Button("返回")]
    public void Return()
    {
        gameObject.SetActive(false);
        OnReturn?.Invoke();
    }
    #region Create UI
#if UNITY_EDITOR
    [Button("创建结算面板UI", ButtonSizes.Large)]
    void CreateResultPanelUI()
    {
        if(titleText != null || replayButton != null)
        {
            Debug.LogWarning("[PhotoStudioResultPanel] UI 已存在，若要重建请先清空已绑定的引用。");
            return;
        }

        RectTransform root = transform as RectTransform;
        if(root == null)
            root = Undo.AddComponent<RectTransform>(gameObject);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(900f, 360f);
        root.anchoredPosition = Vector2.zero;

        // 圆角窗口背景
        Image windowBg = UICreateTool.CreateImage("WindowBg", root, new Color(0.72f, 0.74f, 0.78f, 0.95f));
        UIAnchorTool.Stretch(windowBg.rectTransform);

        // 标题：本局结算（顶部居中）
        titleText = UICreateTool.CreateText("Title", root, "本局结算", 34f);
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(300f, 48f);
        titleRt.anchoredPosition = new Vector2(0f, -16f);
        LocTool.AttachText(titleText, LocTableSet.PhotoStudio, "SettleTitle", "本局结算");

        // 左侧照片预览
        Image photo = UICreateTool.CreateImage("PhotoReview", root, Color.white);
        UIAnchorTool.TopLeft(photo.rectTransform, new Vector2(40f, -80f), new Vector2(180f, 230f));
        photoReviewImage = photo;

        // 点评气泡（照片右上）
        Image bubble = UICreateTool.CreateImage("CommentBubble", root, new Color(1f, 1f, 1f, 0.95f));
        UIAnchorTool.TopLeft(bubble.rectTransform, new Vector2(190f, -84f), new Vector2(150f, 44f));
        TextMeshProUGUI commentText = UICreateTool.CreateText("CommentText", bubble.rectTransform, "干得不错~", 20f);
        UIAnchorTool.Stretch(commentText.rectTransform);
        commentText.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // 中部数值三行：分数 / 灵感 / 女主好感
        float rowY = -96f;
        const float rowStep = 52f;
        scoreValueText = CreateStatRow(root, rowY, "分数", "SettleScore", "分数", new Color(0.15f, 0.15f, 0.15f, 1f), "100");
        inspirationValueText = CreateStatRow(root, rowY - rowStep, "灵感", "SettleInspiration", "灵感", new Color(0.30f, 0.45f, 0.95f, 1f), "+100");
        affinityValueText = CreateStatRow(root, rowY - rowStep * 2f, "女主好感", "SettleAffinity", "女主好感", new Color(0.15f, 0.15f, 0.15f, 1f), "+100");

        // 入包道具：图标 + new + 数量 + 自动入包提示
        Image itemIcon = UICreateTool.CreateImage("ItemIcon", root, new Color(0.95f, 0.95f, 0.95f, 1f));
        UIAnchorTool.TopLeft(itemIcon.rectTransform, new Vector2(380f, -252f), new Vector2(70f, 70f));
        itemIconImage = itemIcon;

        TextMeshProUGUI newBadge = UICreateTool.CreateText("NewBadge", itemIcon.rectTransform, "new", 18f);
        RectTransform newRt = newBadge.rectTransform;
        newRt.anchorMin = newRt.anchorMax = new Vector2(1f, 1f);
        newRt.pivot = new Vector2(1f, 1f);
        newRt.sizeDelta = new Vector2(44f, 24f);
        newRt.anchoredPosition = new Vector2(8f, 8f);
        newBadge.color = new Color(0.95f, 0.55f, 0.1f, 1f);

        TextMeshProUGUI itemLabel = UICreateTool.CreateText("ItemLabel", root, "已放入背包", 20f);
        itemLabel.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(itemLabel.rectTransform, new Vector2(458f, -262f), new Vector2(150f, 32f));
        LocTool.AttachText(itemLabel, LocTableSet.PhotoStudio, "SettleIntoBag", "已放入背包");

        itemCountText = UICreateTool.CreateText("ItemCount", root, "x1", 22f);
        itemCountText.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(itemCountText.rectTransform, new Vector2(610f, -262f), new Vector2(60f, 32f));

        TextMeshProUGUI autoTip = UICreateTool.CreateText("AutoTip", root, "道具已自动发放进背包", 18f);
        autoTip.alignment = TextAlignmentOptions.Left;
        autoTip.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        UIAnchorTool.TopLeft(autoTip.rectTransform, new Vector2(380f, -304f), new Vector2(360f, 30f));
        LocTool.AttachText(autoTip, LocTableSet.PhotoStudio, "SettleAutoToBag", "道具已自动发放进背包");

        // 右侧：再来一局（含消耗提示）
        Button replayBtn = UICreateTool.CreateButton("ReplayButton", root, "再来一局", new Color(0.96f, 0.97f, 0.94f, 1f), out TextMeshProUGUI replayLabel);
        UIAnchorTool.TopLeft(replayBtn.GetComponent<RectTransform>(), new Vector2(700f, -90f), new Vector2(160f, 56f));
        LocTool.AttachText(replayLabel, LocTableSet.PhotoStudio, "SettleReplay", "再来一局");
        replayButton = replayBtn;

        TextMeshProUGUI costText = UICreateTool.CreateText("ReplayCost", root, "消耗行动值-1", 18f);
        costText.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        UIAnchorTool.TopLeft(costText.rectTransform, new Vector2(700f, -150f), new Vector2(160f, 28f));
        LocTool.AttachText(costText, LocTableSet.PhotoStudio, "SettleReplayCost", "消耗行动值-1");

        // 右侧：返回（黄色）
        Button returnBtn = UICreateTool.CreateButton("ReturnButton", root, "返回", new Color(0.96f, 0.86f, 0.42f, 1f), out TextMeshProUGUI returnLabel);
        UIAnchorTool.TopLeft(returnBtn.GetComponent<RectTransform>(), new Vector2(700f, -190f), new Vector2(160f, 56f));
        LocTool.AttachText(returnLabel, LocTableSet.PhotoStudio, "SettleReturn", "返回");
        returnButton = returnBtn;

        EditorUtility.SetDirty(this);
        if(!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[PhotoStudioResultPanel] 结算面板UI已创建并自动绑定字段。");
    }

    // 创建一行「标签 + 数值」，返回数值文本
    TextMeshProUGUI CreateStatRow(RectTransform parent, float y, string labelZh, string labelKey, string defaultZh, Color valueColor, string defaultValue)
    {
        TextMeshProUGUI label = UICreateTool.CreateText(labelKey + "_Label", parent, labelZh, 26f);
        label.alignment = TextAlignmentOptions.Left;
        UIAnchorTool.TopLeft(label.rectTransform, new Vector2(380f, y), new Vector2(180f, 40f));
        LocTool.AttachText(label, LocTableSet.PhotoStudio, labelKey, defaultZh);

        TextMeshProUGUI value = UICreateTool.CreateText(labelKey + "_Value", parent, defaultValue, 28f);
        value.alignment = TextAlignmentOptions.Left;
        value.color = valueColor;
        value.fontStyle = FontStyles.Bold;
        UIAnchorTool.TopLeft(value.rectTransform, new Vector2(540f, y), new Vector2(160f, 40f));
        return value;
    }
#endif
#endregion
}
