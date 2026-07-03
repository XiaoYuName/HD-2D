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
/// 「物料制作」面板（简化版）：选一个<b>框架</b>(<see cref="ItemType.FigureModel"/>) 作画布底图，再选一枚<b>贴纸</b>(<see cref="ItemType.Painting"/>)，
/// 两者都<b>固定摆放</b>在 CanvasArea 下的 <c>FrameImage</c> / <c>StickerImage</c>（不再生成可拖拽的贴纸实例）。
/// 点「完成制作」时，按<b>合成表</b>(<see cref="FactoryMoldMgConfig"/> 的 craftRecipes) 用「框架Id+贴纸Id」查出合成结果物品 Id，
/// 从 ItemConfig 取到真实 <see cref="ItemData"/> 创建普通 <see cref="ItemInfo"/> 发放进背包。规则：<b>框架不消耗</b>、每次<b>消耗 1 张贴纸</b>。
///
/// 顶部 ①②③ 为 3 个制作模板，各自独立保存「框架 + 贴纸」的选择，可随时切换。
/// 画布精灵走 <see cref="FactoryMoldMgConfig"/>（非物品 128×128 图标）。由 <see cref="FactoryMainPanel.OnMoldMgButton"/> 打开。
///
/// 备注：贴纸自由拖拽/缩放/图层/镜像(FactoryMoldStickerView)、贴纸功能框(StickerPopup)、框架物料工具(FrameTools)、
/// 离屏拍照合成图 等旧功能已按策划停用，代码见文件底部「#if false 备份」，需要时可恢复。
/// </summary>
public class FactoryMoldMgPanel : UIBase
{
    const ItemType FrameType = ItemType.FigureModel;
    const ItemType StickerType = ItemType.Painting;
    const int TemplateCount = 3;

    enum Tab { Frame, Sticker }

    [Title("配置")]
    [LabelText("物料制作配置(精灵/售价/合成表)")][SerializeField] FactoryMoldMgConfig moldConfig;

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

    [Title("制作画布 (CanvasArea)")]
    [LabelText("框架底图 FrameImage")][SerializeField] Image frameImage;
    [LabelText("贴纸固定图 StickerImage")][SerializeField] Image stickerImage;
    [LabelText("模具蒙版图 MaskImage(盖在贴纸上层，遮住溢出框架外的部分)")][SerializeField] Image maskImage;
    [LabelText("叠加图(花纹等)")][SerializeField] Image addImage;
    [Title("价格")]
    [LabelText("框架加价文本(框架 +{Price})")][SerializeField] LocalizeStringEvent frameAddText;
    [LabelText("贴纸加价文本(贴纸 +{Price})")][SerializeField] LocalizeStringEvent stickerAddText;
    [LabelText("预估售出价格文本")][SerializeField] LocalizeStringEvent sellPriceText;   // = 框架货币价格 + 贴纸货币价格(物品 Value)
    [LabelText("预估制作成本文本")][SerializeField] LocalizeStringEvent craftPriceText;   // = 框架成本 + 贴纸成本(moldConfig.frameCost / stickerCost)

    [Title("合成结果预览 (左上角，框架+贴纸都选中后显示)")]
    [LabelText("结果格子(复用 FactorySelectCellUI：图标/名称/数量)")][SerializeField] FactorySelectCellUI resultCell;
    [LabelText("结果描述文本(多语言，走 InventoryItem 表)")][SerializeField] LocalizeStringEvent resultDescText;

    [Title("Button / 提示")]
    [LabelText("完成制作")][SerializeField] Button completeButton;
    [LabelText("退出")][SerializeField] Button closeButton;
    [LabelText("提示")][SerializeField] WarnTip warnTip;

    readonly List<FactoryMoldItemCellUI> cells = new ();
    readonly List<ItemInfo> source = new ();

    // 每模板独立保存：框架(不消耗) + 贴纸(每次制作消耗 1 张)
    readonly ItemInfo[] tplFrame = new ItemInfo[TemplateCount];
    readonly ItemInfo[] tplSticker = new ItemInfo[TemplateCount];

    int curTpl;
    Tab curTab = Tab.Frame;

    ItemInfo SelFrame { get => tplFrame[curTpl]; set => tplFrame[curTpl] = value; }
    ItemInfo SelSticker { get => tplSticker[curTpl]; set => tplSticker[curTpl] = value; }

    #region 生命周期
    public override void Init()
    {
        frameTabButton.onClick.AddListener(() => SwitchTab(Tab.Frame));
        stickerTabButton.onClick.AddListener(() => SwitchTab(Tab.Sticker));
        for(int i = 0; i < templateTabs.Count; i++)
        {
            int idx = i;
            templateTabs[i].onClick.AddListener(() => SwitchTemplate(idx));
        }

        completeButton.onClick.AddListener(OnCompleteButton);
        closeButton.onClick.AddListener(OnCloseButton);

        cellTemplate.gameObject.SetActive(false);
        stickerImage.enabled = false;
    }

    public override void Open()
    {
        base.Open();
        ValidateSelections();
        SetTemplateVisual();
        SwitchTab(Tab.Frame);
        RebuildCanvas();
        RefreshComposition();
    }

    // 校正各模板的框架/贴纸选择：背包里已不存在的置空（贴纸会被消耗，可能已用光）
    void ValidateSelections()
    {
        List<ItemInfo> frames = InventoryManager.Instance.GetItemList(FrameType);
        List<ItemInfo> stickers = InventoryManager.Instance.GetItemList(StickerType);
        for(int i = 0; i < TemplateCount; i++)
        {
            if(tplFrame[i] != null && !frames.Contains(tplFrame[i]))
                tplFrame[i] = null;
            if(tplSticker[i] != null && !stickers.Contains(tplSticker[i]))
                tplSticker[i] = null;
        }
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
        source.AddRange(InventoryManager.Instance.GetItemList(curTab == Tab.Frame ? FrameType : StickerType));

        for(int i = 0; i < cells.Count; i++)
            Destroy(cells[i].gameObject);
        cells.Clear();

        // 框架页高亮当前框架、贴纸页高亮当前贴纸（均为单选）
        ItemInfo sel = curTab == Tab.Frame ? SelFrame : SelSticker;
        for(int i = 0; i < source.Count; i++)
        {
            ItemInfo info = source[i];
            FactoryMoldItemCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Set(i, info.IconPath, info.Name, info.Count, info == sel, OnCellClick);
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
            SetSticker(source[index]);
    }

    void SetCellSelection(ItemInfo sel)
    {
        for(int i = 0; i < cells.Count; i++)
            cells[i].SetSelected(source[i] == sel);
    }
    #endregion

    #region 画布：框架 + 贴纸（固定位置）
    void SetFrame(ItemInfo frame)
    {
        SelFrame = frame;
        SetCellSelection(frame);
        RefreshCanvasFrame();
        RefreshComposition();
    }

    void SetSticker(ItemInfo sticker)
    {
        SelSticker = sticker;
        SetCellSelection(sticker);
        RefreshCanvasSticker();
        RefreshComposition();
    }

    void RefreshCanvasFrame()
    {
        if(SelFrame != null)
        {
            frameImage.enabled  = true;
            frameImage.SetIcon(moldConfig.GetSpriteKey(SelFrame.Id));

            maskImage.enabled = true;
            maskImage.SetIcon(moldConfig.GetMaskKey(SelFrame.Id));
            // addImage.enabled = true;
            // addImage.SetIcon();
        }
        else
        {
            frameImage.enabled = false;
            maskImage.enabled = false;
            addImage.enabled = false;
        }
    }

    void RefreshCanvasSticker()
    {
        if(SelSticker != null)
        {
            stickerImage.enabled = true;
            stickerImage.SetIcon(moldConfig.GetSpriteKey(SelSticker.Id));
        }
        else
        {
            stickerImage.enabled = false;
        }
    }

    // 重建当前模板画布：框架底图 + 贴纸固定图 + 模具蒙版图
    void RebuildCanvas()
    {
        RefreshCanvasFrame();
        RefreshCanvasSticker();
    }
    #endregion

    #region 价格 / 完成态
    // 选择变化后统一刷新：完成按钮可用态 + 售价/制作价 + 合成结果预览
    void RefreshComposition()
    {
        RefreshComplete();
        RefreshPrices();
        RefreshResultPreview();
    }

    // 任一模板「框架+贴纸」都选好即可点完成制作（完成时会把所有选齐的模板逐个制作）
    void RefreshComplete()
    {
        bool anyReady = false;
        for(int i = 0; i < TemplateCount; i++)
        if(tplFrame[i] != null && tplSticker[i] != null)
        {
            anyReady = true;
            break;
        }
        completeButton.interactable = anyReady;
    }

    // 售出价：框架 / 贴纸的「物品货币价格」(ItemInfo.Value → ItemData.Value)。预估售出价 = 两者之和。
    int FrameSellValue() => SelFrame != null ? SelFrame.Value : 0;
    int StickerSellValue() => SelSticker != null ? SelSticker.Value : 0;

    // 制作成本：框架 / 贴纸的基础成本(moldConfig.frameCost / stickerCost)。预估制作成本 = 两者之和。
    int FrameCost() => SelFrame != null ? moldConfig.GetFramePrice(SelFrame.Id) : 0;
    int StickerCost() => SelSticker != null ? moldConfig.GetStickerPrice(SelSticker.Id) : 0;

    // 展示：框架 +{货币价}、贴纸 +{货币价}、预估售出价 ¥{两货币价之和}、预估制作成本 ¥{frameCost + stickerCost}
    void RefreshPrices()
    {
        int frameValue = FrameSellValue();
        int stickerValue = StickerSellValue();

        frameAddText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Mold.FramePriceFmt,
            (LocVarSet.FactoryMold.Price, frameValue));
        stickerAddText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Mold.StickerPriceFmt,
            (LocVarSet.FactoryMold.Price, stickerValue));
        sellPriceText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Mold.SellPriceFmt,
            (LocVarSet.FactoryMold.Price, frameValue + stickerValue));
        craftPriceText.SetTextWithVars(LocTableSet.Factory, FactoryLocKeySet.Mold.PriceFmt,
            (LocVarSet.FactoryMold.Price, FrameCost() + StickerCost()));
    }

    // 合成结果预览（左上角）：框架+贴纸都选中且合成表能查到结果物品时，展示 名称/图标/当前拥有数量 + 多语言描述；否则隐藏格子。
    void RefreshResultPreview()
    {
        if(resultCell == null)
            return;

        long resultId = SelFrame != null && SelSticker != null ? moldConfig.GetCraftResultId(SelFrame.Id, SelSticker.Id) : 0;
        ItemData data = resultId > 0 ? ItemManager.St.GetItemData(resultId) : null;

        resultCell.gameObject.SetActive(data != null);
        if(data == null)
            return;

        resultCell.SetIcon(data.IconPath);
        resultCell.SetName(LocTableSet.InventoryItem, data.NameKey);
        resultCell.SetCount("x" + InventoryManager.Instance.GetItemCount(resultId));
        resultDescText.SetText(LocTableSet.InventoryItem, data.DescKey);
    }
    #endregion

    #region 完成制作
    // 完成制作：遍历 3 个模板，凡「框架+贴纸」都选好的都各制作 1 件（贴纸跨模板累计消耗，够几个做几个）。
    // 按合成表用「框架Id+贴纸Id」查结果物品 Id，从 ItemConfig 取 ItemData 创建 ItemInfo 入包；框架不消耗，每件消耗 1 张贴纸。
    void OnCompleteButton()
    {
        InventoryManager bag = InventoryManager.Instance;

        List<FactoryMoldSettlePanel.Product> products = new ();
        Dictionary<long, int> productIndex = new ();   // 同一结果物品合并计数：resultId → products 下标
        bool anySelected = false;   // 有模板选齐了框架+贴纸
        bool lackSticker = false;   // 有选齐的模板因贴纸不足没做成

        for(int i = 0; i < TemplateCount; i++)
        {
            ItemInfo frame = tplFrame[i];
            ItemInfo sticker = tplSticker[i];
            if(frame == null || sticker == null)
                continue;
            anySelected = true;

            // 贴纸每件消耗 1 张，跨模板累计：确认背包里还有余量
            if(bag.GetItemCount(sticker.Id) < 1)
            {
                lackSticker = true;
                continue;
            }

            // 合成表：框架Id + 贴纸Id → 合成结果物品 Id
            long resultId = moldConfig.GetCraftResultId(frame.Id, sticker.Id);
            if(resultId <= 0)
            {
                Debug.LogError($"[FactoryMoldMgPanel] 合成表未配置该组合：框架 {frame.Id} + 贴纸 {sticker.Id}。请在 FactoryMoldMgConfig 生成/补充合成表。", this);
                continue;
            }

            ItemData data = ItemManager.St.GetItemData(resultId);
            if(data == null)
            {
                Debug.LogError($"[FactoryMoldMgPanel] 合成结果物品未配入 ItemConfig：resultId={resultId}（框架 {frame.Id} + 贴纸 {sticker.Id}）。请补充 ItemConfig 后重新导入。", this);
                continue;
            }

            // 产出合成物品（真实配置物品，1 件），消耗 1 张贴纸（框架不消耗）
            bag.AddItem(resultId, 1);
            ItemInfo stickerInBag = bag.GetItem(sticker.Id);
            if(stickerInBag != null)
                bag.ConsumeItem(stickerInBag, 1);

            // 结算清单：同一结果物品合并数量，单价 = 两物品货币价格之和
            if(productIndex.TryGetValue(resultId, out int idx))
            {
                FactoryMoldSettlePanel.Product prod = products[idx];
                prod.Count += 1;
                products[idx] = prod;
            }
            else
            {
                productIndex[resultId] = products.Count;
                products.Add(new FactoryMoldSettlePanel.Product
                {
                    IconPath = data.IconPath,
                    NameKey = data.NameKey,
                    Count = 1,
                    UnitPrice = frame.Value + sticker.Value,
                    CardSpriteKey = moldConfig.GetSpriteKey(resultId),
                });
            }
        }

        // 一件都没做成：按原因提示（都没选 → 提示选框架；选齐了但贴纸不足 → 贴纸不足；否则缺配方/未配 ItemConfig）
        if(products.Count == 0)
        {
            string tip = !anySelected ? FactoryLocKeySet.Mold.NeedFrame
                       : lackSticker ? FactoryLocKeySet.Mold.NotEnoughSticker
                       : FactoryLocKeySet.Mold.NoRecipe;
            warnTip.ShowTip(LocTableSet.Factory, tip);
            return;
        }

        ShowSettlePanel(products);

        // 贴纸可能已用光：校正选择并刷新列表/画布
        ValidateSelections();
        RebuildCanvas();
        RebuildList();
        RefreshComposition();
    }

    void OnCloseButton() => Close();

    // 完成制作后弹结算面板：展示本次全部产出（可能多种/多件） + Hover 大图（合成成品图，走 moldConfig 精灵表）
    void ShowSettlePanel(List<FactoryMoldSettlePanel.Product> products)
    {
        FactoryMoldSettlePanel.Data settleData = new ()
        {
            Products = products,
            OnBack = OnSettleBack,
        };
        UISystem.Instance.OpenUI<FactoryMoldSettlePanel>(UIPanelIdSet.FactoryMoldSettlePanel).Show(settleData);
    }

    // 返回：只关结算面板，物料制作面板保持打开，可继续制作
    void OnSettleBack() => UISystem.Instance.CloseUI(UIPanelIdSet.FactoryMoldSettlePanel);
    #endregion

#if UNITY_EDITOR
    #region 测试（仅编辑器）
    [PropertySpace(8)]
    [Button("【测试】发放框架 / 贴纸到背包", ButtonSizes.Large), GUIColor(1f, 0.85f, 0.5f)]
    void AddTestItems()
    {
        if(!Application.isPlaying)
        {
            Debug.LogWarning("[FactoryMoldMgPanel] 测试发放需在运行时（且 ItemManager/PlayerInfo 已就绪）点击。", this);
            return;
        }

        InventoryManager bag = InventoryManager.Instance;
        int frameKinds = 0, stickerKinds = 0;
        foreach(ItemData item in ItemManager.St.Config.ItemDataDict.Values)
        {
            if(item.Type == FrameType)
             { bag.AddItem(item.Id, 1); frameKinds++; }   // 框架不消耗，1 个够测
            else if(item.Type == StickerType) { bag.AddItem(item.Id, 5); stickerKinds++; } // 贴纸会被消耗，多给几个
        }

        Debug.Log($"[FactoryMoldMgPanel] 测试物品已发放：框架 {frameKinds} 种、贴纸 {stickerKinds} 种。" +
                  (frameKinds == 0 ? " 注意：ItemConfig 暂无 FigureModel(框架) 物品，框架列表会为空，需先把框架配成 FigureModel。" : ""), this);

        if(isOpen)
        {
            ValidateSelections();
            SwitchTab(curTab);   // 刷新当前列表显示
        }
    }
    #endregion

    #region 一键生成界面（仅编辑器）
    // 仅在「重建整套基础界面」时用，会清空所有子物体——已调好的界面别点！
    // 制作画布用下方增量按钮，不会动其它已调好的部分。
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

        // 制作画布 改由下方「创建/重建 制作画布」按钮增量生成，避免覆盖已调好的界面。

        // 完成制作（右下）
        completeButton = FactoryUIGen.Btn("CompleteButton", root.transform, FactoryLocKeySet.Mold.Complete, new Color(0.96f, 0.96f, 0.97f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)completeButton.transform, new Vector2(1, 0), new Vector2(1, 0), 280, 90, -40, 40);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 基础界面已重建。再点「创建/重建 制作画布」补齐画布(框架底图+贴纸固定图)。", this);
    }

    // 增量生成的父级：优先用现有 Window 节点，没有则退回面板根
    Transform UIRoot()
    {
        Transform w = transform.Find("Window");
        return w != null ? w : transform;
    }

    [PropertySpace(6)]
    [Button("创建/重建 制作画布 (FrameImage + StickerImage + MaskImage)", ButtonSizes.Large), GUIColor(0.7f, 0.9f, 1f)]
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

        // 贴纸固定图：居中、约画布 55%，压在框架之上（后创建=更上层）
        stickerImage = FactoryUIGen.Img("StickerImage", canvasArea, Color.white);
        FactoryUIGen.Center(stickerImage.rectTransform, 300, 300, 0, 0);
        stickerImage.preserveAspect = true;
        stickerImage.enabled = false;

        // 模具蒙版图：压在贴纸之上（后创建=更上层），遮住贴纸溢出框架外的部分，非每个框架都需要
        maskImage = FactoryUIGen.Img("MaskImage", canvasArea, Color.white);
        FactoryUIGen.Stretch(maskImage.rectTransform);
        maskImage.preserveAspect = true;
        maskImage.enabled = false;

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 制作画布已生成，已回填 frameImage / stickerImage / maskImage。", this);
    }

    [PropertySpace(6)]
    [Button("创建/重建 合成结果描述文本 (挂在「结果格子」下)", ButtonSizes.Large), GUIColor(0.8f, 0.95f, 0.8f)]
    [InfoBox("先把场景里已放置的 FactoryMoldItemUIPrefab 实例(FactorySelectCellUI 组件)拖给上方「结果格子(resultCell)」字段，再点本按钮。\n" +
             "会在该格子下新建一段多语言文本(ResultDescText)并回填「结果描述文本」字段；运行时按合成结果物品的 Desc 多语言 Key 自动刷新，无需手动配置 Key。", InfoMessageType.Info)]
    void BuildResultDescText()
    {
        if(resultCell == null)
        {
            Debug.LogError("[FactoryMoldMgPanel] 请先把场景里的 FactoryMoldItemUIPrefab 实例拖给「结果格子(resultCell)」字段，再点本按钮。", this);
            return;
        }

        Transform cellRoot = resultCell.transform;
        Transform old = cellRoot.Find("ResultDescText");
        if(old != null)
            DestroyImmediate(old.gameObject);

        LocalizeStringEvent desc = FactoryUIGen.Loc("ResultDescText", cellRoot, FactoryLocKeySet.Mold.Title, 20, new Color(0.35f, 0.3f, 0.25f), TextAlignmentOptions.TopLeft);
        desc.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        FactoryUIGen.Center((RectTransform)desc.transform, 260, 90, 0, -110);
        resultDescText = desc;

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgPanel] 已生成合成结果描述文本(ResultDescText)，已回填「结果描述文本」字段。", this);
    }

    [PropertySpace(6)]
    [Button("【停用】隐藏旧的 StickerPopup / FrameTools", ButtonSizes.Large), GUIColor(0.9f, 0.8f, 0.7f)]
    [InfoBox("新版改为固定位置贴图 + 合成表，旧的贴纸功能框(StickerPopup)与物料工具(FrameTools)已停用。本按钮在预制体里把它们隐藏(SetActive false)。", InfoMessageType.Info)]
    void DisableLegacyNodes()
    {
        Transform root = UIRoot();
        HideChild(root, "StickerPopup");
        HideChild(root, "FrameTools");
        EditorUtility.SetDirty(this);
    }

    static void HideChild(Transform root, string childName)
    {
        Transform t = root.Find(childName);
        if(t != null)
        {
            t.gameObject.SetActive(false);
            Debug.Log($"[FactoryMoldMgPanel] 已隐藏 {childName}。");
        }
        else
        {
            Debug.LogWarning($"[FactoryMoldMgPanel] 未找到子节点 {childName}（可能已删除或改名）。");
        }
    }
    #endregion
#endif

    // =====================================================================================
    // 【已停用·保留备份】以下为旧版「可拖拽贴纸(FactoryMoldStickerView)+贴纸功能框(StickerPopup)+
    // 框架物料工具(FrameTools)+离屏拍照合成图+运行时合成物 FactoryProductionMtItemInfo」相关字段与方法。
    // 按策划改版后整体停用，以 #if false 保留（不参与编译/序列化）。需要恢复请删掉 #if false / #endif 并接回主流程。
    // =====================================================================================
#if false
    #region 旧·字段
    [LabelText("画布最多贴纸数(暂为1，后续可拓展)"), MinValue(1)][SerializeField] int maxStickers = 1;
    [LabelText("贴纸层容器")][SerializeField] RectTransform stickerLayer;
    [LabelText("贴纸实例模板(隐藏)")][SerializeField] FactoryMoldStickerView stickerViewTemplate;
    [LabelText("功能框根物体")][SerializeField] GameObject stickerPopup;
    [LabelText("镜像翻转")][SerializeField] Button popupMirrorButton;
    [LabelText("图层往上")][SerializeField] Button popupLayerUpButton;
    [LabelText("图层往下")][SerializeField] Button popupLayerDownButton;
    [LabelText("删除")][SerializeField] Button popupDeleteButton;
    [LabelText("图片调整")][SerializeField] Button frameAdjustButton;
    [LabelText("物料镜像")][SerializeField] Button frameMirrorButton;
    [LabelText("物料转向")][SerializeField] Button frameTurnButton;

    // 每模板：画布上所有贴纸的摆放（位置/缩放/镜像/图层）
    readonly List<StickerPlacement>[] tplPlacements = new List<StickerPlacement>[TemplateCount];
    readonly List<FactoryMoldStickerView> stickerViews = new ();   // 当前模板的活动贴纸实例
    FactoryMoldStickerView curStickerView;                          // 当前选中的贴纸（功能框作用对象）
    Canvas rootCanvas;
    List<StickerPlacement> CurPlacements => tplPlacements[curTpl];
    #endregion

    #region 旧·生命周期 / 校正
    void EnsurePlacements()
    {
        for(int i = 0; i < TemplateCount; i++)
            tplPlacements[i] ??= new List<StickerPlacement>();
    }

    // 校正各模板的框架选择：背包里已不存在的置空（框架本身不消耗，一般只在被它处移除时触发）
    void ValidateFrames()
    {
        List<ItemInfo> frames = InventoryManager.Instance.GetItemList(FrameType);
        for(int i = 0; i < TemplateCount; i++)
            if(tplFrame[i] != null && !frames.Contains(tplFrame[i]))
                tplFrame[i] = null;
    }
    #endregion

    #region 旧·画布：框架 + 可拖拽贴纸
    // 点击贴纸 = 往画布添加一枚新贴纸实例；达上限(maxStickers)则提示。value 取自售价配置。
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

    void RebuildCanvasOld()
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
    #endregion

    #region 旧·拍照（框架+贴纸构图 → 合成图标）
    // 把当前「框架 + 画布上所有贴纸」的构图离屏渲染成 PNG 字节，作为产物图标。
    byte[] CaptureComposition(int longSide = 256)
    {
        if(frameImage == null)
            return null;
        var area = (RectTransform)frameImage.transform.parent;   // CanvasArea：含框架底图 + 贴纸层
        if(area == null)
            return null;

        for(int i = 0; i < stickerViews.Count; i++)
            if(stickerViews[i] != null)
                stickerViews[i].SetSelected(false);
        Canvas.ForceUpdateCanvases();

        Vector2 size = area.rect.size;
        if(size.x < 1f || size.y < 1f)
            return null;

        float aspect = size.x / size.y;
        int rtW = aspect >= 1f ? longSide : Mathf.Max(1, Mathf.RoundToInt(longSide * aspect));
        int rtH = aspect >= 1f ? Mathf.Max(1, Mathf.RoundToInt(longSide / aspect)) : longSide;

        int layer = area.gameObject.layer;
        var pos = new Vector3(10000f, 10000f, 10000f);

        var camGO = new GameObject("__MoldCaptureCam");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = size.y * 0.5f;
        cam.aspect = aspect;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        camGO.transform.position = pos + new Vector3(0f, 0f, -10f);
        camGO.transform.rotation = Quaternion.identity;

        RenderTexture rt = RenderTexture.GetTemporary(rtW, rtH, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        var canvasGO = new GameObject("__MoldCaptureCanvas") { layer = layer };
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var canvasRT = (RectTransform)canvas.transform;
        canvasRT.sizeDelta = size;
        canvasRT.position = pos;
        canvasRT.rotation = Quaternion.identity;
        canvasRT.localScale = Vector3.one;

        GameObject clone = Instantiate(area.gameObject, canvasGO.transform);
        SetLayerRecursive(clone.transform, layer);
        var crt = (RectTransform)clone.transform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        crt.localScale = Vector3.one;
        crt.localRotation = Quaternion.identity;

        Canvas.ForceUpdateCanvases();
        cam.Render();

        var tex = new Texture2D(rtW, rtH, TextureFormat.RGBA32, false);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rtW, rtH), 0, 0);
        tex.Apply(false);
        RenderTexture.active = prev;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(clone);
        Destroy(canvasGO);
        Destroy(camGO);

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);
        return png;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for(int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }
    #endregion

    #region 旧·贴纸功能框（镜像 / 图层 / 删除）
    void OnStickerSelected(FactoryMoldStickerView view)
    {
        for(int i = 0; i < stickerViews.Count; i++)
            stickerViews[i].SetSelected(stickerViews[i] == view);
        curStickerView = view;
        ShowPopup(view);
    }

    void ShowPopup(FactoryMoldStickerView view)
    {
        stickerPopup.SetActive(true);
        RectTransform pr = (RectTransform)stickerPopup.transform;
        RectTransform parent = (RectTransform)pr.parent;

        Camera cam = CanvasCamera();
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, view.transform.position);
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen + new Vector2(110f, 70f), cam, out Vector2 local))
            pr.anchoredPosition = local;
    }

    Camera CanvasCamera()
    {
        if(rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
        if(rootCanvas == null)
            return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
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

    void ResyncPlacementOrder()
    {
        stickerViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        CurPlacements.Clear();
        foreach(FactoryMoldStickerView v in stickerViews)
            CurPlacements.Add(v.Placement);
    }
    #endregion

    #region 旧·框架(物料)工具
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

    #region 旧·完成制作（运行时合成物 FactoryProductionMtItemInfo）
    void OnCompleteButtonOld()
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

        InventoryManager bag = InventoryManager.Instance;

        Dictionary<long, int> need = new ();
        foreach(StickerPlacement p in CurPlacements)
            need[p.itemId] = (need.TryGetValue(p.itemId, out int n) ? n : 0) + 1;
        foreach(KeyValuePair<long, int> kv in need)
            if(bag.GetItemCount(kv.Key) < kv.Value)
            {
                warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NotEnoughSticker);
                return;
            }

        byte[] compositePng = CaptureComposition();
        int sellValue = FramePrice() + StickerPriceSumOld();
        ItemInfo firstSticker = bag.GetItem(CurPlacements[0].itemId);
        FactoryProductionMtItemInfo product = FactoryProductionMtItemInfo.Create(SelFrame, firstSticker, 1, sellValue, compositePng);
        if(product == null)
            return;
        bag.AddRuntimeItem(product);

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

        CurPlacements.Clear();
        RebuildCanvasOld();
        RebuildList();
        RefreshComposition();
    }

    int StickerPriceSumOld()
    {
        int sum = 0;
        foreach(StickerPlacement p in CurPlacements)
            sum += p.value;
        return sum;
    }
    #endregion

    #region 旧·编辑器增量生成（贴纸功能框 / 物料工具）
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
    }

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
    }
    #endregion
#endif
}
