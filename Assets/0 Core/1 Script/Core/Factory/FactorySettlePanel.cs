using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// </summary>
public class FactorySettlePanel : UIBase
{
    [Title("Ref")]
    [SerializeField] AvatarPortraitPop app;
    [LabelText("本次加工数量数值")][SerializeField] TMP_Text craftCountValueText;
    [LabelText("残次品率数值(N%)")][SerializeField] TMP_Text defectRateValueText;
    [LabelText("失败产品数值")][SerializeField] TMP_Text failCountValueText;
    [LabelText("完成生产数值")][SerializeField] TMP_Text doneCountValueText;
    [LabelText("产品卡容器")][SerializeField] RectTransform productContainer;
    [LabelText("产品卡预制(FactoryComposedItemCellUI)")][SerializeField] FactoryComposedItemCellUI itemPrefab;
    [Title("Button")]
    [LabelText("点击任意位置关闭(覆盖全屏)")][SerializeField] Button backButton;

    [LabelText("产品卡水平间距")][SerializeField] float productSpacing = 190f;

    [SerializeField] List<FactoryComposedItemCellUI> productCells;
    Data curData;

    /// <summary>结算展示数据：数值与产品列表均由调用方算好后传入，本面板只负责呈现。</summary>
    public class Data
    {
        /// <summary>本次加工总数（制作成功 + 失败之和）。</summary>
        public int CraftCount;
        /// <summary>残次品率（0~1，显示为百分比整数）。</summary>
        public float DefectRate;
        /// <summary>失败产品数。</summary>
        public int FailCount;
        /// <summary>完成生产数（制作成功、已产出周边商品的件数）。</summary>
        public int DoneCount;
        /// <summary>本局产出的周边商品（运行时自描述物品，图标/名称/数量/单价全部随实例携带）。</summary>
        public IReadOnlyList<FactoryMerchandiseItemInfo> Products;
        /// <summary>点击屏幕任意位置回调；为空时仅关闭本面板。</summary>
        public Action OnBack;
    }

    public override void Init()
    {
        backButton.onClick.AddListener(OnBackButton);
    }

    /// <summary>填充并刷新结算面板，需在 OpenUI 之后调用。</summary>
    public void Show(Data data)
    {
        curData = data;
        craftCountValueText.text = data.CraftCount.ToString();
        defectRateValueText.text = Mathf.RoundToInt(Mathf.Clamp01(data.DefectRate) * 100f) + "%";
        failCountValueText.text = data.FailCount.ToString();
        doneCountValueText.text = data.DoneCount.ToString();
        BuildProducts(data.Products);
    }

    // 清空旧卡，按产品列表逐个克隆 FactoryComposedItemCellUI 并水平居中排布
    void BuildProducts(IReadOnlyList<FactoryMerchandiseItemInfo> products)
    {
        for(int i = 0; i < productCells.Count; i++)
            Destroy(productCells[i].gameObject);
        productCells.Clear();

        if(products == null || products.Count == 0)
            return;

        int n = products.Count;
        float startX = -(n - 1) * productSpacing * 0.5f;
        for(int i = 0; i < n; i++)
        {
            FactoryComposedItemCellUI cell = Instantiate(itemPrefab, productContainer);
            cell.gameObject.SetActive(true);
            cell.Set(products[i]);

            RectTransform rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * productSpacing, 0f);
            productCells.Add(cell);
        }
    }

    // 点击屏幕任意位置关闭（backButton 挂在覆盖全屏的根节点 Image 上，非独立按钮）
    void OnBackButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("在本面板根节点下生成：半透明遮罩 + 居中圆角窗口（头像 / 台词气泡 / 标题 / 本次加工数量·残次品率·失败产品·完成生产 / 产品卡容器 / 道具提示 / 点击关闭提示），" +
             "并自动赋值各引用与 FactoryComposedItemCellUI 产品卡容器；整个面板根节点即为「点击任意位置关闭」按钮，不再有独立返回按钮。\n" +
             "位置 / 尺寸为近似值，背景与头像 Sprite 请在生成后手动指定；重复点击会先清掉上次生成的窗口与遮罩。", InfoMessageType.Info)]
    void BuildSettleUI()
    {
        Color titleColor = new (0.26f, 0.26f, 0.26f);
        Color labelColor = new (0.36f, 0.36f, 0.36f);
        Color valueColor = new (0.2f, 0.2f, 0.2f);
        Color goldColor = new (0.93f, 0.74f, 0.18f);
        Color hintColor = new (0.6f, 0.6f, 0.6f);
        Color panelColor = new (0.78f, 0.78f, 0.78f, 0.95f);

        // 根：充满父级 + 透明 Image 拦截点击 + Button（点击面板任意未被子物体遮挡的位置即关闭）
        RectTransform rootRt = (RectTransform)transform;
        FactoryUIGen.Stretch(rootRt);
        Image rootImg = GetComponent<Image>();
        if(rootImg == null)
            rootImg = gameObject.AddComponent<Image>();
        rootImg.color = new Color(1f, 1f, 1f, 0f);
        Button rootBtn = GetComponent<Button>();
        if(rootBtn == null)
            rootBtn = gameObject.AddComponent<Button>();
        rootBtn.transition = Selectable.Transition.None;
        rootBtn.targetGraphic = rootImg;
        backButton = rootBtn;

        // 清除上次生成
        foreach(string n in new[] { "Mask", "Window" })
        {
            Transform old = transform.Find(n);
            if(old != null)
                DestroyImmediate(old.gameObject);
        }

        // 遮罩
        Image mask = FactoryUIGen.Img("Mask", transform, new Color(0f, 0f, 0f, 0.45f));
        FactoryUIGen.Stretch(mask.rectTransform);

        // 窗口（缩放动画根）
        Image window = FactoryUIGen.Img("Window", transform, panelColor);
        FactoryUIGen.Center(window.rectTransform, 1300f, 440f, 0f, 0f);
        Transform win = window.transform;
        TweenerRoot = window.rectTransform;

        // 标题
        FactoryUIGen.Center(FactoryUIGen.Loc("TitleText", win, FactoryLocKeySet.Settle.Title, 40, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 380f, 60f, 70f, 165f);

        // 本次加工数量
        FactoryUIGen.Center(FactoryUIGen.Loc("CraftCountLabel", win, FactoryLocKeySet.Settle.CraftCountLabel, 28, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 220f, 40f, -300f, 100f);
        craftCountValueText = FactoryUIGen.Text("CraftCountValue", win, "40", 30, valueColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(craftCountValueText.rectTransform, 60f, 40f, -170f, 100f);
        FactoryUIGen.Center(FactoryUIGen.Loc("CraftCountUnit", win, FactoryLocKeySet.Settle.UnitPiece, 26, labelColor, TextAlignmentOptions.Left).GetComponent<RectTransform>(), 50f, 40f, -125f, 100f);

        // 残次品率
        FactoryUIGen.Center(FactoryUIGen.Loc("DefectRateLabel", win, FactoryLocKeySet.Settle.DefectRateLabel, 28, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 150f, 40f, 40f, 100f);
        defectRateValueText = FactoryUIGen.Text("DefectRateValue", win, "10%", 30, valueColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(defectRateValueText.rectTransform, 90f, 40f, 145f, 100f);

        // 失败产品
        FactoryUIGen.Center(FactoryUIGen.Loc("FailProductLabel", win, FactoryLocKeySet.Settle.FailProductLabel, 28, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 220f, 40f, -300f, 48f);
        failCountValueText = FactoryUIGen.Text("FailProductValue", win, "0", 30, valueColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(failCountValueText.rectTransform, 60f, 40f, -170f, 48f);
        FactoryUIGen.Center(FactoryUIGen.Loc("FailProductUnit", win, FactoryLocKeySet.Settle.UnitPiece, 26, labelColor, TextAlignmentOptions.Left).GetComponent<RectTransform>(), 50f, 40f, -125f, 48f);

        // 完成生产（加粗高亮，整行用金色）
        TMP_Text doneLabel = FactoryUIGen.Loc("DoneLabel", win, FactoryLocKeySet.Settle.DoneLabel, 30, goldColor, TextAlignmentOptions.Right).GetComponent<TMP_Text>();
        doneLabel.fontStyle = FontStyles.Bold;
        FactoryUIGen.Center(doneLabel.rectTransform, 220f, 46f, -300f, -8f);
        doneCountValueText = FactoryUIGen.Text("DoneValue", win, "0", 32, goldColor, TextAlignmentOptions.Left);
        doneCountValueText.fontStyle = FontStyles.Bold;
        FactoryUIGen.Center(doneCountValueText.rectTransform, 60f, 46f, -170f, -8f);
        TMP_Text doneUnit = FactoryUIGen.Loc("DoneUnit", win, FactoryLocKeySet.Settle.UnitPiece, 26, goldColor, TextAlignmentOptions.Left).GetComponent<TMP_Text>();
        doneUnit.fontStyle = FontStyles.Bold;
        FactoryUIGen.Center(doneUnit.rectTransform, 50f, 46f, -125f, -8f);

        // 产品卡容器（运行时克隆 FactoryComposedItemCellUI 居中排布）
        productContainer = FactoryUIGen.Node("ProductContainer", win);
        FactoryUIGen.Center(productContainer, 400f, 210f, 400f, 30f);

        // 道具提示 + 点击关闭提示（两行，紧贴窗口底部）
        FactoryUIGen.Center(FactoryUIGen.Loc("ItemHintText", win, FactoryLocKeySet.Settle.ItemHint, 22, hintColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 500f, 30f, -280f, -170f);
        FactoryUIGen.Center(FactoryUIGen.Loc("ClickToCloseText", transform, FactoryLocKeySet.Settle.ClickToClose, 22, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center).GetComponent<RectTransform>(), 600f, 30f, 0f, -260f);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactorySettlePanel] 结算界面已生成。请指定窗口 / 头像 Sprite 后保存为预制。", this);
    }
    #endregion
#endif
}
