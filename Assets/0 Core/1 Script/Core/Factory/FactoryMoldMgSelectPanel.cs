using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 「物料制作」面板：玩家从背包里选一个<b>框架</b>(<see cref="ItemType.FigureModel"/>，如空徽章/抱枕/立牌)和一个<b>贴纸</b>
/// (<see cref="ItemType.Painting"/>，女主绘画)，中间实时预览两者叠加效果；点「完成制作」即合成一件
/// <see cref="FactoryProductionMaterialsData"/>（运行时物品，见该类说明）发放进背包。
///
/// 规则（按策划）：制作时<b>框架不消耗</b>（相当于可复用的模具），<b>仅消耗 1 个贴纸</b>。相同「框架+贴纸」组合在背包里堆叠为同一格。
/// 由 <see cref="FactoryMainPanel.OnMoldMgButton"/> 通过 <see cref="UIPanelIdSet.FactoryMoldMgSelectPanel"/> 打开。
/// 备注：原型图里的贴纸拖拽/镜像/图层/多贴纸编辑器为后续阶段；本版先做「选框架 + 选贴纸 → 完成制作」的功能闭环。
/// </summary>
public class FactoryMoldMgSelectPanel : UIBase
{
    // 框架 / 贴纸对应的物品类型（与 ItemType 注释一致）。备注：当前 ItemConfig 中框架可能尚未按 FigureModel 配置，
    // 待配置更新后框架列表才有内容；面板逻辑不依赖具体配置，只按类型从背包取。
    const ItemType FrameType = ItemType.FigureModel;
    const ItemType StickerType = ItemType.Painting;

    enum Tab { Frame, Sticker }

    [Title("Tab")]
    [LabelText("框架Tab按钮")][SerializeField] Button frameTabButton;
    [LabelText("贴纸Tab按钮")][SerializeField] Button stickerTabButton;
    [LabelText("Tab选中色")][SerializeField] Color tabActiveColor = new (1f, 0.78f, 0.42f, 1f);
    [LabelText("Tab未选色")][SerializeField] Color tabNormalColor = new (0.86f, 0.86f, 0.88f, 1f);

    [Title("列表")]
    [LabelText("格子容器")][SerializeField] RectTransform gridContainer;
    [LabelText("格子模板(隐藏，复用 FactorySelectCellUI)")][SerializeField] FactorySelectCellUI cellTemplate;

    [Title("预览(框架 + 贴纸叠加)")]
    [LabelText("框架预览图")][SerializeField] Image previewFrameImage;
    [LabelText("贴纸预览图")][SerializeField] Image previewStickerImage;

    [Title("分数 / 价格")]
    [LabelText("框架分数文本")][SerializeField] LocalizeStringEvent frameScoreText;
    [LabelText("贴纸分数文本")][SerializeField] LocalizeStringEvent stickerScoreText;
    [LabelText("预估价格文本")][SerializeField] LocalizeStringEvent priceText;
    // 占位数值（与原型图一致）。后续若策划要求，可像 FactoryGameConfig 那样改走 CSV 配置。
    [LabelText("框架加分(占位)")][SerializeField] int frameScore = 50;
    [LabelText("贴纸加分(占位)")][SerializeField] int stickerScore = 50;
    [LabelText("预估单价(占位)")][SerializeField] int unitPrice = 50;

    [Title("Button / 提示")]
    [LabelText("完成制作")][SerializeField] Button completeButton;
    [LabelText("退出")][SerializeField] Button closeButton;
    [LabelText("提示")][SerializeField] WarnTip warnTip;

    readonly List<FactorySelectCellUI> cells = new ();
    readonly List<ItemInfo> source = new ();

    Tab curTab = Tab.Frame;
    ItemInfo selectedFrame;     // 框架不消耗，选中后跨制作保留
    ItemInfo selectedSticker;   // 贴纸消耗，制作后清空
    bool bound;

    #region 生命周期
    public override void Init()
    {
        if(!bound)
        {
            frameTabButton.onClick.AddListener(() => SwitchTab(Tab.Frame));
            stickerTabButton.onClick.AddListener(() => SwitchTab(Tab.Sticker));
            completeButton.onClick.AddListener(OnCompleteButton);
            closeButton.onClick.AddListener(OnCloseButton);
            bound = true;
        }
        cellTemplate.gameObject.SetActive(false);
    }

    public override void Open()
    {
        base.Open();
        // 校正已选项：背包里若已不存在（贴纸被消耗、或被它处用掉）则清空
        if(selectedFrame != null && !BagContains(FrameType, selectedFrame))
            selectedFrame = null;
        if(selectedSticker != null && !BagContains(StickerType, selectedSticker))
            selectedSticker = null;

        RefreshScoreAndPrice();
        SwitchTab(Tab.Frame);
    }

    bool BagContains(ItemType type, ItemInfo info)
        => info != null && PlayerInfo.St.Bag.GetItemList(type).Contains(info);
    #endregion

    #region Tab / 列表
    void SwitchTab(Tab tab)
    {
        curTab = tab;
        SetTabVisual();
        RebuildList();
        RefreshPreview();
        RefreshComplete();
    }

    void SetTabVisual()
    {
        if(frameTabButton != null && frameTabButton.targetGraphic != null)
            frameTabButton.targetGraphic.color = curTab == Tab.Frame ? tabActiveColor : tabNormalColor;
        if(stickerTabButton != null && stickerTabButton.targetGraphic != null)
            stickerTabButton.targetGraphic.color = curTab == Tab.Sticker ? tabActiveColor : tabNormalColor;
    }

    void RebuildList()
    {
        source.Clear();
        source.AddRange(PlayerInfo.St.Bag.GetItemList(curTab == Tab.Frame ? FrameType : StickerType));

        for(int i = 0; i < cells.Count; i++)
            Destroy(cells[i].gameObject);
        cells.Clear();

        ItemInfo selected = curTab == Tab.Frame ? selectedFrame : selectedSticker;
        for(int i = 0; i < source.Count; i++)
        {
            ItemInfo info = source[i];
            FactorySelectCellUI cell = Instantiate(cellTemplate, gridContainer);
            cell.gameObject.SetActive(true);
            cell.SetIcon(info.IconPath);
            cell.SetName(info.Name);
            cell.SetSub("x" + info.Count);
            cell.SetSelected(info == selected);
            cell.Set(i, OnCellClick);
            cells.Add(cell);
        }
    }

    void OnCellClick(int index)
    {
        if(index < 0 || index >= source.Count)
            return;

        ItemInfo clicked = source[index];
        if(curTab == Tab.Frame)
            selectedFrame = clicked;
        else
            selectedSticker = clicked;

        // 单选：刷新本列表选中描边
        ItemInfo selected = curTab == Tab.Frame ? selectedFrame : selectedSticker;
        for(int i = 0; i < cells.Count; i++)
            cells[i].SetSelected(source[i] == selected);

        RefreshPreview();
        RefreshComplete();
    }
    #endregion

    #region 预览 / 分数 / 完成态
    void RefreshPreview()
    {
        if(previewFrameImage != null)
        {
            bool has = selectedFrame != null;
            previewFrameImage.enabled = has;
            if(has) previewFrameImage.SetIcon(selectedFrame.IconPath);
        }
        if(previewStickerImage != null)
        {
            bool has = selectedSticker != null;
            previewStickerImage.enabled = has;
            if(has) previewStickerImage.SetIcon(selectedSticker.IconPath);
        }
    }

    void RefreshScoreAndPrice()
    {
        // 注意：SetTextWithVars 是扩展方法，不能用 ?. 调用，需显式判空
        if(frameScoreText != null)
            frameScoreText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.FrameScoreFmt,
                (LocalizeVarSet.FactoryMold.Score, frameScore));
        if(stickerScoreText != null)
            stickerScoreText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.StickerScoreFmt,
                (LocalizeVarSet.FactoryMold.Score, stickerScore));
        if(priceText != null)
            priceText.SetTextWithVars(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.PriceFmt,
                (LocalizeVarSet.FactoryMold.Price, unitPrice));
    }

    void RefreshComplete()
    {
        completeButton.interactable = selectedFrame != null && selectedSticker != null && selectedSticker.Count > 0;
    }
    #endregion

    #region 完成制作
    // 完成制作：合成 1 件生产资料发放进背包；仅消耗 1 个贴纸，框架不消耗（可复用的模具）。
    void OnCompleteButton()
    {
        if(selectedFrame == null)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NeedFrame);
            return;
        }
        if(selectedSticker == null || selectedSticker.Count <= 0)
        {
            warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.NeedSticker);
            return;
        }

        FactoryProductionMaterialsData product = FactoryProductionMaterialsData.Create(selectedFrame.Data, selectedSticker.Data);
        if(product == null)
            return;

        PlayerBag bag = PlayerInfo.St.Bag;
        bag.AddRuntimeItem(product, 1);   // 运行时物品：不查 ItemConfig，相同组合自动堆叠
        bag.ConsumeItem(selectedSticker, 1);   // 只消耗贴纸；框架保留
        selectedSticker = null;

        warnTip.ShowTip(LocalizeTableSet.Factory, FactoryLocKeySet.Mold.CraftSuccess);

        // 贴纸被消耗后回到贴纸列表刷新数量 / 清空选中；框架列表与选中不变
        SwitchTab(Tab.Sticker);
    }

    void OnCloseButton() => Close();
    #endregion

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("生成「物料制作」面板骨架：左侧标题 + 框架/贴纸 Tab + 物品网格(可滚动)，中部预览(框架底图 + 贴纸叠加)，" +
             "底部分数 / 预估价格，右下「完成制作」、右上「退出」，并回填各引用。\n" +
             "格子模板(cellTemplate)与提示(warnTip)请手动指定：cellTemplate 可复用 FactoryMaterialSelectPanel 里的同款格子。" +
             "可重复点击，旧生成内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // 整体背景
        Image root = FactoryUIGen.Img("Window", transform, new Color(0.20f, 0.18f, 0.16f, 0.98f));
        FactoryUIGen.Stretch(root.rectTransform);

        // 退出（右上）
        closeButton = FactoryUIGen.Btn("ExitButton", root.transform, FactoryLocKeySet.Mold.Exit, new Color(0.95f, 0.95f, 0.97f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), 140, 64, -24, -24);

        // 左侧面板：定宽 470，竖向随父级拉伸（上下各留 24）
        Image left = FactoryUIGen.Img("LeftPanel", root.transform, new Color(0.78f, 0.62f, 0.5f, 1f));
        RectTransform lrt = left.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 0.5f);
        lrt.sizeDelta = new Vector2(470, -48);
        lrt.anchoredPosition = new Vector2(24, 0);

        FactoryUIGen.Loc("Title", left.transform, FactoryLocKeySet.Mold.Title, 34, Color.white, TMPro.TextAlignmentOptions.Center)
            .GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -40);

        // 框架 / 贴纸 Tab（贴左侧面板顶部居中，左右并排）
        frameTabButton = FactoryUIGen.Btn("FrameTab", left.transform, FactoryLocKeySet.Mold.TabFrame, tabActiveColor, new Color(0.3f, 0.25f, 0.2f));
        FactoryUIGen.Anchor((RectTransform)frameTabButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 190, 70, -100, -110);

        stickerTabButton = FactoryUIGen.Btn("StickerTab", left.transform, FactoryLocKeySet.Mold.TabSticker, tabNormalColor, new Color(0.3f, 0.25f, 0.2f));
        FactoryUIGen.Anchor((RectTransform)stickerTabButton.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 190, 70, 100, -110);

        // 物品网格（可上下滚动）
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

        // 中部预览（框架底图 + 贴纸叠加）
        previewFrameImage = FactoryUIGen.Img("PreviewFrame", root.transform, Color.white);
        FactoryUIGen.Center(previewFrameImage.rectTransform, 760, 420, 130, 40);
        previewFrameImage.preserveAspect = true;
        previewFrameImage.enabled = false;

        previewStickerImage = FactoryUIGen.Img("PreviewSticker", previewFrameImage.transform, Color.white);
        FactoryUIGen.Center(previewStickerImage.rectTransform, 760, 420, 0, 0);
        previewStickerImage.preserveAspect = true;
        previewStickerImage.enabled = false;

        // 底部分数 / 预估价格
        frameScoreText = FactoryUIGen.Loc("FrameScore", root.transform, FactoryLocKeySet.Mold.FrameScoreFmt, 30, Color.white, TMPro.TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)frameScoreText.transform, 320, 44, -100, -360);
        stickerScoreText = FactoryUIGen.Loc("StickerScore", root.transform, FactoryLocKeySet.Mold.StickerScoreFmt, 30, Color.white, TMPro.TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)stickerScoreText.transform, 320, 44, -100, -410);
        priceText = FactoryUIGen.Loc("Price", root.transform, FactoryLocKeySet.Mold.PriceFmt, 30, new Color(1f, 0.78f, 0.42f), TMPro.TextAlignmentOptions.Left);
        FactoryUIGen.Center((RectTransform)priceText.transform, 420, 44, 360, -440);

        // 完成制作（右下）
        completeButton = FactoryUIGen.Btn("CompleteButton", root.transform, FactoryLocKeySet.Mold.Complete, new Color(0.96f, 0.96f, 0.97f), new Color(0.25f, 0.25f, 0.3f));
        FactoryUIGen.Anchor((RectTransform)completeButton.transform, new Vector2(1, 0), new Vector2(1, 0), 280, 90, -40, 40);

        EditorUtility.SetDirty(this);
        Debug.Log("[FactoryMoldMgSelectPanel] 界面已生成。请手动指定 cellTemplate(可复用 FactoryMaterialSelectPanel 的格子) 与 warnTip。", this);
    }
    #endregion
#endif
}
