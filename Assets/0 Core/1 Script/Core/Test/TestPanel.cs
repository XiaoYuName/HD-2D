using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XFramework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// 管理员 / GM 测试面板：方便地加物品 / 加金钱，走项目标准 UISystem 打开（由 <see cref="TestManager"/> 用 F1 开关）。
///
/// 界面用编辑器按钮「CreateUI」一键生成并自动连好全部引用（也可自己在预制体里手搭后把控件拖到下面字段）。
///
/// 纯 Inspector 扩展（无需改代码）：
///   • 快捷加钱按钮：Button.OnClick 连 AddMoney(int) / SubMoney(int) / AddGameCoin(int) / SubGameCoin(int)，参数填数值。
///   • 快捷加某物品：Button.OnClick 连 AddItemQuick(int)，参数填物品ID（数量取「数量输入框」）。
///   • 加页签：页签按钮塞 tabButtons、对应页面塞 tabPages（一一对应）。
/// 写代码扩展见文件末尾「扩展入口」。
/// </summary>
public class TestPanel : UIBase
{
    [Title("通用")]
    [LabelText("关闭按钮")][SerializeField] Button closeButton;
    [LabelText("金币显示文本")][SerializeField] TMP_Text moneyText;
    [LabelText("游戏币显示文本")][SerializeField] TMP_Text gameCoinText;
    [LabelText("状态提示文本")][SerializeField] TMP_Text statusText;

    [Title("页签(按钮与页面一一对应)")]
    [LabelText("页签按钮")][SerializeField] Button[] tabButtons;
    [LabelText("页签页面")][SerializeField] GameObject[] tabPages;

    [Title("物品")]
    [LabelText("物品ID输入框")][SerializeField] TMP_InputField itemIdInput;
    [LabelText("数量输入框(空/非法按1)")][SerializeField] TMP_InputField itemCountInput;
    [LabelText("按ID添加按钮")][SerializeField] Button addItemButton;
    [LabelText("搜索输入框")][SerializeField] TMP_InputField searchInput;
    [LabelText("搜索按钮")][SerializeField] Button searchButton;
    [LabelText("目录容器(ScrollRect的Content)")][SerializeField] Transform itemListContent;
    [LabelText("目录行模板(隐藏)")][SerializeField] TestItemSlot itemSlotTemplate;
    [LabelText("目录最多显示条数(0=不限)")][SerializeField] int catalogLimit = 100;

    [Title("货币")]
    [LabelText("金币输入框")][SerializeField] TMP_InputField moneyInput;
    [LabelText("游戏币输入框")][SerializeField] TMP_InputField gameCoinInput;

    #region 生命周期
    // UISystem 加载完预制体后调用一次；逻辑集中在此（Init 一定会被框架调用）
    public override void Init()
    {
        closeButton.onClick.AddListener(OnClose);
        addItemButton.onClick.AddListener(AddItemById);
        searchButton.onClick.AddListener(OnSearch);
        searchInput.onEndEdit.AddListener(_ => OnSearch());

        WireTabs();

        itemSlotTemplate.gameObject.SetActive(false);
        SelectTab(0);
    }

    public override void Open()
    {
        base.Open();
        RefreshCurrency();
        RebuildCatalog(searchInput.text);
        SetStatus(string.Empty);
    }

    void Update()
    {
        if (isOpen) RefreshCurrency();
    }
    #endregion

    #region 页签
    void WireTabs()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int idx = i;
            tabButtons[i].onClick.AddListener(() => SelectTab(idx));
        }
    }

    /// <summary>切换到第 index 个页签页面（可被外部/按钮调用）。</summary>
    public void SelectTab(int index)
    {
        for (int i = 0; i < tabPages.Length; i++)
            tabPages[i].SetActive(i == index);
    }
    #endregion

    #region 物品
    ItemConfig Config => InventoryManager.Instance.Config;

    /// <summary>按「物品ID输入框」+「数量输入框」添加物品（绑定在添加按钮上）。</summary>
    public void AddItemById()
    {
        if (!long.TryParse(itemIdInput.text, out long id) || id <= 0) { SetStatus("请输入有效物品ID"); return; }
        AddItemInternal(id, CurrentCount());
    }

    /// <summary>快捷添加指定ID物品（供预制体上的快捷按钮 OnClick 连接，数量取「数量输入框」）。</summary>
    public void AddItemQuick(int id) => AddItemInternal(id, CurrentCount());

    /// <summary>批量添加一段连续ID（跳过配置里不存在的ID）；供代码扩展调用。</summary>
    public void AddItemRange(long startId, long endId, int count)
    {
        int kinds = 0;
        for (long id = startId; id <= endId; id++)
            if (InventoryManager.Instance.GetItemData(id) != null)
            {
                InventoryManager.Instance.AddItem(id, count);
                kinds++;
            }
        SetStatus($"批量添加 {kinds} 种 x{count}");
        RefreshCurrency();
    }

    void AddItemInternal(long id, int count)
    {
        if (InventoryManager.Instance.GetItemData(id) == null) { SetStatus($"未找到物品配置 ID={id}"); return; }
        InventoryManager.Instance.AddItem(id, count);
        SetStatus($"已添加 [{id}] x{count}");
        RefreshCurrency();
    }

    /// <summary>按当前搜索框内容刷新目录（绑定在搜索按钮 / 搜索框回车）。</summary>
    public void OnSearch() => RebuildCatalog(searchInput.text);

    // 用 ItemConfig 重建目录：清掉旧行（模板保留），按 filter(匹配ID/备注/名称Key) 排序后克隆模板填充
    void RebuildCatalog(string filter)
    {
        for (int i = itemListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = itemListContent.GetChild(i);
            if (child == itemSlotTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        filter = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();

        List<ItemData> list = new();
        foreach (ItemData d in Config.ItemDataDict.Values)
        {
            if (filter != null
                && !d.Id.ToString().Contains(filter)
                && (d.Remark == null || !d.Remark.Contains(filter))
                && (d.NameKey == null || !d.NameKey.Contains(filter)))
                continue;
            list.Add(d);
        }
        list.Sort((a, b) => a.Id.CompareTo(b.Id));

        int limit = catalogLimit > 0 ? catalogLimit : list.Count;
        int shown = Mathf.Min(limit, list.Count);
        for (int i = 0; i < shown; i++)
        {
            TestItemSlot slot = Instantiate(itemSlotTemplate, itemListContent);
            slot.gameObject.SetActive(true);
            slot.Set(list[i], d => AddItemInternal(d.Id, CurrentCount()));
        }

        SetStatus(list.Count > shown ? $"匹配 {list.Count} 项，显示前 {shown}，可搜索缩小范围" : $"显示 {shown} 项");
    }

    int CurrentCount() => ParseInt(itemCountInput, 1);
    #endregion

    #region 货币
    // 供预制体上的快捷按钮 OnClick 直接连接（参数在 Inspector 里填常量，如 1000 / 10000）
    public void AddMoney(int value) => ChangeMoney(value, true);
    public void SubMoney(int value) => ChangeMoney(value, false);
    public void AddGameCoin(int value) => ChangeGameCoin(value, true);
    public void SubGameCoin(int value) => ChangeGameCoin(value, false);

    // 供「金额输入框 + 增加/扣除」按钮连接
    public void AddMoneyFromInput() => ChangeMoney(ParseInt(moneyInput, 0), true);
    public void SubMoneyFromInput() => ChangeMoney(ParseInt(moneyInput, 0), false);
    public void AddGameCoinFromInput() => ChangeGameCoin(ParseInt(gameCoinInput, 0), true);
    public void SubGameCoinFromInput() => ChangeGameCoin(ParseInt(gameCoinInput, 0), false);

    void ChangeMoney(int value, bool add)
    {
        if (value <= 0) return;
        if (add) InventoryManager.Instance.AddMoney(value);
        else InventoryManager.Instance.SubMoney(value);
        SetStatus($"金币 {(add ? "+" : "-")}{value}");
        RefreshCurrency();
    }

    void ChangeGameCoin(int value, bool add)
    {
        if (value <= 0) return;
        if (add) InventoryManager.Instance.AddGameCoin(value);
        else InventoryManager.Instance.SubGameCoin(value);
        SetStatus($"游戏币 {(add ? "+" : "-")}{value}");
        RefreshCurrency();
    }

    void RefreshCurrency()
    {
        moneyText.text = $"金币: {InventoryManager.Instance.Money}";
        gameCoinText.text = $"游戏币: {InventoryManager.Instance.GameCoin}";
    }
    #endregion

    #region 批量测试道具
    /// <summary>【工厂】按类型发放：框架(FigureModel)各1、贴纸(Painting)各5 到背包（同 FactoryMoldMgPanel 测试逻辑）。</summary>
    public void AddFactoryTestItems()
    {
        InventoryManager bag = InventoryManager.Instance;
        int frameKinds = 0, stickerKinds = 0;
        foreach (ItemData item in bag.Config.ItemDataDict.Values)
        {
            if (item.Type == ItemType.FigureModel) { bag.AddItem(item.Id, 1); frameKinds++; }      // 框架不消耗，1 个够测
            else if (item.Type == ItemType.Painting) { bag.AddItem(item.Id, 5); stickerKinds++; }   // 贴纸会被消耗，多给几个
        }
        SetStatus($"工厂测试道具：框架 {frameKinds} 种、贴纸 {stickerKinds} 种");
    }

    /// <summary>【厨房】按几个配方所需食材各备一份，便于测试合成（同 PlayerBag.AddTestFoodMtItems2）。</summary>
    public void AddKitchenTestItems()
    {
        InventoryManager bag = InventoryManager.Instance;
        // 蛋炒饭(110001)
        bag.AddItem(100000, 2); // 米饭
        bag.AddItem(100006, 2); // 鸡蛋
        // 八宝菜(110002)
        bag.AddItem(100007, 2); // 猪肉
        bag.AddItem(100021, 2); // 鱿鱼
        bag.AddItem(100013, 2); // 萝卜
        bag.AddItem(100012, 2); // 香菇
        // 香煎鱼(110007)
        bag.AddItem(100036, 2); // 青花鱼
        // 味增汤(110018)
        bag.AddItem(100001, 2); // 豆腐
        bag.AddItem(100015, 2); // 海苔
        // 调料
        bag.AddItem(100040, 5); // 食用盐
        bag.AddItem(100043, 3); // 高汤
        SetStatus("厨房测试食材已发放");
    }
    #endregion

    #region 工具
    void OnClose() => UISystem.Instance.CloseUI(uiname);

    int ParseInt(TMP_InputField field, int fallback) =>
        int.TryParse(field.text, out int v) && v > 0 ? v : fallback;

    void SetStatus(string msg)
    {
        statusText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[TestPanel] {msg}");
    }
    #endregion

    #region 扩展入口
    // 写代码新增测试功能时，在此加 public 方法后：
    //   · 无参 / 单个 int|float|string|bool 参数 → 可直接在预制体里用 Button.OnClick 连接；
    //   · 需要多参数（如加一段ID区间）→ 包一个无参方法调用 AddItemRange(...) 再连按钮。
    // 示例：
    // public void AddFoodTestItems()
    // {
    //     AddItemRange(600000, 600019, 9); // 蔬菜
    //     AddItemRange(610000, 610019, 9); // 鱼类
    //     AddItemRange(620000, 620009, 9); // 调料
    // }
    #endregion

#if UNITY_EDITOR
    #region 编辑器一键生成
    static readonly Color ColWindow = new Color(0.10f, 0.11f, 0.14f, 0.96f);
    static readonly Color ColBlue = new Color(0.20f, 0.45f, 0.85f, 1f);
    static readonly Color ColGreen = new Color(0.28f, 0.55f, 0.35f, 1f);
    static readonly Color ColRed = new Color(0.70f, 0.35f, 0.30f, 1f);
    static readonly Color ColTab = new Color(0.25f, 0.27f, 0.32f, 1f);
    static readonly Color ColField = new Color(1f, 1f, 1f, 0.12f);

    [PropertySpace(10)]
    [Button("CreateUI (一键生成界面)", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
    [InfoBox("在本面板(TestPanel)下生成完整测试界面并自动连好全部引用。重复点击会先清空重建。生成后可自行调整样式/位置；中文若显示为方块，把生成出的 TMP 文本字体换成 zh-cn SDF。", InfoMessageType.Info)]
    void CreateUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        FactoryUIGen.Stretch((RectTransform)transform);

        // 背景 + 窗口
        FactoryUIGen.Stretch(FactoryUIGen.Img("Backdrop", transform, new Color(0, 0, 0, 0.55f)).rectTransform);
        RectTransform window = FactoryUIGen.Node("Window", transform);
        window.anchorMin = new Vector2(0.06f, 0.06f);
        window.anchorMax = new Vector2(0.94f, 0.94f);
        window.offsetMin = Vector2.zero;
        window.offsetMax = Vector2.zero;
        window.gameObject.AddComponent<Image>().color = ColWindow;

        // 标题
        TopBand(FactoryUIGen.Text("Title", window, "管理员测试面板", 34, Color.white, TextAlignmentOptions.Left).rectTransform, 56, 14, 24, 90);

        // 关闭
        closeButton = PlainBtn("Close", window, "✕", ColRed);
        RectTransform cr = (RectTransform)closeButton.transform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1, 1);
        cr.sizeDelta = new Vector2(56, 56);
        cr.anchoredPosition = new Vector2(-14, -14);

        // 货币显示
        RectTransform curBand = FactoryUIGen.Node("Currency", window);
        TopBand(curBand, 40, 78, 24, 24);
        HorizontalRow(curBand);
        moneyText = FactoryUIGen.Text("Money", curBand, "金币: 0", 26, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Left);
        gameCoinText = FactoryUIGen.Text("GameCoin", curBand, "游戏币: 0", 26, new Color(0.6f, 0.85f, 1f), TextAlignmentOptions.Left);

        // 页签栏
        RectTransform tabBar = FactoryUIGen.Node("TabBar", window);
        TopBand(tabBar, 56, 124, 16, 16);
        HorizontalRow(tabBar);
        Button itemTab = PlainBtn("ItemTab", tabBar, "物品", ColTab); Size(itemTab, 0, 1);
        Button coinTab = PlainBtn("CoinTab", tabBar, "货币", ColTab); Size(coinTab, 0, 1);
        tabButtons = new[] { itemTab, coinTab };

        // 内容区
        RectTransform content = FactoryUIGen.Node("Content", window);
        FillArea(content, 190, 52, 16, 16);

        RectTransform itemPage = BuildItemPage(content);
        RectTransform coinPage = BuildCoinPage(content);
        tabPages = new[] { itemPage.gameObject, coinPage.gameObject };

        // 状态栏
        statusText = FactoryUIGen.Text("Status", window, string.Empty, 22, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Left);
        BottomBand(statusText.rectTransform, 36, 10, 24, 24);

        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(gameObject);
        Debug.Log("[TestPanel] 界面已生成并连好引用。若中文显示异常，把生成的 TMP 文本字体改为 zh-cn SDF。", this);
    }

    RectTransform BuildItemPage(Transform content)
    {
        RectTransform page = FactoryUIGen.Node("ItemPage", content);
        FactoryUIGen.Stretch(page);

        // 精确添加行
        RectTransform r1 = MakeRow(page, 62);
        TopBand(r1, 62, 4, 0, 0);
        Size(FactoryUIGen.Text("L", r1, "ID", 24, Color.white, TextAlignmentOptions.MidlineLeft), 44, 0);
        itemIdInput = MakeInput("IdInput", r1, string.Empty, TMP_InputField.ContentType.IntegerNumber); Size(itemIdInput, 200, 0);
        Size(FactoryUIGen.Text("L", r1, "数量", 24, Color.white, TextAlignmentOptions.MidlineLeft), 64, 0);
        itemCountInput = MakeInput("CountInput", r1, "1", TMP_InputField.ContentType.IntegerNumber); Size(itemCountInput, 110, 0);
        addItemButton = PlainBtn("AddItem", r1, "添加", ColBlue); Size(addItemButton, 150, 1);

        // 搜索行
        RectTransform r2 = MakeRow(page, 56);
        TopBand(r2, 56, 72, 0, 0);
        searchInput = MakeInput("Search", r2, string.Empty, TMP_InputField.ContentType.Standard); Size(searchInput, 200, 1);
        searchButton = PlainBtn("SearchBtn", r2, "搜索/刷新", ColBlue); Size(searchButton, 170, 0);

        // 批量测试道具行
        RectTransform r3 = MakeRow(page, 52);
        TopBand(r3, 52, 134, 0, 0);
        Wire(PlainBtn("FactoryTest", r3, "工厂框架/贴纸", ColGreen), AddFactoryTestItems, 0, 1);
        Wire(PlainBtn("KitchenTest", r3, "厨房食材", ColGreen), AddKitchenTestItems, 0, 1);

        // 目录（下方铺满）
        RectTransform listContent = FactoryUIGen.Node("ItemListContent", page);
        FillArea(listContent, 196, 6, 6, 6);
        VerticalList(listContent);
        itemSlotTemplate = BuildItemRowTemplate(listContent);
        itemListContent = FactoryUIGen.WrapInScrollView(listContent).content;

        return page;
    }

    TestItemSlot BuildItemRowTemplate(Transform parent)
    {
        RectTransform row = MakeRow(parent, 54);
        TMP_Text label = FactoryUIGen.Text("Label", row, "物品", 22, Color.white, TextAlignmentOptions.MidlineLeft);
        label.overflowMode = TextOverflowModes.Ellipsis;
        Size(label, 200, 1);
        Button add = PlainBtn("Add", row, "+", ColGreen);
        Size(add, 90, 0);

        TestItemSlot slot = row.gameObject.AddComponent<TestItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("addButton").objectReferenceValue = add;
        so.ApplyModifiedProperties();

        row.gameObject.SetActive(false);
        return slot;
    }

    RectTransform BuildCoinPage(Transform content)
    {
        RectTransform page = FactoryUIGen.Node("CoinPage", content);
        FactoryUIGen.Stretch(page);

        // 金币
        RectTransform mRow = MakeRow(page, 64);
        TopBand(mRow, 64, 8, 0, 0);
        Size(FactoryUIGen.Text("L", mRow, "金币", 24, Color.white, TextAlignmentOptions.MidlineLeft), 100, 0);
        moneyInput = MakeInput("MoneyInput", mRow, "1000", TMP_InputField.ContentType.IntegerNumber); Size(moneyInput, 200, 0);
        Wire(PlainBtn("Add", mRow, "增加", ColGreen), AddMoneyFromInput, 120, 1);
        Wire(PlainBtn("Sub", mRow, "扣除", ColRed), SubMoneyFromInput, 120, 1);

        RectTransform mQuick = MakeRow(page, 52);
        TopBand(mQuick, 52, 80, 0, 0);
        WireInt(PlainBtn("Q1", mQuick, "+1000", ColBlue), AddMoney, 1000, 0, 1);
        WireInt(PlainBtn("Q2", mQuick, "+1万", ColBlue), AddMoney, 10000, 0, 1);
        WireInt(PlainBtn("Q3", mQuick, "+10万", ColBlue), AddMoney, 100000, 0, 1);

        // 游戏币
        RectTransform gRow = MakeRow(page, 64);
        TopBand(gRow, 64, 144, 0, 0);
        Size(FactoryUIGen.Text("L", gRow, "游戏币", 24, Color.white, TextAlignmentOptions.MidlineLeft), 100, 0);
        gameCoinInput = MakeInput("CoinInput", gRow, "1000", TMP_InputField.ContentType.IntegerNumber); Size(gameCoinInput, 200, 0);
        Wire(PlainBtn("Add", gRow, "增加", ColGreen), AddGameCoinFromInput, 120, 1);
        Wire(PlainBtn("Sub", gRow, "扣除", ColRed), SubGameCoinFromInput, 120, 1);

        RectTransform gQuick = MakeRow(page, 52);
        TopBand(gQuick, 52, 216, 0, 0);
        WireInt(PlainBtn("Q1", gQuick, "+1000", ColBlue), AddGameCoin, 1000, 0, 1);
        WireInt(PlainBtn("Q2", gQuick, "+1万", ColBlue), AddGameCoin, 10000, 0, 1);
        WireInt(PlainBtn("Q3", gQuick, "+10万", ColBlue), AddGameCoin, 100000, 0, 1);

        return page;
    }

    // ---- 生成小工具 ----
    Button PlainBtn(string name, Transform parent, string text, Color bg)
    {
        Image img = FactoryUIGen.Img(name, parent, bg);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        FactoryUIGen.Stretch(FactoryUIGen.Text("Text", img.transform, text, 24, Color.white, TextAlignmentOptions.Center).rectTransform);
        return b;
    }

    TMP_InputField MakeInput(string name, Transform parent, string initial, TMP_InputField.ContentType type)
    {
        Image bg = FactoryUIGen.Img(name, parent, ColField);
        TMP_InputField input = bg.gameObject.AddComponent<TMP_InputField>();

        RectTransform area = FactoryUIGen.Node("TextArea", bg.transform);
        FactoryUIGen.Stretch(area);
        area.offsetMin = new Vector2(10, 6);
        area.offsetMax = new Vector2(-10, -6);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholder = FactoryUIGen.Text("Placeholder", area, string.Empty, 22, new Color(1, 1, 1, 0.4f), TextAlignmentOptions.MidlineLeft);
        FactoryUIGen.Stretch(placeholder.rectTransform);
        TMP_Text txt = FactoryUIGen.Text("Text", area, string.Empty, 22, Color.white, TextAlignmentOptions.MidlineLeft);
        FactoryUIGen.Stretch(txt.rectTransform);

        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = placeholder;
        input.contentType = type;
        input.text = initial;
        return input;
    }

    RectTransform MakeRow(Transform parent, float height)
    {
        RectTransform row = FactoryUIGen.Node("Row", parent);
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        HorizontalRow(row).spacing = 8;
        return row;
    }

    static HorizontalLayoutGroup HorizontalRow(RectTransform rt)
    {
        HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.padding = new RectOffset(4, 4, 2, 2);
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.childAlignment = TextAnchor.MiddleLeft;
        return h;
    }

    static void VerticalList(RectTransform rt)
    {
        VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.padding = new RectOffset(6, 6, 6, 6);
        v.childControlWidth = true;
        v.childForceExpandWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
    }

    static void Size(Component c, float pref, float flex)
    {
        LayoutElement le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = pref;
        le.flexibleWidth = flex;
        if (flex <= 0) le.minWidth = pref;
    }

    void Wire(Button b, UnityAction call, float pref, float flex)
    {
        UnityEventTools.AddPersistentListener(b.onClick, call);
        Size(b, pref, flex);
    }

    void WireInt(Button b, UnityAction<int> call, int arg, float pref, float flex)
    {
        UnityEventTools.AddIntPersistentListener(b.onClick, call, arg);
        Size(b, pref, flex);
    }

    static void TopBand(RectTransform rt, float height, float yFromTop, float left, float right)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(left, -(yFromTop + height));
        rt.offsetMax = new Vector2(-right, -yFromTop);
    }

    static void BottomBand(RectTransform rt, float height, float yFromBottom, float left, float right)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(left, yFromBottom);
        rt.offsetMax = new Vector2(-right, yFromBottom + height);
    }

    static void FillArea(RectTransform rt, float top, float bottom, float left, float right)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
    #endregion
#endif
}
