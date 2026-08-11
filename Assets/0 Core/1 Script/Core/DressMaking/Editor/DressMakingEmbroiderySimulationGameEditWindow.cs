#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;

/// <summary>
/// 刺绣棋盘工作台。静态布局和样式由 UXML/USS 定义，窗口只负责数据绑定与编辑命令。
/// <para>编辑器以分割线为几何输入，线段交点自动生成运行时网格单元。</para>
/// </summary>
public class DressMakingEmbroiderySimulationGameEditWindow : EditorWindow
{
    const string MenuItem = "Tools/MiniGame/Dress Making/刺绣模拟工作台";
    const string WindowTitle = "刺绣模拟工作台";
    const string LayoutPath =
        "Assets/0 Core/1 Script/Core/DressMaking/Editor/DressMakingEmbroiderySimulationGameEditWindow.uxml";
    const string StylePath =
        "Assets/0 Core/1 Script/Core/DressMaking/Editor/DressMakingEmbroiderySimulationGameEditWindow.uss";
    const string ClipboardPrefix = "AFramework.EmbroideryGridLine:";
    const float MinWindowWidth = 1080f;
    const float MinWindowHeight = 680f;

    ObjectField configField;
    ListView levelList;
    ListView gridLineList;
    ListView regionList;
    VisualElement canvasHost;
    VisualElement levelInspector;
    VisualElement regionInspector;
    VisualElement statusArea;
    VisualElement validationArea;
    DressMakingEmbroideryCanvasElement canvas;
    Label selectedTitle;
    HelpBox statusBox;
    HelpBox validationBox;
    Button levelTab;
    Button regionTab;
    Button generatePrefabButton;
    readonly List<long> shownLevelIds = new();
    [SerializeField] DressMakingEmbroiderySimulationGameConfig config;
    [SerializeField] long selectedLevelId;
    [SerializeField] int selectedRegionIndex = -1;
    [SerializeField] List<int> selectedRegionIndices = new();
    [SerializeField] int selectedGridLineIndex = -1;
    UnityEngine.Object pendingAsset;
    int canvasDragUndoGroup = -1;
    bool topologyPreviewDirty;
    double lastTopologyPreviewTime;

    DressMakingEmbroideryLevelData SelectedLevel =>
        config != null && config.DataDict.TryGetValue(selectedLevelId, out DressMakingEmbroideryLevelData level)
            ? level
            : null;

    DressMakingEmbroideryRegionData SelectedRegion =>
        SelectedLevel != null && selectedRegionIndex >= 0 && selectedRegionIndex < SelectedLevel.Regions.Count
            ? SelectedLevel.Regions[selectedRegionIndex]
            : null;

    DressMakingEmbroideryGridLineData SelectedGridLine =>
        SelectedLevel != null
        && selectedGridLineIndex >= 0
        && selectedGridLineIndex < SelectedLevel.GridLines.Count
            ? SelectedLevel.GridLines[selectedGridLineIndex]
            : null;

    [MenuItem(MenuItem)]
    public static void Open()
    {
        DressMakingEmbroiderySimulationGameEditWindow window =
            GetWindow<DressMakingEmbroiderySimulationGameEditWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
    }
    void OnEnable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        EditorApplication.delayCall -= EnsureConfigAfterReload;
        EditorApplication.delayCall += EnsureConfigAfterReload;
    }

    void EnsureConfigAfterReload()
    {
        EditorApplication.delayCall -= EnsureConfigAfterReload;
        if (config == null)
        {
            config = AssetDatabase.LoadAssetAtPath<DressMakingEmbroiderySimulationGameConfig>(
                DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath);
        }
        if (config != null && configField != null && configField.value == null)
            SetConfig(config);
    }
    void CreateGUI()
    {
        rootVisualElement.Clear();
        VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (layout == null || styleSheet == null)
        {
            rootVisualElement.Add(new HelpBox(
                "刺绣工作台 UXML/USS 资源缺失，请重新导入编辑器资源。",
                HelpBoxMessageType.Error));
            return;
        }

        layout.CloneTree(rootVisualElement);
        rootVisualElement.styleSheets.Add(styleSheet);
        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnWorkbenchKeyDown);

        rootVisualElement.Q<VisualElement>("toolbar-slot").Add(BuildToolbar());
        rootVisualElement.Q<VisualElement>("level-slot").Add(BuildLevelPane());

        selectedTitle = rootVisualElement.Q<Label>("canvas-title");
        canvasHost = rootVisualElement.Q<VisualElement>("canvas-host");
        canvasHost.RegisterCallback<GeometryChangedEvent>(_ => UpdateCanvasAspect());
        canvas = new DressMakingEmbroideryCanvasElement
        {
            style =
            {
                flexShrink = 0f,
                backgroundColor = new Color(0.09f, 0.11f, 0.12f, 1f),
            }
        };
        BindCanvasEvents();
        canvasHost.Add(canvas);

        levelInspector = rootVisualElement.Q<ScrollView>("level-inspector");
        regionInspector = rootVisualElement.Q<ScrollView>("region-inspector");
        levelTab = rootVisualElement.Q<Button>("level-tab");
        regionTab = rootVisualElement.Q<Button>("region-tab");
        levelTab.clicked += () => SetInspectorTab(false);
        regionTab.clicked += () => SetInspectorTab(true);

        statusArea = rootVisualElement.Q<VisualElement>("status-area");
        validationArea = rootVisualElement.Q<VisualElement>("validation-area");
        statusBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        validationBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        rootVisualElement.Q<VisualElement>("status-content").Add(statusBox);
        rootVisualElement.Q<VisualElement>("validation-content").Add(validationBox);
        rootVisualElement.Q<Button>("clear-status").clicked += ClearStatus;
        rootVisualElement.Q<Button>("clear-validation").clicked += ClearValidation;
        rootVisualElement.Q<Button>("clear-hint").clicked += () =>
            rootVisualElement.Q<VisualElement>(className: "canvas-hint-row")
                .style.display = DisplayStyle.None;

        if (pendingAsset != null)
        {
            UnityEngine.Object asset = pendingAsset;
            pendingAsset = null;
            SelectAsset(asset);
        }
        else
        {
            SelectAsset(Selection.activeObject);
        }

        if (config == null)
        {
            SetConfig(AssetDatabase.LoadAssetAtPath<DressMakingEmbroiderySimulationGameConfig>(
                DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath));
        }
    }

    void BindCanvasEvents()
    {
        canvas.RegionClicked += SelectRegionFromCanvas;
        canvas.GridLineClicked += SelectGridLineFromCanvas;
        canvas.DragStarted += OnCanvasDragStarted;
        canvas.DragEnded += OnCanvasPointDragEnded;
        canvas.GridLinePointChanged += OnGridLinePointChanged;
        canvas.BezierControlChanged += OnBezierControlChanged;
        canvas.GridLineTranslated += OnGridLineTranslated;
        canvas.LabelPositionChanged += OnLabelPositionChanged;
        canvas.AddLinePointRequested += AddCanvasLinePoint;
    }

    void OnWorkbenchKeyDown(KeyDownEvent evt)
    {
        if (evt.altKey)
            return;
        VisualElement target = evt.target as VisualElement;
        bool isCanvasTarget = target == canvas || target == rootVisualElement;
        bool isGridLineListTarget = target != null && gridLineList?.Contains(target) == true;
        if ((isCanvasTarget || isGridLineListTarget) &&
            evt.keyCode is KeyCode.Delete or KeyCode.Backspace)
        {
            RemoveGridLine();
            evt.StopPropagation();
            return;
        }
        if (!isCanvasTarget || !evt.ctrlKey)
            return;
        if (evt.keyCode == KeyCode.C)
        {
            CopySelectedGridLine();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.V)
        {
            PasteGridLine();
            evt.StopPropagation();
        }
    }

    void CopySelectedGridLine()
    {
        DressMakingEmbroideryGridLineData line = SelectedGridLine;
        if (line == null)
            return;
        EditorGUIUtility.systemCopyBuffer = ClipboardPrefix + JsonUtility.ToJson(line);
        SetStatus($"已复制线 #{line.id}，Ctrl+V 将在右上方偏移粘贴。", HelpBoxMessageType.Info);
    }

    void PasteGridLine()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        string clipboard = EditorGUIUtility.systemCopyBuffer;
        if (level == null || string.IsNullOrEmpty(clipboard) ||
            !clipboard.StartsWith(ClipboardPrefix, StringComparison.Ordinal))
            return;
        DressMakingEmbroideryGridLineData copy = JsonUtility.FromJson<DressMakingEmbroideryGridLineData>(
            clipboard.Substring(ClipboardPrefix.Length));
        if (copy == null || copy.points == null || copy.points.Count < 2)
            return;

        Undo.RecordObject(config, "粘贴刺绣分割线");
        copy.id = NextGridLineId(level);
        copy.EnsureBezierControls();
        Vector2 offset = ClampTranslation(copy, new Vector2(0.035f, 0.035f));
        for (int i = 0; i < copy.points.Count; i++)
            copy.points[i] += offset;
        for (int i = 0; i < copy.BezierControls.Count; i++)
            copy.BezierControls[i] += offset;
        level.GridLines.Add(copy);
        selectedGridLineIndex = level.GridLines.Count - 1;
        MarkDirty();
        GenerateRegionsFromGridLines(false);
        RefreshLevelInspector();
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
        SetStatus($"已粘贴为线 #{copy.id}。", HelpBoxMessageType.Info);
    }

    void SetInspectorTab(bool showRegion)
    {
        if (levelInspector == null || regionInspector == null)
            return;
        levelInspector.style.display = showRegion ? DisplayStyle.None : DisplayStyle.Flex;
        regionInspector.style.display = showRegion ? DisplayStyle.Flex : DisplayStyle.None;
        levelTab.EnableInClassList("inspector-tab--active", !showRegion);
        regionTab.EnableInClassList("inspector-tab--active", showRegion);
    }

    void SetStatus(string message, HelpBoxMessageType type)
    {
        statusBox.text = message;
        statusBox.messageType = type;
        statusArea.style.display = string.IsNullOrEmpty(message)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    void ClearStatus()
    {
        statusBox.text = string.Empty;
        statusArea.style.display = DisplayStyle.None;
    }

    void ClearValidation()
    {
        validationBox.text = string.Empty;
        validationArea.style.display = DisplayStyle.None;
    }

    VisualElement BuildToolbar()
    {
        VisualElement bar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                flexShrink = 0f,
                marginBottom = 5f,
            }
        };

        configField = new ObjectField("配置")
        {
            objectType = typeof(DressMakingEmbroiderySimulationGameConfig),
            allowSceneObjects = false,
            style = { flexGrow = 1f, minWidth = 260f }
        };
        configField.RegisterValueChangedCallback(e =>
        {
            if (e.newValue == config)
                return;
            SetConfig(e.newValue as DressMakingEmbroiderySimulationGameConfig);
        });
        bar.Add(configField);

        Button createButton = new Button(CreateConfigAsset) { text = "新建配置" };
        createButton.style.marginLeft = 4f;
        bar.Add(createButton);

        Button sampleButton = new Button(EnsureSampleData) { text = "补充 1001/1002 示例" };
        sampleButton.style.marginLeft = 4f;
        bar.Add(sampleButton);

        Button normalizeButton = new Button(NormalizeConfig) { text = "整理数据" };
        normalizeButton.style.marginLeft = 4f;
        bar.Add(normalizeButton);

        Button validateButton = new Button(ValidateConfig) { text = "校验" };
        validateButton.style.marginLeft = 4f;
        bar.Add(validateButton);

        Button saveButton = new Button(SaveConfig) { text = "保存" };
        saveButton.style.marginLeft = 4f;
        bar.Add(saveButton);
        return bar;
    }

    VisualElement BuildLevelPane()
    {
        VisualElement pane = Card("服装关卡");
        VisualElement buttons = Row();
        buttons.Add(new Button(AddLevel) { text = "+" });
        buttons.Add(new Button(DuplicateLevel) { text = "复制" });
        buttons.Add(new Button(RemoveLevel) { text = "删除" });
        pane.Add(buttons);

        levelList = new ListView
        {
            itemsSource = shownLevelIds,
            fixedItemHeight = 28f,
            makeItem = () =>
            {
                Label label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.paddingLeft = 6f;
                return label;
            },
            bindItem = (element, index) =>
            {
                if (index < 0 || index >= shownLevelIds.Count)
                    return;
                long id = shownLevelIds[index];
                DressMakingEmbroideryLevelData level = config != null && config.DataDict.TryGetValue(id, out DressMakingEmbroideryLevelData value)
                    ? value
                    : null;
                ((Label)element).text = level == null ? id.ToString() : $"{id}  {level.DisplayName}";
            },
        };
        levelList.style.flexGrow = 1f;
        levelList.style.marginTop = 5f;
        levelList.selectionChanged += OnLevelSelectionChanged;
        pane.Add(levelList);
        return pane;
    }

    void UpdateCanvasAspect()
    {
        if (canvasHost == null || canvas == null)
            return;

        Rect available = canvasHost.contentRect;
        if (available.width <= 1f || available.height <= 1f)
            return;

        Vector2 size = SelectedLevel?.CanvasSize ?? new Vector2(1000f, 640f);
        float aspect = Mathf.Max(0.01f, size.x / Mathf.Max(1f, size.y));
        float width = Mathf.Min(available.width, available.height * aspect);
        canvas.style.width = width;
        canvas.style.height = width / aspect;
        canvas.RefreshLabels();
        canvas.MarkDirtyRepaint();
    }

    void SelectAsset(UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        if (configField == null)
        {
            pendingAsset = asset;
            return;
        }

        if (asset is DressMakingEmbroiderySimulationGameConfig selected)
            SetConfig(selected);
    }

    public void SelectConfig(DressMakingEmbroiderySimulationGameConfig selected)
    {
        SelectAsset(selected);
    }

    void SetConfig(DressMakingEmbroiderySimulationGameConfig selected)
    {
        config = selected;
        bool migrated = false;
        foreach (DressMakingEmbroideryLevelData level in config.DataDict.Values)
        {
            foreach (DressMakingEmbroideryRegionData region in level.Regions)
            {
                if (!string.IsNullOrEmpty(region.fillTexturePath))
                    continue;
                region.fillTexturePath = DressMakingEmbroiderySimulationGameConfig.DefaultStitchTexturePath;
                migrated = true;
            }
        }
        if (migrated)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        configField?.SetValueWithoutNotify(config);
        selectedLevelId = 0;
        selectedRegionIndex = -1;
        selectedRegionIndices.Clear();
        selectedGridLineIndex = -1;
        RefreshLevelList();
        RefreshAllViews();
    }

    void RefreshLevelList()
    {
        shownLevelIds.Clear();
        if (config != null && config.DataDict != null)
            shownLevelIds.AddRange(config.DataDict.Keys.OrderBy(id => id));
        levelList?.Rebuild();

        if (shownLevelIds.Count == 0)
        {
            selectedLevelId = 0;
            return;
        }

        if (!shownLevelIds.Contains(selectedLevelId))
            selectedLevelId = shownLevelIds[0];
        int index = shownLevelIds.IndexOf(selectedLevelId);
        levelList?.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Enumerable.Empty<int>());
    }

    void OnLevelSelectionChanged(IEnumerable<object> selection)
    {
        object first = selection.FirstOrDefault();
        if (first is long id)
            selectedLevelId = id;
        else if (first is int index && index >= 0 && index < shownLevelIds.Count)
            selectedLevelId = shownLevelIds[index];
        else
            return;

        selectedRegionIndex = -1;
        selectedRegionIndices.Clear();
        selectedGridLineIndex = -1;
        RefreshAllViews();
    }

    void RefreshAllViews()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        selectedTitle.text = level == null
            ? "未选择关卡"
            : $"{level.ClothingId} · {level.DisplayName}  ({level.GridLines.Count} 条线 / {level.Regions.Count} 个单元)";
        RefreshRegionList();
        RefreshLevelInspector();
        RefreshRegionInspector();
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
        UpdateCanvasAspect();
        generatePrefabButton?.SetEnabled(level != null);
    }

    void RefreshRegionList()
    {
        if (regionList == null)
            return;

        DressMakingEmbroideryLevelData level = SelectedLevel;
        regionList.itemsSource = level?.Regions ?? new List<DressMakingEmbroideryRegionData>();
        regionList.Rebuild();
        selectedRegionIndices.RemoveAll(index => level == null || index < 0 || index >= level.Regions.Count);
        if (level == null || selectedRegionIndex < 0 || selectedRegionIndex >= level.Regions.Count)
        {
            selectedRegionIndices.Clear();
            regionList.SetSelectionWithoutNotify(Enumerable.Empty<int>());
            return;
        }

        if (!selectedRegionIndices.Contains(selectedRegionIndex))
            selectedRegionIndices.Add(selectedRegionIndex);
        regionList.SetSelectionWithoutNotify(selectedRegionIndices);
    }
    void RefreshLevelInspector()
    {
        if (levelInspector == null)
            return;
        levelInspector.Clear();
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null)
        {
            levelInspector.Add(new HelpBox("请先在左侧选择服装关卡。", HelpBoxMessageType.Info));
            return;
        }

        levelInspector.Add(Heading("关卡属性"));
        LongField idField = new LongField("服装 Id") { value = level.clothingId };
        levelInspector.Add(idField);
        idField.RegisterValueChangedCallback(e => RenameLevel(level, e.newValue));

        TextField nameField = new TextField("名称") { value = level.displayName };
        levelInspector.Add(nameField);
        nameField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣关卡名称");
            level.displayName = e.newValue;
            MarkDirty();
            RefreshLevelList();
            RefreshCanvasTitle();
        });

        levelInspector.Add(BuildPreviewSpriteSelector(level));

        Vector2Field canvasSizeField = new Vector2Field("画布尺寸") { value = level.canvasSize };
        levelInspector.Add(canvasSizeField);
        canvasSizeField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣画布尺寸");
            level.canvasSize = new Vector2(Mathf.Max(1f, e.newValue.x), Mathf.Max(1f, e.newValue.y));
            MarkDirty();
            UpdateCanvasAspect();
            canvas?.MarkDirtyRepaint();
        });

        ColorField backgroundField = new ColorField("背景颜色") { value = level.backgroundColor };
        levelInspector.Add(backgroundField);
        backgroundField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣背景颜色");
            level.backgroundColor = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        BuildWavyGridInspector(levelInspector, level);
        BuildGridDividerInspector(levelInspector, level);

        LongField startField = new LongField("编辑器默认区域 Id") { value = level.startRegionId };
        levelInspector.Add(startField);
        startField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣默认区域");
            level.startRegionId = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        Toggle includeNumberField = new Toggle("数字块本身计数")
        {
            value = level.includeNumberBlockInCount,
        };
        levelInspector.Add(includeNumberField);
        includeNumberField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改数字块计数规则");
            level.includeNumberBlockInCount = e.newValue;
            MarkDirty();
        });

        Toggle countQuantityField = new Toggle("按区域 Quantity 累加")
        {
            value = level.countByQuantity,
        };
        levelInspector.Add(countQuantityField);
        countQuantityField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣 Quantity 规则");
            level.countByQuantity = e.newValue;
            MarkDirty();
        });

        ObjectField prefabField = new ObjectField("棋盘预制体")
        {
            objectType = typeof(GameObject),
            allowSceneObjects = false,
            value = level.levelPrefab,
        };
        levelInspector.Add(prefabField);
        prefabField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣棋盘预制体");
            level.levelPrefab = e.newValue as GameObject;
            level.levelPrefabPath = level.levelPrefab == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(level.levelPrefab);
            MarkDirty();
        });

        generatePrefabButton = new Button(GenerateLevelPrefab) { text = "生成/更新运行时棋盘预制体" };
        generatePrefabButton.style.marginTop = 4f;
        levelInspector.Add(generatePrefabButton);

        levelInspector.Add(Heading("分割线列表（几何输入）"));
        gridLineList = new ListView
        {
            fixedItemHeight = 26f,
            itemsSource = level.GridLines,
            makeItem = () =>
            {
                Label label = new Label();
                label.style.paddingLeft = 4f;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            },
            bindItem = (element, index) =>
            {
                DressMakingEmbroideryGridLineData line = level.GridLines[index];
                ((Label)element).text = line == null
                    ? $"{index}: (空)"
                    : $"{index}: 线 #{line.id}  {line.points.Count} 点"
                      + (line.isBoundary ? "  [外轮廓]" : string.Empty)
                      + (line.isClosed ? "  [闭合]" : string.Empty)
                      + (line.UseBezier ? "  [曲线]" : string.Empty);
            },
        };
        gridLineList.style.height = 150f;
        gridLineList.selectionChanged += OnGridLineSelectionChanged;
        levelInspector.Add(gridLineList);
        if (selectedGridLineIndex >= 0 && selectedGridLineIndex < level.GridLines.Count)
            gridLineList.SetSelectionWithoutNotify(new[] { selectedGridLineIndex });

        VisualElement lineButtons = Row();
        lineButtons.Add(new Button(AddGridLine) { text = "新增线" });
        lineButtons.Add(new Button(DuplicateGridLine) { text = "复制线" });
        lineButtons.Add(new Button(RemoveGridLine) { text = "删除线" });
        lineButtons.Add(new Button(ImportGridLinesFromRegions) { text = "从当前单元导入" });
        levelInspector.Add(lineButtons);

        VisualElement generateButtons = Row();
        generateButtons.Add(new Button(GenerateRegionsFromGridLines) { text = "根据线网生成/更新单元" });
        levelInspector.Add(generateButtons);

        DressMakingEmbroideryGridLineData selectedLine = SelectedGridLine;
        if (selectedLine != null)
        {
            LongField lineIdField = new LongField("当前线 Id") { value = selectedLine.id };
            levelInspector.Add(lineIdField);
            lineIdField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "修改刺绣分割线 Id");
                selectedLine.id = e.newValue;
                MarkDirty();
                RefreshLevelInspector();
            });

            Toggle boundaryField = new Toggle("作为外轮廓") { value = selectedLine.isBoundary };
            levelInspector.Add(boundaryField);
            boundaryField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "切换刺绣外轮廓线");
                selectedLine.isBoundary = e.newValue;
                if (e.newValue)
                    selectedLine.isClosed = true;
                MarkDirty();
                RefreshLevelInspector();
                canvas?.MarkDirtyRepaint();
            });

            Toggle closedField = new Toggle("闭合折线") { value = selectedLine.isClosed };
            levelInspector.Add(closedField);
            closedField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "切换刺绣闭合线");
                selectedLine.isClosed = e.newValue;
                selectedLine.EnsureBezierControls();
                MarkDirty();
                GenerateRegionsFromGridLines(false);
                RefreshLevelInspector();
            });

            Toggle curveField = new Toggle("使用贝塞尔曲线") { value = selectedLine.useBezier };
            levelInspector.Add(curveField);
            curveField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "切换刺绣贝塞尔曲线");
                selectedLine.useBezier = e.newValue;
                selectedLine.EnsureBezierControls();
                MarkDirty();
                GenerateRegionsFromGridLines(false);
                RefreshLevelInspector();
            });

            if (selectedLine.UseBezier)
            {
                IntegerField samplesField = new IntegerField("曲线采样精度")
                {
                    value = selectedLine.CurveSegments,
                };
                levelInspector.Add(samplesField);
                samplesField.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(config, "修改刺绣曲线精度");
                    selectedLine.curveSegments = Mathf.Clamp(e.newValue, 4, 48);
                    MarkDirty();
                    GenerateRegionsFromGridLines(false);
                });
            }

            BuildGridLinePointEditors(levelInspector, selectedLine);
        }

        levelInspector.Add(Heading("网格单元列表"));
        regionList = new ListView
        {
            fixedItemHeight = 26f,
            makeItem = () =>
            {
                Label label = new Label();
                label.style.paddingLeft = 4f;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            },
            bindItem = (element, index) =>
            {
                DressMakingEmbroideryLevelData current = SelectedLevel;
                if (current == null || index < 0 || index >= current.Regions.Count)
                    return;
                DressMakingEmbroideryRegionData region = current.Regions[index];
                ((Label)element).text = region == null
                    ? $"{index}: (空)"
                    : $"{index}: #{region.id}  {(region.isNumberBlock ? $"数字 {region.RequiredCount}" : "普通块")}";
            },
            itemsSource = level.Regions,
            selectionType = SelectionType.Multiple,
        };
        regionList.style.height = 170f;
        regionList.selectionChanged += OnRegionSelectionChanged;
        levelInspector.Add(regionList);

        levelInspector.Add(new HelpBox(
            "网格单元由上方线网自动生成；在这里选择单元后只编辑颜色、数字和标签。",
            HelpBoxMessageType.Info));
    }

    void BuildGridLinePointEditors(
        VisualElement parent,
        DressMakingEmbroideryGridLineData line)
    {
        parent.Add(Heading("锚点（白色）"));
        for (int i = 0; i < line.points.Count; i++)
        {
            int index = i;
            VisualElement row = Row();
            Vector2Field field = new Vector2Field($"点 {i + 1}")
            {
                value = line.points[index],
                style = { flexGrow = 1f },
            };
            field.AddToClassList("coordinate-field");
            row.Add(field);
            Button remove = new Button(() =>
            {
                if (line.points.Count <= 2)
                    return;
                Undo.RecordObject(config, "删除刺绣分割线点");
                line.points.RemoveAt(index);
                line.EnsureBezierControls();
                MarkDirty();
                GenerateRegionsFromGridLines(false);
                RefreshLevelInspector();
            }) { text = "−" };
            remove.style.width = 24f;
            row.Add(remove);
            parent.Add(row);

            field.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "移动刺绣分割线点");
                line.points[index] = Clamp01(e.newValue);
                MarkDirty();
                GenerateRegionsFromGridLines(false);
            });
        }

        parent.Add(new Button(() =>
        {
            Undo.RecordObject(config, "添加刺绣分割线点");
            Vector2 value = line.points.Count == 0
                ? new Vector2(0.5f, 0.5f)
                : line.points[^1] + new Vector2(0.05f, 0f);
            line.points.Add(Clamp01(value));
            line.EnsureBezierControls();
            MarkDirty();
            GenerateRegionsFromGridLines(false);
            RefreshLevelInspector();
        }) { text = "添加锚点" });

        if (!line.UseBezier)
            return;
        line.EnsureBezierControls();
        parent.Add(Heading("贝塞尔控制柄（橙色）"));
        for (int i = 0; i < line.BezierControls.Count; i++)
        {
            int index = i;
            Vector2Field controlField = new Vector2Field($"段 {i + 1} 控制柄")
            {
                value = line.BezierControls[index],
            };
            controlField.AddToClassList("coordinate-field");
            parent.Add(controlField);
            controlField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(config, "移动刺绣贝塞尔控制柄");
                line.BezierControls[index] = Clamp01(e.newValue);
                MarkDirty();
                GenerateRegionsFromGridLines(false);
            });
        }
    }
    void BuildWavyGridInspector(VisualElement parent, DressMakingEmbroideryLevelData level)
    {
        DressMakingEmbroideryWavyGridSettings settings = level.WavyGrid;
        Foldout foldout = new ()
        {
            text = "蜿蜒曲折网格",
            value = false,
        };
        foldout.AddToClassList("inspector-section");
        parent.Add(foldout);

        Toggle enabledField = new ("启用") { value = settings.enabled };
        foldout.Add(enabledField);
        enabledField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "切换刺绣蜿蜒网格");
            settings.enabled = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        Toggle overlayField = new Toggle("叠加在区域上方") { value = settings.overlay };
        foldout.Add(overlayField);
        overlayField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣网格层级");
            settings.overlay = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        ColorField colorField = new ColorField("颜色") { value = settings.color };
        foldout.Add(colorField);
        colorField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣网格颜色");
            settings.color = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        AddGridFloatField(foldout, "列间距", settings.columnSpacing, value =>
        {
            settings.columnSpacing = Mathf.Max(8f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "行间距", settings.rowSpacing, value =>
        {
            settings.rowSpacing = Mathf.Max(8f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "列曲折幅度", settings.columnAmplitude, value =>
        {
            settings.columnAmplitude = Mathf.Max(0f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "行曲折幅度", settings.rowAmplitude, value =>
        {
            settings.rowAmplitude = Mathf.Max(0f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "列波长", settings.columnWavelength, value =>
        {
            settings.columnWavelength = Mathf.Max(16f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "行波长", settings.rowWavelength, value =>
        {
            settings.rowWavelength = Mathf.Max(16f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "线宽", settings.lineWidth, value =>
        {
            settings.lineWidth = Mathf.Max(0.25f, value);
            canvas?.MarkDirtyRepaint();
        });

        IntegerField segmentsField = new IntegerField("每条线采样段数")
        {
            value = settings.segmentsPerLine,
        };
        foldout.Add(segmentsField);
        segmentsField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣网格采样");
            settings.segmentsPerLine = Mathf.Clamp(e.newValue, 4, 128);
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        Toggle dashedField = new Toggle("绘制虚线") { value = settings.dashed };
        foldout.Add(dashedField);
        dashedField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "切换刺绣网格虚线");
            settings.dashed = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        AddGridFloatField(foldout, "虚线长度", settings.dashLength, value =>
        {
            settings.dashLength = Mathf.Max(1f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "虚线间隔", settings.gapLength, value =>
        {
            settings.gapLength = Mathf.Max(1f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "波形相位", settings.phase, value =>
        {
            settings.phase = value;
            canvas?.MarkDirtyRepaint();
        });
    }

    void AddGridFloatField(
        VisualElement parent,
        string label,
        float value,
        Action<float> setter)
    {
        FloatField field = new FloatField(label) { value = value };
        parent.Add(field);
        field.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, $"修改刺绣网格{label}");
            setter(e.newValue);
            MarkDirty();
        });
    }

    void BuildGridDividerInspector(VisualElement parent, DressMakingEmbroideryLevelData level)
    {
        DressMakingEmbroideryGridDividerSettings settings = level.GridDivider;
        Foldout foldout = new Foldout
        {
            text = "网格单元棕色分段线",
            value = false,
        };
        foldout.AddToClassList("inspector-section");
        parent.Add(foldout);

        Toggle enabledField = new Toggle("启用") { value = settings.enabled };
        foldout.Add(enabledField);
        enabledField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "切换刺绣单元分割线");
            settings.enabled = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        ColorField dividerColorField = new ColorField("分割线颜色") { value = settings.dividerColor };
        foldout.Add(dividerColorField);
        dividerColorField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣分割线颜色");
            settings.dividerColor = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        ColorField borderColorField = new ColorField("外轮廓颜色") { value = settings.borderColor };
        foldout.Add(borderColorField);
        borderColorField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣外轮廓颜色");
            settings.borderColor = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        AddGridFloatField(foldout, "分割线宽度", settings.dividerWidth, value =>
        {
            settings.dividerWidth = Mathf.Max(0.25f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "外轮廓宽度", settings.borderWidth, value =>
        {
            settings.borderWidth = Mathf.Max(0.25f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "虚线长度", settings.dashLength, value =>
        {
            settings.dashLength = Mathf.Max(1f, value);
            canvas?.MarkDirtyRepaint();
        });
        AddGridFloatField(foldout, "虚线间隔", settings.gapLength, value =>
        {
            settings.gapLength = Mathf.Max(1f, value);
            canvas?.MarkDirtyRepaint();
        });

        Toggle borderField = new Toggle("绘制外轮廓") { value = settings.drawOuterBorder };
        foldout.Add(borderField);
        borderField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "切换刺绣外轮廓");
            settings.drawOuterBorder = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        foldout.Add(new HelpBox(
            "每个区域就是鼠标可逐格经过的一个网格单元；共享边界在运行时绘制为棕色分段线。",
            HelpBoxMessageType.Info));
    }

    void RefreshRegionInspector()
    {
        if (regionInspector == null)
            return;
        regionInspector.Clear();
        DressMakingEmbroideryRegionData region = SelectedRegion;
        if (region == null)
        {
            regionInspector.Add(new HelpBox("选择一个网格单元编辑范围、颜色和数字。", HelpBoxMessageType.Info));
            return;
        }
        regionInspector.Add(Heading(selectedRegionIndices.Count > 1
            ? $"已选择 {selectedRegionIndices.Count} 个网格单元"
            : $"网格单元 #{region.id}"));
        if (selectedRegionIndices.Count > 1)
        {
            regionInspector.Add(new HelpBox(
                "多选状态：刺绣纹理和平铺尺寸会批量应用到所有已选单元；其它属性仍编辑当前主单元。",
                HelpBoxMessageType.Info));
        }

        LongField idField = new LongField("区域 Id") { value = region.id };
        regionInspector.Add(idField);
        idField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣区域 Id");
            region.id = e.newValue;
            MarkDirty();
            RefreshRegionList();
            canvas?.MarkDirtyRepaint();
        });

        Toggle numberToggle = new Toggle("显示数字块") { value = region.isNumberBlock };
        IntegerField requiredField = new IntegerField("数字：路径目标总数") { value = region.requiredCount };
        regionInspector.Add(requiredField);
        requiredField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣数字块数量");
            region.requiredCount = Mathf.Max(0, e.newValue);
            region.isNumberBlock = region.requiredCount > 0;
            numberToggle.SetValueWithoutNotify(region.isNumberBlock);
            MarkDirty();
            RefreshRegionList();
            canvas?.MarkDirtyRepaint();
            canvas?.RefreshLabels();
        });
        IntegerField quantityField = new IntegerField("本单元计数权重（通常为1）") { value = region.quantity };
        regionInspector.Add(quantityField);
        quantityField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣区域数量");
            region.quantity = Mathf.Max(1, e.newValue);
            MarkDirty();
            RefreshRegionList();
            canvas?.MarkDirtyRepaint();
        });

        regionInspector.Add(new HelpBox(
            "路径目标总数是数字块显示的目标；计数权重是经过本单元时贡献的数量。普通玩法每个单元权重保持 1。",
            HelpBoxMessageType.Info));

        regionInspector.Add(numberToggle);
        numberToggle.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "切换刺绣数字块");
            region.isNumberBlock = e.newValue;
            region.requiredCount = e.newValue ? Mathf.Max(1, region.requiredCount, region.quantity) : 0;
            requiredField.SetValueWithoutNotify(region.requiredCount);
            MarkDirty();
            RefreshRegionList();
            canvas?.MarkDirtyRepaint();
            canvas?.RefreshLabels();
        });

        TextField labelField = new TextField("标签/数字") { value = region.label };
        regionInspector.Add(labelField);
        labelField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣区域标签");
            region.label = e.newValue;
            MarkDirty();
            canvas?.RefreshLabels();
        });

        Vector2Field labelPosField = new Vector2Field("标签位置(归一化)") { value = region.labelPosition };
        labelPosField.AddToClassList("coordinate-field");
        regionInspector.Add(labelPosField);
        labelPosField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣标签位置");
            region.labelPosition = ClampPointToRegion(region, e.newValue);
            MarkDirty();
            canvas?.RefreshLabels();
        });

        FloatField labelSizeField = new FloatField("数字/标签字号")
        {
            value = region.labelFontSize,
        };
        regionInspector.Add(labelSizeField);
        labelSizeField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣数字字号");
            region.labelFontSize = Mathf.Max(8f, e.newValue);
            MarkDirty();
            canvas?.RefreshLabels();
        });

        ColorField fillField = new ColorField("未填充颜色") { value = region.fillColor };
        regionInspector.Add(fillField);
        fillField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣未填充颜色");
            region.fillColor = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        ColorField completeField = new ColorField("已填充颜色") { value = region.completedColor };
        regionInspector.Add(completeField);
        completeField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣已填充颜色");
            region.completedColor = e.newValue;
            MarkDirty();
            canvas?.MarkDirtyRepaint();
        });

        regionInspector.Add(BuildStitchTextureSelector(region));
        FloatField tileSizeField = new FloatField("刺绣纹理平铺尺寸")
        {
            value = region.stitchTileSize,
        };
        regionInspector.Add(tileSizeField);
        tileSizeField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "批量修改刺绣纹理平铺尺寸");
            float tileSize = Mathf.Max(4f, e.newValue);
            for (int i = 0; i < selectedRegionIndices.Count; i++)
                SelectedLevel.Regions[selectedRegionIndices[i]].stitchTileSize = tileSize;
            MarkDirty();
        });

        TextField remarkField = new TextField("备注") { value = region.remark };
        regionInspector.Add(remarkField);
        remarkField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(config, "修改刺绣区域备注");
            region.remark = e.newValue;
            MarkDirty();
        });

        regionInspector.Add(new HelpBox(
            "单元边界由分割线交点自动生成，不能在单元属性中直接修改。",
            HelpBoxMessageType.Info));
    }

    VisualElement BuildStitchTextureSelector(DressMakingEmbroideryRegionData region)
    {
        VisualElement row = Row();
        row.style.flexWrap = Wrap.NoWrap;
        row.Add(new Label("刺绣纹理")
        {
            style =
            {
                minWidth = 120f,
                unityTextAlign = TextAnchor.MiddleLeft,
            },
        });
        Image preview = new Image
        {
            image = GetStitchTexture(region.FillTexturePath),
            scaleMode = ScaleMode.ScaleToFit,
            style =
            {
                width = 42f,
                height = 42f,
                marginRight = 6f,
            },
        };
        row.Add(preview);
        Button selectButton = new Button
        {
            text = string.IsNullOrEmpty(region.FillTexturePath) ? "无纹理 ▼" : $"{Path.GetFileNameWithoutExtension(region.FillTexturePath)} ▼",
            style =
            {
                flexGrow = 1f,
                flexShrink = 1f,
                minWidth = 0f,
                height = 30f,
            },
        };
        selectButton.clicked += () =>
        {
            List<Texture2D> textures = GetStitchTextureOptions();
            UnityEditor.PopupWindow.Show(
                selectButton.worldBound,
                new StitchTexturePopup(
                    textures,
                    GetStitchTexture(region.FillTexturePath),
                    texture => SetSelectedStitchTexture(texture, preview, selectButton)));
        };
        row.Add(selectButton);
        return row;
    }

    List<Texture2D> GetStitchTextureOptions()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { DressMakingEmbroiderySimulationGameConfig.StitchTextureFolder });
        var result = new List<Texture2D>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
            result.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[i])));
        return result;
    }

    static Texture2D GetStitchTexture(string path)
        => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    static Sprite GetSprite(string path)
        => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);

    VisualElement BuildPreviewSpriteSelector(DressMakingEmbroideryLevelData level)
    {
        VisualElement row = Row();
        row.Add(new Label("服装预览图") { style = { minWidth = 120f } });
        Sprite currentSprite = GetSprite(level.PreviewSpritePath);
        Image preview = new Image { image = currentSprite?.texture, scaleMode = ScaleMode.ScaleToFit };
        preview.style.width = 42f;
        preview.style.height = 42f;
        row.Add(preview);
        Button button = new Button { text = string.IsNullOrEmpty(level.PreviewSpritePath) ? "无预览图 ▼" : $"{Path.GetFileNameWithoutExtension(level.PreviewSpritePath)} ▼" };
        button.style.flexGrow = 1f;
        button.clicked += () => UnityEditor.PopupWindow.Show(button.worldBound, new PreviewSpritePopup(
            GetPreviewSpriteOptions(), GetSprite(level.PreviewSpritePath), sprite =>
            {
                Undo.RecordObject(config, "修改服装预览图");
                level.previewSpritePath = sprite == null ? string.Empty : AssetDatabase.GetAssetPath(sprite);
                preview.image = sprite?.texture;
                button.text = sprite == null ? "无预览图 ▼" : $"{sprite.name} ▼";
                MarkDirty();
            }));
        row.Add(button);
        return row;
    }

    static List<Sprite> GetPreviewSpriteOptions()
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { DressMakingEmbroiderySimulationGameConfig.PreviewSpriteFolder });
        var result = new List<Sprite>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (sprite != null)
                result.Add(sprite);
        }
        return result;
    }

    void SetSelectedStitchTexture(Texture2D texture, Image preview, Button selectButton)
    {
        Undo.RecordObject(config, "批量修改刺绣纹理");
        for (int i = 0; i < selectedRegionIndices.Count; i++)
            SelectedLevel.Regions[selectedRegionIndices[i]].fillTexturePath = texture == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(texture);
        preview.image = texture;
        selectButton.text = texture == null ? "无纹理 ▼" : $"{texture.name} ▼";
        MarkDirty();
    }

    sealed class StitchTexturePopup : PopupWindowContent
    {
        const float RowHeight = 52f;
        readonly IReadOnlyList<Texture2D> textures;
        readonly Texture2D selectedTexture;
        readonly Action<Texture2D> selected;

        public StitchTexturePopup(
            IReadOnlyList<Texture2D> textures,
            Texture2D selectedTexture,
            Action<Texture2D> selected)
        {
            this.textures = textures;
            this.selectedTexture = selectedTexture;
            this.selected = selected;
        }

        public override Vector2 GetWindowSize()
        {
            float height = Mathf.Min(420f, (textures.Count + 1) * RowHeight + 8f);
            return new Vector2(360f, height);
        }

        public override void OnGUI(Rect rect)
        {
            DrawOption(null, "无纹理");
            for (int i = 0; i < textures.Count; i++)
                DrawOption(textures[i], textures[i].name);
        }

        void DrawOption(Texture2D texture, string label)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            bool isSelected = selectedTexture == texture;
            bool isHover = rowRect.Contains(Event.current.mousePosition);
            if (isSelected || isHover)
            {
                EditorGUI.DrawRect(
                    rowRect,
                    isSelected
                        ? new Color(0.18f, 0.42f, 0.72f, 0.55f)
                        : new Color(1f, 1f, 1f, 0.08f));
            }

            Rect iconRect = new(rowRect.x + 8f, rowRect.y + 4f, 44f, 44f);
            if (texture != null)
                EditorGUI.DrawPreviewTexture(iconRect, texture, null, ScaleMode.ScaleToFit);
            else
                GUI.Box(iconRect, "∅");
            GUI.Label(
                new Rect(iconRect.xMax + 10f, rowRect.y, rowRect.width - 72f, RowHeight),
                label,
                EditorStyles.label);

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
            {
                selected(texture);
                editorWindow.Close();
            }
            if (isHover && Event.current.type == EventType.Repaint)
                editorWindow.Repaint();
        }
    }

    sealed class PreviewSpritePopup : PopupWindowContent
    {
        readonly IReadOnlyList<Sprite> sprites;
        readonly Sprite selectedSprite;
        readonly Action<Sprite> selected;

        public PreviewSpritePopup(IReadOnlyList<Sprite> sprites, Sprite selectedSprite, Action<Sprite> selected)
        {
            this.sprites = sprites;
            this.selectedSprite = selectedSprite;
            this.selected = selected;
        }

        public override Vector2 GetWindowSize() => new(360f, Mathf.Min(420f, (sprites.Count + 1) * 52f + 8f));

        public override void OnGUI(Rect rect)
        {
            DrawOption(null, "无预览图");
            for (int i = 0; i < sprites.Count; i++)
                DrawOption(sprites[i], sprites[i].name);
        }

        void DrawOption(Sprite sprite, string label)
        {
            Rect row = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            if (selectedSprite == sprite)
                EditorGUI.DrawRect(row, new Color(0.18f, 0.42f, 0.72f, 0.55f));
            Rect icon = new(row.x + 8f, row.y + 4f, 44f, 44f);
            if (sprite != null)
                EditorGUI.DrawPreviewTexture(icon, sprite.texture, null, ScaleMode.ScaleToFit);
            else
                GUI.Box(icon, "∅");
            GUI.Label(new Rect(icon.xMax + 10f, row.y, row.width - 72f, 52f), label, EditorStyles.label);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                selected(sprite);
                editorWindow.Close();
            }
        }
    }

    void OnRegionSelectionChanged(IEnumerable<object> selection)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null)
            return;
        selectedRegionIndices.Clear();
        selectedRegionIndices.AddRange(regionList.selectedIndices);
        if (selectedRegionIndices.Count == 0)
        {
            selectedRegionIndex = -1;
            RefreshRegionInspector();
            canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
            return;
        }
        selectedRegionIndex = regionList.selectedIndex >= 0
            ? regionList.selectedIndex
            : selectedRegionIndices[^1];
        RefreshRegionInspector();
        SetInspectorTab(true);
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
    }

    void SelectRegionFromCanvas(int index, bool additive)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || index < 0 || index >= level.Regions.Count)
            return;
        if (!additive)
        {
            selectedRegionIndices.Clear();
            selectedRegionIndices.Add(index);
        }
        else if (!selectedRegionIndices.Remove(index))
        {
            selectedRegionIndices.Add(index);
        }
        selectedRegionIndex = selectedRegionIndices.Count == 0
            ? -1
            : selectedRegionIndices.Contains(index) ? index : selectedRegionIndices[^1];
        RefreshRegionList();
        RefreshRegionInspector();
        SetInspectorTab(true);
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
    }

    void OnGridLineSelectionChanged(IEnumerable<object> selection)
    {
        object first = selection.FirstOrDefault();
        DressMakingEmbroideryGridLineData line = first as DressMakingEmbroideryGridLineData;
        int index = line == null || SelectedLevel == null ? -1 : SelectedLevel.GridLines.IndexOf(line);
        if (index < 0)
            return;
        selectedGridLineIndex = index;
        RefreshLevelInspector();
        SetInspectorTab(false);
        canvas?.SetData(SelectedLevel, selectedRegionIndices, SelectedGridLine);
    }

    void SelectGridLineFromCanvas(int index)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || index < 0 || index >= level.GridLines.Count)
            return;
        selectedGridLineIndex = index;
        RefreshLevelInspector();
        SetInspectorTab(false);
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
    }

    void OnGridLinePointChanged(int lineIndex, int pointIndex, Vector2 value)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || lineIndex < 0 || lineIndex >= level.GridLines.Count)
            return;
        DressMakingEmbroideryGridLineData line = level.GridLines[lineIndex];
        if (pointIndex < 0 || pointIndex >= line.points.Count)
            return;
        Undo.RecordObject(config, "移动刺绣分割线点");
        line.points[pointIndex] = Clamp01(value);
        MarkDirty();
        RefreshTopologyPreview();
    }

    void OnBezierControlChanged(int lineIndex, int controlIndex, Vector2 value)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || lineIndex < 0 || lineIndex >= level.GridLines.Count)
            return;
        DressMakingEmbroideryGridLineData line = level.GridLines[lineIndex];
        line.EnsureBezierControls();
        if (controlIndex < 0 || controlIndex >= line.BezierControls.Count)
            return;
        Undo.RecordObject(config, "移动刺绣贝塞尔控制柄");
        line.BezierControls[controlIndex] = Clamp01(value);
        MarkDirty();
        RefreshTopologyPreview();
    }

    void OnGridLineTranslated(int lineIndex, Vector2 requestedDelta)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || lineIndex < 0 || lineIndex >= level.GridLines.Count)
            return;
        DressMakingEmbroideryGridLineData line = level.GridLines[lineIndex];
        line.EnsureBezierControls();
        Vector2 delta = ClampTranslation(line, requestedDelta);
        if (delta.sqrMagnitude <= Mathf.Epsilon)
            return;
        Undo.RecordObject(config, "移动整条刺绣分割线");
        for (int i = 0; i < line.points.Count; i++)
            line.points[i] += delta;
        for (int i = 0; i < line.BezierControls.Count; i++)
            line.BezierControls[i] += delta;
        MarkDirty();
        RefreshTopologyPreview();
    }

    static Vector2 ClampTranslation(
        DressMakingEmbroideryGridLineData line,
        Vector2 delta)
    {
        float minX = 1f;
        float minY = 1f;
        float maxX = 0f;
        float maxY = 0f;
        void Include(Vector2 point)
        {
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }
        for (int i = 0; i < line.points.Count; i++)
            Include(line.points[i]);
        if (line.UseBezier)
        {
            for (int i = 0; i < line.BezierControls.Count; i++)
                Include(line.BezierControls[i]);
        }
        return new Vector2(
            Mathf.Clamp(delta.x, -minX, 1f - maxX),
            Mathf.Clamp(delta.y, -minY, 1f - maxY));
    }

    void OnLabelPositionChanged(int regionIndex, Vector2 value)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || regionIndex < 0 || regionIndex >= level.Regions.Count)
            return;
        Undo.RecordObject(config, "移动刺绣数字");
        level.Regions[regionIndex].labelPosition = value;
        MarkDirty();
        canvas?.RefreshLabels();
        if (regionIndex == selectedRegionIndex)
            RefreshRegionInspector();
    }

    void OnCanvasDragStarted(string undoName)
    {
        OnCanvasPointDragEnded();
        Undo.IncrementCurrentGroup();
        canvasDragUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
    }

    void OnCanvasPointDragEnded()
    {
        if (topologyPreviewDirty)
            RefreshTopologyPreview(true);
        if (canvasDragUndoGroup < 0)
            return;
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(canvasDragUndoGroup);
        canvasDragUndoGroup = -1;
    }

    void RefreshTopologyPreview(bool force = false)
    {
        topologyPreviewDirty = true;
        double now = EditorApplication.timeSinceStartup;
        if (!force && now - lastTopologyPreviewTime < 0.1d)
        {
            canvas?.MarkDirtyRepaint();
            return;
        }

        topologyPreviewDirty = false;
        lastTopologyPreviewTime = now;
        GenerateRegionsFromGridLines(false);
    }

    void AddCanvasLinePoint(Vector2 value)
    {
        DressMakingEmbroideryGridLineData line = SelectedGridLine;
        if (line == null)
            return;
        Undo.RecordObject(config, "添加刺绣分割线点");
        line.points.Add(Clamp01(value));
        line.EnsureBezierControls();
        MarkDirty();
        GenerateRegionsFromGridLines(false);
    }

    void AddLevel()
    {
        if (config == null)
        {
            CreateConfigAsset();
            if (config == null)
                return;
        }

        long id = 1001;
        while (config.DataDict.ContainsKey(id))
            id++;
        Undo.RecordObject(config, "新增刺绣关卡");
        DressMakingEmbroideryLevelData level = DressMakingEmbroiderySample.CreateLevel1001();
        level.clothingId = id;
        level.displayName = $"{id} · 新刺绣";
        config.SetLevel(id, level);
        selectedLevelId = id;
        selectedRegionIndex = -1;
        selectedRegionIndices.Clear();
        selectedGridLineIndex = -1;
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
    }

    void DuplicateLevel()
    {
        DressMakingEmbroideryLevelData source = SelectedLevel;
        if (source == null)
            return;
        long id = 1001;
        while (config.DataDict.ContainsKey(id))
            id++;
        Undo.RecordObject(config, "复制刺绣关卡");
        DressMakingEmbroideryLevelData copy = CloneLevel(source);
        copy.clothingId = id;
        copy.displayName = $"{source.displayName} · 副本";
        config.SetLevel(id, copy);
        selectedLevelId = id;
        selectedRegionIndex = -1;
        selectedRegionIndices.Clear();
        selectedGridLineIndex = -1;
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
    }

    void RemoveLevel()
    {
        if (SelectedLevel == null)
            return;
        if (!EditorUtility.DisplayDialog(WindowTitle, $"删除服装 {selectedLevelId} 的刺绣关卡？", "删除", "取消"))
            return;
        Undo.RecordObject(config, "删除刺绣关卡");
        config.RemoveLevel(selectedLevelId);
        selectedLevelId = 0;
        selectedRegionIndex = -1;
        selectedRegionIndices.Clear();
        selectedGridLineIndex = -1;
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
    }

    void RenameLevel(DressMakingEmbroideryLevelData level, long newId)
    {
        if (newId <= 0 || newId == selectedLevelId)
            return;
        if (config.DataDict.ContainsKey(newId))
        {
            SetStatus($"服装 Id {newId} 已存在，未修改。", HelpBoxMessageType.Warning);
            RefreshLevelInspector();
            return;
        }

        Undo.RecordObject(config, "修改服装 Id");
        config.DataDict.Remove(selectedLevelId);
        config.SetLevel(newId, level);
        selectedLevelId = newId;
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
    }

    void AddGridLine()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null)
            return;
        Undo.RecordObject(config, "新增刺绣分割线");
        level.GridLines.Add(new DressMakingEmbroideryGridLineData
        {
            id = NextGridLineId(level),
            points = new List<Vector2>
            {
                new(0.25f, 0.5f),
                new(0.75f, 0.5f),
            },
        });
        selectedGridLineIndex = level.GridLines.Count - 1;
        MarkDirty();
        RefreshAllViews();
    }

    void DuplicateGridLine()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        DressMakingEmbroideryGridLineData source = SelectedGridLine;
        if (level == null || source == null)
            return;
        Undo.RecordObject(config, "复制刺绣分割线");
        var copy = new DressMakingEmbroideryGridLineData
        {
            id = NextGridLineId(level),
            isBoundary = false,
            isClosed = source.isClosed,
            useBezier = source.useBezier,
            curveSegments = source.curveSegments,
            points = new List<Vector2>(source.points),
            bezierControls = new List<Vector2>(source.BezierControls),
        };
        Vector2 offset = ClampTranslation(copy, new Vector2(0.035f, 0.035f));
        for (int i = 0; i < copy.points.Count; i++)
            copy.points[i] += offset;
        for (int i = 0; i < copy.BezierControls.Count; i++)
            copy.BezierControls[i] += offset;
        level.GridLines.Add(copy);
        selectedGridLineIndex = level.GridLines.Count - 1;
        MarkDirty();
        GenerateRegionsFromGridLines(false);
        RefreshAllViews();
    }

    void RemoveGridLine()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || SelectedGridLine == null)
            return;
        Undo.RecordObject(config, "删除刺绣分割线");
        level.GridLines.RemoveAt(selectedGridLineIndex);
        selectedGridLineIndex = Mathf.Clamp(
            selectedGridLineIndex - 1,
            -1,
            level.GridLines.Count - 1);
        MarkDirty();
        GenerateRegionsFromGridLines(false);
        RefreshAllViews();
    }

    void ImportGridLinesFromRegions()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || level.Regions.Count == 0)
            return;
        if (level.GridLines.Count > 0
            && !EditorUtility.DisplayDialog(
                WindowTitle,
                "将用当前单元边界替换现有线网，是否继续？",
                "替换",
                "取消"))
            return;

        Undo.RecordObject(config, "从刺绣单元导入线网");
        int count = DressMakingEmbroideryGridTopology.ImportLinesFromRegions(level);
        selectedGridLineIndex = count > 0 ? 0 : -1;
        MarkDirty();
        RefreshAllViews();
        SetStatus($"已从当前单元导入 {count} 条去重线段。", HelpBoxMessageType.Info);
    }

    void GenerateRegionsFromGridLines()
        => GenerateRegionsFromGridLines(true);

    void GenerateRegionsFromGridLines(bool showStatus)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        long selectedRegionId = SelectedRegion?.id ?? 0;
        List<long> selectedRegionIds = selectedRegionIndices
            .Where(index => level != null && index >= 0 && index < level.Regions.Count)
            .Select(index => level.Regions[index].id)
            .ToList();
        if (level == null)
            return;
        if (!DressMakingEmbroideryGridTopology.GenerateRegions(
                level,
                out int count,
                out string error))
        {
            canvas?.MarkDirtyRepaint();
            if (showStatus)
            {
                SetStatus(error, HelpBoxMessageType.Warning);
            }
            return;
        }

        selectedRegionIndices.Clear();
        for (int i = 0; i < selectedRegionIds.Count; i++)
        {
            int index = level.Regions.FindIndex(region => region != null && region.id == selectedRegionIds[i]);
            if (index >= 0)
                selectedRegionIndices.Add(index);
        }
        selectedRegionIndex = selectedRegionId == 0
            ? selectedRegionIndices.Count > 0
                ? selectedRegionIndices[^1]
                : Mathf.Clamp(selectedRegionIndex, -1, count - 1)
            : level.Regions.FindIndex(region => region != null && region.id == selectedRegionId);
        if (selectedRegionIndex < 0 && count > 0)
            selectedRegionIndex = 0;
        MarkDirty();
        RefreshRegionList();
        RefreshRegionInspector();
        canvas?.SetData(level, selectedRegionIndices, SelectedGridLine);
        if (showStatus)
        {
            SetStatus($"已根据线段交点生成 {count} 个闭合网格单元。", HelpBoxMessageType.Info);
        }
    }

    void AddRegion()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null)
            return;
        Undo.RecordObject(config, "新增刺绣区域");
        long id = NextRegionId(level);
        level.Regions.Add(new DressMakingEmbroideryRegionData
        {
            id = id,
            labelPosition = new Vector2(0.5f, 0.5f),
            boundaryPoints = new List<Vector2>
            {
                new(0.35f, 0.35f), new(0.65f, 0.35f),
                new(0.65f, 0.65f), new(0.35f, 0.65f),
            },
        });
        selectedRegionIndex = level.Regions.Count - 1;
        MarkDirty();
        RefreshAllViews();
    }

    static long NextGridLineId(DressMakingEmbroideryLevelData level)
    {
        long id = 1;
        while (level.GridLines.Any(line => line != null && line.id == id))
            id++;
        return id;
    }

    void DuplicateRegion()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        DressMakingEmbroideryRegionData source = SelectedRegion;
        if (level == null || source == null)
            return;
        Undo.RecordObject(config, "复制刺绣区域");
        DressMakingEmbroideryRegionData copy = CloneRegion(source);
        copy.id = NextRegionId(level);
        copy.label = $"{source.label} · 副本";
        level.Regions.Add(copy);
        selectedRegionIndex = level.Regions.Count - 1;
        MarkDirty();
        RefreshAllViews();
    }

    void RemoveRegion()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || SelectedRegion == null)
            return;
        Undo.RecordObject(config, "删除刺绣区域");
        level.Regions.RemoveAt(selectedRegionIndex);
        selectedRegionIndex = Mathf.Clamp(selectedRegionIndex - 1, -1, level.Regions.Count - 1);
        MarkDirty();
        RefreshAllViews();
    }

    void MoveRegion(int delta)
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || SelectedRegion == null)
            return;
        int target = selectedRegionIndex + delta;
        if (target < 0 || target >= level.Regions.Count)
            return;
        Undo.RecordObject(config, "调整刺绣区域绘制层级");
        DressMakingEmbroideryRegionData value = level.Regions[selectedRegionIndex];
        level.Regions[selectedRegionIndex] = level.Regions[target];
        level.Regions[target] = value;
        selectedRegionIndex = target;
        MarkDirty();
        RefreshAllViews();
    }

    void EnsureSampleData()
    {
        if (config == null)
        {
            CreateConfigAsset();
            if (config == null)
                return;
        }
        Undo.RecordObject(config, "补充刺绣示例数据");
        config.EnsureSampleData();
        config.Normalize();
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
        SetStatus("已补充示例关卡 1001、1002（已有 Id 不会覆盖）。", HelpBoxMessageType.Info);
    }

    void NormalizeConfig()
    {
        if (config == null)
            return;
        Undo.RecordObject(config, "整理刺绣配置");
        config.Normalize();
        MarkDirty();
        RefreshLevelList();
        RefreshAllViews();
        SetStatus("已整理坐标和尺寸。", HelpBoxMessageType.Info);
    }

    void ValidateConfig()
    {
        if (config == null)
            return;
        List<string> errors = config.Validate();
        validationBox.messageType = errors.Count == 0
            ? HelpBoxMessageType.Info
            : HelpBoxMessageType.Warning;
        validationBox.text = errors.Count == 0
            ? string.Empty
            : string.Join("\n", errors.Take(12));
        validationArea.style.display = errors.Count == 0
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        SetStatus(
            errors.Count == 0 ? "校验通过。" : $"发现 {errors.Count} 个问题。",
            errors.Count == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
    }

    void SaveConfig()
    {
        if (config == null)
            return;
        config.UpgradeSchema();
        config.Normalize();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        SetStatus($"已保存：{AssetDatabase.GetAssetPath(config)}", HelpBoxMessageType.Info);
        titleContent = new GUIContent(WindowTitle);
    }

    void CreateConfigAsset()
    {
        string defaultFolder = Path.GetDirectoryName(DressMakingEmbroiderySimulationGameConfig.DefaultConfigPath)
            ?.Replace('\\', '/');
        string path = EditorUtility.SaveFilePanelInProject(
            "创建刺绣配置",
            nameof(DressMakingEmbroiderySimulationGameConfig),
            "asset",
            "选择配置资源保存位置",
            defaultFolder);
        if (string.IsNullOrEmpty(path))
            return;

        DressMakingEmbroiderySimulationGameConfig asset =
            CreateInstance<DressMakingEmbroiderySimulationGameConfig>();
        asset.EnsureSampleData();
        asset.UpgradeSchema();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = asset;
        SetConfig(asset);
        SetStatus($"已创建并写入示例关卡：{path}", HelpBoxMessageType.Info);
    }

    void GenerateLevelPrefab()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null)
            return;

        string path = EditorUtility.SaveFilePanelInProject(
            "生成刺绣棋盘预制体",
            $"EmbroideryLevel_{level.clothingId}",
            "prefab",
            "生成的预制体保存位置");
        if (string.IsNullOrEmpty(path))
            return;

        Undo.RecordObject(config, "生成刺绣棋盘预制体");
        GameObject prefab = DressMakingEmbroideryLevelPrefabBuilder.CreateOrUpdate(level, path);
        if (prefab == null)
        {
            SetStatus("预制体生成失败。", HelpBoxMessageType.Error);
            return;
        }

        level.levelPrefab = prefab;
        level.levelPrefabPath = path;
        MarkDirty();
        RefreshLevelInspector();
        SetStatus(
            $"已生成完整运行时棋盘预制体：{path}（形状以 Config 区域数据为准）",
            HelpBoxMessageType.Info);
        EditorGUIUtility.PingObject(prefab);
    }

    void OnDisable()
    {
        OnCanvasPointDragEnded();
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        EditorApplication.delayCall -= EnsureConfigAfterReload;
        if (config != null)
            EditorUtility.SetDirty(config);
    }

    void OnUndoRedoPerformed()
    {
        if (config == null || selectedTitle == null)
            return;

        canvasDragUndoGroup = -1;
        RefreshLevelList();
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (level == null || selectedRegionIndex >= level.Regions.Count)
            selectedRegionIndex = -1;
        if (level == null || selectedGridLineIndex >= level.GridLines.Count)
            selectedGridLineIndex = -1;
        RefreshAllViews();
        Repaint();
    }

    void MarkDirty()
    {
        if (config == null)
            return;
        EditorUtility.SetDirty(config);
        titleContent = new GUIContent(WindowTitle + " *");
    }

    void RefreshCanvasTitle()
    {
        DressMakingEmbroideryLevelData level = SelectedLevel;
        if (selectedTitle != null && level != null)
            selectedTitle.text =
                $"{level.ClothingId} · {level.DisplayName}  ({level.GridLines.Count} 条线 / {level.Regions.Count} 个单元)";
    }

    static long NextRegionId(DressMakingEmbroideryLevelData level)
    {
        long id = 1;
        while (level.Regions.Any(region => region != null && region.id == id))
            id++;
        return id;
    }

    static DressMakingEmbroideryLevelData CloneLevel(DressMakingEmbroideryLevelData source)
    {
        DressMakingEmbroideryLevelData copy = new ()
        {
            clothingId = source.clothingId,
            displayName = source.displayName,
            canvasSize = source.canvasSize,
            backgroundColor = source.backgroundColor,
            wavyGrid = CloneWavyGrid(source.WavyGrid),
            gridDivider = CloneGridDivider(source.GridDivider),
            gridLines = source.GridLines
                .Select(line => line == null
                    ? null
                    : new DressMakingEmbroideryGridLineData
                    {
                        id = line.id,
                        isBoundary = line.isBoundary,
                        isClosed = line.isClosed,
                        useBezier = line.useBezier,
                        curveSegments = line.curveSegments,
                        points = new List<Vector2>(line.points),
                        bezierControls = new List<Vector2>(line.BezierControls),
                    })
                .ToList(),
            startRegionId = source.startRegionId,
            previewSpritePath = source.previewSpritePath,
            includeNumberBlockInCount = source.includeNumberBlockInCount,
            countByQuantity = source.countByQuantity,
            levelPrefab = source.levelPrefab,
            levelPrefabPath = source.levelPrefabPath,
        };
        foreach (DressMakingEmbroideryRegionData region in source.Regions)
            copy.regions.Add(region == null ? null : CloneRegion(region));
        return copy;
    }

    static DressMakingEmbroideryWavyGridSettings CloneWavyGrid(
        DressMakingEmbroideryWavyGridSettings source)
    {
        if (source == null)
            return new ();

        return new ()
        {
            enabled = source.enabled,
            overlay = source.overlay,
            color = source.color,
            columnSpacing = source.columnSpacing,
            rowSpacing = source.rowSpacing,
            columnAmplitude = source.columnAmplitude,
            rowAmplitude = source.rowAmplitude,
            columnWavelength = source.columnWavelength,
            rowWavelength = source.rowWavelength,
            lineWidth = source.lineWidth,
            segmentsPerLine = source.segmentsPerLine,
            dashed = source.dashed,
            dashLength = source.dashLength,
            gapLength = source.gapLength,
            phase = source.phase,
        };
    }

    static DressMakingEmbroideryGridDividerSettings CloneGridDivider(
        DressMakingEmbroideryGridDividerSettings source)
    {
        if (source == null)
            return new ();

        return new ()
        {
            enabled = source.enabled,
            dividerColor = source.dividerColor,
            borderColor = source.borderColor,
            dividerWidth = source.dividerWidth,
            borderWidth = source.borderWidth,
            dashLength = source.dashLength,
            gapLength = source.gapLength,
            drawOuterBorder = source.drawOuterBorder,
        };
    }

    static DressMakingEmbroideryRegionData CloneRegion(DressMakingEmbroideryRegionData source)
    {
        return new ()
        {
            id = source.id,
            requiredCount = source.requiredCount,
            quantity = source.quantity,
            fillColor = source.fillColor,
            completedColor = source.completedColor,
            fillTexturePath = source.fillTexturePath,
            stitchTileSize = source.stitchTileSize,
            label = source.label,
            labelPosition = source.labelPosition,
            labelFontSize = source.labelFontSize,
            isNumberBlock = source.isNumberBlock,
            isClosed = source.isClosed,
            boundaryPoints = source.boundaryPoints == null ? new List<Vector2>() : new List<Vector2>(source.boundaryPoints),
            bezierPoints = source.bezierPoints == null ? new List<Vector2>() : new List<Vector2>(source.bezierPoints),
            neighbourIds = source.neighbourIds == null ? new List<long>() : new List<long>(source.neighbourIds),
            fillSpritePath = source.fillSpritePath,
            numberSpritePath = source.numberSpritePath,
            remark = source.remark,
        };
    }

    static VisualElement Card(string title)
    {
        VisualElement card = new ()
        {
            style =
            {
                flexGrow = 1f,
                paddingLeft = 6f,
                paddingRight = 6f,
                paddingTop = 5f,
                paddingBottom = 5f,
            }
        };
        Label heading = Heading(title);
        card.Add(heading);
        return card;
    }

    static Label Heading(string text)
    {
        Label heading = new (text);
        heading.AddToClassList("section-title");
        heading.style.marginTop = 6f;
        heading.style.marginBottom = 3f;
        return heading;
    }

    static VisualElement Row()
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                flexShrink = 0f,
            }
        };
        row.AddToClassList("inspector-actions");
        return row;
    }

    static Vector2 Clamp01(Vector2 value)
        => new(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));

    static Vector2 ClampPointToRegion(
        DressMakingEmbroideryRegionData region,
        Vector2 value)
    {
        value = Clamp01(value);
        List<Vector2> polygon = region.GetSampledBoundary(10);
        if (EmbroideryGeometry.ContainsPoint(polygon, value))
            return value;

        Vector2 closest = region.labelPosition;
        float distance = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 start = polygon[i];
            Vector2 end = polygon[(i + 1) % polygon.Count];
            Vector2 direction = end - start;
            float t = direction.sqrMagnitude <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(value - start, direction) / direction.sqrMagnitude);
            Vector2 candidate = start + direction * t;
            float candidateDistance = Vector2.SqrMagnitude(value - candidate);
            if (candidateDistance < distance)
            {
                distance = candidateDistance;
                closest = candidate;
            }
        }

        return Vector2.Lerp(closest, DressMakingEmbroideryGridTopology.GetCentroid(polygon), 0.01f);
    }
}
#endif
