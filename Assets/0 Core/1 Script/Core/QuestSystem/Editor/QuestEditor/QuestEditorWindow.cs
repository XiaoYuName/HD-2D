using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using XFramework;
using UIColumn = UnityEngine.UIElements.Column;

/// <summary>
/// 任务编辑器：上面五个表页签，中间一张表格（一行一条记录、一列一个字段），
/// 列表 / 触发 / 目标 / 奖励这些装不进单元格的字段在格子里显示摘要，点开在弹层里编辑。
///
/// 结构在 QuestEditorWindow.uxml、样式在 .uss，C# 只做数据绑定。
/// **不碰 SerializedProperty**：数据是 Odin 序列化的普通 C# 对象（字典 ＋ 多态实例），
/// 列和弹层都由 <see cref="QuestEditorFields"/> 按反射生成 —— 加一种目标/触发/奖励，编辑器不用改。
/// </summary>
public class QuestEditorWindow : EditorWindow
{
    const string UxmlPath = "Assets/0 Core/1 Script/Core/QuestSystem/Editor/QuestEditor/QuestEditorWindow.uxml";
    const string UssPath = "Assets/0 Core/1 Script/Core/QuestSystem/Editor/QuestEditor/QuestEditorWindow.uss";

    [MenuItem("Tools/QuestSystem - 任务系统/QuestEditor - 任务编辑器")]
    public static void Open()
    {
        QuestEditorWindow window = GetWindow<QuestEditorWindow>();
        window.titleContent = new GUIContent("任务编辑器");
        window.minSize = new Vector2(1100, 620);
    }

    /// <summary>表格的一行：字典的 Key ＋ 值对象。</summary>
    class Row
    {
        public object Key;
        public object Value;
        public string Search;
    }

    ITable[] tables;
    ITable table;

    QuestConfig config;

    readonly List<Row> rows = new();
    readonly Dictionary<ITable, Button> tabButtons = new();
    readonly QuestEditorUndo undo = new();

    /// <summary>Shift 连选的起点。</summary>
    int anchorIndex = -1;

    VisualElement tabsRoot;
    VisualElement overlay;
    MultiColumnListView grid;
    ToolbarSearchField searchField;
    Label statusLabel;

    void CreateGUI()
    {
        tables = new ITable[]
        {
            new Table<long, QuestData>("任务", data => data.EditorQuests, value => value.Remark),
            new Table<long, QuestObjConfigData>("目标", data => data.EditorObjs, value => value.Remark),
            new Table<long, QuestCondData>("接受条件", data => data.EditorConds, value => value.Remark),
            new Table<long, QuestCategory>("类别", data => data.EditorCategories, value => value.Remark),
            new RewardViewTable(),
        };
        table = tables[0];

        AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree(rootVisualElement);
        rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath));

        tabsRoot = rootVisualElement.Q("tabs");
        overlay = rootVisualElement.Q("overlay");
        statusLabel = rootVisualElement.Q<Label>("status");

        ObjectField configField = rootVisualElement.Q<ObjectField>("config-field");
        configField.objectType = typeof(QuestConfig);
        configField.RegisterValueChangedCallback(evt => SetConfig(evt.newValue as QuestConfig));

        searchField = rootVisualElement.Q<ToolbarSearchField>("search-field");
        searchField.RegisterValueChangedCallback(_ => RefreshRows());

        grid = rootVisualElement.Q<MultiColumnListView>("table");
        grid.fixedItemHeight = 28;
        grid.selectionType = SelectionType.Multiple;
        grid.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        grid.itemsSource = rows;
        grid.RegisterCallback<PointerDownEvent>(OnGridPointerDown, TrickleDown.TrickleDown);

        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        rootVisualElement.Q<Button>("import-button").clicked += ImportExcel;
        rootVisualElement.Q<Button>("validate-button").clicked += Validate;
        rootVisualElement.Q<Button>("loc-button").clicked
            += () => LocWorkbenchWindow.OpenTable(LocTableSet.QuestSystem);
        rootVisualElement.Q<Button>("save-button").clicked += Save;
        rootVisualElement.Q<Button>("add-button").clicked += AddRecord;
        rootVisualElement.Q<Button>("duplicate-button").clicked += DuplicateSelected;
        rootVisualElement.Q<Button>("delete-button").clicked += DeleteSelected;

        BuildTabs();

        QuestConfig loaded =
            AssetDatabase.LoadAssetAtPath<QuestConfig>(QuestConfig.AssetPath);
        configField.SetValueWithoutNotify(loaded);
        SetConfig(loaded);
    }

    #region 页签与表格

    void BuildTabs()
    {
        tabsRoot.Clear();
        tabButtons.Clear();
        foreach (ITable value in tables)
        {
            ITable captured = value;
            Button button = new(() => SwitchTable(captured)) { text = value.Title };
            button.AddToClassList("quest-tab-btn");
            button.EnableInClassList("quest-tab-btn-active", value == table);
            tabButtons.Add(value, button);
            tabsRoot.Add(button);
        }
    }

    void SwitchTable(ITable value)
    {
        if (table == value) return;

        table = value;
        foreach (KeyValuePair<ITable, Button> pair in tabButtons)
            pair.Value.EnableInClassList("quest-tab-btn-active", pair.Key == table);

        ClearRows();
        BuildColumns();
        RefreshRows();
    }

    void SetConfig(QuestConfig value)
    {
        config = value;
        undo.Reset(config);
        ClearRows();
        BuildColumns();
        RefreshRows();
    }

    /// <summary>columns 增删会立刻重绑，所以换表前先把行清掉，别让新列去读旧表的记录。</summary>
    void ClearRows()
    {
        rows.Clear();
        grid.ClearSelection();
        grid.RefreshItems();
    }

    /// <summary>这一行的记录，顺带挡住「列和行还没对上」的那一帧。</summary>
    object Record(int index, FieldInfo field)
        => index >= 0 && index < rows.Count && field.DeclaringType.IsInstanceOfType(rows[index].Value)
            ? rows[index].Value
            : null;

    /// <summary>列直接从记录类型的字段生成，所以数据类加个字段、表格就自动多一列。</summary>
    void BuildColumns()
    {
        grid.columns.Clear();
        AddColumn(table.CreateKeyColumn(this));

        foreach (FieldInfo field in QuestEditorFields.Fields(table.RecordType))
            AddColumn(CreateColumn(field));
    }

    /// <summary>加一列，顺手接上「宽度记在 EditorPrefs 里」，下次开窗还是这个宽度。</summary>
    void AddColumn(UIColumn column)
    {
        string key = WidthKey(table.Title, column.title);
        float saved = EditorPrefs.GetFloat(key, 0f);
        if (saved > 0f) column.width = saved;

        column.propertyChanged += (_, evt) =>
        {
            if (evt.propertyName == nameof(UIColumn.width)) EditorPrefs.SetFloat(key, column.width.value);
        };
        grid.columns.Add(column);
    }

    static string WidthKey(string tableTitle, string columnTitle)
        => $"QuestEditor.ColumnWidth.{tableTitle}.{columnTitle}";

    UIColumn CreateColumn(FieldInfo field)
    {
        string title = QuestEditorFields.Label(field);
        Type type = field.FieldType;

        if (type == typeof(string)) return Inline<TextField, string>(title, 170, field);
        if (type == typeof(int)) return Inline<IntegerField, int>(title, 90, field);
        if (type == typeof(bool)) return Inline<Toggle, bool>(title, 90, field);
        if (type.IsEnum) return EnumColumn(title, 140, field);
        if (type == typeof(AssetReferenceSprite)) return IconColumn(title, 80, field);

        // 单个 ID 引用（接受条件这种）候选不多，直接在格子里下拉着改，不用开弹层
        if (type == typeof(long))
        {
            QuestRefKind? kind = QuestRefField.KindOf(field);
            if (kind != null && QuestRefField.FitsInCell(kind.Value)) return RefColumn(title, 200, field, kind.Value);
        }

        // 剩下装不进格子的（多语言、列表、多态）显示摘要，点开在弹层里编辑
        return SummaryColumn(title, Width(type), field);
    }

    static float Width(Type type)
    {
        if (type == typeof(LocKeyRef)) return 220;
        if (type == typeof(long)) return 190;
        return 150;
    }

    /// <summary>能直接在格子里改的那几种。控件在 makeCell 建一次，绑定时把记录挂到 userData 上。</summary>
    UIColumn Inline<TField, TValue>(string title, float width, FieldInfo field)
        where TField : BaseField<TValue>, new()
        => new()
        {
            title = title,
            width = width,
            makeCell = () =>
            {
                TField control = new();
                control.AddToClassList("quest-cell");
                control.RegisterValueChangedCallback(evt =>
                {
                    if (control.userData == null) return;

                    field.SetValue(control.userData, evt.newValue);
                    MarkDirty((control.userData, field));
                });
                return control;
            },
            bindCell = (element, index) =>
            {
                TField control = (TField)element;
                control.userData = null;

                object record = Record(index, field);
                if (record == null) return;

                control.SetValueWithoutNotify((TValue)field.GetValue(record));
                control.userData = record;
            },
        };

    UIColumn EnumColumn(string title, float width, FieldInfo field)
        => new()
        {
            title = title,
            width = width,
            makeCell = () =>
            {
                EnumField control = new();
                control.AddToClassList("quest-cell");
                control.RegisterValueChangedCallback(evt =>
                {
                    if (control.userData == null) return;

                    field.SetValue(control.userData, evt.newValue);
                    MarkDirty((control.userData, field));
                });
                return control;
            },
            bindCell = (element, index) =>
            {
                EnumField control = (EnumField)element;
                control.userData = null;

                object record = Record(index, field);
                if (record == null) return;

                control.Init((Enum)field.GetValue(record));
                control.userData = record;
            },
        };

    /// <summary>单个 ID 引用的格子：下拉里是「ID | 备注」，存回去的是 ID。</summary>
    UIColumn RefColumn(string title, float width, FieldInfo field, QuestRefKind kind)
    {
        IReadOnlyList<QuestRefOption> options = QuestRefCatalog.Options(kind);
        List<string> choices = options.Select(option => option.Label).ToList();

        return new UIColumn
        {
            title = title,
            width = width,
            makeCell = () =>
            {
                DropdownField control = new() { choices = choices };
                control.AddToClassList("quest-cell");
                control.RegisterValueChangedCallback(evt =>
                {
                    if (control.userData == null) return;

                    int picked = choices.IndexOf(evt.newValue);
                    if (picked < 0) return;

                    field.SetValue(control.userData, options[picked].Id);
                    MarkDirty((control.userData, field));
                });
                return control;
            },
            bindCell = (element, index) =>
            {
                DropdownField control = (DropdownField)element;
                control.userData = null;

                object record = Record(index, field);
                if (record == null) return;

                // 配了个已经不存在的 ID 时显示「1234 | 未找到」，不静悄悄改成别的
                control.SetValueWithoutNotify(QuestRefField.Text(kind, (long)field.GetValue(record)));
                control.userData = record;
            },
        };
    }

    /// <summary>图标列：格子里直接显示小图（没配就显示占位文字），点开还是在弹层里换图。</summary>
    UIColumn IconColumn(string title, float width, FieldInfo field)
        => new()
        {
            title = title,
            width = width,
            makeCell = () =>
            {
                Button button = new();
                button.AddToClassList("quest-cell-icon");

                Image preview = new() { scaleMode = ScaleMode.ScaleToFit };
                preview.AddToClassList("quest-cell-icon-image");
                button.Add(preview);

                Label empty = new();
                empty.AddToClassList("quest-cell-icon-empty");
                button.Add(empty);

                button.clicked += () =>
                {
                    if (button.userData != null) OpenFieldEditor(button.userData, field);
                };
                return button;
            },
            bindCell = (element, index) =>
            {
                Button button = (Button)element;
                object record = Record(index, field);
                button.userData = record;

                Sprite sprite = record == null
                    ? null
                    : QuestIconEditorUtility.GetSprite((AssetReferenceSprite)field.GetValue(record));

                Image preview = (Image)button[0];
                Label empty = (Label)button[1];
                preview.sprite = sprite;
                preview.style.display = sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
                empty.style.display = sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
                empty.text = record == null ? string.Empty : "未配置";
                button.tooltip = sprite == null ? string.Empty : sprite.name;
            },
        };

    UIColumn SummaryColumn(string title, float width, FieldInfo field)
        => new()
        {
            title = title,
            width = width,
            makeCell = () =>
            {
                Button button = new();
                button.AddToClassList("quest-cell-button");
                button.clicked += () =>
                {
                    if (button.userData != null) OpenFieldEditor(button.userData, field);
                };
                return button;
            },
            bindCell = (element, index) =>
            {
                Button button = (Button)element;
                object record = Record(index, field);
                button.userData = record;
                button.text = record == null ? string.Empty : QuestEditorFields.Summary(record, field);
            },
        };

    void RefreshRows()
    {
        rows.Clear();
        if (config != null)
        {
            string query = searchField?.value ?? string.Empty;
            foreach ((object key, object value, string search) in table.Rows(config))
            {
                if (query.Length > 0 && search.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                rows.Add(new Row { Key = key, Value = value, Search = search });
            }
        }

        grid.ClearSelection();
        grid.RefreshItems();
        statusLabel.text = config == null
            ? "先在上面选一个任务配置资产（Assets/AddressableAssets/Remote/Config/QuestConfig.asset）。"
            : $"{table.Title}：{rows.Count} 条。列表 / 触发 / 目标 / 奖励的格子点开在弹层里编辑；"
              + "Ctrl / Shift ＋点行可多选，Ctrl+Z 撤销、Ctrl+Y 重做。";
    }

    #endregion

    #region 多选与快捷键

    /// <summary>
    /// 格子里铺满了输入控件，点哪都被控件吃掉，所以按住 Ctrl/Shift 时先把这一下当成选行。
    /// 不带修饰键的点击照旧交给控件，只记下连选的起点。
    /// </summary>
    void OnGridPointerDown(PointerDownEvent evt)
    {
        int index = RowIndexAt(evt.target as VisualElement);
        if (index < 0) return;

        bool toggle = evt.ctrlKey || evt.commandKey;
        if (!toggle && !evt.shiftKey)
        {
            // 不带修饰键就是「只选这一行」，多选靠它退回来。不拦事件，格子照旧能进编辑
            anchorIndex = index;
            if (grid.selectedIndices.Count() != 1 || grid.selectedIndex != index) grid.SetSelection(index);
            return;
        }

        List<int> selection = new(grid.selectedIndices);
        if (evt.shiftKey && anchorIndex >= 0)
        {
            selection.Clear();
            for (int i = Mathf.Min(anchorIndex, index); i <= Mathf.Max(anchorIndex, index); i++) selection.Add(i);
        }
        else
        {
            if (!selection.Remove(index)) selection.Add(index);
            anchorIndex = index;
        }

        grid.SetSelection(selection);
        evt.StopPropagation();
    }

    void ClearSelection()
    {
        anchorIndex = -1;
        grid.ClearSelection();
    }

    /// <summary>光标在文本输入里，这时 Esc / Ctrl+A 是输入框自己的语义。</summary>
    static bool InTextField(VisualElement element)
    {
        for (VisualElement node = element; node != null; node = node.parent)
        {
            if (node is ITextEdition) return true;
        }
        return false;
    }

    /// <summary>单元格控件都把记录挂在 userData 上，顺着父级找回它是第几行。</summary>
    int RowIndexAt(VisualElement element)
    {
        for (VisualElement node = element; node != null; node = node.parent)
        {
            if (node.userData == null) continue;

            int index = rows.FindIndex(row => ReferenceEquals(row.Value, node.userData));
            if (index >= 0) return index;
        }
        return -1;
    }

    /// <summary>拦在 TrickleDown：不拦住的话 Ctrl+Z 会漏给 Unity 的全局撤销，去撤销别处的操作。</summary>
    void OnKeyDown(KeyDownEvent evt)
    {
        bool typing = InTextField(evt.target as VisualElement);

        // Esc：开着弹层就关弹层，否则清空选择。正在输入时留给输入框自己撤销这次编辑
        if (evt.keyCode == KeyCode.Escape && !typing)
        {
            if (overlay.ClassListContains("is-hidden")) ClearSelection();
            else CloseOverlay();

            evt.StopImmediatePropagation();
            return;
        }

        if (!evt.ctrlKey && !evt.commandKey) return;

        // Ctrl+A 全选行；输入框里它是全选文本，不能抢
        if (evt.keyCode == KeyCode.A && !evt.shiftKey && !typing)
        {
            anchorIndex = rows.Count > 0 ? 0 : -1;
            grid.SetSelection(Enumerable.Range(0, rows.Count).ToList());
            evt.StopImmediatePropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Z && !evt.shiftKey) PerformUndo();
        else if (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey)) PerformRedo();
        else return;

        evt.StopImmediatePropagation();
    }

    void PerformUndo()
    {
        if (!undo.Undo(config))
        {
            statusLabel.text = "没有可撤销的操作。";
            return;
        }

        AfterHistoryStep($"已撤销，还能撤销 {undo.UndoCount} 步。");
    }

    void PerformRedo()
    {
        if (!undo.Redo(config))
        {
            statusLabel.text = "没有可重做的操作。";
            return;
        }

        AfterHistoryStep($"已重做，还能重做 {undo.RedoCount} 步。");
    }

    /// <summary>撤销换掉的是整份记录对象，弹层和下拉候选都指着旧的，全部重建。</summary>
    void AfterHistoryStep(string message)
    {
        CloseOverlay();
        QuestRefCatalog.ClearCache();
        EditorUtility.SetDirty(config);
        hasUnsavedChanges = true;

        BuildColumns();
        RefreshRows();
        statusLabel.text = message;
    }

    #endregion

    #region 弹层

    void OpenFieldEditor(object record, FieldInfo field)
        => OpenOverlay(QuestEditorFields.Label(field),
            content => content.Add(QuestEditorFields.BuildOne(record, field, MarkDirty)));

    void OpenOverlay(string title, Action<ScrollView> buildContent)
    {
        overlay.Clear();
        overlay.RemoveFromClassList("is-hidden");

        VisualElement panel = new();
        panel.AddToClassList("quest-popup");

        VisualElement header = new();
        header.AddToClassList("quest-popup-header");
        Label label = new(title);
        label.AddToClassList("quest-popup-title");
        Button close = new(CloseOverlay) { text = "关闭" };
        close.AddToClassList("quest-btn");
        header.Add(label);
        header.Add(close);
        panel.Add(header);

        ScrollView content = new();
        content.AddToClassList("quest-popup-content");
        panel.Add(content);
        buildContent(content);

        overlay.Add(panel);
    }

    void CloseOverlay()
    {
        overlay.AddToClassList("is-hidden");
        overlay.Clear();
        grid.RefreshItems();
    }

    #endregion

    #region 增删改存

    List<Row> SelectedRows => grid.selectedItems.OfType<Row>().ToList();

    void AddRecord()
    {
        if (config == null) return;

        object key = table.Add(config);
        MarkDirty();
        RefreshRows();
        SelectByKeys(new[] { key });
    }

    void DuplicateSelected()
    {
        if (config == null) return;

        List<Row> targets = SelectedRows;
        if (targets.Count == 0) return;

        List<object> keys = targets.Select(row => table.Duplicate(config, row.Key)).ToList();
        MarkDirty();
        RefreshRows();
        SelectByKeys(keys);
    }

    void DeleteSelected()
    {
        if (config == null) return;

        List<Row> targets = SelectedRows;
        if (targets.Count == 0) return;

        string message = targets.Count == 1
            ? $"删除 {targets[0].Search}？"
            : $"删除选中的 {targets.Count} 条记录？";
        if (!EditorUtility.DisplayDialog("删除记录", message, "删除", "取消")) return;

        foreach (Row row in targets) table.Delete(config, row.Key);
        MarkDirty();
        RefreshRows();
    }

    /// <summary>改 ID 就是换字典 Key，成功后要重排。</summary>
    void Rekey(object record, object newKey)
    {
        Row row = rows.Find(item => ReferenceEquals(item.Value, record));
        if (row == null || !table.TryRekey(config, row.Key, newKey)) return;

        MarkDirty();
        RefreshRows();
    }

    void SelectByKeys(IEnumerable<object> keys)
    {
        List<int> indices = new();
        foreach (object key in keys)
        {
            int index = rows.FindIndex(row => Equals(row.Key, key));
            if (index >= 0) indices.Add(index);
        }

        if (indices.Count == 0) return;

        anchorIndex = indices[0];
        grid.SetSelection(indices);
    }

    void MarkDirty() => MarkDirty(null);

    /// <summary>
    /// <paramref name="coalesceKey"/> 相同的连续改动在撤销栈里并成一步 —— 传（记录, 字段），
    /// 否则在一个输入框里打十个字就是十步。传 null 表示这次单独算一步。
    /// </summary>
    void MarkDirty(object coalesceKey)
    {
        if (config == null) return;

        config.EditorSyncIds();
        undo.Record(config, coalesceKey);
        EditorUtility.SetDirty(config);
        hasUnsavedChanges = true;
    }

    void Validate()
    {
        if (config == null) return;

        QuestConfigValidator.CheckStructure(config);
        statusLabel.text = "校验结果见 Console（道具/角色的存在性要进游戏后才查）。";
    }

    void ImportExcel()
    {
        QuestLubanMigration.ImportFromMenu();
        SetConfig(AssetDatabase.LoadAssetAtPath<QuestConfig>(QuestConfig.AssetPath));
    }

    void Save()
    {
        if (config == null) return;

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        QuestConfigProvider.ClearCache();
        QuestRefCatalog.ClearCache();
        hasUnsavedChanges = false;
        statusLabel.text = "已保存。";
    }

    public override void SaveChanges()
    {
        Save();
        base.SaveChanges();
    }

    #endregion

    #region 五张表

    interface ITable
    {
        string Title { get; }
        Type RecordType { get; }
        IEnumerable<(object Key, object Value, string Search)> Rows(QuestConfig config);
        object Add(QuestConfig config);
        object Duplicate(QuestConfig config, object key);
        void Delete(QuestConfig config, object key);
        bool TryRekey(QuestConfig config, object oldKey, object newKey);
        UIColumn CreateKeyColumn(QuestEditorWindow window);
    }

    /// <summary>long 为 ID 的四张表，差别只在取哪个字典、备注在哪个属性上。</summary>
    class Table<TKey, TValue> : ITable where TValue : class, new()
    {
        readonly Func<QuestConfig, Dictionary<TKey, TValue>> dict;
        readonly Func<TValue, string> remark;

        public Table(string title, Func<QuestConfig, Dictionary<TKey, TValue>> dict, Func<TValue, string> remark)
        {
            Title = title;
            this.dict = dict;
            this.remark = remark;
        }

        public string Title { get; }
        public Type RecordType => typeof(TValue);

        public IEnumerable<(object Key, object Value, string Search)> Rows(QuestConfig config)
            => dict(config)
                .OrderBy(pair => pair.Key)
                .Select(pair => ((object)pair.Key, (object)pair.Value, $"{pair.Key} {remark(pair.Value)}"));

        public object Add(QuestConfig config)
        {
            Dictionary<TKey, TValue> target = dict(config);
            TKey key = NextKey(target);
            target[key] = new TValue();
            return key;
        }

        public object Duplicate(QuestConfig config, object key)
        {
            Dictionary<TKey, TValue> target = dict(config);
            if (!target.TryGetValue((TKey)key, out TValue source)) return key;

            TKey newKey = NextKey(target);
            target[newKey] = Sirenix.Serialization.SerializationUtility.CreateCopy(source) as TValue ?? new TValue();
            return newKey;
        }

        public void Delete(QuestConfig config, object key) => dict(config).Remove((TKey)key);

        public bool TryRekey(QuestConfig config, object oldKey, object newKey)
        {
            Dictionary<TKey, TValue> target = dict(config);
            TKey from = (TKey)oldKey;
            TKey to = (TKey)newKey;

            if (Equals(from, to) || !target.TryGetValue(from, out TValue value)) return false;
            if (target.ContainsKey(to))
            {
                Debug.LogError($"[Quest] {Title} 已经有 {to} 了，ID 不能重复");
                return false;
            }

            target.Remove(from);
            target[to] = value;
            return true;
        }

        public virtual UIColumn CreateKeyColumn(QuestEditorWindow window)
            => new()
            {
                title = "ID",
                width = 110,
                makeCell = () =>
                {
                    LongField control = new();
                    control.AddToClassList("quest-cell");
                    // 每敲一个字符就换 Key 会把表打乱，所以只在失焦/回车时提交
                    control.RegisterCallback<FocusOutEvent>(_ =>
                    {
                        if (control.userData != null)
                            window.Rekey(control.userData, Convert.ChangeType(control.value, typeof(TKey)));
                    });
                    return control;
                },
                bindCell = (element, index) =>
                {
                    LongField control = (LongField)element;
                    control.userData = null;
                    if (index >= window.rows.Count || window.rows[index].Value is not TValue) return;

                    control.SetValueWithoutNotify(Convert.ToInt64(window.rows[index].Key));
                    control.userData = window.rows[index].Value;
                },
            };

        /// <summary>新 ID 取现有最大值 +1，空表从 10001 开始。</summary>
        protected virtual TKey NextKey(Dictionary<TKey, TValue> target)
        {
            long next = 10001;
            foreach (TKey key in target.Keys) next = Math.Max(next, Convert.ToInt64(key) + 1);
            return (TKey)Convert.ChangeType(next, typeof(TKey));
        }
    }

    /// <summary>奖励显示表的 Key 是枚举，所以新建和改 Key 都在「还没配过的类型」里挑。</summary>
    sealed class RewardViewTable : Table<QuestRewardType, QuestRewardPresentation>
    {
        public RewardViewTable()
            : base("奖励显示", data => data.EditorRewardViews, value => value.Remark) { }

        protected override QuestRewardType NextKey(Dictionary<QuestRewardType, QuestRewardPresentation> target)
        {
            foreach (QuestRewardType type in Enum.GetValues(typeof(QuestRewardType)))
            {
                if (type != QuestRewardType.None && !target.ContainsKey(type)) return type;
            }

            Debug.LogError("[Quest] 每种奖励类型都配过了，没有可新增的了");
            return QuestRewardType.None;
        }

        public override UIColumn CreateKeyColumn(QuestEditorWindow window)
            => new()
            {
                title = "奖励类型",
                width = 160,
                makeCell = () =>
                {
                    EnumField control = new();
                    control.AddToClassList("quest-cell");
                    control.RegisterValueChangedCallback(evt =>
                    {
                        if (control.userData != null) window.Rekey(control.userData, evt.newValue);
                    });
                    return control;
                },
                bindCell = (element, index) =>
                {
                    EnumField control = (EnumField)element;
                    control.userData = null;
                    if (index >= window.rows.Count || window.rows[index].Value is not QuestRewardPresentation) return;

                    control.Init((QuestRewardType)window.rows[index].Key);
                    control.userData = window.rows[index].Value;
                },
            };
    }

    #endregion
}
