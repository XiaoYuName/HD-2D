#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        var split = new TwoPaneSplitView(0, 230, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1f;
        root.Add(split);

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
            },
        };
        tableList.AddToClassList("table-list");
        tableList.selectionChanged += _ => OnTableSelected();
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
                return label;
            },
            bindItem = (ve, i) => ((Label)ve).text = Path.GetFileName(csvPaths[i]),
        };
        csvList.AddToClassList("csv-list");
        csvList.selectionChanged += _ => OnCsvSelected();
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

    // ================= 左侧表列表 =================

    void RefreshTables()
    {
        allCollections = LocalizationEditorSettings.GetStringTableCollections()
            .OrderBy(c => c.TableCollectionName).ToList();
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
        string path = csvPaths[csvList.selectedIndex];
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
