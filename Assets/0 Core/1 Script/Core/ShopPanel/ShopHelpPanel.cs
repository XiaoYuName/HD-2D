using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 商店帮忙小游戏界面：顶部倒计时(CountDownPop)、中央货架网格、右侧可拖拽的货物箱、结束本局按钮、左下角色台词(AvatarPortraitPop)与说明。
/// 玩家从右侧箱子按住货物图标拖到货架上（每格 1 件，拖到已占用格替换、原货物退回箱子），
/// 倒计时内摆满 = 胜利，超时/主动结束未摆满 = 失败，结算走内嵌的 <see cref="ShopHelpSettlePanel"/>。逻辑由 <see cref="ShopHelpGameManager"/> 处理。
/// </summary>
[RequireComponent(typeof(ShopHelpGameManager))]
public class ShopHelpPanel : UIBase
{
    [SerializeField] ShopHelpGameManager manager;

    [Title("顶部")]
    [LabelText("标题(小游戏)")][SerializeField] LocalizeStringEvent titleText;
    [LabelText("倒计时 CountDownPop")][SerializeField] CountDownPop countDownPop;

    [Title("货架")]
    [LabelText("格子父级")][SerializeField] RectTransform gridContainer;
    [SerializeField] GridLayoutGroup glg;
    [LabelText("格子模板")][SerializeField] ShopHelpItemCellUI cellPrefab;

    [Title("货物箱(预置位置，按品类数显隐)")]
    [SerializeField] ShopHelpBoxUI[] boxes;
    [LabelText("拖拽层(货物图标拖拽时的父级，最上层)")][SerializeField] RectTransform dragLayer;

    [Title("角色 / 说明")]
    [LabelText("左下角色台词(AvatarPortraitPop)")][SerializeField] AvatarPortraitPop npcPop;
    [LabelText("游戏说明")][SerializeField] LocalizeStringEvent helpText;

    [Title("结算")]
    [LabelText("结算面板(内嵌子物体)")][SerializeField] ShopHelpSettlePanel settlePanel;

    [Title("按钮")]
    [LabelText("结束本局")][SerializeField] Button endButton;

    // 音效 Key（与 AudioManager 注册的 Key 约定一致）
    const string ShopHelp = nameof(ShopHelp);
    const string Place = ShopHelp + nameof(Place);       // 货物摆上货架
    const string Return = ShopHelp + nameof(Return);     // 货物退回箱子
    const string Win = ShopHelp + nameof(Win);           // 胜利
    const string Lose = ShopHelp + nameof(Lose);         // 失败

    readonly List<ShopHelpItemCellUI> cells = new List<ShopHelpItemCellUI>();
    bool subscribed;
    bool settleHooked;
    Camera uiCamera;

    // 拖拽状态
    ShopHelpBoxUI draggingBox;
    int dragType = -1;
    bool dragActive;
    int lastSlot = -1;
    Vector3 iconHomeLocalPos;

    ShopHelpGameConfig Config => manager != null ? manager.Config : null;

    #region 生命周期
    public override void Init()
    {
        if(manager == null)
            manager = GetComponent<ShopHelpGameManager>();

        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        endButton.onClick.AddListener(OnEndButton);

        BuildGrid();
        HookBoxes();
        HookSettle();
        Subscribe();

        titleText.SetTextSafe(LocTableSet.ShopHelpPanel, "ShopHelpTitle");
        helpText.SetTextSafe(LocTableSet.ShopHelpPanel, "ShopHelpHelp");
        if(npcPop != null)
            npcPop.SetContext(LanguageManager.Instance.GetLocalizedString(LocTableSet.ShopHelpPanel, "ShopHelpSpeech"));
    }

    public override void Open()
    {
        base.Open();
        Subscribe();
        if(settlePanel != null)
            settlePanel.Hide();
        // 每次打开即为新的一局（入场消耗已由 GameEnterPanel 校验，这里扣一次以完成实际消耗）
        if(!manager.StartGame(true))
            Debug.LogWarning("[ShopHelpPanel] 资源不足，无法开局。", this);
    }

    public override void Close()
    {
        base.Close();
        Unsubscribe();
    }
    #endregion

    #region 订阅
    void Subscribe()
    {
        if(subscribed)
            return;
        subscribed = true;
        manager.OnSetup += OnSetup;
        manager.OnTimeChanged += OnTimeChanged;
        manager.OnGameEnd += OnGameEnd;
    }

    void Unsubscribe()
    {
        if(!subscribed)
            return;
        subscribed = false;
        manager.OnSetup -= OnSetup;
        manager.OnTimeChanged -= OnTimeChanged;
        manager.OnGameEnd -= OnGameEnd;
    }

    void HookBoxes()
    {
        if(boxes == null)
            return;
        foreach(ShopHelpBoxUI box in boxes)
        {
            if(box == null)
                continue;
            box.BeginDragEvt += OnBoxBeginDrag;
            box.DragEvt += OnBoxDrag;
            box.EndDragEvt += OnBoxEndDrag;
        }
    }

    void HookSettle()
    {
        if(settleHooked || settlePanel == null)
            return;
        settleHooked = true;
        settlePanel.OnReplay += OnSettleReplay;
        settlePanel.OnBack += OnSettleBack;
    }
    #endregion

    #region 货架
    void BuildGrid()
    {
        int total = Config != null ? Config.TotalSlots : 20;
        if(cells.Count == total)
            return;

        for(int i = gridContainer.childCount - 1; i >= 0; i--)
            Destroy(gridContainer.GetChild(i).gameObject);
        cells.Clear();

        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = Config != null ? Config.Cols : 5;

        for(int i = 0; i < total; i++)
        {
            ShopHelpItemCellUI cell = Instantiate(cellPrefab, gridContainer);
            cell.gameObject.SetActive(true);
            cell.Init(i);
            cells.Add(cell);
        }
    }
    #endregion

    #region 管理器事件
    // 开局：重建货架为空、按品类数显隐并绑定右侧箱子
    void OnSetup()
    {
        CancelDrag();
        if(settlePanel != null)
            settlePanel.Hide();

        foreach(ShopHelpItemCellUI cell in cells)
            cell.SetEmpty();

        IReadOnlyList<ShopHelpGameManager.GoodsType> goods = manager.Goods;
        for(int i = 0; i < boxes.Length; i++)
        {
            if(boxes[i] == null)
                continue;
            if(i < goods.Count)
                boxes[i].Setup(i, goods[i].IconPath, goods[i].Total);
            else
                boxes[i].Hide();
        }
    }

    void OnTimeChanged(float secondsLeft)
    {
        if(countDownPop != null)
            countDownPop.SetTime(secondsLeft);
    }
    #endregion

    #region 拖拽
    void OnBoxBeginDrag(ShopHelpBoxUI box, PointerEventData e)
    {
        if(manager.State != ShopHelpGameManager.GameState.Playing)
            return;
        int type = box.TypeIndex;
        if(type < 0 || type >= manager.Goods.Count || manager.Goods[type].Remaining <= 0)
            return;

        draggingBox = box;
        dragType = type;
        dragActive = true;
        lastSlot = -1;
        iconHomeLocalPos = box.IconRoot.localPosition;

        box.IconRoot.SetParent(dragLayer, true);   // 保持世界坐标，改由拖拽层承载以覆盖在货架之上
        box.IconRoot.SetAsLastSibling();
        MoveIconToPointer(box, e);
    }

    void OnBoxDrag(ShopHelpBoxUI box, PointerEventData e)
    {
        if(!dragActive || draggingBox != box)
            return;

        MoveIconToPointer(box, e);

        int slot = FindSlot(e.position);
        if(slot < 0)
        {
            lastSlot = -1;
            return;
        }
        if(slot == lastSlot)
            return;
        lastSlot = slot;

        // 缓存到局部：若这是最后一件（触发胜利），TryPlaceInto 内部会走结算并 CancelDrag 把 dragType 置 -1，
        // 之后仍要用它读货物数据刷新画面，故不能再依赖字段 dragType。
        int type = dragType;
        ShopHelpGameManager.PlaceResult r = manager.TryPlaceInto(slot, type);
        if(!r.Placed)
            return;

        // 货架格出现新货物：缩放动效 + 音效
        cells[slot].Show(manager.Goods[type].IconPath);
        AudioManager.Instance.PlayAudio(Place);

        // 手上货物数量 -1 并做放大缩小反馈
        box.SetCount(manager.Goods[type].Remaining);
        box.PulseDrag();

        // 被替换下来的货物退回它的箱子：缩放动效 + 音效
        if(r.DisplacedType >= 0)
        {
            ShopHelpBoxUI db = FindBox(r.DisplacedType);
            if(db != null)
            {
                db.SetCount(manager.Goods[r.DisplacedType].Remaining);
                db.PlayAppear();
            }
            AudioManager.Instance.PlayAudio(Return);
        }

        // 手上货物耗尽：拖拽失效、图标消失
        if(manager.Goods[type].Remaining <= 0)
            dragActive = false;
    }

    void OnBoxEndDrag(ShopHelpBoxUI box, PointerEventData e)
    {
        if(draggingBox != box)
            return;

        box.IconRoot.SetParent(box.transform, true);

        if(dragType >= 0 && dragType < manager.Goods.Count && manager.Goods[dragType].Remaining > 0)
        {
            // 松开后货物飞回箱子起点并展示
            box.SetCount(manager.Goods[dragType].Remaining);
            box.IconRoot.localScale = Vector3.one;
            Tween.LocalPosition(box.IconRoot, iconHomeLocalPos, 0.22f, Ease.OutCubic);
        }
        else
        {
            box.IconRoot.localPosition = iconHomeLocalPos;
        }

        draggingBox = null;
        dragType = -1;
        dragActive = false;
        lastSlot = -1;
    }

    // 开局/异常时收回可能残留的拖拽图标
    void CancelDrag()
    {
        if(draggingBox != null)
        {
            draggingBox.IconRoot.SetParent(draggingBox.transform, false);
            draggingBox.IconRoot.localPosition = iconHomeLocalPos;
            draggingBox.IconRoot.localScale = Vector3.one;
        }
        draggingBox = null;
        dragType = -1;
        dragActive = false;
        lastSlot = -1;
    }

    void MoveIconToPointer(ShopHelpBoxUI box, PointerEventData e)
    {
        if(RectTransformUtility.ScreenPointToWorldPointInRectangle(dragLayer, e.position, uiCamera, out Vector3 world))
            box.IconRoot.position = world;
    }

    int FindSlot(Vector2 screenPoint)
    {
        for(int i = 0; i < cells.Count; i++)
            if(cells[i].ContainsScreenPoint(screenPoint, uiCamera))
                return i;
        return -1;
    }

    ShopHelpBoxUI FindBox(int typeIndex)
    {
        if(typeIndex >= 0 && typeIndex < boxes.Length)
            return boxes[typeIndex];
        return null;
    }
    #endregion

    #region 按钮 / 结算
    void OnEndButton() => manager.ManualEnd();

    void OnGameEnd(bool win, int coin, int favor)
    {
        CancelDrag();
        AudioManager.Instance.PlayAudio(win ? Win : Lose);

        settlePanel.Show(win, coin, favor, manager.HasEnough());
    }

    // 再来一局：资源够则扣费重开
    void OnSettleReplay()
    {
        if(!manager.HasEnough())
            return;
        settlePanel.Hide();
        manager.StartGame(true);
    }

    // 返回：关闭结算并关闭本小游戏面板（回到商店）
    void OnSettleBack()
    {
        settlePanel.Hide();
        Close();
    }
    #endregion

#if UNITY_EDITOR
    #region 一键生成界面（仅编辑器）
    const string GuidCountDownPop = "d887aeafd7baf7f46b9e0ac4072326c8";
    const string GuidAvatarPortraitPop = "63526206517900343b4917c4606b2e76";

    [PropertySpace(8)]
    [Button("创建界面 UI", ButtonSizes.Large), GUIColor(0.5f, 0.85f, 1f)]
    [InfoBox("生成顶部倒计时(CountDownPop)、中央货架网格、右侧 4 个可拖拽货物箱、拖拽层、左下角色台词(AvatarPortraitPop)、说明、结束本局按钮与内嵌结算面板(ShopHelpSettlePanel)，并自动赋值引用与多语言 Key（表：ShopHelpPanel）。可重复点击，旧内容会先清空。", InfoMessageType.Info)]
    void BuildUI()
    {
        if(manager == null)
            manager = GetComponent<ShopHelpGameManager>();

        int cols = Config != null ? Config.Cols : 5;
        int rows = Config != null ? Config.Rows : 4;

        for(int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        cells.Clear();

        // 半透明底（点击不穿透到场景），全屏
        Image root = UIGen.Img("Dim", transform, new Color(0f, 0f, 0f, 0.35f));
        UIGen.Stretch(root.rectTransform);

        // 顶部米黄色标题栏
        Image header = UIGen.Img("Header", transform, new Color(0.925f, 0.874f, 0.647f, 1f));
        UIGen.Anchor(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 90, 0, 0);

        titleText = UIGen.Loc("Title", header.transform, "ShopHelpTitle", 40, new Color(0.35f, 0.28f, 0.1f), TextAlignmentOptions.Left);
        UIGen.Anchor(titleText.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), 260, 70, 40, 0);

        // 倒计时 CountDownPop（顶部居中）
        GameObject popGo = UIGen.InstantiatePrefab(GuidCountDownPop, header.transform);
        if(popGo != null)
        {
            RectTransform popRt = (RectTransform)popGo.transform;
            popRt.anchorMin = popRt.anchorMax = popRt.pivot = new Vector2(0.5f, 0.5f);
            popRt.anchoredPosition = Vector2.zero;
            countDownPop = popGo.GetComponent<CountDownPop>();
        }

        // 货架网格（中央偏左）
        Image gridBg = UIGen.Img("GridContainer", transform, new Color(0.35f, 0.28f, 0.22f, 0.95f));
        UIGen.Center(gridBg.rectTransform, cols * 150 + 40, rows * 130 + 40, -120, -40);
        gridContainer = gridBg.rectTransform;
        glg = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(140, 120);
        glg.spacing = new Vector2(10, 10);
        glg.padding = new RectOffset(20, 20, 20, 20);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = cols;

        // 格子模板（隐藏，运行时克隆）
        cellPrefab = MakeCellTemplate(transform);

        // 右侧 4 个货物箱（竖排，位置可后续手动微调）
        boxes = new ShopHelpBoxUI[4];
        for(int i = 0; i < 4; i++)
            boxes[i] = MakeBox(transform, i);

        // 拖拽层（最上层，全屏；货物图标拖拽时挂到这里）。仅需 RectTransform，不放 Graphic 以免拦截射线。
        dragLayer = UIGen.Node("DragLayer", transform);
        UIGen.Stretch(dragLayer);

        // 左下角色台词（AvatarPortraitPop 预制体）
        GameObject avatarGo = UIGen.InstantiatePrefab(GuidAvatarPortraitPop, transform);
        if(avatarGo != null)
        {
            RectTransform art = (RectTransform)avatarGo.transform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0, 0);
            art.anchoredPosition = new Vector2(30, 30);
            npcPop = avatarGo.GetComponent<AvatarPortraitPop>();
        }

        // 游戏说明（底部）
        helpText = UIGen.Loc("Help", transform, "ShopHelpHelp", 22, new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Left);
        UIGen.Anchor(helpText.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), 700, 30, 470, 16);

        // 结束本局（右下）
        endButton = UIGen.Button("EndButton", transform, "ShopHelpEnd", new Color(0.95f, 0.95f, 0.95f), Color.black);
        UIGen.Anchor((RectTransform)endButton.transform, new Vector2(1, 0), new Vector2(1, 0), 220, 72, -30, 30);

        // 内嵌结算面板（默认隐藏，胜/败时激活）
        RectTransform settleRt = UIGen.Node("SettlePanel", transform);
        UIGen.Stretch(settleRt);
        settlePanel = settleRt.gameObject.AddComponent<ShopHelpSettlePanel>();
        settlePanel.EditorBuild(GuidAvatarPortraitPop);

        EditorUtility.SetDirty(this);
        Debug.Log("[ShopHelpPanel] 界面已生成，请按需调整样式/位置，并给结算窗口/角色头像赋图。", this);
    }

    // 货架格子模板：底框 + 货物图标(默认隐藏)
    ShopHelpItemCellUI MakeCellTemplate(Transform parent)
    {
        Image bg = UIGen.Img("CellTemplate", parent, new Color(0.85f, 0.82f, 0.75f));
        bg.raycastTarget = false;
        ShopHelpItemCellUI cell = bg.gameObject.AddComponent<ShopHelpItemCellUI>();

        Image icon = UIGen.Img("Icon", bg.transform, Color.white);
        icon.raycastTarget = false;
        icon.enabled = false;
        UIGen.Stretch(icon.rectTransform);

        SerializedObject so = new SerializedObject(cell);
        so.FindProperty("bg").objectReferenceValue = bg;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.ApplyModifiedProperties();

        bg.gameObject.SetActive(false);   // 模板隐藏，运行时克隆并激活
        return cell;
    }

    // 一个货物箱：箱体 + 可拖拽的货物图标根(图标 + 数量)。竖排在右侧。
    ShopHelpBoxUI MakeBox(Transform parent, int index)
    {
        Image box = UIGen.Img("Box" + index, parent, new Color(0.85f, 0.72f, 0.5f, 1f));
        box.raycastTarget = false;
        UIGen.Anchor(box.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 170, 130, -120, 180 - index * 150);
        ShopHelpBoxUI boxUI = box.gameObject.AddComponent<ShopHelpBoxUI>();

        RectTransform iconRoot = UIGen.Node("IconRoot", box.transform);
        iconRoot.anchorMin = iconRoot.anchorMax = iconRoot.pivot = new Vector2(0.5f, 0.5f);
        iconRoot.sizeDelta = new Vector2(120, 120);
        iconRoot.anchoredPosition = Vector2.zero;

        Image icon = iconRoot.gameObject.AddComponent<Image>();   // 货物图标（可拖拽，射线目标）
        icon.color = Color.white;
        icon.raycastTarget = true;

        TMP_Text count = UIGen.Text("Count", iconRoot, "0", 30, Color.white, TextAlignmentOptions.BottomRight);
        RectTransform countRt = count.rectTransform;
        countRt.anchorMin = countRt.anchorMax = countRt.pivot = new Vector2(1, 0);
        countRt.sizeDelta = new Vector2(60, 40);
        countRt.anchoredPosition = new Vector2(4, -4);
        count.fontStyle = FontStyles.Bold;

        SerializedObject so = new SerializedObject(boxUI);
        so.FindProperty("boxImage").objectReferenceValue = box;
        so.FindProperty("iconRoot").objectReferenceValue = iconRoot;
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("countText").objectReferenceValue = count;
        so.ApplyModifiedProperties();

        return boxUI;
    }
    #endregion
#endif
}
