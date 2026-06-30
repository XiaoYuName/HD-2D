using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// </summary>
public class FactorySettlePanel : UIBase
{
    [Title("Ref")]
    [LabelText("左侧头像")][SerializeField] Image avatarImage;
    [LabelText("分数数值")][SerializeField] TMP_Text scoreValueText;
    [LabelText("制作成功数值(XN)")][SerializeField] TMP_Text successValueText;
    [LabelText("完成率数值(N%)")][SerializeField] TMP_Text completionValueText;
    [LabelText("售价倍率数值(XN.N)")][SerializeField] TMP_Text saleMultiplierValueText;
    [LabelText("产品卡容器")][SerializeField] RectTransform productContainer;
    [LabelText("产品卡预制(ProductItemUIPrefab)")][SerializeField] FactorySelectCellUI productItemPrefab;
    [Title("Button")]
    [LabelText("返回")][SerializeField] Button backButton;

    [LabelText("产品卡水平间距")][SerializeField] float productSpacing = 190f;
    
    [SerializeField] List<FactorySelectCellUI> productCells;
    Data curData;

    /// <summary>结算展示数据：数值与产品列表均由调用方算好后传入，本面板只负责呈现。</summary>
    public class Data
    {
        /// <summary>左侧头像；为 null 时保留面板上现有头像。</summary>
        public Sprite Avatar;
        /// <summary>分数。</summary>
        public int Score;
        /// <summary>制作成功数（显示为 X{Count}）。</summary>
        public int SuccessCount;
        /// <summary>完成率（0~1，显示为百分比整数）。</summary>
        public float Completion;
        /// <summary>售价倍率（显示为 X{Value:0.0}）。当前为占位值，待策划数值确定。</summary>
        public float SaleMultiplier = 1f;
        /// <summary>本局产出的产品（图标 / 名称 Key / 数量 / 单价）。</summary>
        public IReadOnlyList<Product> Products;
        /// <summary>点击「返回」回调；为空时仅关闭本面板。</summary>
        public Action OnBack;
    }

    /// <summary>结算面板里一张产品卡的展示数据。</summary>
    public struct Product
    {
        /// <summary>图标 Addressable Key。</summary>
        public string IconPath;
        /// <summary>名称多语言 Key（<see cref="LocalizeTableSet.InventoryItem"/> 表，物品名所在表）。</summary>
        public string NameKey;
        /// <summary>产出数量（显示为 x{Count}）。</summary>
        public int Count;
        /// <summary>单价（显示为 ¥{Price}/个）。</summary>
        public int UnitPrice;
    }

    public override void Init()
    {
        backButton.onClick.AddListener(OnBackButton);
    }

    /// <summary>填充并刷新结算面板，需在 OpenUI 之后调用。</summary>
    public void Show(Data data)
    {
        curData = data;

        if(data.Avatar != null)
            avatarImage.sprite = data.Avatar;

        scoreValueText.text = data.Score.ToString();
        successValueText.text = "X" + data.SuccessCount;
        completionValueText.text = Mathf.RoundToInt(Mathf.Clamp01(data.Completion) * 100f) + "%";
        saleMultiplierValueText.text = "X" + data.SaleMultiplier.ToString("0.0");

        BuildProducts(data.Products);
    }

    // 清空旧卡，按产品列表逐个克隆 ProductItemUIPrefab 并水平居中排布
    void BuildProducts(IReadOnlyList<Product> products)
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
            Product p = products[i];
            FactorySelectCellUI cell = Instantiate(productItemPrefab, productContainer);
            cell.gameObject.SetActive(true);
            cell.SetSelected(false);
            cell.SetIcon(p.IconPath);
            cell.SetName(LocalizeTableSet.InventoryItem, p.NameKey);
            cell.SetCount("x" + p.Count);
            cell.SetSub(GetPriceText(p.UnitPrice));

            RectTransform rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * productSpacing, 0f);
            productCells.Add(cell);
        }
    }

    void OnBackButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }

    // 单价含 {Price} 占位符，单独构造 LocalizedString 灌值后取当前语言成品串（同 FactoryProductSelectPanel）
    static string GetPriceText(int price)
    {
        LocalizedString ls = new () { TableReference = LocalizeTableSet.Factory, TableEntryReference = FactoryLocKeySet.UnitPriceFmt };
        ls.SetVar(LocalizeVarSet.FactoryMain.Price, price, false);
        return ls.GetLocalizedString();
    }

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
    const string ProductItemPrefabPath = "Assets/AddressableAssets/Remote/Prefabs/UGUI/FactoryUI/ProductItemUIPrefab.prefab";

    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("在本面板根节点下生成：半透明遮罩 + 居中圆角窗口（头像 / 台词气泡 / 标题 / 分数·制作成功·完成率·售价倍率 / 产品卡容器 / 道具提示 / 返回），并自动赋值各引用与 ProductItemUIPrefab。\n" +
             "位置 / 尺寸为近似值，背景与头像 Sprite 请在生成后手动指定；重复点击会先清掉上次生成的窗口与遮罩。", InfoMessageType.Info)]
    void BuildSettleUI()
    {
        Color titleColor = new (0.26f, 0.26f, 0.26f);
        Color labelColor = new (0.36f, 0.36f, 0.36f);
        Color valueColor = new (0.2f, 0.2f, 0.2f);
        Color goldColor = new (0.93f, 0.74f, 0.18f);
        Color hintColor = new (0.6f, 0.6f, 0.6f);
        Color panelColor = new (0.78f, 0.78f, 0.78f, 0.95f);
        Color bubbleColor = new (1f, 1f, 1f, 0.95f);
        Color btnBg = new (0.93f, 0.74f, 0.5f);

        // 根：充满父级 + 透明 Image 拦截点击
        RectTransform rootRt = (RectTransform)transform;
        FactoryUIGen.Stretch(rootRt);
        if(GetComponent<Image>() == null)
            gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

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

        // 头像（左，超出窗口顶部）
        avatarImage = FactoryUIGen.Img("Avatar", win, Color.white);
        FactoryUIGen.Center(avatarImage.rectTransform, 360f, 470f, -470f, 30f);
        avatarImage.preserveAspect = true;

        // 台词气泡
        Image bubble = FactoryUIGen.Img("SpeechBubble", win, bubbleColor);
        FactoryUIGen.Center(bubble.rectTransform, 170f, 60f, -285f, 95f);
        FactoryUIGen.Stretch(FactoryUIGen.Loc("SpeechText", bubble.transform, FactoryLocKeySet.Settle.Speech, 26, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>());

        // 标题
        FactoryUIGen.Center(FactoryUIGen.Loc("TitleText", win, FactoryLocKeySet.Settle.Title, 40, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 380f, 60f, 70f, 165f);

        // 分数
        FactoryUIGen.Center(FactoryUIGen.Loc("ScoreLabel", win, FactoryLocKeySet.Settle.ScoreLabel, 30, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 150f, 40f, -150f, 100f);
        scoreValueText = FactoryUIGen.Text("ScoreValue", win, "100", 32, valueColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(scoreValueText.rectTransform, 180f, 40f, 30f, 100f);

        // 制作成功（星标 + 文案 + 数值）
        Image star = FactoryUIGen.Img("SuccessStar", win, goldColor);
        FactoryUIGen.Center(star.rectTransform, 40f, 40f, -210f, 48f);
        FactoryUIGen.Center(FactoryUIGen.Loc("SuccessLabel", win, FactoryLocKeySet.Settle.SuccessLabel, 28, labelColor, TextAlignmentOptions.Left).GetComponent<RectTransform>(), 160f, 40f, -100f, 48f);
        successValueText = FactoryUIGen.Text("SuccessValue", win, "X1", 30, valueColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(successValueText.rectTransform, 90f, 40f, 80f, 48f);

        // 完成率
        FactoryUIGen.Center(FactoryUIGen.Loc("CompletionLabel", win, FactoryLocKeySet.Settle.CompletionLabel, 28, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 130f, 40f, -170f, 0f);
        completionValueText = FactoryUIGen.Text("CompletionValue", win, "100%", 30, goldColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(completionValueText.rectTransform, 100f, 40f, -45f, 0f);

        // 售价倍率
        FactoryUIGen.Center(FactoryUIGen.Loc("SaleMultiplierLabel", win, FactoryLocKeySet.Settle.SaleMultiplierLabel, 28, labelColor, TextAlignmentOptions.Right).GetComponent<RectTransform>(), 170f, 40f, 150f, 0f);
        saleMultiplierValueText = FactoryUIGen.Text("SaleMultiplierValue", win, "X2.0", 34, goldColor, TextAlignmentOptions.Left);
        FactoryUIGen.Center(saleMultiplierValueText.rectTransform, 120f, 46f, 300f, 0f);

        // 产品卡容器（运行时克隆 ProductItemUIPrefab 居中排布）
        productContainer = FactoryUIGen.Node("ProductContainer", win);
        FactoryUIGen.Center(productContainer, 660f, 210f, 280f, -90f);

        // 道具提示
        FactoryUIGen.Center(FactoryUIGen.Loc("ItemHintText", win, FactoryLocKeySet.Settle.ItemHint, 22, hintColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 360f, 30f, -430f, -185f);

        // 返回
        backButton = FactoryUIGen.Btn("BackButton", win, FactoryLocKeySet.Settle.Back, btnBg, Color.white);
        FactoryUIGen.Center((RectTransform)backButton.transform, 180f, 66f, 540f, -135f);

        // 产品卡预制引用
        productItemPrefab = AssetDatabase.LoadAssetAtPath<FactorySelectCellUI>(ProductItemPrefabPath);
        if(productItemPrefab == null)
            Debug.LogWarning($"[FactorySettlePanel] 未找到产品卡预制：{ProductItemPrefabPath}，请手动拖入 productItemPrefab。", this);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactorySettlePanel] 结算界面已生成。请指定窗口 / 头像 Sprite 后保存为预制。", this);
    }
    #endregion
#endif
}
