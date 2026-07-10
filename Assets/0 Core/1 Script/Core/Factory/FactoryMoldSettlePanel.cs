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
/// 产出清单用横向 ScrollView 承载（<see cref="FactoryUIGen.HorizontalScrollList"/>），产品卡复用
/// <see cref="FactoryMoldMgLeftUpItemUI"/> 预制，数量随本次产出件数变化，多了可左右滑动。
/// 产出物是运行时自描述合成物 <see cref="FactoryMoldItemInfo"/>（图标 / 名称 / 售价 / 数量全部随实例携带），
/// 本面板只负责呈现，不依赖 PaintingConfig / ItemConfig。鼠标放上任意产品卡时，下方固定的 Hover 卡片区
/// 展示该物品的合成成品大图（<see cref="FactoryMoldItemIcon"/> 按框架+贴纸实时三层合成）+ 名称。
/// 悬停事件由挂在每张卡上的 <see cref="FactoryMoldSettleItemHover"/> 转发。
/// </summary>
public class FactoryMoldSettlePanel : UIBase
{
    [Title("Ref")]
    [LabelText("左侧头像")][SerializeField] Image avatarImage;
    [LabelText("容器(横向 ScrollView 的 Content)")][SerializeField] RectTransform productContainer;
    [SerializeField] FactoryMoldMgLeftUpItemUI factoryMoldMgLeftUpItemUI;
    [SerializeField] FactoryMoldItemIcon itemIcon;
    [SerializeField] TextMeshProUGUI itemNameText;

    [Title("Hover 卡片 (鼠标放上产品卡时展示，固定位置)")]
    [LabelText("卡片Cg")][SerializeField] CanvasGroup hoverCardCg;
    [SerializeField] TweenSettings hoverCardTs;
    Tween hoverCardTween;
  

    [Title("Button")]
    [LabelText("返回")][SerializeField] Button backButton;

    readonly List<FactoryMoldMgLeftUpItemUI> productCells = new ();
    Data curData;

    /// <summary>结算展示数据：产品列表由调用方（<see cref="FactoryMoldMgPanel"/>）算好后传入，本面板只负责呈现。</summary>
    public class Data
    {
        /// <summary>本次制作产出的产品（每件已按「框架+贴纸」组合合并计数，Count 即本次该组合产出数）。</summary>
        public IReadOnlyList<FactoryMoldItemInfo> Products;
        /// <summary>点击「返回」回调；为空时仅关闭本面板。</summary>
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
        HideHoverCardForce();
        BuildItemList(data.Products);
    }

    void BuildItemList(IReadOnlyList<FactoryMoldItemInfo> products)
    {
        for(int i = 0; i < productCells.Count; i++)
            Destroy(productCells[i].gameObject);
        productCells.Clear();

        if(products == null)
            return;

        foreach(FactoryMoldItemInfo info in products)
        {
            FactoryMoldMgLeftUpItemUI cell = Instantiate(factoryMoldMgLeftUpItemUI, productContainer);
            cell.gameObject.SetActive(true);
            cell.Set(info);

            // 复用产品卡预制自带的悬停转发；预制未挂时兜底补一个，保证 Hover 大图始终可用
            if(!cell.TryGetComponent(out FactoryMoldSettleItemHover hover))
                hover = cell.gameObject.AddComponent<FactoryMoldSettleItemHover>();
            hover.Setup(info, ShowHoverCard, HideHoverCard);

            productCells.Add(cell);
        }
    }

    // 展示 Hover 卡片：合成成品大图 + 名称（由产品卡上的 FactoryMoldSettleItemHover 在鼠标进入时回调）
    void ShowHoverCard(ItemInfo itemInfo)
    {
        if(itemInfo is not FactoryMoldItemInfo moldInfo)
            return;

        hoverCardTween.Stop();
        hoverCardTween = Tween.Alpha(hoverCardCg, new TweenSettings<float>(hoverCardCg.alpha, 1f, hoverCardTs));

        itemIcon.Set(moldInfo);
        itemNameText.text = moldInfo.GetName();
    }

    void HideHoverCard()
    {
        hoverCardTween.Stop();
        hoverCardTween = Tween.Alpha(hoverCardCg, new TweenSettings<float>(hoverCardCg.alpha, 0f, hoverCardTs));
    }
    void HideHoverCardForce()
    {
        hoverCardTween.Stop();
        hoverCardCg.alpha = 0;
    }

    void OnBackButton()
    {
        curData?.OnBack?.Invoke();
        Close();
    }

#if UNITY_EDITOR
    #region 一键生成（仅编辑器）
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

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldSettlePanel] 结算界面已生成。请指定窗口 / 头像 Sprite 后保存为预制。", this);
    }
    #endregion
#endif
}
