using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using XFramework;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「物料制作」面板：选一个<b>框架</b>(<see cref="ItemType.FigureModel"/>) 作画布底图，再从<b>贴纸</b>(<see cref="ItemType.Painting"/>) 列表点选，
/// 每点一次就往画布添加一枚可拖拽/缩放/镜像/图层/删除的贴纸(<see cref="FactoryMoldStickerView"/>)。点「完成制作」合成一件
/// <see cref="FactoryProductionMtItemInfo"/>（运行时物品）发放进背包。
///
/// 顶部 ①②③ 为 <b>3 个制作顺序模板</b>，各自独立保存「框架 + 画布上所有贴纸的位置/缩放/镜像/图层」，可随时切换。
/// 规则（按策划）：制作时<b>框架不消耗</b>（可复用模具），<b>消耗画布上用到的贴纸</b>（按枚数）。
/// 画布精灵与框架/贴纸售价走 <see cref="FactoryMoldMgConfig"/>（非物品 128×128 图标）。由 <see cref="FactoryMainPanel.OnMoldMgButton"/> 打开。
/// 备注：多贴纸构图当前仅以「框架+首枚贴纸」记录进产物，完整构图（多贴纸+变换）的产物记录为后续扩展。
/// </summary>
public class FactoryMoldMgPanel : UIBase
{
    const ItemType FrameType = ItemType.FigureModel;
    const ItemType StickerType = ItemType.Painting;
    const int TemplateCount = 3;

    enum Tab { Frame, Sticker }

    [Title("配置")]
    [LabelText("物料制作配置(精灵/售价)")][SerializeField] FactoryMoldMgConfig moldConfig;
    [LabelText("画布最多贴纸数(暂为1，后续可拓展)"), MinValue(1)][SerializeField] int maxStickers = 1;

    [Title("制作顺序模板 (①②③)")]
    [LabelText("模板Tab按钮(3个)")][SerializeField] List<Button> templateTabs = new ();
    [LabelText("模板选中色")][SerializeField] Color templateActiveColor = new (0.93f, 0.85f, 0.66f, 1f);
    [LabelText("模板未选色")][SerializeField] Color templateNormalColor = new (0.78f, 0.78f, 0.78f, 1f);

    [Title("分类 Tab (框架 / 贴纸)")]
    [LabelText("框架Tab按钮")][SerializeField] Button frameTabButton;
    [LabelText("贴纸Tab按钮")][SerializeField] Button stickerTabButton;
    [LabelText("Tab选中色")][SerializeField] Color tabActiveColor = new (1f, 0.78f, 0.42f, 1f);
    [LabelText("Tab未选色")][SerializeField] Color tabNormalColor = new (0.86f, 0.86f, 0.88f, 1f);

    [Title("列表")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏)")][SerializeField] FactoryMoldItemCellUI cellTemplate;

    [Title("制作画布")]
    [LabelText("框架底图")][SerializeField] Image frameImage;
    [LabelText("贴纸层容器")][SerializeField] RectTransform stickerLayer;
    [LabelText("贴纸实例模板(隐藏)")][SerializeField] FactoryMoldStickerView stickerViewTemplate;

    [Title("贴纸功能框")]
    [LabelText("功能框根物体")][SerializeField] GameObject stickerPopup;
    [LabelText("镜像翻转")][SerializeField] Button popupMirrorButton;
    [LabelText("图层往上")][SerializeField] Button popupLayerUpButton;
    [LabelText("图层往下")][SerializeField] Button popupLayerDownButton;
    [LabelText("删除")][SerializeField] Button popupDeleteButton;

    [Title("框架(物料)工具")]
    [LabelText("图片调整")][SerializeField] Button frameAdjustButton;
    [LabelText("物料镜像")][SerializeField] Button frameMirrorButton;
    [LabelText("物料转向")][SerializeField] Button frameTurnButton;

    [Title("价格")]
    [LabelText("框架加价文本(框架 +{Price})")][SerializeField] LocalizeStringEvent frameAddText;
    [LabelText("贴纸加价文本(贴纸 +{Price})")][SerializeField] LocalizeStringEvent stickerAddText;
    [LabelText("预估售出价格文本")][SerializeField] LocalizeStringEvent sellPriceText;   // = 框架售价 + 画布上各贴纸售价之和
    [LabelText("预估制作成本文本")][SerializeField] LocalizeStringEvent craftPriceText;   // 工厂批量制作成本，暂占位
    [LabelText("预估制作成本(占位)")][SerializeField] int craftCost = 50;

    [Title("Button / 提示")]
    [LabelText("完成制作")][SerializeField] Button completeButton;
    [LabelText("退出")][SerializeField] Button closeButton;
    [LabelText("提示")][SerializeField] WarnTip warnTip;

    readonly List<FactoryMoldItemCellUI> cells = new ();
    readonly List<ItemInfo> source = new ();

    // 每模板：框架(不消耗)、画布上所有贴纸的摆放(位置/缩放/镜像/图层)
    readonly ItemInfo[] tplFrame = new ItemInfo[TemplateCount];
    readonly List<StickerPlacement>[] tplPlacements = new List<StickerPlacement>[TemplateCount];

    readonly List<FactoryMoldStickerView> stickerViews = new ();   // 当前模板的活动贴纸实例
    FactoryMoldStickerView curStickerView;                          // 当前选中的贴纸（功能框作用对象）

    int curTpl;
    Tab curTab = Tab.Frame;

    ItemInfo SelFrame { get => tplFrame[curTpl]; set => tplFrame[curTpl] = value; }
    List<StickerPlacement> CurPlacements => tplPlacements[curTpl];

    #region 生命周期
    public override void Init()
    {
        EnsurePlacements();

        frameTabButton.onClick.AddListener(() => SwitchTab(Tab.Frame));
        stickerTabButton.onClick.AddListener(() => SwitchTab(Tab.Sticker));
        for(int i = 0; i < templateTabs.Count; i++)
        {
            int idx = i;
            templateTabs[i].onClick.AddListener(() => SwitchTemplate(idx));
        }

        popupMirrorButton.onClick.AddListener(OnPopupMirror);
        popupLayerUpButton.onClick.AddListener(OnPopupLayerUp);
        popupLayerDownButton.onClick.AddListener(OnPopupLayerDown);
        popupDeleteButton.onClick.AddListener(OnPopupDelete);

        frameAdjustButton.onClick.AddListener(OnFrameAdjust);
        frameMirrorButton.onClick.AddListener(OnFrameMirror);
        frameTurnButton.onClick.AddListener(OnFrameTurn);

        completeButton.onClick.AddListener(OnCompleteButton);
        closeButton.onClick.AddListener(OnCloseButton);

        cellTemplate.gameObject.SetActive(false);
        stickerViewTemplate.gameObject.SetActive(false);
        stickerPopup.SetActive(false);
    }

    public override void Open()
    {
        base.Open();
        EnsurePlacements();
        ValidateFrames();
        SetTemplateVisual();
        SwitchTab(Tab.Frame);
        RebuildCanvas();
        RefreshComposition();
    }

    void EnsurePlacements()
    {
        for(int i = 0; i < TemplateCount; i++)
            tplPlacements[i] ??= new List<StickerPlacement>();
    }

    // 校正各模板的框架选择：背包里已不存在的置空（框架本身不消耗，一般只在被它处移除时触发）
    void ValidateFrames()
    {
        List<ItemInfo> frames = PlayerInfo.St.Bag.GetItemList(FrameType);
        for(int i = 0; i < TemplateCount; i++)
            if(tplFrame[i] != null && !frames.Contains(tplFrame[i]))
                tplFrame[i] = null;
    }
    #endregion

    #region 模板切换
    void SwitchTemplate(int idx)
    {
        curTpl = Mathf.Clamp(idx, 0, TemplateCount - 1);
        SetTemplateVisual();
        RebuildList();
        RebuildCanvas();
        RefreshComposition();
    }

    void SetTemplateVisual()
    {
        for(int i = 0; i < templateTabs.Count; i++)
            templateTabs[i].targetGraphic.color = i == curTpl ? templateActiveColor : templateNormalColor;
    }
    #endregion

    #region 分类 Tab / 列表
    void SwitchTab(Tab tab)
    {
        curTab = tab;
        SetTabVisual();
        RebuildList();
    }

    void SetTabVisual()
    {
        frameTabButton.targetGraphic.color = curTab == Tab.Frame ? tabActiveColor : tabNormalColor;
        stickerTabButton.targetGraphic.color = curTab == Tab.Sticker ? tabActiveColor : tabNormalColor;
    }

    void RebuildList()
    {
        source.Clear();
        source.AddRange(PlayerInfo.St.Bag.GetItemList(curTab == Tab.Frame ? FrameType : StickerType));

        for(int i = 0; i < cells.Count; i++)
            Destroy(cells[i].gameObject);
        cells.Clear();

        // 框架页单选高亮当前框架；贴纸页是「点击即添加到画布」，列表不做选中态
        ItemInfo frameSel = curTab == Tab.Frame ? SelFrame : null;
        for(int i = 0; i < source.Count; i++)
        {
            ItemInfo info = source[i];
            FactoryMoldItemCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Set(i, info.IconPath, info.Name, info.Count, info == frameSel, OnCellClick);
            cells.Add(cell);
        }
    }

    void OnCellClick(int index)
    {
        if(index < 0 || index >= source.Count)
            return;

        if(curTab == Tab.Frame)
            SetFrame(source[index]);
        else
            AddSticker(source[index]);
    }
    #endregion

    #region 画布：框架 + 贴纸
    void SetFrame(ItemInfo frame)
    {
        SelFrame = frame;
        for(int i = 0; i < cells.Count; i++)
            cells[i].SetSelected(source[i] == frame);
        RefreshCanvasFrame();
        RefreshComposition();
    }

    void RefreshCanvasFrame()
    {
        bool has = SelFrame != null;
        frameImage.enabled = has;
        if(has)
            frameImage.SetIcon(GetSprite(SelFrame));
    }

    // 点击贴纸 = 往画布添加一枚新贴纸实例；达上限(maxStickers，暂为1)则提示不再添加。value 取自售价配置。
    void AddSticker(ItemInfo sticker)
    {
        if(CurPlacements.Count >= maxStickers)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.StickerLimit);
            return;
        }

        int price = moldConfig.GetStickerPrice(sticker.Id);
        StickerPlacement p = new (sticker.Id, GetSprite(sticker), Vector2.zero) { value = price };
        CurPlacements.Add(p);
        FactoryMoldStickerView view = SpawnStickerView(p);
        OnStickerSelected(view);
        RefreshComposition();
    }

    FactoryMoldStickerView SpawnStickerView(StickerPlacement p)
    {
        FactoryMoldStickerView view = Instantiate(stickerViewTemplate, stickerLayer);
        view.gameObject.SetActive(true);
        view.Setup(p, OnStickerSelected);
        stickerViews.Add(view);
        return view;
    }

    // 重建当前模板画布：框架底图 + 按 placements 还原所有贴纸
    void RebuildCanvas()
    {
        RefreshCanvasFrame();
        ClearStickerViews();
        foreach(StickerPlacement p in CurPlacements)
            SpawnStickerView(p);
        HidePopup();
    }

    void ClearStickerViews()
    {
        for(int i = 0; i < stickerViews.Count; i++)
            if(stickerViews[i] != null)
                Destroy(stickerViews[i].gameObject);
        stickerViews.Clear();
        curStickerView = null;
    }

    // 画布精灵：走配置精灵表；未配置时由 moldConfig 回退缺省图并 LogError（不再回退物品 128×128 图标）
    string GetSprite(ItemInfo item) => moldConfig.GetSpriteKey(item.Id);
    #endregion

    #region 贴纸功能框（镜像 / 图层 / 删除）
    void OnStickerSelected(FactoryMoldStickerView view)
    {
        for(int i = 0; i < stickerViews.Count; i++)
            stickerViews[i].SetSelected(stickerViews[i] == view);
        curStickerView = view;
        ShowPopup(view);
    }

    // 把功能框移到贴纸右上方（屏幕坐标换算，兼容画布缩放）
    void ShowPopup(FactoryMoldStickerView view)
    {
        stickerPopup.SetActive(true);
        RectTransform pr = (RectTransform)stickerPopup.transform;
        RectTransform parent = (RectTransform)pr.parent;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, view.transform.position);
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen + new Vector2(110f, 70f), null, out Vector2 local))
            pr.anchoredPosition = local;
    }

    void HidePopup()
    {
        stickerPopup.SetActive(false);
        if(curStickerView != null)
            curStickerView.SetSelected(false);
        curStickerView = null;
    }

    void OnPopupMirror()
    {
        if(curStickerView != null)
            curStickerView.Mirror();
    }

    void OnPopupLayerUp()
    {
        if(curStickerView == null)
            return;
        curStickerView.LayerUp();
        ResyncPlacementOrder();
    }

    void OnPopupLayerDown()
    {
        if(curStickerView == null)
            return;
        curStickerView.LayerDown();
        ResyncPlacementOrder();
    }

    void OnPopupDelete()
    {
        if(curStickerView == null)
            return;
        CurPlacements.Remove(curStickerView.Placement);
        stickerViews.Remove(curStickerView);
        Destroy(curStickerView.gameObject);
        curStickerView = null;
        HidePopup();
        RefreshComposition();
    }

    // 图层上下改了 SiblingIndex 后，把 placements / views 重排成与之一致，保证模板切换后图层顺序不丢
    void ResyncPlacementOrder()
    {
        stickerViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        CurPlacements.Clear();
        foreach(FactoryMoldStickerView v in stickerViews)
            CurPlacements.Add(v.Placement);
    }
    #endregion

    #region 框架(物料)工具
    // 图片调整：策划尚未细化（亮度/裁剪/缩放等），暂作「重置框架变换」占位，避免空按钮。
    void OnFrameAdjust()
    {
        frameImage.rectTransform.localScale = Vector3.one;
        frameImage.rectTransform.localRotation = Quaternion.identity;
    }

    void OnFrameMirror()
    {
        Vector3 s = frameImage.rectTransform.localScale;
        frameImage.rectTransform.localScale = new Vector3(-s.x, s.y, s.z);
    }

    void OnFrameTurn() => frameImage.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, 90f);
    #endregion

    #region 价格 / 完成态
    // 画布变化后统一刷新：完成按钮可用态 + 售价/制作价
    void RefreshComposition()
    {
        RefreshComplete();
        RefreshPrices();
    }

    void RefreshComplete()
    {
        completeButton.interactable = SelFrame != null && CurPlacements.Count > 0;
    }

    // 框架售价（取自 moldConfig 框架售价字典）
    int FramePrice() => SelFrame != null ? moldConfig.GetFramePrice(SelFrame.Id) : 0;

    // 画布上各贴纸售价之和（每枚 placement.value 已在添加时从贴纸售价字典取好）
    int StickerPriceSum()
    {
        int sum = 0;
        foreach(StickerPlacement p in CurPlacements)
            sum += p.value;
        return sum;
    }

    // 预估售出价 = 框架售价 + 贴纸售价之和
    int SellValue() => FramePrice() + StickerPriceSum();

    // 展示：框架 +{framePrice}、贴纸 +{stickerSum}、预估售出价 ¥{sell}、预估制作成本 ¥{craftCost}
    void RefreshPrices()
    {
        int framePrice = FramePrice();
        int stickerSum = StickerPriceSum();

        frameAddText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.FramePriceFmt,
            (LocalizeVarSet.FactoryMold.Price, framePrice));
        stickerAddText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.StickerPriceFmt,
            (LocalizeVarSet.FactoryMold.Price, stickerSum));
        sellPriceText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.SellPriceFmt,
            (LocalizeVarSet.FactoryMold.Price, framePrice + stickerSum));
        craftPriceText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.PriceFmt,
            (LocalizeVarSet.FactoryMold.Price, craftCost));
    }
    #endregion

    #region 完成制作
    // 完成制作：合成 1 件生产资料发放进背包；框架不消耗，按画布上贴纸枚数消耗对应贴纸。
    void OnCompleteButton()
    {
        if(SelFrame == null)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NeedFrame);
            return;
        }
        if(CurPlacements.Count == 0)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NeedSticker);
            return;
        }

        PlayerBag bag = PlayerInfo.St.Bag;

        // 统计各贴纸用量并校验背包是否足够
        Dictionary<long, int> need = new ();
        foreach(StickerPlacement p in CurPlacements)
            need[p.itemId] = (need.TryGetValue(p.itemId, out int n) ? n : 0) + 1;
        foreach(KeyValuePair<long, int> kv in need)
            if(bag.GetItemCount(kv.Key) < kv.Value)
            {
                warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NotEnoughSticker);
                return;
            }

        // 产出：以 框架 + 首枚贴纸 为代表（多贴纸完整构图记录为后续扩展）；售价 = 框架 + 全部贴纸价值之和（清空前算好）
        int sellValue = SellValue();
        ItemInfo firstSticker = bag.GetItem(CurPlacements[0].itemId);
        FactoryProductionMtItemInfo product = FactoryProductionMtItemInfo.Create(SelFrame, firstSticker, 1, sellValue);
        if(product == null)
            return;
        bag.AddRuntimeItem(product);

        // 消耗各贴纸（框架不消耗）
        foreach(KeyValuePair<long, int> kv in need)
        {
            int remain = kv.Value;
            while(remain > 0)
            {
                ItemInfo it = bag.GetItem(kv.Key);
                if(it == null)
                    break;
                int c = Mathf.Min(remain, it.Count);
                bag.ConsumeItem(it, c);
                remain -= c;
            }
        }

        warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.CraftSuccess);

        // 贴纸已消耗：清空当前画布（框架保留），刷新列表数量
        CurPlacements.Clear();
        RebuildCanvas();
        RebuildList();
        RefreshComposition();
    }

    void OnCloseButton() => Close();
    #endregion

#if UNITY_EDITOR
    #region 测试（仅编辑器）
    [PropertySpace(8)]
    [Button("【测试】发放框架 / 贴纸到背包", ButtonSizes.Large), GUIColor(1f, 0.85f, 0.5f)]
    void AddTestItems()
    {
        if(!Application.isPlaying || PlayerInfo.St == null || ItemManager.St == null || ItemManager.St.Config == null)
        {
            Debug.LogWarning("[FactoryMoldMgPanel] 测试发放需在运行时（且 ItemManager/PlayerInfo 已就绪）点击。", this);
            return;
        }

        PlayerBag bag = PlayerInfo.St.Bag;
        int frameKinds = 0, stickerKinds = 0;
        foreach(ItemData item in ItemManager.St.Config.ItemDataDict.Values)
        {
            if(item == null)
                continue;
            if(item.Type == FrameType)        { bag.AddItem(item.Id, 1); frameKinds++; }   // 框架不消耗，1 个够测
            else if(item.Type == StickerType) { bag.AddItem(item.Id, 5); stickerKinds++; } // 贴纸会被消耗，多给几个
        }

        Debug.Log($"[FactoryMoldMgPanel] 测试物品已发放：框架 {frameKinds} 种、贴纸 {stickerKinds} 种。" +
                  (frameKinds == 0 ? " 注意：ItemConfig 暂无 FigureModel(框架) 物品，框架列表会为空，需先把框架配成 FigureModel。" : ""), this);

        if(isOpen)
        {
            ValidateFrames();
            SwitchTab(curTab);   // 刷新当前列表显示
        }
    }
    #endregion

    #region 一键生成界面（仅编辑器）
    // 仅在「重建整套基础界面」时用，会清空所有子物体——已调好的界面别点！
    // 制作画布 / 贴纸功能框 / 物料工具 用下方各自的增量按钮，不会动其它已调好的部分。
    [PropertySpace(8)]
    [Button("重建基础界面(会清空全部)", ButtonSizes.Large), GUIColor(1f, 0.7f, 0.5f)]
    void BuildUI()
    {
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Image root = FactoryUIGen.Img("Window", transform, new Color(0.20f, 0.18f, 0.16f, 0.98f));
        FactoryUIGen.Stretch(root.rectTransform);

        // 退出（右上）
        closeButton = FactoryUIGen.Btn("ExitButton", root.transform, FactoryLocKeySet.Mold.Exit, new Color(0.95f, 0.95f, 0.97f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), 140, 64, -24, -24);

        // 顶部 ①②③ 模板 Tab
        templateTabs.Clear();
        string[] numerals = { "①", "②", "③" };
        for(int i = 0; i < TemplateCount; i++)
        {
            Image flag = FactoryUIGen.Img("TemplateTab" + (i + 1), root.transform, i == 0 ? templateActiveColor : templateNormalColor);
            FactoryUIGen.Anchor(flag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), 120, 56, 520, -24 - i * 66);
            Button btn = flag.gameObject.AddComponent<Button>();
            btn.targetGraphic = flag;
            TMP_Text num = FactoryUIGen.Text("Num", flag.transform, numerals[i], 32, new Color(0.3f, 0.25f, 0.2f), TextAlignmentOptions.Center);
            FactoryUIGen.Stretch(num.rectTransform);
            templateTabs.Add(btn);
        }

        // 左侧面板：定宽 470，竖向随父级拉伸
        Image left = FactoryUIGen.Img("LeftPanel", root.transform, new Color(0.78f, 0.62f, 0.5f, 1f));
        RectTransform lrt = left.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 0.5f);
        lrt.sizeDelta = new Vector2(470, -48);
        lrt.anchoredPosition = new Vector2(24, 0);

        FactoryUIGen.Loc("Title", left.transform, FactoryLocKeySet.Mold.Title, 34, Color.white, TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -40);

        frameTabButton = FactoryUIGen.Btn("FrameTab", left.transform, FactoryLocKeySet.Mold.TabFrame, tabActiveColor, new Color(0.3f, 0.25f, 0.2f));
        FactoryUIGen.Anchor((RectTransform)frameTabButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 190, 70, -100, -110);
        stickerTabButton = FactoryUIGen.Btn("StickerTab", left.transform, FactoryLocKeySet.Mold.TabSticker, tabNormalColor, new Color(0.3f, 0.25f, 0.2f));
        FactoryUIGen.Anchor((RectTransform)stickerTabButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 190, 70, 100, -110);

        RectTransform grid = FactoryUIGen.Node("Grid", left.transform);
        grid.anchorMin = new Vector2(0, 0);
        grid.anchorMax = new Vector2(1, 1);
        grid.offsetMin = new Vector2(20, 24);
        grid.offsetMax = new Vector2(-20, -190);
        GridLayoutGroup glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(190, 230);
        glg.spacing = new Vector2(20, 20);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.UpperCenter;
        gridContainer = grid;
        FactoryUIGen.WrapInScrollView(grid);

        // 价格（左下）：框架 +N / 贴纸 +N（明细）→ 预估售出价(两者之和) + 预估制作成本(占位)
        frameAddText = FactoryUIGen.Loc("FrameAdd", root.transform, FactoryLocKeySet.Mold.FramePriceFmt, 26, Color.white, TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)frameAddText.transform, 300, 40, -120, -330);
        stickerAddText = FactoryUIGen.Loc("StickerAdd", root.transform, FactoryLocKeySet.Mold.StickerPriceFmt, 26, Color.white, TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)stickerAddText.transform, 300, 40, -120, -370);
        sellPriceText = FactoryUIGen.Loc("SellPrice", root.transform, FactoryLocKeySet.Mold.SellPriceFmt, 30, new Color(1f, 0.85f, 0.5f), TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)sellPriceText.transform, 480, 44, -40, -415);
        craftPriceText = FactoryUIGen.Loc("CraftPrice", root.transform, FactoryLocKeySet.Mold.PriceFmt, 30, new Color(1f, 0.78f, 0.42f), TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)craftPriceText.transform, 480, 44, -40, -460);

        // 制作画布 / 贴纸功能框 / 物料工具 改由各自的「创建…」按钮增量生成，避免覆盖已调好的界面（见下方按钮）。

        // 完成制作（右下）
        completeButton = FactoryUIGen.Btn("CompleteButton", root.transform, FactoryLocKeySet.Mold.Complete, new Color(0.96f, 0.96f, 0.97f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)completeButton.transform, new Vector2(1, 0), new Vector2(1, 0), 280, 90, -40, 40);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 基础界面已重建。再点「创建/重建 制作画布 / 贴纸功能框 / 物料工具」补齐其余部分。", this);
    }

    // 增量生成的父级：优先用现有 Window 节点，没有则退回面板根
    Transform UIRoot()
    {
        Transform w = transform.Find("Window");
        return w != null ? w : transform;
    }

    [PropertySpace(6)]
    [Button("创建/重建 制作画布", ButtonSizes.Large), GUIColor(0.7f, 0.9f, 1f)]
    void BuildCanvasArea()
    {
        Transform root = UIRoot();
        Transform old = root.Find("CanvasArea");
        if(old != null)
            DestroyImmediate(old.gameObject);

        RectTransform canvasArea = FactoryUIGen.Node("CanvasArea", root);
        FactoryUIGen.Center(canvasArea, 800, 460, 150, 60);
        frameImage = FactoryUIGen.Img("FrameImage", canvasArea, Color.white);
        FactoryUIGen.Stretch(frameImage.rectTransform);
        frameImage.preserveAspect = true;
        frameImage.enabled = false;
        stickerLayer = FactoryUIGen.Node("StickerLayer", canvasArea);
        FactoryUIGen.Stretch(stickerLayer);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 制作画布已生成，已回填 frameImage / stickerLayer。", this);
    }

    [PropertySpace(6)]
    [Button("创建/重建 贴纸功能框", ButtonSizes.Large), GUIColor(0.7f, 0.9f, 1f)]
    void BuildStickerPopup()
    {
        Transform root = UIRoot();
        Transform old = root.Find("StickerPopup");
        if(old != null)
            DestroyImmediate(old.gameObject);

        Image popupBg = FactoryUIGen.Img("StickerPopup", root, new Color(0.85f, 0.85f, 0.88f, 0.97f));
        FactoryUIGen.Center(popupBg.rectTransform, 230, 240, 0, 0);
        stickerPopup = popupBg.gameObject;
        popupMirrorButton    = FactoryUIGen.Btn("PopMirror",    popupBg.transform, FactoryLocKeySet.Mold.StickerMirror,    new Color(1, 1, 1, 0f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)popupMirrorButton.transform,    new Vector2(0, 1), new Vector2(1, 1), 0, 56, 0, -4);
        popupLayerUpButton   = FactoryUIGen.Btn("PopLayerUp",   popupBg.transform, FactoryLocKeySet.Mold.StickerLayerUp,   new Color(1, 1, 1, 0f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)popupLayerUpButton.transform,   new Vector2(0, 1), new Vector2(1, 1), 0, 56, 0, -64);
        popupLayerDownButton = FactoryUIGen.Btn("PopLayerDown", popupBg.transform, FactoryLocKeySet.Mold.StickerLayerDown, new Color(1, 1, 1, 0f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)popupLayerDownButton.transform, new Vector2(0, 1), new Vector2(1, 1), 0, 56, 0, -124);
        popupDeleteButton    = FactoryUIGen.Btn("PopDelete",    popupBg.transform, FactoryLocKeySet.Mold.StickerDelete,    new Color(1, 1, 1, 0f), new Color(0.8f, 0.3f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)popupDeleteButton.transform,    new Vector2(0, 1), new Vector2(1, 1), 0, 56, 0, -184);
        stickerPopup.SetActive(false);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 贴纸功能框已生成，已回填 4 个按钮。", this);
    }

    [PropertySpace(6)]
    [Button("创建/重建 物料工具", ButtonSizes.Large), GUIColor(0.7f, 0.9f, 1f)]
    [InfoBox("在现有界面上(不清空其它)生成/重建『框架(物料)工具』(图片调整/物料镜像/物料转向)并回填引用。", InfoMessageType.Info)]
    void BuildFrameTools()
    {
        Transform root = UIRoot();
        Transform old = root.Find("FrameTools");
        if(old != null)
            DestroyImmediate(old.gameObject);

        RectTransform bar = FactoryUIGen.Node("FrameTools", root);
        FactoryUIGen.Center(bar, 400, 80, 470, -300);
        frameAdjustButton = FactoryUIGen.Btn("FrameAdjust", bar, FactoryLocKeySet.Mold.FrameAdjust, new Color(0.7f, 0.7f, 0.74f), Color.white);
        FactoryUIGen.Center((RectTransform)frameAdjustButton.transform, 120, 64, -130, 0);
        frameMirrorButton = FactoryUIGen.Btn("FrameMirror", bar, FactoryLocKeySet.Mold.FrameMirror, new Color(0.7f, 0.7f, 0.74f), Color.white);
        FactoryUIGen.Center((RectTransform)frameMirrorButton.transform, 120, 64, 0, 0);
        frameTurnButton = FactoryUIGen.Btn("FrameTurn", bar, FactoryLocKeySet.Mold.FrameTurn, new Color(0.7f, 0.7f, 0.74f), Color.white);
        FactoryUIGen.Center((RectTransform)frameTurnButton.transform, 120, 64, 130, 0);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 物料工具已生成，已回填 3 个按钮。", this);
    }
    #endregion
#endif
}
