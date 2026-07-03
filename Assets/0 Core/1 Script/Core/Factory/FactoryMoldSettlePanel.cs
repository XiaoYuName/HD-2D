using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
using PrimeTween;

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
#endif

/// <summary>
/// 「物料制作」结算面板（<see cref="FactoryMoldMgPanel"/> 完成制作后弹出）：头像 + 台词 + 产出清单 + 返回。
/// 产出清单用横向 ScrollView 承载（<see cref="FactoryUIGen.HorizontalScrollList"/>），产品卡复用 ProductItemUIPrefab
/// （<see cref="FactorySelectCellUI"/>），数量随本次产出件数变化，多了可左右滑动。
/// 鼠标放上任意产品卡时，下方固定的 Hover 卡片区展示该物品的合成成品大图（AA Key 由调用方用
/// <see cref="FactoryMoldMgConfig"/>.GetSpriteKey(resultId) 算好，随 <see cref="Product.CardSpriteKey"/> 传入，
/// 本面板不直接依赖 FactoryMoldMgConfig，只负责呈现）。悬停事件由挂在每张卡上的 <see cref="FactoryMoldSettleItemHover"/> 转发。
/// </summary>
public class FactoryMoldSettlePanel : UIBase
{
    [Title("Ref")]
    [LabelText("左侧头像")][SerializeField] Image avatarImage;
    [LabelText("产品卡容器(横向 ScrollView 的 Content)")][SerializeField] RectTransform productContainer;
    [LabelText("产品卡预制(ProductItemUIPrefab)")][SerializeField] FactorySelectCellUI productItemPrefab;

    [Title("Hover 卡片 (鼠标放上产品卡时展示，固定位置)")]
    [LabelText("卡片Cg")][SerializeField] CanvasGroup hoverCardCg;
    [SerializeField] TweenSettings hoverCardTs;
    Tween hoverCardTween;
    [LabelText("卡片大图")][SerializeField] Image hoverCardImage;
    [LabelText("卡片名称")][SerializeField] LocalizeStringEvent hoverCardNameLse;

    [Title("Button")]
    [LabelText("返回")][SerializeField] Button backButton;

    readonly List<FactorySelectCellUI> productCells = new ();
    Data curData;

    /// <summary>结算展示数据：产品列表由调用方算好后传入，本面板只负责呈现。</summary>
    public class Data
    {
        /// <summary>本次制作产出的产品（图标 / 名称 Key / 数量 / 单价 / Hover 大图）。</summary>
        public IReadOnlyList<Product> Products;
        /// <summary>点击「返回」回调；为空时仅关闭本面板。</summary>
        public Action OnBack;
    }

    /// <summary>结算面板里一张产品卡的展示数据。</summary>
    public struct Product
    {
        /// <summary>列表小图标 Addressable Key。</summary>
        public string IconPath;
        /// <summary>名称多语言 Key（<see cref="LocalizeTableSet.InventoryItem"/> 表，物品名所在表）。</summary>
        public string NameKey;
        /// <summary>产出数量（显示为 x{Count}）。</summary>
        public int Count;
        /// <summary>单价（显示为 ¥{Price}/个）。</summary>
        public int UnitPrice;
        /// <summary>Hover 大图 Addressable Key（合成成品图；调用方用 FactoryMoldMgConfig.GetSpriteKey(resultId) 算好传入）。</summary>
        public string CardSpriteKey;
    }

    public override void Init()
    {
        backButton.onClick.AddListener(OnBackButton);
    }

    /// <summary>填充并刷新结算面板，需在 OpenUI 之后调用。</summary>
    public void Show(Data data)
    {
        curData = data;
        HideHoverCard();
        BuildProducts(data.Products);
    }

    // 清空旧卡，按产品列表逐个克隆 ProductItemUIPrefab 塞进 ScrollView 内容区，并挂上悬停转发脚本
    void BuildProducts(IReadOnlyList<Product> products)
    {
        for(int i = 0; i < productCells.Count; i++)
            Destroy(productCells[i].gameObject);
        productCells.Clear();

        foreach(Product p in products)
        {
            FactorySelectCellUI cell = Instantiate(productItemPrefab, productContainer);
            cell.gameObject.SetActive(true);
            cell.SetSelected(false);
            cell.SetIcon(p.IconPath);
            cell.SetName(LocalizeTableSet.InventoryItem, p.NameKey);
            cell.SetCount("x" + p.Count);
            cell.SetSub(GetPriceText(p.UnitPrice));

            FactoryMoldSettleItemHover hover = cell.GetComponent<FactoryMoldSettleItemHover>();
            hover.Setup(p.CardSpriteKey, p.NameKey, ShowHoverCard, HideHoverCard);

            productCells.Add(cell);
        }
    }

    // 展示 Hover 卡片：合成成品大图 + 名称（由产品卡上的 FactoryMoldSettleItemHover 在鼠标进入时回调）
    void ShowHoverCard(string cardSpriteKey, string nameKey)
    {
        hoverCardTween.Stop();
        hoverCardTween = Tween.Alpha(hoverCardCg, new TweenSettings<float>(0, 1, hoverCardTs));
        hoverCardImage.SetIcon(cardSpriteKey);
        hoverCardNameLse.SetText(LocalizeTableSet.InventoryItem, nameKey);
    }

    void HideHoverCard()
    {
        hoverCardTween.Stop();
        hoverCardTween = Tween.Alpha(hoverCardCg, new TweenSettings<float>(1, 0, hoverCardTs));
    }

    void OnBackButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }

    // 单价含 {Price} 占位符，单独构造 LocalizedString 灌值后取当前语言成品串（同 FactorySettlePanel）
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
    [InfoBox("在本面板根节点下生成：半透明遮罩 + 居中圆角窗口（头像 / 台词气泡 / 标题 / 产品卡横向 ScrollView / 道具提示 / 返回）+ 固定位置的 Hover 卡片区（默认隐藏），" +
             "并自动赋值各引用与 ProductItemUIPrefab。\n位置 / 尺寸为近似值，背景与头像 Sprite 请在生成后手动指定；重复点击会先清掉上次生成的窗口与遮罩。", InfoMessageType.Info)]
    void BuildSettleUI()
    {
        Color titleColor = new (0.26f, 0.26f, 0.26f);
        Color hintColor = new (0.6f, 0.6f, 0.6f);
        Color panelColor = new (0.78f, 0.78f, 0.78f, 0.95f);
        Color bubbleColor = new (1f, 1f, 1f, 0.95f);
        Color btnBg = new (0.93f, 0.74f, 0.5f);
        Color cardBg = new (0.98f, 0.97f, 0.95f, 1f);

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
        FactoryUIGen.Stretch(FactoryUIGen.Loc("SpeechText", bubble.transform, FactoryLocKeySet.MoldSettle.Speech, 26, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>());

        // 标题
        FactoryUIGen.Center(FactoryUIGen.Loc("TitleText", win, FactoryLocKeySet.MoldSettle.Title, 40, titleColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 380f, 60f, 70f, 165f);

        // 产品卡容器：横向 ScrollView，数量随产出件数变化，多了可左右滑动
        RectTransform scrollArea = FactoryUIGen.Node("ProductScrollArea", win);
        FactoryUIGen.Center(scrollArea, 700f, 260f, 260f, 20f);
        productContainer = FactoryUIGen.HorizontalScrollList("ProductScrollView", scrollArea);

        // 道具提示
        FactoryUIGen.Center(FactoryUIGen.Loc("ItemHintText", win, FactoryLocKeySet.Settle.ItemHint, 22, hintColor, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 360f, 30f, -430f, -185f);

        // 返回
        backButton = FactoryUIGen.Btn("BackButton", win, FactoryLocKeySet.Settle.Back, btnBg, Color.white);
        FactoryUIGen.Center((RectTransform)backButton.transform, 180f, 66f, 540f, -170f);

        // Hover 卡片区：固定位置（头像正下方），默认隐藏；鼠标放上任意产品卡时显示该物品合成成品大图 + 名称
        Image cardBgImg = FactoryUIGen.Img("HoverCard", win, cardBg);
        FactoryUIGen.Center(cardBgImg.rectTransform, 220f, 300f, -285f, -190f);

        hoverCardImage = FactoryUIGen.Img("CardImage", cardBgImg.transform, Color.white);
        FactoryUIGen.Center(hoverCardImage.rectTransform, 200f, 240f, 0f, 30f);
        hoverCardImage.preserveAspect = true;
        hoverCardNameLse = FactoryUIGen.Loc("CardName", cardBgImg.transform, FactoryLocKeySet.MoldSettle.Title, 24, titleColor, TextAlignmentOptions.Center);
        FactoryUIGen.Center((RectTransform)hoverCardNameLse.transform, 200f, 40f, 0f, -110f);

        // 产品卡预制引用
        productItemPrefab = AssetDatabase.LoadAssetAtPath<FactorySelectCellUI>(ProductItemPrefabPath);
        if(productItemPrefab == null)
            Debug.LogWarning($"[FactoryMoldSettlePanel] 未找到产品卡预制：{ProductItemPrefabPath}，请手动拖入 productItemPrefab。", this);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldSettlePanel] 结算界面已生成。请指定窗口 / 头像 Sprite 后保存为预制。", this);
    }
    #endregion
#endif
}
