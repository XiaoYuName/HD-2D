#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// 可视化查看/行级编辑各 CSV，并做导入（增量 / 清空重建）、跨 CSV 重复 Key 与表内多余 Key 检测。
/// 合并核心复用 <see cref="LocCsvMerger"/>，CSV 读写见 <see cref="LocCsvDoc"/>，样式见同目录 LocWorkbench.uss。
/// </summary>
public class LocWorkbenchWindow : EditorWindow
{
    const string LocCsvToolPath = "Assets/0 Core/1 Script/Tool/Localization/LocCsv.ps1";
    [System.Serializable]
    sealed class LocCsvBatchOp
    {
        public string action;
        public string csv;
        public string key;
    }

    // ---- 左侧：表列表 ----
    ListView tableList;
    ToolbarSearchField tableSearch;
    List<StringTableCollection> allCollections = new();   // 工程内全部
    List<StringTableCollection> collections = new();      // 搜索过滤后（tableList.itemsSource）

    // ---- 右侧：工作区 ----
    VisualElement tableActions;
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
    VisualElement createTablePopup;
    string contextCsvPath;
    StringTableCollection contextTable;

    List<string> csvPaths = new();     // 当前表已关联的本地化 CSV（工程相对路径）
    LocCsvDoc doc;                     // 当前打开的 CSV 文档
    StringTableCollection docCollection;   // doc 所属的表（selectionChanged 在选中变化后才触发，保存导入必须用它而非 Selected）
    List<LocCsvDoc.Row> filtered = new();
    bool dirty;
    string pendingTableName;
    int prevTableIndex = -1;           // ConfirmDiscard 取消时回滚选择用
    int prevCsvIndex = -1;

    StringTableCollection Selected
        => tableList.selectedIndex >= 0 && tableList.selectedIndex < collections.Count
            ? collections[tableList.selectedIndex] : null;

    [MenuItem("Tools/Loc/多语言工作台")]
    static void Open() => OpenTable(null);

    /// <summary>打开工作台，并在界面构建完成后定位指定字符串表。</summary>
    public static void OpenTable(string tableName)
    {
        LocWorkbenchWindow window = GetWindow<LocWorkbenchWindow>("多语言工作台");
        window.minSize = new Vector2(900, 480);
        window.pendingTableName = tableName;
        window.Show();
        window.SelectPendingTable();
    }

    void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        LocWorkbenchViewConfig viewConfig = LocWorkbenchViewConfig.St;
        if(viewConfig == null)
        {
            return;
        }
        if(viewConfig.workbenchUss == null)
            Debug.LogError("[Loc工作台] View Config 的 Workbench Uss 未配置。");
        else
            root.styleSheets.Add(viewConfig.workbenchUss);
        if(viewConfig.workbenchUxml == null)
        {
            Debug.LogError("[Loc工作台] View Config 的 Workbench Uxml 未配置。");
            return;
        }
        root.Add(viewConfig.workbenchUxml.CloneTree());
        SetupDrop(root);
        BuildCsvContextMenu(root);
        BuildTableContextMenu(root);
        BindTabs(root);
        BindWorkbenchControls(root);
        BindToolsControls(root);
        BindSettingsControls(root);
        RefreshTables();
        SelectPendingTable();
    }

    static void BindTabs(VisualElement root)
    {
        var workbenchContent = root.Q<VisualElement>("WorkbenchContent");
        var toolsContent = root.Q<VisualElement>("ToolsContent");
        var settingsContent = root.Q<VisualElement>("SettingsContent");
        var workbenchTabBtn = root.Q<Button>("WorkbenchTabBtn");
        var toolsTabBtn = root.Q<Button>("ToolsTabBtn");
        var settingsTabBtn = root.Q<Button>("SettingsTabBtn");
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
    }

    void BindWorkbenchControls(VisualElement root)
    {
        tableSearch = root.Q<ToolbarSearchField>("TableSearch");
        tableSearch.RegisterValueChangedCallback(_ => ApplyTableFilter());
        tableList = root.Q<ListView>("TableList");
        tableList.makeItem = () =>
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
        };
        tableList.bindItem = (ve, i) =>
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
        };
        tableList.selectionChanged += _ => OnTableSelected();
        tableList.RegisterCallback<KeyDownEvent>(OnTableKeyDown);

        tableActions = root.Q<VisualElement>("TableActions");
        tableTitle = root.Q<Label>("TableTitle");
        csvSection = root.Q<VisualElement>("CsvSection");
        csvList = root.Q<ListView>("CsvList");
        csvList.makeItem = () =>
        {
            var label = new Label();
            label.AddToClassList("table-item");
            label.RegisterCallback<PointerUpEvent>(evt => ShowCsvContextMenu(label, evt));
            return label;
        };
        csvList.bindItem = (ve, i) =>
        {
            var label = (Label)ve;
            string path = csvPaths[i];
            label.text = Path.GetFileName(path);
            label.userData = path;
        };
        csvList.selectionChanged += _ => OnCsvSelected();
        csvList.RegisterCallback<KeyDownEvent>(OnCsvKeyDown);

        searchField = root.Q<ToolbarSearchField>("SearchField");
        searchField.RegisterValueChangedCallback(_ => ApplyFilter());
        autoImportToggle = root.Q<Toggle>("AutoImportToggle");
        grid = root.Q<MultiColumnListView>("Grid");
        grid.selectionType = SelectionType.Multiple;
        csvPlaceholder = root.Q<Label>("CsvPlaceholder");
        saveBtn = root.Q<Button>("SaveCsvBtn");
        status = root.Q<Label>("Status");
        Button createTableBtn = root.Q<Button>("CreateStringTableDialogBtn");

        root.Q<Button>("RefreshTablesBtn").clicked += RefreshTables;
        createTableBtn.clicked += () => ToggleCreateStringTablePopup(createTableBtn);
        root.RegisterCallback<PointerDownEvent>(evt =>
        {
            if(createTablePopup != null && !createTablePopup.worldBound.Contains(evt.position) && !createTableBtn.worldBound.Contains(evt.position))
                HideCreateStringTablePopup();
        }, TrickleDown.TrickleDown);
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if(evt.keyCode == KeyCode.Escape)
                HideCreateStringTablePopup();
        });
        root.Q<Button>("ImportAllBtn").clicked += () => ImportAll(clearFirst: false);
        root.Q<Button>("RebuildImportBtn").clicked += () => ImportAll(clearFirst: true);
        root.Q<Button>("AnalyzeCsvsBtn").clicked += AnalyzeCsvs;
        root.Q<Button>("CreateCsvBtn").clicked += CreateCsv;
        root.Q<Button>("AddCsvBtn").clicked += AddCsvDialog;
        root.Q<Button>("RemoveCsvBtn").clicked += RemoveSelectedCsv;
        root.Q<Button>("AddRowBtn").clicked += AddRow;
        root.Q<Button>("DeleteRowsBtn").clicked += DeleteRows;
        root.Q<Button>("ReloadCsvBtn").clicked += ReloadCsv;
        saveBtn.clicked += SaveCsv;
    }

    static Button Btn(string text, System.Action onClick, string extraClass = null)
    {
        var b = new Button(onClick) { text = text };
        b.AddToClassList("btn");
        if(extraClass != null)
            b.AddToClassList(extraClass);
        return b;
    }

    void BindToolsControls(VisualElement root)
    {
        Label toolsStatus = root.Q<Label>("ToolsStatus");
        root.Q<Button>("AutoMarkSmartBtn").clicked += () =>
        {
            int count = AutoMarkSmartString.MarkAll();
            toolsStatus.text = $"✓ 本次新标记 {count} 个 Smart String 条目。";
        };
        root.Q<Button>("CleanSmartFormatBtn").clicked += () =>
        {
            LocSmartFormatTagCleaner.Result result = LocSmartFormatTagCleaner.CleanAll();
            toolsStatus.text = result.removedIdCount == 0
                ? "✓ 未发现无效 Smart Format ID。"
                : $"✓ 已从 {result.tableCount} 张语言表清理 {result.removedIdCount} 个无效 Smart Format ID。";
        };
    }

    void BindSettingsControls(VisualElement root)
    {
        scanPathField = root.Q<TextField>("ScanPathField");
        scanPathField.SetValueWithoutNotify(LocWorkbenchConfig.St.GetScanPath());
        newTableNameField = root.Q<TextField>("NewTableNameField");
        scanStatus = root.Q<Label>("ScanStatus");
        tableCacheLabel = root.Q<Label>("TableCacheLabel");
        root.Q<Button>("SelectScanPathBtn").clicked += SelectScanPath;
        root.Q<Button>("SaveScanPathBtn").clicked += SaveScanPathAndRefresh;
        root.Q<Button>("RefreshSettingsBtn").clicked += RefreshTables;
        root.Q<Button>("CreateStringTableBtn").clicked += () => { CreateStringTable(newTableNameField, scanStatus); };
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

    bool CreateStringTable(TextField nameField, Label statusLabel)
    {
        string tableName = nameField.value?.Trim();
        if(string.IsNullOrEmpty(tableName))
        {
            statusLabel.text = "✗ 请先填写表名。";
            return false;
        }

        try
        {
            string directory = LocWorkbenchConfig.St.GetScanPath().TrimEnd('/') + "/" + tableName;
            StringTableCollection collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, directory);
            if(collection == null)
            {
                statusLabel.text = "✗ 字符串表创建失败。";
                return false;
            }

            LocWorkbenchConfig.St.RefreshTableCache();
            RefreshTables();
            int index = collections.IndexOf(collection);
            if(index >= 0)
                tableList.SetSelection(index);
            nameField.SetValueWithoutNotify("");
            statusLabel.text = $"✓ 已创建「{tableName}」并加入扫描缓存。";
            return true;
        }
        catch(System.Exception e)
        {
            statusLabel.text = $"✗ 创建失败：{e.Message}";
            return false;
        }
    }

    void ToggleCreateStringTablePopup(Button anchor)
    {
        if(createTablePopup != null)
        {
            HideCreateStringTablePopup();
            return;
        }

        createTablePopup = new VisualElement();
        createTablePopup.AddToClassList("create-table-popup");
        createTablePopup.Add(new Label("新建字符串表"));
        var nameField = new TextField("表名");
        createTablePopup.Add(nameField);
        var message = new Label();
        message.AddToClassList("dialog-message");
        createTablePopup.Add(message);
        var actions = new VisualElement();
        actions.AddToClassList("btn-row");
        actions.Add(Btn("创建", () =>
        {
            if(!CreateStringTable(nameField, message))
                return;
            status.text = message.text;
            HideCreateStringTablePopup();
        }, "btn-primary"));
        actions.Add(Btn("取消", HideCreateStringTablePopup));
        createTablePopup.Add(actions);
        rootVisualElement.Add(createTablePopup);
        Vector2 position = rootVisualElement.WorldToLocal(new Vector2(anchor.worldBound.xMax, anchor.worldBound.yMax));
        createTablePopup.style.left = Mathf.Clamp(position.x - 220f, 4f, rootVisualElement.contentRect.width - 224f);
        createTablePopup.style.top = position.y + 3f;
        createTablePopup.BringToFront();
        nameField.Focus();
    }

    void HideCreateStringTablePopup()
    {
        createTablePopup?.RemoveFromHierarchy();
        createTablePopup = null;
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

    void SelectPendingTable()
    {
        if (string.IsNullOrEmpty(pendingTableName) || tableList == null) return;
        tableSearch.SetValueWithoutNotify(string.Empty);
        collections = new List<StringTableCollection>(allCollections);
        tableList.itemsSource = collections;
        tableList.Rebuild();
        int index = collections.FindIndex(collection => collection.TableCollectionName == pendingTableName);
        if (index < 0) return;
        pendingTableName = null;
        tableList.SetSelection(index);
        tableList.ScrollToItem(index);
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
        tableActions.SetEnabled(col != null);
        csvSection.SetEnabled(col != null);
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
        if(!WindowsRecycleBin.MoveToRecycleBin(paths.Select(Path.GetFullPath).ToArray(), out string error))
        {
            SetStatus($"✗ 删除字符串表失败：{error}");
            return;
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
        if(!WindowsRecycleBin.MoveToRecycleBin(fullPath, out string error))
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

        // 多余 Key：表中原始 Key 不在按导入规则 Trim 后的 CSV Key 集合中（重建导入即可清掉）
        var union = new HashSet<string>(all.SelectMany(x => x.keys));
        List<string> extraKeys = Selected.SharedData.Entries
            .Select(e => e.Key).Where(k => !union.Contains(k)).ToList();
        if(extraKeys.Count > 0)
        {
            string KeyForDisplay(string key) => key.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            sb.Append($"\n⚠ 表内多余 Key {extraKeys.Count} 个（按导入规则处理后不在任何 CSV 中，「重建导入」可清除）：");
            sb.Append('\n').Append(string.Join(", ", extraKeys.Take(20).Select(KeyForDisplay)));
            if(extraKeys.Count > 20)
                sb.Append($" …等共 {extraKeys.Count} 个（完整列表见 Console）");
            Debug.Log($"[Loc工作台] {Selected.TableCollectionName} 多余 Key：\n" +
                string.Join("\n", extraKeys.Select(KeyForDisplay)));
        }
        else
        {
            sb.Append("\n✓ 无多余 Key，表与已关联 CSV 一致。");
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
