using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using XFramework;
using UIColumn = UnityEngine.UIElements.Column;

public class QuestEditorWindow : EditorWindow
{
    const int LocalizationPageSize = 8;

    enum Section
    {
        [InspectorName("任务")] Quests,
        [InspectorName("目标")] Objectives,
        [InspectorName("接受条件")] Conditions,
        [InspectorName("类别")] Categories,
        [InspectorName("奖励显示")] RewardPresentations,
    }

    static readonly Dictionary<Section, string> SectionNames = new()
    {
        { Section.Quests, "任务" },
        { Section.Objectives, "目标" },
        { Section.Conditions, "接受条件" },
        { Section.Categories, "类别" },
        { Section.RewardPresentations, "奖励显示" },
    };

    readonly List<object> visibleRecords = new();
    readonly Dictionary<Section, Button> sectionButtons = new();
    readonly Dictionary<Section, Dictionary<string, float>> pendingColumnWidths = new();

    QuestDatabaseData database;
    SerializedObject serializedDatabase;
    Section section;
    IVisualElementScheduledItem columnWidthSaveItem;

    ObjectField databaseField;
    EnumField sourceModeField;
    ToolbarSearchField searchField;
    MultiColumnListView table;
    VisualElement validationView;
    VisualElement overlay;
    Label statusLabel;

    [MenuItem("Tools/QuestSystem - 任务系统/QuestEditor - 任务编辑器")]
    public static void Open()
    {
        QuestEditorWindow window = GetWindow<QuestEditorWindow>();
        window.titleContent = new GUIContent("任务编辑器");
        window.minSize = new Vector2(1100, 620);
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        saveChangesMessage = "任务配置还有未保存的修改，是否保存后关闭？";
    }

    void OnDisable()
    {
        columnWidthSaveItem?.Pause();
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnInspectorUpdate()
    {
        if (database != null && EditorUtility.IsDirty(database)) hasUnsavedChanges = true;
    }

    public override void SaveChanges()
    {
        Save();
        base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        columnWidthSaveItem?.Pause();
        pendingColumnWidths.Clear();
        string path = database == null ? string.Empty : AssetDatabase.GetAssetPath(database);
        hasUnsavedChanges = false;
        if (!string.IsNullOrEmpty(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        QuestDatabaseProvider.ClearCache();
        base.DiscardChanges();
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        sectionButtons.Clear();
        root.AddToClassList("quest-editor");
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/0 Core/1 Script/Core/QuestSystem/Editor/QuestEditor/QuestEditorWindow.uss");
        root.styleSheets.Add(styleSheet);
        root.Add(BuildToolbar());
        root.Add(BuildSectionTabs());

        VisualElement workArea = new();
        workArea.AddToClassList("quest-work-area");
        workArea.Add(BuildTableToolbar());
        workArea.Add(BuildTable());

        validationView = new VisualElement();
        validationView.AddToClassList("quest-validation");
        workArea.Add(validationView);

        statusLabel = new Label();
        statusLabel.AddToClassList("quest-status");
        workArea.Add(statusLabel);
        root.Add(workArea);

        overlay = new VisualElement();
        overlay.AddToClassList("quest-overlay");
        overlay.AddToClassList("is-hidden");
        root.Add(overlay);

        QuestDatabaseData loaded =
            AssetDatabase.LoadAssetAtPath<QuestDatabaseData>(QuestLubanMigration.DatabaseAssetPath);
        if (loaded == null) loaded = QuestLubanMigration.ImportFromExcel(false);
        SetDatabase(loaded);
    }

    VisualElement BuildToolbar()
    {
        VisualElement toolbar = new();
        toolbar.AddToClassList("quest-settings-bar");

        databaseField = new ObjectField("数据库")
        {
            objectType = typeof(QuestDatabaseData),
            allowSceneObjects = false,
        };
        databaseField.AddToClassList("quest-database-field");
        databaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as QuestDatabaseData));
        toolbar.Add(databaseField);

        sourceModeField = new EnumField("数据源", QuestConfigSourceMode.LubanOnly);
        sourceModeField.AddToClassList("quest-source-mode-field");
        sourceModeField.RegisterValueChangedCallback(evt =>
        {
            if (database == null) return;
            Undo.RecordObject(database, "切换任务数据源");
            database.SourceMode = (QuestConfigSourceMode)evt.newValue;
            MarkDirty();
            Save();
            ShowSourceModeHint();
        });
        toolbar.Add(sourceModeField);

        toolbar.Add(CreateButton("重新导入 Excel", ImportExcel));
        toolbar.Add(CreateButton("校验", ValidateDatabase));
        toolbar.Add(CreateButton("配置多语言", () => LocWorkbenchWindow.OpenTable(LocTableSet.QuestSystem)));
        toolbar.Add(CreateButton("保存", Save, "quest-btn-primary"));
        return toolbar;
    }

    VisualElement BuildSectionTabs()
    {
        VisualElement tabs = new();
        tabs.AddToClassList("quest-tab-bar");
        foreach (Section value in Enum.GetValues(typeof(Section)))
        {
            Button button = new(() => SwitchSection(value)) { text = SectionNames[value] };
            button.AddToClassList("quest-tab-btn");
            button.EnableInClassList("quest-tab-btn-active", value == section);
            sectionButtons.Add(value, button);
            tabs.Add(button);
        }
        return tabs;
    }

    VisualElement BuildTableToolbar()
    {
        VisualElement toolbar = new();
        toolbar.AddToClassList("quest-grid-toolbar");
        searchField = new ToolbarSearchField();
        searchField.AddToClassList("quest-search");
        searchField.RegisterValueChangedCallback(_ => RefreshTable());
        toolbar.Add(searchField);
        toolbar.Add(CreateButton("＋ 新增行", AddRecord));
        toolbar.Add(CreateButton("复制选中行", DuplicateSelected));
        toolbar.Add(CreateButton("删除选中行", DeleteSelected, "quest-btn-danger"));
        return toolbar;
    }

    static Button CreateButton(string text, Action clicked, string extraClass = null)
    {
        Button button = new(clicked) { text = text };
        button.AddToClassList("quest-btn");
        if (!string.IsNullOrEmpty(extraClass)) button.AddToClassList(extraClass);
        return button;
    }

    VisualElement BuildTable()
    {
        table = new MultiColumnListView
        {
            fixedItemHeight = 28,
            selectionType = SelectionType.Single,
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
        };
        table.AddToClassList("quest-table");
        return table;
    }

    void SwitchSection(Section value)
    {
        if (section == value) return;
        FlushPendingColumnWidths();
        section = value;
        foreach ((Section key, Button button) in sectionButtons)
            button.EnableInClassList("quest-tab-btn-active", key == section);
        ConfigureColumns();
        RefreshTable();
        CloseOverlay();
    }

    void SetDatabase(QuestDatabaseData value)
    {
        columnWidthSaveItem?.Pause();
        pendingColumnWidths.Clear();
        database = value;
        serializedDatabase = database == null ? null : new SerializedObject(database);
        hasUnsavedChanges = database != null && EditorUtility.IsDirty(database);
        databaseField?.SetValueWithoutNotify(database);
        sourceModeField?.SetValueWithoutNotify(database == null
            ? QuestConfigSourceMode.LubanOnly
            : database.SourceMode);
        ConfigureColumns();
        RefreshTable();
        ShowSourceModeHint();
    }

    /// <summary>先解除旧数据源，再重建列，避免虚拟化行在页签切换时绑定到错误的数据类型。</summary>
    void ConfigureColumns()
    {
        if (table == null) return;
        table.ClearSelection();
        table.itemsSource = Array.Empty<object>();
        table.Rebuild();
        table.columns.Clear();
        switch (section)
        {
            case Section.Quests:
                ConfigureQuestColumns();
                break;
            case Section.Objectives:
                ConfigureObjectiveColumns();
                break;
            case Section.Conditions:
                ConfigureConditionColumns();
                break;
            case Section.Categories:
                ConfigureCategoryColumns();
                break;
            case Section.RewardPresentations:
                ConfigureRewardPresentationColumns();
                break;
        }
        AddActionsColumn();
        RestoreColumnWidths();
        TrackColumnWidths();
    }

    void ConfigureQuestColumns()
    {
        AddEnabledColumn(row => ((QuestDefinition)row).enabled, (row, value) => ((QuestDefinition)row).enabled = value);
        AddIdColumn(row => ((QuestDefinition)row).id, (row, value) => ((QuestDefinition)row).id = value);
        AddTextColumn("备注", 150, row => ((QuestDefinition)row).remark,
            (row, value) => ((QuestDefinition)row).remark = value);
        AddButtonColumn("领取触发", 190,
            row => SpecsSummary(((QuestDefinition)row).triggers, spec => QuestTriggerTypeLabels.Get(spec.type)),
            row => OpenPropertyEditor(row, "领取触发", "triggers"));
        AddReferenceColumn("接受条件", 180, QuestReferenceKind.Condition, true,
            row => ((QuestDefinition)row).acceptConditionId,
            (row, value) => ((QuestDefinition)row).acceptConditionId = value);
        AddButtonColumn("名称", 210, row => LocalizedSummary(((QuestDefinition)row).name),
            row => OpenLocalizationEditor(row, "任务名称", QuestLocKey.Prefix.Quest,
                data => ((QuestDefinition)data).name,
                (data, value) => ((QuestDefinition)data).name = value));
        AddButtonColumn("描述", 210, row => LocalizedSummary(((QuestDefinition)row).desc),
            row => OpenLocalizationEditor(row, "任务描述", QuestLocKey.Prefix.Quest,
                data => ((QuestDefinition)data).desc,
                (data, value) => ((QuestDefinition)data).desc = value));
        AddButtonColumn("图标", 90, row => IconSummary(((QuestDefinition)row).icon),
            row => OpenIconEditor(row, "任务图标",
                data => ((QuestDefinition)data).icon,
                (data, value) => ((QuestDefinition)data).icon = value));
        AddButtonColumn("目标 ID", 180, row => IdListSummary(((QuestDefinition)row).objectiveIds, QuestReferenceKind.Objective),
            row => OpenLongListEditor(row, "目标 ID", "objectiveIds", QuestReferenceKind.Objective));
        AddButtonColumn("任务奖励", 140, row => RewardsSummary(((QuestDefinition)row).rewards),
            row => OpenPropertyEditor(row, "任务奖励", "rewards"));
        AddToggleColumn("顺序完成", 82, row => ((QuestDefinition)row).objectivesInOrder,
            (row, value) => ((QuestDefinition)row).objectivesInOrder = value);
    }

    void ConfigureObjectiveColumns()
    {
        AddEnabledColumn(row => ((QuestObjectiveDefinition)row).enabled,
            (row, value) => ((QuestObjectiveDefinition)row).enabled = value);
        AddIdColumn(row => ((QuestObjectiveDefinition)row).id,
            (row, value) => ((QuestObjectiveDefinition)row).id = value);
        AddTextColumn("备注", 150, row => ((QuestObjectiveDefinition)row).remark,
            (row, value) => ((QuestObjectiveDefinition)row).remark = value);
        AddButtonColumn("目标描述", 230, row => LocalizedSummary(((QuestObjectiveDefinition)row).desc),
            row => OpenLocalizationEditor(row, "目标描述", QuestLocKey.Prefix.Objective,
                data => ((QuestObjectiveDefinition)data).desc,
                (data, value) => ((QuestObjectiveDefinition)data).desc = value));
        AddObjectiveTypeColumn("目标类型", 240, row => ((QuestObjectiveDefinition)row).objective.type,
            (row, value) => ((QuestObjectiveDefinition)row).objective.type = value);
        AddButtonColumn("目标参数", 130, row => ObjectiveSummary(((QuestObjectiveDefinition)row).objective),
            row => OpenPropertyEditor(row, "目标参数", "objective"));
        AddButtonColumn("目标奖励", 130, row => RewardsSummary(((QuestObjectiveDefinition)row).rewards),
            row => OpenPropertyEditor(row, "目标奖励", "rewards"));
        AddToggleColumn("超额目标", 78, row => ((QuestObjectiveDefinition)row).hasExtra,
            (row, value) => ((QuestObjectiveDefinition)row).hasExtra = value);
        AddButtonColumn("超额描述", 230, row => LocalizedSummary(((QuestObjectiveDefinition)row).extraDesc),
            row => OpenLocalizationEditor(row, "超额描述", QuestLocKey.Prefix.Objective,
                data => ((QuestObjectiveDefinition)data).extraDesc,
                (data, value) => ((QuestObjectiveDefinition)data).extraDesc = value));
        AddObjectiveTypeColumn("超额类型", 240, row => ((QuestObjectiveDefinition)row).extraObjective.type,
            (row, value) => ((QuestObjectiveDefinition)row).extraObjective.type = value);
        AddButtonColumn("超额参数", 130, row => ObjectiveSummary(((QuestObjectiveDefinition)row).extraObjective),
            row => OpenPropertyEditor(row, "超额参数", "extraObjective"));
        AddButtonColumn("超额奖励", 130, row => RewardsSummary(((QuestObjectiveDefinition)row).extraRewards),
            row => OpenPropertyEditor(row, "超额奖励", "extraRewards"));
    }

    void ConfigureConditionColumns()
    {
        AddEnabledColumn(row => ((QuestConditionDefinition)row).enabled,
            (row, value) => ((QuestConditionDefinition)row).enabled = value);
        AddIdColumn(row => ((QuestConditionDefinition)row).id,
            (row, value) => ((QuestConditionDefinition)row).id = value);
        AddTextColumn("备注", 150, row => ((QuestConditionDefinition)row).remark,
            (row, value) => ((QuestConditionDefinition)row).remark = value);
        AddButtonColumn("道具要求", 120, row => CountSummary(((QuestConditionDefinition)row).items.Count),
            row => OpenPropertyEditor(row, "道具持有要求", "items"));
        AddButtonColumn("角色属性", 120, row => CountSummary(((QuestConditionDefinition)row).characterProps.Count),
            row => OpenPropertyEditor(row, "角色属性要求", "characterProps"));
        AddButtonColumn("前置剧情", 130, row => RawIdListSummary(((QuestConditionDefinition)row).plotPrerequisites),
            row => OpenLongListEditor(row, "前置剧情 ID", "plotPrerequisites", null));
        AddButtonColumn("前置对话", 170,
            row => IdListSummary(((QuestConditionDefinition)row).dialoguePrerequisites, QuestReferenceKind.Dialogue),
            row => OpenLongListEditor(row, "前置对话 ID", "dialoguePrerequisites", QuestReferenceKind.Dialogue));
        AddIntColumn("最早天数", 80, row => ((QuestConditionDefinition)row).day,
            (row, value) => ((QuestConditionDefinition)row).day = value);
        AddEnumColumn("时间段", 120, row => ((QuestConditionDefinition)row).timeSlot,
            (row, value) => ((QuestConditionDefinition)row).timeSlot = value);
        AddButtonColumn("前置任务", 180,
            row => IdListSummary(((QuestConditionDefinition)row).questPrerequisites, QuestReferenceKind.Quest),
            row => OpenLongListEditor(row, "前置任务 ID", "questPrerequisites", QuestReferenceKind.Quest));
        AddButtonColumn("满足分支", 120, row => RawIdListSummary(((QuestConditionDefinition)row).satisfyBranches),
            row => OpenLongListEditor(row, "满足时分支 ID", "satisfyBranches", null));
        AddButtonColumn("不满足分支", 120,
            row => RawIdListSummary(((QuestConditionDefinition)row).notSatisfyBranches),
            row => OpenLongListEditor(row, "不满足时分支 ID", "notSatisfyBranches", null));
        AddIntColumn("游戏分数", 85, row => ((QuestConditionDefinition)row).gameScore,
            (row, value) => ((QuestConditionDefinition)row).gameScore = value);
    }

    void ConfigureCategoryColumns()
    {
        AddEnabledColumn(row => ((QuestCategoryDefinition)row).enabled,
            (row, value) => ((QuestCategoryDefinition)row).enabled = value);
        AddIdColumn(row => ((QuestCategoryDefinition)row).id,
            (row, value) => ((QuestCategoryDefinition)row).id = value);
        AddTextColumn("备注", 160, row => ((QuestCategoryDefinition)row).remark,
            (row, value) => ((QuestCategoryDefinition)row).remark = value);
        AddButtonColumn("名称", 220, row => LocalizedSummary(((QuestCategoryDefinition)row).name),
            row => OpenLocalizationEditor(row, "类别名称", QuestLocKey.Prefix.Category,
                data => ((QuestCategoryDefinition)data).name,
                (data, value) => ((QuestCategoryDefinition)data).name = value));
        AddButtonColumn("描述", 220, row => LocalizedSummary(((QuestCategoryDefinition)row).desc),
            row => OpenLocalizationEditor(row, "类别描述", QuestLocKey.Prefix.Category,
                data => ((QuestCategoryDefinition)data).desc,
                (data, value) => ((QuestCategoryDefinition)data).desc = value));
        AddButtonColumn("图标", 90, row => IconSummary(((QuestCategoryDefinition)row).icon),
            row => OpenIconEditor(row, "类别图标",
                data => ((QuestCategoryDefinition)data).icon,
                (data, value) => ((QuestCategoryDefinition)data).icon = value));
        AddButtonColumn("任务 ID", 190, row => IdListSummary(((QuestCategoryDefinition)row).questIds, QuestReferenceKind.Quest),
            row => OpenLongListEditor(row, "类别任务 ID", "questIds", QuestReferenceKind.Quest));
        AddButtonColumn("类别奖励", 140, row => RewardsSummary(((QuestCategoryDefinition)row).rewards),
            row => OpenPropertyEditor(row, "类别奖励", "rewards"));
    }

    void ConfigureRewardPresentationColumns()
    {
        AddEnabledColumn(row => ((QuestRewardPresentation)row).enabled,
            (row, value) => ((QuestRewardPresentation)row).enabled = value);
        AddRewardTypeColumn("奖励类型", 190, row => ((QuestRewardPresentation)row).type,
            (row, value) => ((QuestRewardPresentation)row).type = value);
        AddTextColumn("备注", 180, row => ((QuestRewardPresentation)row).remark,
            (row, value) => ((QuestRewardPresentation)row).remark = value);
        AddButtonColumn("显示名称", 240, row => LocalizedSummary(((QuestRewardPresentation)row).name),
            row => OpenLocalizationEditor(row, "奖励显示名称", QuestLocKey.Prefix.RewardName,
                data => ((QuestRewardPresentation)data).name,
                (data, value) => ((QuestRewardPresentation)data).name = value));
        AddButtonColumn("显示图标", 120, row => IconSummary(((QuestRewardPresentation)row).icon),
            row => OpenIconEditor(row, "奖励显示图标",
                data => ((QuestRewardPresentation)data).icon,
                (data, value) => ((QuestRewardPresentation)data).icon = value));
    }

    void AddEnabledColumn(Func<object, bool> getter, Action<object, bool> setter)
        => AddToggleColumn("启用", 52, getter, setter);

    void AddIdColumn(Func<object, long> getter, Action<object, long> setter)
        => table.columns.Add(new UIColumn
        {
            title = "ID",
            width = 92,
            minWidth = 76,
            makeCell = () =>
            {
                LongField field = Prepare(new LongField { isDelayed = true });
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData != null) Modify("修改任务配置 ID", () => setter(field.userData, evt.newValue));
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                LongField field = (LongField)element;
                field.userData = RowAt(index);
                field.SetValueWithoutNotify(getter(field.userData));
            },
        });

    void AddTextColumn(string title, float width, Func<object, string> getter, Action<object, string> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 80,
            stretchable = true,
            makeCell = () =>
            {
                TextField field = Prepare(new TextField { isDelayed = true });
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData != null) Modify($"修改{title}", () => setter(field.userData, evt.newValue));
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                TextField field = (TextField)element;
                field.userData = RowAt(index);
                field.SetValueWithoutNotify(getter(field.userData) ?? string.Empty);
            },
        });

    void AddIntColumn(string title, float width, Func<object, int> getter, Action<object, int> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 65,
            makeCell = () =>
            {
                IntegerField field = Prepare(new IntegerField { isDelayed = true });
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData != null) Modify($"修改{title}", () => setter(field.userData, evt.newValue));
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                IntegerField field = (IntegerField)element;
                field.userData = RowAt(index);
                field.SetValueWithoutNotify(getter(field.userData));
            },
        });

    void AddToggleColumn(string title, float width, Func<object, bool> getter, Action<object, bool> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = width,
            makeCell = () =>
            {
                Toggle field = Prepare(new Toggle());
                field.AddToClassList("quest-cell-toggle");
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData != null) Modify($"修改{title}", () => setter(field.userData, evt.newValue));
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                Toggle field = (Toggle)element;
                field.userData = RowAt(index);
                field.SetValueWithoutNotify(getter(field.userData));
            },
        });

    void AddEnumColumn<TEnum>(string title, float width, Func<object, TEnum> getter, Action<object, TEnum> setter)
        where TEnum : Enum
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 90,
            makeCell = () =>
            {
                EnumField field = Prepare(new EnumField((Enum)(object)default(TEnum)));
                field.RegisterValueChangedCallback(evt =>
                {
                    if (field.userData != null)
                        Modify($"修改{title}", () => setter(field.userData, (TEnum)(object)evt.newValue));
                });
                return field;
            },
            bindCell = (element, index) =>
            {
                EnumField field = (EnumField)element;
                field.userData = RowAt(index);
                field.SetValueWithoutNotify(getter(field.userData));
            },
        });

    void AddObjectiveTypeColumn(string title, float width, Func<object, QuestObjType> getter,
        Action<object, QuestObjType> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 180,
            stretchable = true,
            makeCell = () =>
            {
                QuestObjectiveTypeDropdown field = Prepare(new QuestObjectiveTypeDropdown());
                return field;
            },
            bindCell = (element, index) =>
            {
                QuestObjectiveTypeDropdown field = (QuestObjectiveTypeDropdown)element;
                object row = RowAt(index);
                field.userData = row;
                field.Bind(getter(row), value =>
                {
                    if (field.userData != null)
                        Modify($"修改{title}", () => setter(field.userData, value));
                });
            },
        });

    void AddRewardTypeColumn(string title, float width, Func<object, QuestRewardType> getter,
        Action<object, QuestRewardType> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 150,
            stretchable = true,
            makeCell = () => Prepare(new QuestRewardTypeDropdown()),
            bindCell = (element, index) =>
            {
                QuestRewardTypeDropdown field = (QuestRewardTypeDropdown)element;
                object row = RowAt(index);
                field.userData = row;
                field.Bind(getter(row), value =>
                {
                    if (field.userData != null)
                        Modify($"修改{title}", () => setter(field.userData, value));
                });
            },
        });

    void AddReferenceColumn(string title, float width, QuestReferenceKind kind, bool allowNone,
        Func<object, long> getter, Action<object, long> setter)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 120,
            stretchable = true,
            makeCell = () => Prepare(new QuestReferenceDropdown()),
            bindCell = (element, index) =>
            {
                QuestReferenceDropdown field = (QuestReferenceDropdown)element;
                object row = RowAt(index);
                field.userData = row;
                field.Bind(getter(row), kind, database, allowNone,
                    value => Modify($"修改{title}", () => setter(field.userData, value)));
            },
        });

    void AddButtonColumn(string title, float width, Func<object, string> summary, Action<object> clicked)
        => table.columns.Add(new UIColumn
        {
            title = title,
            width = width,
            minWidth = 80,
            stretchable = true,
            makeCell = () =>
            {
                Button button = Prepare(new Button());
                button.clicked += () =>
                {
                    if (button.userData != null) clicked(button.userData);
                };
                button.AddToClassList("quest-cell-button");
                return button;
            },
            bindCell = (element, index) =>
            {
                Button button = (Button)element;
                button.userData = RowAt(index);
                button.text = summary(button.userData);
                button.tooltip = button.text;
            },
        });

    void AddActionsColumn()
        => table.columns.Add(new UIColumn
        {
            title = "操作",
            width = 104,
            minWidth = 104,
            makeCell = () =>
            {
                VisualElement row = new();
                row.AddToClassList("quest-action-row");
                Button copy = new(() =>
                {
                    table.SetSelection(visibleRecords.IndexOf(row.userData));
                    DuplicateSelected();
                }) { text = "复制" };
                Button delete = new(() =>
                {
                    table.SetSelection(visibleRecords.IndexOf(row.userData));
                    DeleteSelected();
                }) { text = "删除" };
                copy.AddToClassList("quest-action-button");
                delete.AddToClassList("quest-action-button");
                row.Add(copy);
                row.Add(delete);
                return row;
            },
            bindCell = (element, index) => element.userData = RowAt(index),
        });

    /// <summary>按页签恢复稳定列标识对应的宽度，避免列顺序之间互相覆盖。</summary>
    void RestoreColumnWidths()
    {
        if (database == null || table == null) return;
        QuestEditorTableLayout layout = database.EditorTableLayouts
            .Find(item => item.tableId == section.ToString());
        if (layout == null) return;

        for (int i = 0; i < table.columns.Count; i++)
        {
            UIColumn column = table.columns[i];
            string columnId = GetColumnId(column, i);
            QuestEditorColumnLayout saved = layout.columns.Find(item => item.columnId == columnId);
            if (saved != null && saved.width > 0f) column.width = saved.width;
        }
    }

    void TrackColumnWidths()
    {
        if (table == null) return;
        Section trackedSection = section;
        foreach (UIColumn column in table.columns)
        {
            column.propertyChanged += (_, evt) =>
            {
                string propertyName = evt.propertyName;
                if (propertyName != nameof(UIColumn.width)) return;
                CaptureColumnWidths(trackedSection);
            };
        }
    }

    void CaptureColumnWidths(Section targetSection)
    {
        if (database == null || table == null || targetSection != section) return;
        Dictionary<string, float> widths = new(StringComparer.Ordinal);
        for (int i = 0; i < table.columns.Count; i++)
        {
            UIColumn column = table.columns[i];
            widths[GetColumnId(column, i)] = column.width.value;
        }

        pendingColumnWidths[targetSection] = widths;
        hasUnsavedChanges = true;
        if (statusLabel != null) statusLabel.text = $"● {SectionNames[section]}列宽有未保存修改";
        columnWidthSaveItem?.Pause();
        columnWidthSaveItem = rootVisualElement.schedule.Execute(FlushPendingColumnWidths).StartingIn(250);
    }

    void FlushPendingColumnWidths()
    {
        columnWidthSaveItem?.Pause();
        if (database == null || pendingColumnWidths.Count == 0) return;

        Undo.RecordObject(database, "修改任务编辑器列宽");
        bool changed = false;
        foreach ((Section layoutSection, Dictionary<string, float> widths) in pendingColumnWidths)
        {
            string tableId = layoutSection.ToString();
            QuestEditorTableLayout layout = database.EditorTableLayouts.Find(item => item.tableId == tableId);
            if (layout == null)
            {
                layout = new QuestEditorTableLayout { tableId = tableId };
                database.EditorTableLayouts.Add(layout);
                changed = true;
            }

            if (layout.columns.RemoveAll(item => !widths.ContainsKey(item.columnId)) > 0) changed = true;
            foreach ((string columnId, float width) in widths)
            {
                QuestEditorColumnLayout saved = layout.columns.Find(item => item.columnId == columnId);
                if (saved == null)
                {
                    layout.columns.Add(new QuestEditorColumnLayout { columnId = columnId, width = width });
                    changed = true;
                }
                else if (!Mathf.Approximately(saved.width, width))
                {
                    saved.width = width;
                    changed = true;
                }
            }
        }

        pendingColumnWidths.Clear();
        if (changed) MarkDirty();
        else hasUnsavedChanges = EditorUtility.IsDirty(database);
    }

    static string GetColumnId(UIColumn column, int index) => $"{index}:{column.title}";

    static TField Prepare<TField>(TField field) where TField : VisualElement
    {
        field.AddToClassList("quest-cell");
        return field;
    }

    object RowAt(int index) => table.itemsSource[index];

    void Modify(string undoName, Action change)
    {
        if (database == null) return;
        Undo.RecordObject(database, undoName);
        change();
        MarkDirty();
        serializedDatabase?.Update();
        table?.RefreshItems();
    }

    void MarkDirty()
    {
        if (database == null) return;
        hasUnsavedChanges = true;
        EditorUtility.SetDirty(database);
        QuestDatabaseProvider.ClearCache();
        if (statusLabel != null) statusLabel.text = $"● {SectionNames[section]}有未保存修改";
    }

    void RefreshTable(object selectRecord = null)
    {
        visibleRecords.Clear();
        if (database == null || table == null)
        {
            if (table != null) table.itemsSource = visibleRecords;
            return;
        }

        string search = searchField?.value?.Trim();
        foreach (object record in CurrentRecords)
        {
            if (string.IsNullOrEmpty(search) ||
                SearchText(record).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                visibleRecords.Add(record);
        }

        table.itemsSource = visibleRecords;
        table.Rebuild();
        if (selectRecord == null) return;
        int selectedIndex = visibleRecords.IndexOf(selectRecord);
        if (selectedIndex < 0) return;
        table.SetSelection(selectedIndex);
        table.ScrollToItem(selectedIndex);
    }

    string SearchText(object record)
    {
        string json = JsonUtility.ToJson(record);
        return record switch
        {
            QuestDefinition data => $"{data.id} {data.remark} {json}",
            QuestObjectiveDefinition data => $"{data.id} {data.remark} {json}",
            QuestConditionDefinition data => $"{data.id} {data.remark} {json}",
            QuestCategoryDefinition data => $"{data.id} {data.remark} {json}",
            QuestRewardPresentation data => $"{data.type} {data.remark} {json}",
            _ => json,
        };
    }

    void AddRecord()
    {
        if (database == null) return;
        Undo.RecordObject(database, "新增任务配置");
        object added = section switch
        {
            Section.Quests => Add(database.Quests,
                new QuestDefinition { id = NextId(database.Quests.Select(data => data.id)) }),
            Section.Objectives => Add(database.Objectives,
                new QuestObjectiveDefinition { id = NextId(database.Objectives.Select(data => data.id)) }),
            Section.Conditions => Add(database.Conditions,
                new QuestConditionDefinition { id = NextId(database.Conditions.Select(data => data.id)) }),
            Section.Categories => Add(database.Categories,
                new QuestCategoryDefinition { id = NextId(database.Categories.Select(data => data.id)) }),
            _ => Add(database.RewardPresentations, new QuestRewardPresentation()),
        };
        serializedDatabase = new SerializedObject(database);
        MarkDirty();
        RefreshTable(added);
    }

    static T Add<T>(ICollection<T> collection, T value)
    {
        collection.Add(value);
        return value;
    }

    void DuplicateSelected()
    {
        if (serializedDatabase == null || table.selectedItem == null) return;
        int arrayIndex = CurrentRecords.IndexOf(table.selectedItem);
        if (arrayIndex < 0) return;

        Undo.RecordObject(database, "复制任务配置");
        serializedDatabase.Update();
        SerializedProperty array = CurrentArray;
        array.InsertArrayElementAtIndex(arrayIndex);
        int copyIndex = Math.Min(arrayIndex + 1, array.arraySize - 1);
        SerializedProperty copy = array.GetArrayElementAtIndex(copyIndex);
        SerializedProperty id = copy.FindPropertyRelative("id");
        if (id != null) id.longValue = NextId(GetIds(array, copyIndex));
        serializedDatabase.ApplyModifiedProperties();
        object copied = CurrentRecords[copyIndex];
        MarkDirty();
        RefreshTable(copied);
    }

    void DeleteSelected()
    {
        if (serializedDatabase == null || table.selectedItem == null) return;
        object selected = table.selectedItem;
        int arrayIndex = CurrentRecords.IndexOf(selected);
        if (arrayIndex < 0) return;
        if (!EditorUtility.DisplayDialog("删除配置", $"确定删除 {RecordLabel(selected)}？", "删除", "取消")) return;

        Undo.RecordObject(database, "删除任务配置");
        serializedDatabase.Update();
        CurrentArray.DeleteArrayElementAtIndex(arrayIndex);
        serializedDatabase.ApplyModifiedProperties();
        MarkDirty();
        RefreshTable();
        CloseOverlay();
    }

    void OpenLocalizationEditor(object record, string title, string keyPrefix,
        Func<object, LocalizedString> getter, Action<object, LocalizedString> setter)
    {
        IReadOnlyList<QuestLocalizationOption> allOptions =
            QuestLocalizationEditorUtility.GetOptions(keyPrefix);
        OpenOverlay(title, content =>
        {
            VisualElement currentCard = new();
            currentCard.AddToClassList("quest-localization-current");
            Label currentKey = new();
            currentKey.AddToClassList("quest-localization-current-key");
            Label currentPreview = new();
            currentPreview.AddToClassList("quest-localization-current-preview");
            currentCard.Add(currentKey);
            currentCard.Add(currentPreview);
            content.Add(currentCard);

            Label scope = new($"可选择范围：{LocTableSet.QuestSystem}/{keyPrefix}*");
            scope.AddToClassList("quest-localization-scope");
            content.Add(scope);

            ToolbarSearchField search = new();
            search.AddToClassList("quest-localization-search");
            search.tooltip = "可按多语言 Key 或中文内容搜索";
            content.Add(search);

            VisualElement rows = new();
            rows.AddToClassList("quest-localization-list");
            content.Add(rows);

            VisualElement pager = new();
            pager.AddToClassList("quest-localization-pager");
            Button previous = CreateButton("上一页", null);
            Label pageLabel = new();
            pageLabel.AddToClassList("quest-localization-page-label");
            Button next = CreateButton("下一页", null);
            pager.Add(previous);
            pager.Add(pageLabel);
            pager.Add(next);
            content.Add(pager);
            int page = 0;

            void RefreshCurrent()
            {
                LocalizedString value = getter(record);
                currentKey.text = QuestLocalizationEditorUtility.GetDisplayPath(value);
                string preview = QuestLocalizationEditorUtility.GetPreview(value);
                currentPreview.text = string.IsNullOrEmpty(preview)
                    ? "中文预览：未找到内容"
                    : $"中文预览：{preview}";
            }

            void RefreshRows()
            {
                rows.Clear();
                string query = search.value?.Trim();
                string selectedKey = QuestLocalizationEditorUtility.GetKey(getter(record));
                QuestLocalizationOption[] options = allOptions
                    .Where(option => string.IsNullOrEmpty(query) ||
                                     option.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
                int pageCount = Math.Max(1, (options.Length + LocalizationPageSize - 1) / LocalizationPageSize);
                page = Mathf.Clamp(page, 0, pageCount - 1);

                foreach (QuestLocalizationOption option in options.Skip(page * LocalizationPageSize)
                             .Take(LocalizationPageSize))
                {
                    bool isCurrent = string.Equals(option.Key, selectedKey, StringComparison.Ordinal);
                    Button row = new(() =>
                    {
                        if (isCurrent) return;
                        Modify($"修改{title}多语言 Key", () =>
                            setter(record, QuestLocalizationEditorUtility.Create(option.Key)));
                        RefreshCurrent();
                        RefreshRows();
                    });
                    row.AddToClassList("quest-localization-row");
                    row.EnableInClassList("is-current", isCurrent);
                    row.tooltip = $"点击选择 {LocTableSet.QuestSystem}/{option.Key}";

                    VisualElement text = new();
                    text.AddToClassList("quest-localization-row-text");
                    Label key = new($"{LocTableSet.QuestSystem}/{option.Key}");
                    key.AddToClassList("quest-localization-key");
                    Label preview = new(string.IsNullOrEmpty(option.Preview) ? "（中文内容为空）" : option.Preview);
                    preview.AddToClassList("quest-localization-preview");
                    text.Add(key);
                    text.Add(preview);
                    row.Add(text);
                    rows.Add(row);
                }

                if (options.Length == 0)
                    rows.Add(new HelpBox("没有找到符合前缀与搜索条件的多语言 Key。", HelpBoxMessageType.Info));

                pageLabel.text = $"第 {page + 1} / {pageCount} 页 · 共 {options.Length} 项";
                previous.SetEnabled(page > 0);
                next.SetEnabled(page + 1 < pageCount);
            }

            previous.clicked += () => { page--; RefreshRows(); };
            next.clicked += () => { page++; RefreshRows(); };
            search.RegisterValueChangedCallback(_ => { page = 0; RefreshRows(); });
            RefreshCurrent();
            RefreshRows();
            search.schedule.Execute(search.Focus);
        });
    }

    void OpenIconEditor(object record, string title,
        Func<object, UnityEngine.AddressableAssets.AssetReferenceSprite> getter,
        Action<object, UnityEngine.AddressableAssets.AssetReferenceSprite> setter)
    {
        OpenOverlay(title, content =>
        {
            ObjectField field = new("Sprite")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = QuestIconEditorUtility.GetSprite(getter(record)),
            };
            field.AddToClassList("quest-popup-field");
            field.RegisterValueChangedCallback(evt => Modify($"修改{title}",
                () => setter(record, QuestIconEditorUtility.Create(evt.newValue as Sprite))));
            content.Add(field);

            Label hint = new("这里只需要选择一个 Sprite；编辑器会自动保存对应的 Addressables GUID 与子资源信息。");
            hint.AddToClassList("quest-field-hint");
            content.Add(hint);
        });
    }

    void OpenPropertyEditor(object record, string title, params string[] relativeNames)
    {
        SerializedProperty row = FindRecordProperty(record);
        if (row == null) return;
        OpenOverlay(title, content =>
        {
            foreach (string relativeName in relativeNames)
            {
                SerializedProperty property = row.FindPropertyRelative(relativeName);
                if (property == null) continue;
                if (property.isArray && property.propertyType != SerializedPropertyType.String)
                    AddExpandedArrayEditor(content, property, relativeName);
                else
                {
                    PropertyField field = new(property, string.Empty);
                    field.AddToClassList("quest-popup-field");
                    content.Add(field);
                    field.Bind(serializedDatabase);
                }
            }
            content.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                MarkDirty();
                table.RefreshItems();
            });
        });
    }

    void AddExpandedArrayEditor(VisualElement content, SerializedProperty original, string relativeName)
    {
        string arrayPath = original.propertyPath;
        VisualElement section = new();
        section.AddToClassList("quest-array-section");
        Label caption = new();
        caption.AddToClassList("quest-array-caption");
        section.Add(caption);
        VisualElement rows = new();
        section.Add(rows);

        void ApplyArrayChange(string undoName, Action<SerializedProperty> change)
        {
            Undo.RecordObject(database, undoName);
            serializedDatabase.Update();
            change(serializedDatabase.FindProperty(arrayPath));
            serializedDatabase.ApplyModifiedProperties();
            MarkDirty();
            Rebuild();
            table.RefreshItems();
        }

        void Rebuild()
        {
            rows.Clear();
            serializedDatabase.Update();
            SerializedProperty array = serializedDatabase.FindProperty(arrayPath);
            caption.text = $"{original.displayName}（{array.arraySize} 项）";
            for (int i = 0; i < array.arraySize; i++)
            {
                int elementIndex = i;
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                VisualElement card = new();
                card.AddToClassList("quest-array-item");

                VisualElement header = new();
                header.AddToClassList("quest-array-item-header");
                Label indexLabel = new($"#{i + 1}");
                indexLabel.AddToClassList("quest-array-index");
                header.Add(indexLabel);

                Button moveUp = CreateButton("↑", () => ApplyArrayChange(
                    $"上移{original.displayName}",
                    current => current.MoveArrayElement(elementIndex, elementIndex - 1)));
                moveUp.AddToClassList("quest-icon-btn");
                moveUp.SetEnabled(i > 0);
                header.Add(moveUp);

                Button moveDown = CreateButton("↓", () => ApplyArrayChange(
                    $"下移{original.displayName}",
                    current => current.MoveArrayElement(elementIndex, elementIndex + 1)));
                moveDown.AddToClassList("quest-icon-btn");
                moveDown.SetEnabled(i < array.arraySize - 1);
                header.Add(moveDown);

                Button remove = CreateButton("删除", () => ApplyArrayChange(
                    $"删除{original.displayName}",
                    current => current.DeleteArrayElementAtIndex(elementIndex)), "quest-btn-danger");
                remove.AddToClassList("quest-array-remove");
                header.Add(remove);
                card.Add(header);

                PropertyField elementField = new(element, string.Empty);
                elementField.AddToClassList("quest-array-element");
                card.Add(elementField);
                elementField.Bind(serializedDatabase);
                rows.Add(card);
            }

            if (array.arraySize == 0)
            {
                Label empty = new("当前没有配置项");
                empty.AddToClassList("quest-empty-hint");
                rows.Add(empty);
            }
        }

        Button add = CreateButton("＋ 添加一项", () => ApplyArrayChange(
            $"新增{original.displayName}",
            array =>
            {
                int index = array.arraySize++;
                InitializeArrayElement(array.GetArrayElementAtIndex(index), relativeName);
            }), "quest-btn-primary");
        add.AddToClassList("quest-array-add");
        section.Add(add);
        content.Add(section);
        Rebuild();
    }

    static void InitializeArrayElement(SerializedProperty element, string relativeName)
    {
        if (relativeName == "triggers")
        {
            element.FindPropertyRelative("type").intValue = (int)QuestTriggerType.Auto;
            element.FindPropertyRelative("staySeconds").intValue = 1;
            return;
        }

        if (relativeName.EndsWith("rewards", StringComparison.OrdinalIgnoreCase))
        {
            element.FindPropertyRelative("type").intValue = (int)QuestRewardType.Coin;
            element.FindPropertyRelative("amount").intValue = 1;
            return;
        }

        if (relativeName == "items")
        {
            element.FindPropertyRelative("count").intValue = 1;
            return;
        }

        if (relativeName == "characterProps")
        {
            element.FindPropertyRelative("propType").intValue = (int)CharacterPropType.Goodwill;
            element.FindPropertyRelative("value").intValue = 1;
        }
    }

    void OpenLongListEditor(object record, string title, string relativeName, QuestReferenceKind? kind)
    {
        SerializedProperty row = FindRecordProperty(record);
        SerializedProperty original = row?.FindPropertyRelative(relativeName);
        if (original == null || !original.isArray) return;
        string arrayPath = original.propertyPath;

        OpenOverlay(title, content =>
        {
            VisualElement rows = new();
            content.Add(rows);
            void Rebuild()
            {
                rows.Clear();
                serializedDatabase.Update();
                SerializedProperty array = serializedDatabase.FindProperty(arrayPath);
                for (int i = 0; i < array.arraySize; i++)
                {
                    int elementIndex = i;
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    string elementPath = element.propertyPath;
                    VisualElement line = new();
                    line.AddToClassList("quest-list-row");

                    if (kind.HasValue)
                    {
                        QuestReferenceDropdown dropdown = new();
                        dropdown.Bind(element.longValue, kind.Value, database, false, value =>
                        {
                            Undo.RecordObject(database, $"修改{title}");
                            serializedDatabase.Update();
                            serializedDatabase.FindProperty(elementPath).longValue = value;
                            serializedDatabase.ApplyModifiedProperties();
                            MarkDirty();
                            table.RefreshItems();
                        });
                        line.Add(dropdown);
                    }
                    else
                    {
                        LongField field = new() { value = element.longValue, isDelayed = true };
                        field.AddToClassList("quest-list-field");
                        field.RegisterValueChangedCallback(evt =>
                        {
                            Undo.RecordObject(database, $"修改{title}");
                            serializedDatabase.Update();
                            serializedDatabase.FindProperty(elementPath).longValue = evt.newValue;
                            serializedDatabase.ApplyModifiedProperties();
                            MarkDirty();
                            table.RefreshItems();
                        });
                        line.Add(field);
                    }

                    Button remove = CreateButton("删除", () =>
                    {
                        Undo.RecordObject(database, $"删除{title}");
                        serializedDatabase.Update();
                        SerializedProperty current = serializedDatabase.FindProperty(arrayPath);
                        current.DeleteArrayElementAtIndex(elementIndex);
                        serializedDatabase.ApplyModifiedProperties();
                        MarkDirty();
                        Rebuild();
                        table.RefreshItems();
                    }, "quest-btn-danger");
                    remove.AddToClassList("quest-list-remove");
                    line.Add(remove);
                    rows.Add(line);
                }

                if (array.arraySize == 0)
                    rows.Add(new HelpBox("当前列表为空。", HelpBoxMessageType.Info));
            }

            Button add = CreateButton("＋ 添加一项", () =>
            {
                Undo.RecordObject(database, $"新增{title}");
                serializedDatabase.Update();
                SerializedProperty array = serializedDatabase.FindProperty(arrayPath);
                int index = array.arraySize;
                array.InsertArrayElementAtIndex(index);
                array.GetArrayElementAtIndex(index).longValue = 0;
                serializedDatabase.ApplyModifiedProperties();
                MarkDirty();
                Rebuild();
                table.RefreshItems();
            }, "quest-btn-primary");
            add.AddToClassList("quest-list-add");
            content.Add(add);
            Rebuild();
        });
    }

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
        header.Add(label);
        header.Add(CreateButton("关闭", CloseOverlay));
        panel.Add(header);

        ScrollView content = new();
        content.AddToClassList("quest-popup-content");
        panel.Add(content);
        buildContent(content);
        overlay.Add(panel);
    }

    void CloseOverlay()
    {
        if (overlay == null) return;
        overlay.AddToClassList("is-hidden");
        overlay.Clear();
        table?.RefreshItems();
    }

    SerializedProperty FindRecordProperty(object record)
    {
        if (serializedDatabase == null || record == null) return null;
        int index = CurrentRecords.IndexOf(record);
        if (index < 0) return null;
        serializedDatabase.Update();
        return CurrentArray.GetArrayElementAtIndex(index);
    }

    void ImportExcel()
    {
        QuestDatabaseData imported = QuestLubanMigration.ImportFromExcel(true);
        QuestEditorReferenceCatalog.Invalidate();
        if (imported != null) SetDatabase(imported);
    }

    void Save()
    {
        if (database == null) return;
        FlushPendingColumnWidths();
        serializedDatabase?.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        hasUnsavedChanges = false;
        QuestDatabaseProvider.ClearCache();
        if (statusLabel != null) statusLabel.text = "✓ 已保存";
        table?.RefreshItems();
    }

    void ValidateDatabase()
    {
        if (database == null) return;
        Save();
        QuestDatabaseValidationReport report = QuestDatabaseValidator.Validate(database);
        validationView.Clear();
        HelpBoxMessageType summaryType = report.Errors.Count > 0
            ? HelpBoxMessageType.Error
            : report.Warnings.Count > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
        validationView.Add(new HelpBox(
            $"校验完成：{report.Errors.Count} 个错误，{report.Warnings.Count} 个警告。",
            summaryType));
        foreach (string error in report.Errors.Take(8))
            validationView.Add(new HelpBox(error, HelpBoxMessageType.Error));
        foreach (string warning in report.Warnings.Take(5))
            validationView.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
        Debug.Log($"[QuestEditor] 校验完成：{report.Errors.Count} 错误，{report.Warnings.Count} 警告。\n" +
                  string.Join("\n", report.Errors.Concat(report.Warnings)));
    }

    void ShowSourceModeHint()
    {
        if (validationView == null) return;
        validationView.Clear();
        string text = database?.SourceMode switch
        {
            QuestConfigSourceMode.PreferScriptableObject =>
                "SO 优先：启用的 SO 记录覆盖同 ID Luban 记录，缺失项继续回退 Luban。",
            QuestConfigSourceMode.ScriptableObjectOnly =>
                "仅 SO：完全停止读取这 5 张 Luban 任务表，发布前请先通过校验。",
            _ => "仅 Luban：SO 数据已保存但不会进入运行时，适合先编辑和校验再逐步切换。",
        };
        validationView.Add(new HelpBox(text, HelpBoxMessageType.Info));
    }

    void OnUndoRedo()
    {
        if (database == null) return;
        serializedDatabase = new SerializedObject(database);
        QuestEditorReferenceCatalog.Invalidate();
        RefreshTable();
    }

    System.Collections.IList CurrentRecords => section switch
    {
        Section.Quests => database.Quests,
        Section.Objectives => database.Objectives,
        Section.Conditions => database.Conditions,
        Section.Categories => database.Categories,
        _ => database.RewardPresentations,
    };

    SerializedProperty CurrentArray => serializedDatabase.FindProperty(section switch
    {
        Section.Quests => "quests",
        Section.Objectives => "objectives",
        Section.Conditions => "conditions",
        Section.Categories => "categories",
        _ => "rewardPresentations",
    });

    string RecordLabel(object record) => record switch
    {
        QuestDefinition data => $"{data.id}  {data.remark}",
        QuestObjectiveDefinition data => $"{data.id}  {data.remark}",
        QuestConditionDefinition data => $"{data.id}  {data.remark}",
        QuestCategoryDefinition data => $"{data.id}  {data.remark}",
        QuestRewardPresentation data => $"{data.type}  {data.remark}",
        _ => SectionNames[section],
    };

    string IdListSummary(IReadOnlyList<long> ids, QuestReferenceKind kind)
    {
        if (ids.Count == 0) return "未配置";
        string first = QuestEditorReferenceCatalog.Format(ids[0], kind, database);
        return ids.Count == 1 ? first : $"{first} 等 {ids.Count} 项";
    }

    static string RawIdListSummary(IReadOnlyList<long> ids)
        => ids.Count == 0 ? "未配置" : ids.Count == 1 ? ids[0].ToString() : $"{ids[0]} 等 {ids.Count} 项";

    static string CountSummary(int count) => count == 0 ? "未配置" : $"{count} 项";

    static string SpecsSummary<T>(IReadOnlyList<T> values, Func<T, object> type)
    {
        if (values.Count == 0) return "未配置";
        return values.Count == 1 ? type(values[0]).ToString() : $"{type(values[0])} 等 {values.Count} 项";
    }

    static string RewardsSummary(IReadOnlyList<QuestRewardSpec> rewards)
        => SpecsSummary(rewards, reward => QuestRewardTypeLabels.Get(reward.type));

    static string ObjectiveSummary(QuestObjectiveSpec objective)
        => objective == null ? "未配置" : QuestObjectiveTypeLabels.Get(objective.type);

    static string LocalizedSummary(LocalizedString value)
        => QuestLocalizationEditorUtility.GetDisplayPath(value);

    static string IconSummary(UnityEngine.AddressableAssets.AssetReferenceSprite value)
        => value != null && value.RuntimeKeyIsValid() ? "已配置" : "未配置";

    static long NextId(IEnumerable<long> ids)
    {
        long[] values = ids.ToArray();
        return values.Length == 0 ? 10001 : values.Max() + 1;
    }

    static IEnumerable<long> GetIds(SerializedProperty array, int exceptIndex)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (i != exceptIndex)
                yield return array.GetArrayElementAtIndex(i).FindPropertyRelative("id").longValue;
        }
    }
}
