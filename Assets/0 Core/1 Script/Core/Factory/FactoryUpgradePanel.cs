using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「升级设备」标签内容（挂在 <see cref="FactoryMainPanel"/> 的 upgradeContent 上）：列出可升级的流水线设备，
/// 每格显示 名称 / 等级 / 描述 / 升级费用，点击升级扣金币并提升等级（数据与持久化走 <see cref="FactoryEquipManager"/>）。
/// 由主面板在切到本标签时调用 <see cref="Refresh"/>。原「回收站」标签（FactoryRecyclePanel）已停用，脚本保留作备份。
/// </summary>
public class FactoryUpgradePanel : MonoBehaviour
{
    [Title("Ref")]
    [LabelText("格子容器(滚动内容)")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏，运行时克隆)")][SerializeField] FactoryUpgradeCellUI cellTemplate;
    [LabelText("空列表提示")][SerializeField] LocalizeStringEvent emptyText;
    [Title("Tip")]
    [LabelText("提示气泡(可复用主面板)")][SerializeField] WarnTip warnTip;

    readonly List<FactoryUpgradeCellUI> cells = new ();
    bool inited;

    void Awake() => EnsureInit();

    void EnsureInit()
    {
        if(inited)
            return;
        inited = true;
        cellTemplate.gameObject.SetActive(false);
    }

    /// <summary>重建设备列表；主面板每次切到升级设备标签时调用。</summary>
    public void Refresh()
    {
        EnsureInit();
        BuildCells();
    }

    void BuildCells()
    {
        for(int i = cells.Count - 1; i >= 0; i--)
            Destroy(cells[i].gameObject);
        cells.Clear();

        List<FactoryEquipData> list = new (FactoryEquipManager.Instance.Config.DataDict.Values);
        list.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach(FactoryEquipData d in list)
        {
            FactoryUpgradeCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Set(d, OnUpgrade);
            cells.Add(cell);
        }
        if(emptyText != null)
            emptyText.gameObject.SetActive(list.Count == 0);
    }

    // 升级按钮回调：判满级 / 判金币 / 执行升级并刷新本格
    void OnUpgrade(FactoryUpgradeCellUI cell)
    {
        FactoryEquipManager mgr = FactoryEquipManager.Instance;
        if(mgr == null || cell == null)
            return;

        int id = cell.Id;
        if(mgr.IsMax(id))
        {
            ShowTip(FactoryLocKeySet.Upgrade.Maxed);
            return;
        }

        int cost = mgr.GetNextCost(id);
        PlayerBag bag = PlayerInfo.St != null ? PlayerInfo.St.Bag : null;
        if(bag == null || !bag.HasMoney(cost))
        {
            ShowTip(FactoryLocKeySet.Upgrade.NotEnough);
            return;
        }

        if(mgr.TryUpgrade(id))
        {
            cell.Refresh();
            ShowTip(FactoryLocKeySet.Upgrade.Upgraded);
        }
    }

    void ShowTip(string key)
    {
        if(warnTip != null)
            warnTip.ShowTip(LocalizeTableSet.Factory, key);
    }

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    // 配色（近设计图）
    static readonly Color RowBg = new (0.92f, 0.88f, 0.80f, 1f);
    static readonly Color CardBg = new (0.97f, 0.95f, 0.91f, 1f);
    static readonly Color BadgeColor = new (0.86f, 0.55f, 0.22f, 1f);
    static readonly Color DescColor = new (0.32f, 0.29f, 0.25f, 1f);
    static readonly Color PillBg = new (0.98f, 0.81f, 0.35f, 1f);
    static readonly Color PillTextColor = new (0.40f, 0.30f, 0.10f, 1f);
    static readonly Color MaxColor = new (0.55f, 0.55f, 0.55f, 1f);
    static readonly Color EmptyColor = new (0.55f, 0.55f, 0.60f, 1f);

    [PropertySpace(8)]
    [Button("生成升级设备列表界面", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("在本对象（升级设备内容 upgradeContent）下生成一个上下滑动的设备列表(ScrollRect)，含一张隐藏的设备格子模板，" +
             "并自动赋值 gridContainer / cellTemplate / emptyText。\n" +
             "格子结构：左侧名称卡(LV徽标+名称图) + 中部效果描述(两行) + 右侧金币价格胶囊(=升级按钮)。重复点击会先清除上次生成的内容。", InfoMessageType.Info)]
    void BuildUI()
    {
        // 清除上次生成
        if(gridContainer != null)
        {
            ScrollRect old = gridContainer.GetComponentInParent<ScrollRect>();
            if(old != null)
                DestroyImmediate(old.gameObject);
            gridContainer = null;
            cellTemplate = null;
        }
        if(emptyText != null)
        {
            DestroyImmediate(emptyText.gameObject);
            emptyText = null;
        }

        // 列表内容容器：垂直排布 + 包成可滚动视图
        RectTransform content = FactoryUIGen.Node("UpgradeListContent", transform);
        FactoryUIGen.Stretch(content);
        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        FactoryUIGen.WrapInScrollView(content);
        gridContainer = content;

        // 设备格子模板
        cellTemplate = BuildCellTemplate(content);
        cellTemplate.gameObject.SetActive(false);

        // 空列表提示（居中，独立于列表）
        LocalizeStringEvent empty = FactoryUIGen.Loc("UpgradeEmptyText", transform,
            FactoryLocKeySet.Upgrade.Empty, 30, EmptyColor, TextAlignmentOptions.Center);
        FactoryUIGen.Center(empty.GetComponent<RectTransform>(), 460, 60, 0, 0);
        empty.gameObject.SetActive(false);
        emptyText = empty;

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryUpgradePanel] 升级设备列表界面已生成。请在主面板把本对象接到 upgradePanel 字段。", this);
    }

    FactoryUpgradeCellUI BuildCellTemplate(Transform parent)
    {
        RectTransform cellRt = FactoryUIGen.Node("UpgradeCellTemplate", parent);
        cellRt.sizeDelta = new Vector2(0f, 120f);
        LayoutElement le = cellRt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 120f;

        Image bg = cellRt.gameObject.AddComponent<Image>();
        bg.color = RowBg;
        FactoryUpgradeCellUI cell = cellRt.gameObject.AddComponent<FactoryUpgradeCellUI>();

        // 左：名称卡（LV 徽标 + 名称文本）
        Image card = FactoryUIGen.Img("NameCard", cellRt, CardBg);
        FactoryUIGen.Anchor(card.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 150f, 96f, 12f, 0f);

        TMP_Text lv = FactoryUIGen.Text("LvBadge", card.transform, "LV1", 26, BadgeColor, TextAlignmentOptions.TopLeft);
        FactoryUIGen.Anchor(lv.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), 90f, 30f, 8f, -6f);

        TMP_Text nameText = FactoryUIGen.Text("NameText", card.transform, "设备名称", 24, DescColor, TextAlignmentOptions.Center);
        FactoryUIGen.Center(nameText.rectTransform, 134f, 56f, 0f, -10f);
        nameText.textWrappingMode = TextWrappingModes.Normal;

        // 中：设备描述（多语言，自动换行）
        TMP_Text desc = FactoryUIGen.Text("Desc", cellRt, string.Empty, 24, DescColor, TextAlignmentOptions.Left);
        RectTransform descRt = desc.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0.5f, 0.5f);
        descRt.offsetMin = new Vector2(180f, 12f);
        descRt.offsetMax = new Vector2(-160f, -12f);
        desc.textWrappingMode = TextWrappingModes.Normal;

        // 右：金币价格胶囊（= 升级按钮）
        Image pill = FactoryUIGen.Img("CostPill", cellRt, PillBg);
        FactoryUIGen.Anchor(pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), 120f, 56f, -16f, 0f);
        Button btn = pill.gameObject.AddComponent<Button>();
        btn.targetGraphic = pill;
        TMP_Text cost = FactoryUIGen.Text("CostText", pill.transform, "¥0", 28, PillTextColor, TextAlignmentOptions.Center);
        FactoryUIGen.Stretch(cost.rectTransform);

        // 满级标识：盖住价格胶囊位置，默认隐藏
        TMP_Text max = FactoryUIGen.Text("MaxFlag", cellRt, "MAX", 28, MaxColor, TextAlignmentOptions.Center);
        FactoryUIGen.Anchor(max.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), 120f, 56f, -16f, 0f);
        max.gameObject.SetActive(false);

        cell.EditorBind(nameText, lv, desc, cost, max.gameObject, btn);
        return cell;
    }
    #endregion
#endif
}
