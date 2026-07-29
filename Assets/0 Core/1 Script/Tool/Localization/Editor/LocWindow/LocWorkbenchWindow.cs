#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using UIColumn = UnityEngine.UIElements.Column;

/// <summary>
/// 多语言工作台（UIToolkit）：左侧列出工程内全部字符串表集合（可搜索），右侧是工作区——
/// 给选中的表逐个显式关联 CSV 文件（一表多 CSV、每面板一份，对象引用存 <see cref="LocWorkbenchConfig"/>），
/// 可视化查看/行级编辑各 CSV，并做导入（增量 / 清空重建）、跨 CSV 重复 Key 与表内孤儿 Key 检测。
/// 合并核心复用 <see cref="LocCsvMerger"/>，CSV 读写见 <see cref="LocCsvDoc"/>，样式见同目录 LocWorkbench.uss。
/// </summary>
public class LocWorkbenchWindow : EditorWindow
{
    const string UssPath = "Assets/0 Core/1 Script/Tool/Localization/Editor/LocWindow/LocWorkbench.uss";
    const string LocCsvToolPath = "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1";
    const uint FoDelete = 3;
    const ushort FofAllowUndo = 0x0040;
    const ushort FofNoConfirmation = 0x0010;
    const ushort FofSilent = 0x0004;

    [System.Serializable]
    sealed class LocCsvBatchOp
    {
        public string action;
        public string csv;
        public string key;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShellFileOperation
    {
        public System.IntPtr hwnd;
        public uint func;
        public string from;
        public string to;
        public ushort flags;
        public bool aborted;
        public System.IntPtr nameMappings;
        public string progressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref ShellFileOperation operation);

    // ---- 左侧：表列表 ----
    ListView tableList;
    ToolbarSearchField tableSearch;
    List<StringTableCollection> allCollections = new();   // 工程内全部
    List<StringTableCollection> collections = new();      // 搜索过滤后（tableList.itemsSource）

    // ---- 右侧：工作区 ----
    VisualElement workArea;
    Label tableTitle;
    VisualElement csvSection;
    ListView csvList;
    Label csvPlaceholder;
    MultiColumnListView grid;
    ToolbarSearchField searchField;
    Toggle autoImportToggle;
    Button saveBtn;
    Label status;
    TextField scanPathField;
    TextField newTableNameField;
    Label scanStatus;
    Label tableCacheLabel;
    VisualElement csvContextMenu;
    VisualElement tableContextMenu;
    string contextCsvPath;
    StringTableCollection contextTable;

    List<string> csvPaths = new();     // 当前表已关联的本地化 CSV（工程相对路径）
    LocCsvDoc doc;                     // 当前打开的 CSV 文档
    StringTableCollection docCollection;   // doc 所属的表（selectionChanged 在选中变化后才触发，保存导入必须用它而非 Selected）
    List<LocCsvDoc.Row> filtered = new();
    bool dirty;
    int prevTableIndex = -1;           // ConfirmDiscard 取消时回滚选择用
    int prevCsvIndex = -1;

    StringTableCollection Selected
        => tableList.selectedIndex >= 0 && tableList.selectedIndex < collections.Count
            ? collections[tableList.selectedIndex] : null;

    [MenuItem("Tools/Loc/多语言工作台")]
    static void Open() => GetWindow<LocWorkbenchWindow>("多语言工作台").minSize = new Vector2(900, 480);

    void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if(uss != null)
            root.styleSheets.Add(uss);
        else
            Debug.LogError($"[Loc工作台] 样式表加载失败（界面会退化为无样式）：{UssPath}");
        SetupDrop(root);
        BuildCsvContextMenu(root);
        BuildTableContextMenu(root);

        var tabBar = new VisualElement();
        tabBar.AddToClassList("tab-bar");
        root.Add(tabBar);

        var contentArea = new VisualElement { style = { flexGrow = 1f } };
        root.Add(contentArea);

        var workbenchContent = new VisualElement { style = { flexGrow = 1f } };
        var toolsContent = new VisualElement { style = { flexGrow = 1f } };
        var settingsContent = new VisualElement { style = { flexGrow = 1f } };
        contentArea.Add(workbenchContent);
        contentArea.Add(toolsContent);
        contentArea.Add(settingsContent);
        BuildToolsTab(toolsContent);
        BuildSettingsTab(settingsContent);

        var workbenchTabBtn = new Button { text = "工作台" };
        workbenchTabBtn.AddToClassList("tab-btn");
        var toolsTabBtn = new Button { text = "工具" };
        toolsTabBtn.AddToClassList("tab-btn");
        var settingsTabBtn = new Button { text = "设置" };
        settingsTabBtn.AddToClassList("tab-btn");
        tabBar.Add(workbenchTabBtn);
        tabBar.Add(toolsTabBtn);
        tabBar.Add(settingsTabBtn);

        void SwitchTab(int index)
        {
            workbenchContent.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            toolsContent.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            settingsContent.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            workbenchTabBtn.EnableInClassList("tab-btn-active", index == 0);
            toolsTabBtn.EnableInClassList("tab-btn-active", index == 1);
            settingsTabBtn.EnableInClassList("tab-btn-active", index == 2);
        }
        workbenchTabBtn.clicked += () => SwitchTab(0);
        toolsTabBtn.clicked += () => SwitchTab(1);
        settingsTabBtn.clicked += () => SwitchTab(2);
        SwitchTab(0);

        var split = new TwoPaneSplitView(0, 230, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1f;
        workbenchContent.Add(split);

        // ===== 左侧：字符串表列表 =====
        var left = new VisualElement();
        left.AddToClassList("side-panel");

        var sideHeader = new VisualElement();
        sideHeader.AddToClassList("side-header");
        var title = new Label("字符串表");
        title.AddToClassList("section-title");
        sideHeader.Add(title);
        var refreshBtn = new Button(RefreshTables) { text = "⟳", tooltip = "重新扫描工程内的字符串表" };
        refreshBtn.AddToClassList("icon-btn");
        sideHeader.Add(refreshBtn);
        left.Add(sideHeader);

        tableSearch = new ToolbarSearchField();
        tableSearch.AddToClassList("search-field");
        tableSearch.RegisterValueChangedCallback(_ => ApplyTableFilter());
        left.Add(tableSearch);

        tableList = new ListView
        {
            fixedItemHeight = 26,
            makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("table-item");
                var dot = new Label("●");
                dot.AddToClassList("table-item-dot");
                var name = new Label();
                name.AddToClassList("table-item-name");
                row.Add(dot);
                row.Add(name);
                row.RegisterCallback<PointerUpEvent>(evt => ShowTableContextMenu(row, evt));
                return row;
            },
            bindItem = (ve, i) =>
            {
                StringTableCollection col = collections[i];
                bool mapped = LocWorkbenchConfig.St.GetCsvFiles(col.TableCollectionName).Count > 0;
                var dot = (Label)ve[0];
                var name = (Label)ve[1];
                dot.tooltip = mapped ? "已关联 CSV" : "未关联 CSV";
                dot.EnableInClassList("dot-on", mapped);
                dot.EnableInClassList("dot-off", !mapped);
                name.text = col.TableCollectionName;
                name.style.opacity = mapped ? 1f : 0.6f;
                ve.userData = col;
            },
        };
        tableList.AddToClassList("table-list");
        tableList.selectionChanged += _ => OnTableSelected();
        tableList.RegisterCallback<KeyDownEvent>(OnTableKeyDown);
        left.Add(tableList);
        split.Add(left);

        // ===== 右侧：工作区 =====
        workArea = new VisualElement();
        workArea.AddToClassList("work-area");
        split.Add(workArea);

        tableTitle = new Label();
        tableTitle.AddToClassList("page-title");
        workArea.Add(tableTitle);

        // 配置卡片：表级操作（CSV 关联在下方「本表 CSV」栏用 ＋/－ 管理）
        var configCard = new VisualElement();
        configCard.AddToClassList("card");

        var actions = new VisualElement();
        actions.AddToClassList("btn-row");
        actions.Add(Btn("增量导入全部 CSV", () => ImportAll(clearFirst: false)));
        actions.Add(Btn("重建导入（清空表后导入）", () => ImportAll(clearFirst: true), "btn-danger"));
        actions.Add(Btn("检测重复 / 孤儿 Key", AnalyzeCsvs));
        actions.Add(Btn("新建 CSV", CreateCsv));
        configCard.Add(actions);
        workArea.Add(configCard);

        // CSV 列表 + 表格编辑区
        csvSection = new VisualElement { style = { flexGrow = 1f } };
        workArea.Add(csvSection);

        var innerSplit = new TwoPaneSplitView(0, 190, TwoPaneSplitViewOrientation.Horizontal);
        innerSplit.style.flexGrow = 1f;
        csvSection.Add(innerSplit);

        var csvPane = new VisualElement();
        csvPane.AddToClassList("csv-pane");
        var csvHeader = new VisualElement();
        csvHeader.AddToClassList("side-header");
        var csvTitle = new Label("本表 CSV");
        csvTitle.AddToClassList("section-title");
        csvHeader.Add(csvTitle);
        var addCsvBtn = new Button(AddCsvDialog) { text = "＋", tooltip = "关联一份已有 CSV 文件" };
        addCsvBtn.AddToClassList("icon-btn");
        csvHeader.Add(addCsvBtn);
        var removeCsvBtn = new Button(RemoveSelectedCsv) { text = "－", tooltip = "取消选中 CSV 与本表的关联（不删除文件）" };
        removeCsvBtn.AddToClassList("icon-btn");
        csvHeader.Add(removeCsvBtn);
        csvPane.Add(csvHeader);
        csvList = new ListView
        {
            fixedItemHeight = 24,
            makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("table-item");
                label.RegisterCallback<PointerUpEvent>(evt => ShowCsvContextMenu(label, evt));
                return label;
            },
            bindItem = (ve, i) =>
            {
                var label = (Label)ve;
                string path = csvPaths[i];
                label.text = Path.GetFileName(path);
                label.userData = path;
            },
        };
        csvList.AddToClassList("csv-list");
        csvList.selectionChanged += _ => OnCsvSelected();
        csvList.RegisterCallback<KeyDownEvent>(OnCsvKeyDown);
        csvPane.Add(csvList);
        innerSplit.Add(csvPane);

        var gridBox = new VisualElement();
        gridBox.AddToClassList("grid-area");
        innerSplit.Add(gridBox);

        var gridBar = new VisualElement();
        gridBar.AddToClassList("grid-toolbar");
        searchField = new ToolbarSearchField { style = { flexGrow = 1f, flexShrink = 1f } };
        searchField.RegisterValueChangedCallback(_ => ApplyFilter());
        gridBar.Add(searchField);
        gridBar.Add(Btn("＋ 加行", AddRow));
        gridBar.Add(Btn("－ 删选中行", DeleteRows, "btn-danger"));
        gridBar.Add(Btn("还原", ReloadCsv));
        saveBtn = Btn("保存 CSV", SaveCsv, "btn-primary");
        gridBar.Add(saveBtn);
        gridBox.Add(gridBar);

        autoImportToggle = new Toggle("保存后自动导入本表") { value = true };
        gridBox.Add(autoImportToggle);

        grid = new MultiColumnListView
        {
            fixedItemHeight = 24,
            selectionType = SelectionType.Multiple,
        };
        grid.AddToClassList("grid-view");
        gridBox.Add(grid);

        csvPlaceholder = new Label("← 在左侧选择一个 CSV");
        csvPlaceholder.AddToClassList("placeholder");
        gridBox.Add(csvPlaceholder);

        status = new Label();
        status.AddToClassList("status-bar");
        workArea.Add(status);

        RefreshTables();
    }

    static Button Btn(string text, System.Action onClick, string extraClass = null)
    {
        var b = new Button(onClick) { text = text };
        b.AddToClassList("btn");
        if(extraClass != null)
            b.AddToClassList(extraClass);
        return b;
    }

    // ================= 工具页 =================

    void BuildToolsTab(VisualElement tab)
    {
        var area = new VisualElement();
        area.AddToClassList("work-area");
        tab.Add(area);

        var title = new Label("工具");
        title.AddToClassList("page-title");
        area.Add(title);

        var card = new VisualElement();
        card.AddToClassList("card");
        card.Add(new Label("扫描工程内全部 String 表集合，给含 {占位符} 的文案自动勾选 Smart String。"));

        Label toolsStatus = null;
        var actions = new VisualElement();
        actions.AddToClassList("btn-row");
        actions.Add(Btn("给所有 String 表集合自动标记 Smart String", () =>
        {
            int n = AutoMarkSmartString.MarkAll();
            toolsStatus.text = $"✓ 本次新标记 {n} 个 Smart String 条目。";
        }));
        card.Add(actions);
        area.Add(card);

        toolsStatus = new Label();
        toolsStatus.AddToClassList("status-bar");
        area.Add(toolsStatus);
    }

    // ================= 设置页 =================

    void BuildSettingsTab(VisualElement tab)
    {
        var area = new VisualElement();
        area.AddToClassList("work-area");
        tab.Add(area);

        var title = new Label("设置");
        title.AddToClassList("page-title");
        area.Add(title);

        var card = new VisualElement();
        card.AddToClassList("card");
        card.Add(new Label("字符串表扫描路径（包含所有子目录）。路径与扫描结果缓存均保存到本目录的 LocWorkbenchConfig.asset。"));

        var pathRow = new VisualElement();
        pathRow.AddToClassList("path-row");
        scanPathField = new TextField("扫描路径")
        {
            value = LocWorkbenchConfig.St.GetScanPath(),
            isDelayed = true,
        };
        scanPathField.AddToClassList("scan-path-field");
        pathRow.Add(scanPathField);
        pathRow.Add(Btn("选择目录", SelectScanPath));
        card.Add(pathRow);

        var actions = new VisualElement();
        actions.AddToClassList("btn-row");
        actions.Add(Btn("保存并重新扫描", SaveScanPathAndRefresh, "btn-primary"));
        actions.Add(Btn("重新扫描", RefreshTables));
        card.Add(actions);

        tableCacheLabel = new Label();
        card.Add(tableCacheLabel);
        area.Add(card);

        var createCard = new VisualElement();
        createCard.AddToClassList("card");
        createCard.Add(new Label("新建字符串表会使用项目当前已配置的 Locale，并创建在扫描目录下以表名命名的子目录中。"));

        var createRow = new VisualElement();
        createRow.AddToClassList("path-row");
        newTableNameField = new TextField("表名") { isDelayed = true };
        newTableNameField.AddToClassList("table-name-field");
        createRow.Add(newTableNameField);
        createRow.Add(Btn("新建字符串表", CreateStringTable, "btn-primary"));
        createCard.Add(createRow);
        area.Add(createCard);

        scanStatus = new Label();
        scanStatus.AddToClassList("status-bar");
        area.Add(scanStatus);
        UpdateTableCacheLabel();
    }

    void SelectScanPath()
    {
        string startFolder = Path.GetFullPath(LocWorkbenchConfig.St.GetScanPath());
        string fullPath = EditorUtility.OpenFolderPanel("选择字符串表扫描目录", startFolder, "");
        if(string.IsNullOrEmpty(fullPath))
            return;
        scanPathField.SetValueWithoutNotify(ToAssetPath(fullPath));
        SaveScanPathAndRefresh();
    }

    void SaveScanPathAndRefresh()
    {
        string path = scanPathField.value;
        if(!LocWorkbenchConfig.St.CanSetScanPath(path))
        {
            scanStatus.text = "✗ 扫描路径必须是工程 Assets 目录内的有效文件夹。";
            return;
        }
        LocWorkbenchConfig.St.SetScanPath(path);
        scanPathField.SetValueWithoutNotify(LocWorkbenchConfig.St.GetScanPath());
        RefreshTables();
        scanStatus.text = $"✓ 已保存并扫描 {LocWorkbenchConfig.St.GetScanPath()}。";
    }

    void CreateStringTable()
    {
        string tableName = newTableNameField.value?.Trim();
        if(string.IsNullOrEmpty(tableName))
        {
            scanStatus.text = "✗ 请先填写表名。";
            return;
        }

        try
        {
            string directory = LocWorkbenchConfig.St.GetScanPath().TrimEnd('/') + "/" + tableName;
            StringTableCollection collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, directory);
            if(collection == null)
            {
                scanStatus.text = "✗ 字符串表创建失败。";
                return;
            }

            LocWorkbenchConfig.St.RefreshTableCache();
            RefreshTables();
            int index = collections.IndexOf(collection);
            if(index >= 0)
                tableList.SetSelection(index);
            newTableNameField.SetValueWithoutNotify("");
            scanStatus.text = $"✓ 已创建「{tableName}」并加入扫描缓存。";
        }
        catch(System.Exception e)
        {
            scanStatus.text = $"✗ 创建失败：{e.Message}";
        }
    }

    void UpdateTableCacheLabel()
    {
        if(tableCacheLabel != null)
            tableCacheLabel.text = $"缓存：{LocWorkbenchConfig.St.GetTableCache().Count} 个 String 表集合。";
    }

    // ================= 左侧表列表 =================

    void RefreshTables()
    {
        LocWorkbenchConfig.St.RefreshTableCache();
        allCollections = LocWorkbenchConfig.St.GetTableCache();
        UpdateTableCacheLabel();
        ApplyTableFilter();
    }

    void ApplyTableFilter()
    {
        string keep = Selected?.TableCollectionName;
        string q = tableSearch.value?.Trim();
        collections = string.IsNullOrEmpty(q)
            ? new List<StringTableCollection>(allCollections)
            : allCollections.Where(c => c.TableCollectionName.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        tableList.itemsSource = collections;
        tableList.Rebuild();

        int idx = collections.FindIndex(c => c.TableCollectionName == keep);
        tableList.SetSelectionWithoutNotify(idx >= 0 ? new[] { idx } : System.Array.Empty<int>());
        OnTableSelected();   // 无论选中是否保持都重刷右侧（CSV 目录内容可能已在外部变化）
    }

    void OnTableSelected()
    {
        if(!ConfirmDiscard())
        {
            if(prevTableIndex >= 0 && prevTableIndex < collections.Count)
                tableList.SetSelectionWithoutNotify(new[] { prevTableIndex });
            return;
        }
        prevTableIndex = tableList.selectedIndex;
        CloseDoc();

        StringTableCollection col = Selected;
        workArea.SetEnabled(col != null);
        if(col == null)
        {
            tableTitle.text = "← 请选择一张字符串表";
            SetStatus("");
            csvPaths = new List<string>();
            csvList.itemsSource = csvPaths;
            csvList.RefreshItems();
            return;
        }

        RefreshTitle();
        RefreshCsvList();
    }

    void BuildTableContextMenu(VisualElement root)
    {
        tableContextMenu = new VisualElement();
        tableContextMenu.AddToClassList("context-menu");
        tableContextMenu.style.display = DisplayStyle.None;
        AddTableContextMenuItem("重命名", RenameTable);
        AddTableContextMenuItem("定位", LocateTable);
        AddTableContextMenuSeparator();
        AddTableContextMenuItem("删除", DeleteTable, true);
        root.Add(tableContextMenu);

        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if(tableContextMenu.style.display == DisplayStyle.Flex && !tableContextMenu.worldBound.Contains(evt.position))
                HideTableContextMenu();
        }, TrickleDown.TrickleDown);
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if(evt.keyCode == KeyCode.Escape)
                HideTableContextMenu();
        });
    }

    void AddTableContextMenuItem(string text, System.Action<StringTableCollection> action, bool danger = false)
    {
        var item = new Button(() =>
        {
            StringTableCollection table = contextTable;
            HideTableContextMenu();
            action(table);
        }) { text = text };
        item.AddToClassList("context-menu-item");
        if(danger)
            item.AddToClassList("context-menu-danger");
        tableContextMenu.Add(item);
    }

    void AddTableContextMenuSeparator()
    {
        var separator = new VisualElement();
        separator.AddToClassList("context-menu-separator");
        tableContextMenu.Add(separator);
    }

    void ShowTableContextMenu(VisualElement item, PointerUpEvent evt)
    {
        if(evt.button != (int)MouseButton.RightMouse || item.userData is not StringTableCollection table)
            return;
        contextTable = table;
        Vector2 position = rootVisualElement.WorldToLocal(evt.position);
        tableContextMenu.style.left = Mathf.Clamp(position.x, 0f, rootVisualElement.contentRect.width - 166f);
        tableContextMenu.style.top = Mathf.Clamp(position.y, 0f, rootVisualElement.contentRect.height - 144f);
        tableContextMenu.style.display = DisplayStyle.Flex;
        tableContextMenu.BringToFront();
        evt.StopPropagation();
    }

    void HideTableContextMenu()
    {
        tableContextMenu.style.display = DisplayStyle.None;
        contextTable = null;
    }

    void RenameTable(StringTableCollection table)
    {
        if(table == Selected && !ConfirmDiscard())
            return;
        string oldName = table.TableCollectionName;
        ShowRenameDialog($"重命名「{oldName}」", oldName, newName =>
        {
            try
            {
                table.SetTableCollectionName(newName, true);
                LocWorkbenchConfig.St.RenameTableCsvFiles(oldName, newName);
                RefreshTables();
                SetStatus($"✓ 已将字符串表「{oldName}」重命名为「{newName}」。");
                return null;
            }
            catch(System.Exception e)
            {
                return e.Message;
            }
        });
    }

    void ShowRenameDialog(string title, string oldName, System.Func<string, string> rename)
    {
        var dialog = new VisualElement();
        dialog.AddToClassList("dialog-overlay");
        var card = new VisualElement();
        card.AddToClassList("dialog-card");
        card.Add(new Label(title));
        var nameField = new TextField("新名称") { value = oldName, isDelayed = true };
        card.Add(nameField);
        var message = new Label();
        message.AddToClassList("dialog-message");
        card.Add(message);
        var actions = new VisualElement();
        actions.AddToClassList("btn-row");
        actions.Add(Btn("确定", () =>
        {
            string newName = nameField.value?.Trim();
            if(string.IsNullOrEmpty(newName))
            {
                message.text = "✗ 表名不能为空。";
                return;
            }
            if(newName == oldName)
            {
                dialog.RemoveFromHierarchy();
                return;
            }
            string error = rename(newName);
            if(!string.IsNullOrEmpty(error))
            {
                message.text = $"✗ 重命名失败：{error}";
                return;
            }
            dialog.RemoveFromHierarchy();
        }, "btn-primary"));
        actions.Add(Btn("取消", () => dialog.RemoveFromHierarchy()));
        card.Add(actions);
        dialog.Add(card);
        rootVisualElement.Add(dialog);
        nameField.Focus();
        nameField.SelectAll();
    }

    void OnTableKeyDown(KeyDownEvent evt)
    {
        if(Selected == null)
            return;
        if(evt.keyCode == KeyCode.F2)
            RenameTable(Selected);
        else if(evt.keyCode == KeyCode.Delete)
            DeleteTable(Selected);
        else if(evt.keyCode == KeyCode.F)
            LocateTable(Selected);
        else
            return;
        evt.StopPropagation();
    }

    static void LocateTable(StringTableCollection table)
    {
        Selection.activeObject = table;
        EditorGUIUtility.PingObject(table);
    }

    void DeleteTable(StringTableCollection table)
    {
        if(table == Selected && !ConfirmDiscard())
            return;
        string tableName = table.TableCollectionName;
        if(!EditorUtility.DisplayDialog("删除字符串表",
            $"将「{tableName}」及其所有语言表资产移至 Windows 回收站。\n\n继续？", "移至回收站", "取消"))
            return;

        var paths = new List<string>
        {
            AssetDatabase.GetAssetPath(table),
            AssetDatabase.GetAssetPath(table.SharedData),
        };
        paths.AddRange(table.StringTables.Select(AssetDatabase.GetAssetPath));
        paths = paths.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToList();
        foreach(string path in paths)
        {
            if(!MoveToRecycleBin(Path.GetFullPath(path), out string error))
            {
                SetStatus($"✗ 删除 {Path.GetFileName(path)} 失败：{error}");
                return;
            }
        }
        LocWorkbenchConfig.St.RemoveTableCsvFiles(tableName);
        CloseDoc();
        AssetDatabase.Refresh();
        RefreshTables();
        SetStatus($"✓ 已将字符串表「{tableName}」移至回收站。");
    }

    // ================= CSV 列表 =================

    void RefreshCsvList()
    {
        if(!ConfirmDiscard())
            return;
        CloseDoc();
        prevCsvIndex = -1;

        csvPaths = Selected == null ? new List<string>() : LocWorkbenchConfig.St.GetCsvFiles(Selected.TableCollectionName);
        csvList.itemsSource = csvPaths;
        csvList.SetSelectionWithoutNotify(System.Array.Empty<int>());   // 清掉残留索引，避免重选同一项不触发事件
        csvList.Rebuild();
        csvSection.style.display = Selected == null ? DisplayStyle.None : DisplayStyle.Flex;
        if(Selected != null && csvPaths.Count == 0)
            SetStatus("该表尚未关联任何 CSV：拖入 CSV 文件，或点击「本表 CSV」旁的「＋」选择文件。");
    }

    static string ToAssetPath(string fullPath)
    {
        string p = fullPath.Replace('\\', '/');
        int idx = p.IndexOf("Assets/", System.StringComparison.Ordinal);
        return idx >= 0 ? p.Substring(idx) : p;
    }

    // 表头是否含 Key 列（区分本地化 CSV 与数据配表）
    static bool IsLocCsv(string assetPath)
    {
        try
        {
            using var reader = new StreamReader(Path.GetFullPath(assetPath));
            string header = reader.ReadLine();
            if(header == null)
                return false;
            return header.Split(',').Any(c =>
                c.Trim().TrimStart('﻿').Trim('"').Equals("Key", System.StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    // ================= 表格编辑区 =================

    void OnCsvSelected()
    {
        if(!ConfirmDiscard())
        {
            if(prevCsvIndex >= 0)
                csvList.SetSelectionWithoutNotify(new[] { prevCsvIndex });
            return;
        }
        prevCsvIndex = csvList.selectedIndex;
        CloseDoc();

        if(csvList.selectedIndex < 0 || csvList.selectedIndex >= csvPaths.Count)
            return;

        string path = csvPaths[csvList.selectedIndex];
        doc = LocCsvDoc.Load(path, out string error);
        if(doc == null)
        {
            SetStatus($"✗ 打开 {Path.GetFileName(path)} 失败：{error}");
            return;
        }
        docCollection = Selected;

        BuildGridColumns();
        ApplyFilter();
        csvPlaceholder.style.display = DisplayStyle.None;
        grid.style.display = DisplayStyle.Flex;
        SetStatus($"已打开 {Path.GetFileName(path)}：{doc.rows.Count} 个 Key，语言列 [{string.Join(", ", doc.LocaleCodes)}]。双击单元格编辑，改完「保存 CSV」。");
    }

    void CloseDoc()
    {
        doc = null;
        docCollection = null;
        dirty = false;
        filtered.Clear();
        if(grid != null)
        {
            grid.itemsSource = filtered;
            grid.style.display = DisplayStyle.None;
        }
        if(csvPlaceholder != null)
            csvPlaceholder.style.display = DisplayStyle.Flex;
        UpdateSaveButton();
    }

    void BuildGridColumns()
    {
        grid.columns.Clear();
        grid.columns.Add(MakeColumn("Key", 180, r => r.key, (r, v) => r.key = v));
        foreach(LocCsvEditor.Column col in doc.columns)
        {
            if(string.IsNullOrEmpty(col.code))
                continue;
            string code = col.code;
            grid.columns.Add(MakeColumn(col.header, 140,
                r => r.values.TryGetValue(code, out string v) ? v : "",
                (r, v) => r.values[code] = v));
        }
    }

    UIColumn MakeColumn(string title, float width,
        System.Func<LocCsvDoc.Row, string> getter, System.Action<LocCsvDoc.Row, string> setter)
    {
        return new UIColumn
        {
            title = title,
            width = width,
            minWidth = 70,
            stretchable = true,
            makeCell = () =>
            {
                var tf = new TextField { isDelayed = true };
                tf.AddToClassList("cell-field");
                tf.RegisterValueChangedCallback(evt =>
                {
                    if(tf.userData is LocCsvDoc.Row row)
                    {
                        setter(row, evt.newValue);
                        MarkDirty();
                    }
                });
                return tf;
            },
            bindCell = (ve, i) =>
            {
                var tf = (TextField)ve;
                var row = (LocCsvDoc.Row)grid.itemsSource[i];
                tf.userData = row;
                tf.SetValueWithoutNotify(getter(row));
            },
        };
    }

    void ApplyFilter()
    {
        if(doc == null)
            return;
        string q = searchField.value?.Trim();
        filtered = string.IsNullOrEmpty(q)
            ? new List<LocCsvDoc.Row>(doc.rows)
            : doc.rows.Where(r => r.key.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
                || r.values.Values.Any(v => v != null && v.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        grid.itemsSource = filtered;
        grid.Rebuild();
    }

    void AddRow()
    {
        if(doc == null)
            return;
        var keys = new HashSet<string>(doc.rows.Select(r => r.key));
        string baseKey = "NewKey";
        string key = baseKey;
        for(int n = 1; keys.Contains(key); n++)
            key = baseKey + n;
        doc.rows.Add(new LocCsvDoc.Row { key = key });
        MarkDirty();
        searchField.SetValueWithoutNotify("");
        ApplyFilter();
        grid.ScrollToItem(filtered.Count - 1);
    }

    void DeleteRows()
    {
        if(doc == null || grid.selectedIndices.Count() == 0)
            return;
        List<LocCsvDoc.Row> victims = grid.selectedIndices
            .Where(i => i >= 0 && i < filtered.Count).Select(i => filtered[i]).ToList();
        if(victims.Count == 0)
            return;
        if(!EditorUtility.DisplayDialog("删除条目",
            $"删除选中的 {victims.Count} 行？\n（保存后若用「重建导入」，表中对应 Key 也会被移除。）", "删除", "取消"))
            return;
        foreach(LocCsvDoc.Row r in victims)
            doc.rows.Remove(r);
        MarkDirty();
        ApplyFilter();
    }

    void ReloadCsv()
    {
        if(doc == null)
            return;
        dirty = false;   // 主动放弃修改，不再弹确认
        int keep = csvList.selectedIndex;
        csvList.SetSelectionWithoutNotify(new[] { keep });
        OnCsvSelected();
    }

    void SaveCsv()
    {
        if(doc == null)
            return;
        if(!doc.Save(out string error))
        {
            SetStatus("✗ " + error);
            return;
        }
        dirty = false;
        UpdateSaveButton();
        AssetDatabase.ImportAsset(doc.assetPath);

        string extra = "";
        if(autoImportToggle.value && docCollection != null)
        {
            LocCsvMerger.Result r = LocCsvMerger.Merge(
                File.ReadAllText(Path.GetFullPath(doc.assetPath)), docCollection, overwrite: true);
            extra = r.ok
                ? $" 并已导入「{docCollection.TableCollectionName}」（新增 {r.added}，更新 {r.updated}）。注意：从 CSV 删掉的 Key 需「重建导入」才会从表中移除。"
                : $" 但导入失败：{r.message}";
            if(docCollection == Selected)
                RefreshTitle();
        }
        SetStatus($"✓ 已保存 {Path.GetFileName(doc.assetPath)}（{doc.rows.Count} 个 Key）。" + extra);
    }

    void MarkDirty()
    {
        dirty = true;
        UpdateSaveButton();
    }

    void UpdateSaveButton()
    {
        if(saveBtn != null)
            saveBtn.text = dirty ? "保存 CSV *" : "保存 CSV";
    }

    // 有未保存修改时弹窗确认；返回 false 表示用户取消当前操作
    bool ConfirmDiscard()
    {
        if(!dirty || doc == null)
            return true;
        int choice = EditorUtility.DisplayDialogComplex("未保存的修改",
            $"{Path.GetFileName(doc.assetPath)} 有未保存的修改。", "保存", "取消", "放弃修改");
        if(choice == 0)
        {
            SaveCsv();
            return !dirty;   // 保存失败（如重复 Key）则留在原地
        }
        if(choice == 2)
        {
            dirty = false;
            return true;
        }
        return false;
    }

    // ================= 拖放（整窗接收）=================
    // 拖 CSV 文件 → 关联到当前选中表；拖文件夹 → 取其下（不含子目录）全部本地化 CSV 一并关联。
    // 用 TrickleDown 在子控件之前接管，保证拖到窗口任意位置都生效。

    void SetupDrop(VisualElement root)
    {
        root.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if(CollectDragCsvPaths().Count == 0)
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            evt.StopPropagation();
        }, TrickleDown.TrickleDown);

        root.RegisterCallback<DragPerformEvent>(evt =>
        {
            List<string> paths = CollectDragCsvPaths();
            if(paths.Count == 0)
                return;
            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
            if(Selected == null)
            {
                SetStatus("⚠ 请先在左侧选择一张字符串表，再拖入 CSV。");
                return;
            }
            LocWorkbenchConfig.St.AddCsvFiles(Selected.TableCollectionName, paths);
            tableList.RefreshItems();
            RefreshCsvList();
            SetStatus($"✓ 已关联 {paths.Count} 份 CSV 到「{Selected.TableCollectionName}」。");
        }, TrickleDown.TrickleDown);
    }

    // 收集拖拽内容里的 CSV 路径：直接拖 CSV 文件，或拖文件夹时取其下（不含子目录）本地化 CSV
    static List<string> CollectDragCsvPaths()
    {
        var result = new List<string>();
        foreach(string p in DragAndDrop.paths ?? System.Array.Empty<string>())
        {
            if(AssetDatabase.IsValidFolder(p))
            {
                foreach(string file in Directory.GetFiles(Path.GetFullPath(p), "*.csv", SearchOption.TopDirectoryOnly))
                {
                    string assetPath = ToAssetPath(file);
                    if(IsLocCsv(assetPath))
                        result.Add(assetPath);
                }
            }
            else if(p.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(p);
            }
        }
        return result;
    }

    // ================= 关联 / 取消关联 CSV =================

    void BuildCsvContextMenu(VisualElement root)
    {
        csvContextMenu = new VisualElement();
        csvContextMenu.AddToClassList("context-menu");
        csvContextMenu.style.display = DisplayStyle.None;
        AddCsvContextMenuItem("重命名", RenameCsv);
        AddCsvContextMenuItem("定位", LocateCsv);
        AddCsvContextMenuSeparator();
        AddCsvContextMenuItem("清空", ClearCsv);
        AddCsvContextMenuItem("取消关联", RemoveCsvFile);
        AddCsvContextMenuSeparator();
        AddCsvContextMenuItem("删除", DeleteCsv, true);
        root.Add(csvContextMenu);

        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if(csvContextMenu.style.display == DisplayStyle.Flex && !csvContextMenu.worldBound.Contains(evt.position))
                HideCsvContextMenu();
        }, TrickleDown.TrickleDown);
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if(evt.keyCode == KeyCode.Escape)
                HideCsvContextMenu();
        });
    }

    void AddCsvContextMenuItem(string text, System.Action<string> action, bool danger = false)
    {
        var item = new Button(() =>
        {
            string path = contextCsvPath;
            HideCsvContextMenu();
            action(path);
        }) { text = text };
        item.AddToClassList("context-menu-item");
        if(danger)
            item.AddToClassList("context-menu-danger");
        csvContextMenu.Add(item);
    }

    void AddCsvContextMenuSeparator()
    {
        var separator = new VisualElement();
        separator.AddToClassList("context-menu-separator");
        csvContextMenu.Add(separator);
    }

    void ShowCsvContextMenu(Label label, PointerUpEvent evt)
    {
        if(evt.button != (int)MouseButton.RightMouse || label.userData is not string path)
            return;
        contextCsvPath = path;
        Vector2 position = rootVisualElement.WorldToLocal(evt.position);
        csvContextMenu.style.left = Mathf.Clamp(position.x, 0f, rootVisualElement.contentRect.width - 166f);
        csvContextMenu.style.top = Mathf.Clamp(position.y, 0f, rootVisualElement.contentRect.height - 172f);
        csvContextMenu.style.display = DisplayStyle.Flex;
        csvContextMenu.BringToFront();
        evt.StopPropagation();
    }

    void HideCsvContextMenu()
    {
        csvContextMenu.style.display = DisplayStyle.None;
        contextCsvPath = null;
    }

    void LocateCsv(string path)
    {
        if(!File.Exists(Path.GetFullPath(path)))
        {
            SetStatus($"✗ 找不到 {Path.GetFileName(path)}。");
            return;
        }
        EditorUtility.RevealInFinder(Path.GetFullPath(path));
    }

    void RenameCsv(string path)
    {
        if(!ConfirmDiscard())
            return;
        string oldName = Path.GetFileNameWithoutExtension(path);
        ShowRenameDialog($"重命名「{Path.GetFileName(path)}」", oldName, newName =>
        {
            CloseDoc();
            string error = AssetDatabase.RenameAsset(path, newName);
            if(!string.IsNullOrEmpty(error))
                return error;
            string newPath = Path.GetDirectoryName(path)?.Replace('\\', '/') + "/" + newName + Path.GetExtension(path);
            RefreshCsvList();
            int index = csvPaths.IndexOf(newPath);
            if(index >= 0)
                csvList.SetSelection(index);
            SetStatus($"✓ 已将 CSV 重命名为「{Path.GetFileName(newPath)}」。");
            return null;
        });
    }

    void OnCsvKeyDown(KeyDownEvent evt)
    {
        if(csvList.selectedIndex < 0 || csvList.selectedIndex >= csvPaths.Count)
            return;
        string path = csvPaths[csvList.selectedIndex];
        if(evt.keyCode == KeyCode.F2)
            RenameCsv(path);
        else if(evt.keyCode == KeyCode.Delete)
            DeleteCsv(path);
        else if(evt.keyCode == KeyCode.F)
            LocateCsv(path);
        else
            return;
        evt.StopPropagation();
    }

    void ClearCsv(string path)
    {
        if(!ConfirmDiscard())
            return;
        LocCsvDoc csv = LocCsvDoc.Load(path, out string error);
        if(csv == null)
        {
            SetStatus($"✗ 打开 {Path.GetFileName(path)} 失败：{error}");
            return;
        }
        if(csv.rows.Count == 0)
        {
            SetStatus($"{Path.GetFileName(path)} 已经没有数据行。");
            return;
        }
        if(!EditorUtility.DisplayDialog("清空 CSV",
            $"清空「{Path.GetFileName(path)}」的 {csv.rows.Count} 个 Key，保留表头？\n\n" +
            "这不会自动删除字符串表中的 Key；需要时请再执行「重建导入」。",
            "清空", "取消"))
            return;

        if(!ClearCsvRows(path, csv.rows, out error))
        {
            SetStatus($"✗ 清空 {Path.GetFileName(path)} 失败：{error}");
            return;
        }
        CloseDoc();
        AssetDatabase.ImportAsset(path);
        RefreshCsvList();
        SetStatus($"✓ 已清空 {Path.GetFileName(path)}，保留 CSV 表头。");
    }

    static bool ClearCsvRows(string path, List<LocCsvDoc.Row> rows, out string error)
    {
        string jsonPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../Library")),
            $"LocCsvClear-{System.Guid.NewGuid():N}.json");
        try
        {
            var ops = new List<string>(rows.Count);
            foreach(LocCsvDoc.Row row in rows)
            {
                ops.Add(JsonUtility.ToJson(new LocCsvBatchOp
                {
                    action = "Remove",
                    csv = path,
                    key = row.key,
                }));
            }
            File.WriteAllText(jsonPath, "[" + string.Join(",", ops) + "]", new UTF8Encoding(false));

            string toolPath = Path.GetFullPath(LocCsvToolPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{toolPath}\" -Action Batch -File \"{jsonPath}\" -Quiet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using Process process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            error = string.IsNullOrWhiteSpace(errors) ? output.Trim() : errors.Trim();
            return process.ExitCode == 0;
        }
        catch(System.Exception e)
        {
            error = e.Message;
            return false;
        }
        finally
        {
            if(File.Exists(jsonPath))
                File.Delete(jsonPath);
        }
    }

    void DeleteCsv(string path)
    {
        if(!ConfirmDiscard())
            return;
        if(!EditorUtility.DisplayDialog("删除 CSV",
            $"将「{Path.GetFileName(path)}」移至 Windows 回收站，并取消与当前表的关联。\n\n继续？",
            "移至回收站", "取消"))
            return;

        string fullPath = Path.GetFullPath(path);
        if(!MoveToRecycleBin(fullPath, out string error))
        {
            SetStatus($"✗ 删除 {Path.GetFileName(path)} 失败：{error}");
            return;
        }
        LocWorkbenchConfig.St.RemoveCsvFile(Selected.TableCollectionName, path);
        CloseDoc();
        AssetDatabase.Refresh();
        tableList.RefreshItems();
        RefreshCsvList();
        SetStatus($"✓ 已将 {Path.GetFileName(path)} 移至回收站并取消关联。");
    }

    static bool MoveToRecycleBin(string path, out string error)
    {
        var operation = new ShellFileOperation
        {
            func = FoDelete,
            from = path + "\0\0",
            flags = FofAllowUndo | FofNoConfirmation | FofSilent,
        };
        int result = SHFileOperation(ref operation);
        error = operation.aborted ? "操作已取消。" : $"系统错误码：{result}";
        return result == 0 && !operation.aborted;
    }

    void AddCsvDialog()
    {
        if(Selected == null)
        {
            SetStatus("⚠ 请先在左侧选择一张字符串表。");
            return;
        }
        string startFolder = csvPaths.Count > 0
            ? Path.GetDirectoryName(Path.GetFullPath(csvPaths[0]))
            : Application.dataPath;
        string full = EditorUtility.OpenFilePanelWithFilters("选择 CSV 文件", startFolder, new[] { "CSV", "csv" });
        if(string.IsNullOrEmpty(full))
            return;
        string assetPath = ToAssetPath(full.Replace('\\', '/'));
        if(!assetPath.StartsWith("Assets/"))
        {
            SetStatus("✗ 只能选择工程 Assets 目录内的文件。");
            return;
        }
        LocWorkbenchConfig.St.AddCsvFiles(Selected.TableCollectionName, new[] { assetPath });
        tableList.RefreshItems();
        RefreshCsvList();
        int idx = csvPaths.IndexOf(assetPath);
        if(idx >= 0)
            csvList.selectedIndex = idx;
        SetStatus($"✓ 已关联 {Path.GetFileName(assetPath)} 到「{Selected.TableCollectionName}」。");
    }

    void RemoveSelectedCsv()
    {
        if(Selected == null || csvList.selectedIndex < 0 || csvList.selectedIndex >= csvPaths.Count)
            return;
        RemoveCsvFile(csvPaths[csvList.selectedIndex]);
    }

    void RemoveCsvFile(string path)
    {
        if(Selected == null)
            return;
        if(!EditorUtility.DisplayDialog("取消关联",
            $"取消「{Path.GetFileName(path)}」与「{Selected.TableCollectionName}」的关联？\n（不会删除文件本身）", "取消关联", "取消"))
            return;
        LocWorkbenchConfig.St.RemoveCsvFile(Selected.TableCollectionName, path);
        tableList.RefreshItems();
        RefreshCsvList();
        SetStatus($"已取消 {Path.GetFileName(path)} 的关联。");
    }

    // ================= 导入 / 检测 =================

    // 收集本表已关联的全部本地化 CSV 的（路径, 文本, Key 列表）
    List<(string path, string text, List<string> keys)> LoadAll()
    {
        var list = new List<(string, string, List<string>)>();
        foreach(string path in csvPaths)
        {
            string text = File.ReadAllText(Path.GetFullPath(path));
            LocCsvEditor.ParseResult p = LocCsvEditor.Parse(text);
            if(p.ok)
                list.Add((path, text, p.keys));
        }
        return list;
    }

    // 跨 CSV 重复 Key：key → 所在文件名列表（仅含出现 2 次以上的）
    static Dictionary<string, List<string>> FindDuplicates(List<(string path, string text, List<string> keys)> all)
    {
        var seen = new Dictionary<string, List<string>>();
        foreach(var (path, _, keys) in all)
            foreach(string key in keys.Distinct())
            {
                if(!seen.TryGetValue(key, out List<string> files))
                    seen[key] = files = new List<string>();
                files.Add(Path.GetFileName(path));
            }
        return seen.Where(kv => kv.Value.Count > 1).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    void ImportAll(bool clearFirst)
    {
        if(Selected == null || !ConfirmDiscard())
            return;
        List<(string path, string text, List<string> keys)> all = LoadAll();
        if(all.Count == 0)
        {
            SetStatus("✗ 该表尚未关联可导入的本地化 CSV。");
            return;
        }

        // 跨 CSV 重复 Key 会被静默覆盖（导入顺序靠后的赢），必须先解决再导
        Dictionary<string, List<string>> dups = FindDuplicates(all);
        if(dups.Count > 0)
        {
            SetStatus("✗ 检测到跨 CSV 重复 Key，已中止导入：\n" + DupReport(dups));
            return;
        }

        if(clearFirst && !EditorUtility.DisplayDialog("重建导入",
            $"将先清空「{Selected.TableCollectionName}」表（当前 {Selected.SharedData.Entries.Count} 个 Key），\n" +
            $"再导入已关联的 {all.Count} 份 CSV。表中不在这些 CSV 里的 Key 会被删除。\n\n继续？", "重建", "取消"))
            return;

        LocCsvMerger.Result r = LocCsvMerger.Import(
            all.Select(x => x.text).ToList(), Selected, overwrite: true, clearFirst: clearFirst);
        if(!r.ok)
        {
            SetStatus($"✗ 导入失败：{r.message}");
            return;
        }
        RefreshTitle();
        string cleared = clearFirst ? $"清空 {r.cleared} 个旧 Key，" : "";
        string missing = r.missingLocales.Count > 0 ? $"（CSV 含表中没有的语言列，已跳过：{string.Join(", ", r.missingLocales)}）" : "";
        SetStatus($"✓ {all.Count} 份 CSV → 「{Selected.TableCollectionName}」：{cleared}新增 {r.added}，更新 {r.updated}，Smart 标记 {r.smartMarked}。{missing}");
    }

    void AnalyzeCsvs()
    {
        if(Selected == null)
            return;
        List<(string path, string text, List<string> keys)> all = LoadAll();
        var sb = new StringBuilder();

        Dictionary<string, List<string>> dups = FindDuplicates(all);
        sb.Append(dups.Count > 0
            ? $"✗ 跨 CSV 重复 Key {dups.Count} 个：\n{DupReport(dups)}\n"
            : "✓ 无跨 CSV 重复 Key。");

        // 孤儿 Key：表里有、但所有 CSV 都没有（重建导入即可清掉）
        var union = new HashSet<string>(all.SelectMany(x => x.keys));
        List<string> orphans = Selected.SharedData.Entries
            .Select(e => e.Key).Where(k => !union.Contains(k)).ToList();
        if(orphans.Count > 0)
        {
            sb.Append($"\n⚠ 表内孤儿 Key {orphans.Count} 个（不在任何 CSV 中，「重建导入」可清除）：");
            sb.Append('\n').Append(string.Join(", ", orphans.Take(20)));
            if(orphans.Count > 20)
                sb.Append($" …等共 {orphans.Count} 个（完整列表见 Console）");
            Debug.Log($"[Loc工作台] {Selected.TableCollectionName} 孤儿 Key：\n" + string.Join("\n", orphans));
        }
        else
        {
            sb.Append("\n✓ 无孤儿 Key，表与已关联 CSV 一致。");
        }
        SetStatus(sb.ToString());
    }

    static string DupReport(Dictionary<string, List<string>> dups)
        => string.Join("\n", dups.Take(10).Select(kv => $"  {kv.Key} ← {string.Join(" / ", kv.Value)}"))
           + (dups.Count > 10 ? $"\n  …等共 {dups.Count} 个" : "");

    // ================= 新建 CSV =================

    void CreateCsv()
    {
        if(Selected == null)
            return;
        string startFolder = csvPaths.Count > 0
            ? Path.GetDirectoryName(csvPaths[0])?.Replace('\\', '/')
            : "Assets";
        string path = EditorUtility.SaveFilePanelInProject(
            "新建本地化 CSV", $"{Selected.TableCollectionName}NewPanelLoc", "csv",
            "建议命名：<表名><面板名>Loc.csv", startFolder);
        if(string.IsNullOrEmpty(path))
            return;

        // 表头优先沿用已关联的第一份 CSV（列序/语言一致），否则按表的语言生成
        string header = null;
        if(csvPaths.Count > 0)
        {
            using var reader = new StreamReader(Path.GetFullPath(csvPaths[0]));
            header = reader.ReadLine()?.TrimStart('﻿');
        }
        if(string.IsNullOrEmpty(header))
        {
            var cells = new List<string> { "\"Key\"", "\"Id\"" };
            foreach(Locale locale in LocalizationEditorSettings.GetLocales())
            {
                string name = locale.Identifier.CultureInfo?.EnglishName ?? locale.LocaleName;
                cells.Add(LocCsvEditor.Quote($"{name}({locale.Identifier.Code})"));
            }
            header = string.Join(",", cells);
        }

        File.WriteAllText(Path.GetFullPath(path), header + "\r\n", new UTF8Encoding(true));
        AssetDatabase.ImportAsset(path);
        LocWorkbenchConfig.St.AddCsvFiles(Selected.TableCollectionName, new[] { path });
        tableList.RefreshItems();
        RefreshCsvList();
        int idx = csvPaths.IndexOf(path);
        if(idx >= 0)
            csvList.selectedIndex = idx;
        SetStatus($"✓ 已创建并关联 {Path.GetFileName(path)}，可直接「＋ 加行」添加条目。");
    }

    // ================= 杂项 =================

    void RefreshTitle()
    {
        if(Selected != null)
            tableTitle.text = $"{Selected.TableCollectionName}（{Selected.SharedData.Entries.Count} 个 Key，{Selected.StringTables.Count} 种语言）";
    }

    void SetStatus(string msg) => status.text = msg;
}
#endif
