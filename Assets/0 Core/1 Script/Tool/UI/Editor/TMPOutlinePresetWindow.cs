using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>
    /// TMP 描边预设生成器（UIToolkit）。
    /// 左侧字体库：自定义字体文件夹，自动检索其中所有 TMP 字体资源；
    /// 右侧工作区：选择源材质、配置描边参数、预览并生成 Material Preset。
    /// 描边是材质层面的设置，单独建预设避免污染同字体的其他文本。
    /// 菜单：Tools/TMP/描边预设生成器；Project 中选中字体或 TMP 材质右键 -> TMP/描边预设生成器。
    /// </summary>
    public class TMPOutlinePresetWindow : EditorWindow
    {
        private const string FolderConfigKey = "TMPOutlinePresetWindow.FontFolder";
        private const string DefaultFolder = "Assets/AddressableAssets/Local/Font";
        private const float PreviewFontSize = 34f;
        private static readonly Color DefaultColor = new Color32(0xC9, 0x9D, 0x45, 0xFF); // #C99D45
        private static readonly (string hex, string name)[] SwatchColors =
        {
            ("C99D45", "金"), ("FFFFFF", "白"), ("000000", "黑"), ("8B2F2F", "红"),
        };

        // ---- 字体库（左）----
        private TextField folderField;
        private HelpBox folderWarning;
        private ToolbarSearchField searchField;
        private ListView fontListView;
        private readonly List<TMP_FontAsset> allFonts = new();
        private readonly List<TMP_FontAsset> shownFonts = new();

        // ---- 工作区（右）----
        private Label placeholder;
        private VisualElement content;
        private Label fontNameLabel;
        private Label fontPathLabel;
        private DropdownField materialDropdown;
        private readonly List<Material> materialChoices = new();
        private ColorField colorField;
        private TextField hexField;
        private Slider widthSlider;
        private Toggle pureOutlineToggle;
        private Slider dilateSlider;
        private TextField suffixField;
        private Label previewLabel;
        private VisualElement previewBox;
        private HelpBox warningBox;
        private HelpBox resultBox;
        private Button generateBtn;
        private Button pingBtn;

        private TMP_FontAsset selectedFont;
        private Material lastPreset;
        private bool suffixAuto = true;
        private UnityEngine.Object pendingSelect;

        [MenuItem("Tools/TMP/描边预设生成器")]
        public static void Open()
        {
            var win = GetWindow<TMPOutlinePresetWindow>();
            win.titleContent = new GUIContent("TMP 描边预设");
            win.minSize = new Vector2(720, 560);
        }

        [MenuItem("Assets/TMP/描边预设生成器", true)]
        private static bool ValidateOpenFromAsset()
        {
            return Selection.activeObject is TMP_FontAsset
                || (Selection.activeObject is Material mat && IsTMPMaterial(mat));
        }

        [MenuItem("Assets/TMP/描边预设生成器", false, 1100)]
        private static void OpenFromAsset()
        {
            Open();
            GetWindow<TMPOutlinePresetWindow>().SelectAsset(Selection.activeObject);
        }

        private void CreateGUI()
        {
            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);
            split.Add(BuildLibraryPane());
            split.Add(BuildWorkspacePane());

            RefreshFonts();
            if (pendingSelect != null)
            {
                var target = pendingSelect;
                pendingSelect = null;
                SelectAsset(target);
            }
            else
            {
                GrabFromSelection();
            }
            OnParamsChanged();
        }

        // ---------- 字体库 ----------

        private VisualElement BuildLibraryPane()
        {
            var pane = new VisualElement();
            pane.style.minWidth = 200;
            pane.style.paddingLeft = pane.style.paddingRight = 8;
            pane.style.paddingTop = 8;

            var header = new Label("字体库");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            pane.Add(header);

            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            folderField = new TextField { isDelayed = true, tooltip = "字体文件夹（Assets 内相对路径），回车确认" };
            folderField.value = EditorUserSettings.GetConfigValue(FolderConfigKey) is { Length: > 0 } saved ? saved : DefaultFolder;
            folderField.style.flexGrow = 1;
            folderField.RegisterValueChangedCallback(e => SetFolder(e.newValue));
            folderRow.Add(folderField);
            var browseBtn = new Button(BrowseFolder) { text = "…", tooltip = "选择文件夹" };
            browseBtn.style.width = 24;
            folderRow.Add(browseBtn);
            var refreshBtn = new Button(RefreshFonts) { text = "↻", tooltip = "重新检索" };
            refreshBtn.style.width = 24;
            folderRow.Add(refreshBtn);
            pane.Add(folderRow);

            folderWarning = new HelpBox("", HelpBoxMessageType.Warning);
            folderWarning.style.display = DisplayStyle.None;
            folderWarning.style.marginTop = 4;
            pane.Add(folderWarning);

            searchField = new ToolbarSearchField();
            searchField.style.width = Length.Percent(100);
            searchField.style.marginTop = 4;
            searchField.RegisterValueChangedCallback(_ => FilterFonts());
            pane.Add(searchField);

            fontListView = new ListView
            {
                fixedItemHeight = 22,
                itemsSource = shownFonts,
                makeItem = () =>
                {
                    var label = new Label();
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    label.style.paddingLeft = 6;
                    return label;
                },
                bindItem = (e, i) => ((Label)e).text = shownFonts[i].name,
            };
            fontListView.style.flexGrow = 1;
            fontListView.style.marginTop = 4;
            fontListView.selectionChanged += _ =>
            {
                if (fontListView.selectedIndex >= 0 && fontListView.selectedIndex < shownFonts.Count)
                    SelectFont(shownFonts[fontListView.selectedIndex]);
            };
            pane.Add(fontListView);

            var grabBtn = new Button(() => GrabFromSelection(true)) { text = "从 Hierarchy 选中的文本抓取" };
            grabBtn.style.marginTop = 4;
            grabBtn.style.marginBottom = 8;
            pane.Add(grabBtn);
            return pane;
        }

        private void SetFolder(string path)
        {
            path = path.Trim().Replace('\\', '/').TrimEnd('/');
            EditorUserSettings.SetConfigValue(FolderConfigKey, path);
            folderField.SetValueWithoutNotify(path);
            RefreshFonts();
        }

        private void BrowseFolder()
        {
            string abs = EditorUtility.OpenFolderPanel("选择字体文件夹", folderField.value, "");
            if (string.IsNullOrEmpty(abs))
                return;
            abs = abs.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!abs.StartsWith(dataPath))
            {
                folderWarning.text = "必须选择本项目 Assets 内的文件夹。";
                folderWarning.style.display = DisplayStyle.Flex;
                return;
            }
            SetFolder("Assets" + abs.Substring(dataPath.Length));
        }

        private void RefreshFonts()
        {
            allFonts.Clear();
            string folder = folderField.value;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                folderWarning.text = "文件夹无效，请输入 Assets 内的有效路径。";
                folderWarning.style.display = DisplayStyle.Flex;
            }
            else
            {
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { folder }))
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (font != null)
                        allFonts.Add(font);
                }
                allFonts.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                folderWarning.text = "该文件夹下未找到 TMP 字体资源。";
                folderWarning.style.display = allFonts.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            FilterFonts();
        }

        private void FilterFonts()
        {
            string key = searchField.value?.Trim() ?? "";
            shownFonts.Clear();
            shownFonts.AddRange(string.IsNullOrEmpty(key)
                ? allFonts
                : allFonts.Where(f => f.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0));
            fontListView.Rebuild();
            int idx = shownFonts.IndexOf(selectedFont);
            fontListView.SetSelectionWithoutNotify(idx >= 0 ? new[] { idx } : Enumerable.Empty<int>());
        }

        // ---------- 工作区 ----------

        private VisualElement BuildWorkspacePane()
        {
            var pane = new ScrollView();
            pane.style.paddingLeft = pane.style.paddingRight = 12;
            pane.style.paddingTop = 8;

            placeholder = new Label("在左侧选择一个字体，或点击左下角按钮从场景选中的文本抓取。");
            placeholder.style.opacity = 0.6f;
            placeholder.style.marginTop = 12;
            pane.Add(placeholder);

            content = new VisualElement();
            content.style.display = DisplayStyle.None;
            pane.Add(content);

            // ---- 字体信息 ----
            var fontCard = MakeCard(content, "当前字体");
            fontNameLabel = new Label();
            fontNameLabel.style.fontSize = 14;
            fontNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            fontCard.Add(fontNameLabel);
            fontPathLabel = new Label();
            fontPathLabel.style.fontSize = 10;
            fontPathLabel.style.opacity = 0.6f;
            fontPathLabel.style.marginBottom = 4;
            fontCard.Add(fontPathLabel);

            var matRow = new VisualElement();
            matRow.style.flexDirection = FlexDirection.Row;
            materialDropdown = new DropdownField("源材质");
            materialDropdown.style.flexGrow = 1;
            materialDropdown.RegisterValueChangedCallback(_ => OnParamsChanged());
            matRow.Add(materialDropdown);
            var pingMatBtn = new Button(() =>
            {
                if (CurrentSourceMaterial != null)
                    EditorGUIUtility.PingObject(CurrentSourceMaterial);
            }) { text = "定位", tooltip = "在 Project 中定位源材质" };
            matRow.Add(pingMatBtn);
            fontCard.Add(matRow);

            // ---- 描边设置 ----
            var setCard = MakeCard(content, "描边设置");

            var colorRow = new VisualElement();
            colorRow.style.flexDirection = FlexDirection.Row;
            colorField = new ColorField("描边颜色") { value = DefaultColor, showAlpha = false };
            colorField.style.flexGrow = 1;
            colorField.RegisterValueChangedCallback(e => SetColor(e.newValue));
            colorRow.Add(colorField);
            hexField = new TextField { value = "C99D45", maxLength = 6 };
            hexField.style.width = 64;
            hexField.RegisterCallback<FocusOutEvent>(_ => ApplyHexInput());
            hexField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ApplyHexInput();
            });
            colorRow.Add(hexField);
            setCard.Add(colorRow);

            // 常用色快速切换
            var swatchRow = new VisualElement();
            swatchRow.style.flexDirection = FlexDirection.Row;
            swatchRow.style.marginTop = 4;
            swatchRow.style.marginLeft = 3;
            foreach (var (hex, name) in SwatchColors)
            {
                ColorUtility.TryParseHtmlString("#" + hex, out var c);
                var swatch = new Button(() => SetColor(c)) { tooltip = $"{name} #{hex}" };
                swatch.style.width = swatch.style.height = 20;
                swatch.style.marginRight = 4;
                swatch.style.backgroundColor = c;
                swatch.style.borderTopLeftRadius = swatch.style.borderTopRightRadius =
                    swatch.style.borderBottomLeftRadius = swatch.style.borderBottomRightRadius = 10;
                swatchRow.Add(swatch);
            }
            setCard.Add(swatchRow);

            widthSlider = new Slider("描边宽度", 0f, 1f) { value = 0.2f, showInputField = true };
            widthSlider.style.marginTop = 6;
            widthSlider.RegisterValueChangedCallback(_ => OnParamsChanged());
            setCard.Add(widthSlider);

            // 纯外描边：Face Dilate 向外推等量，字身不被描边吃细。
            pureOutlineToggle = new Toggle("纯外描边（Face Dilate 同步宽度）") { value = true };
            pureOutlineToggle.RegisterValueChangedCallback(e =>
            {
                dilateSlider.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                OnParamsChanged();
            });
            setCard.Add(pureOutlineToggle);

            dilateSlider = new Slider("Face Dilate", -1f, 1f) { value = 0f, showInputField = true };
            dilateSlider.style.display = DisplayStyle.None;
            dilateSlider.RegisterValueChangedCallback(_ => OnParamsChanged());
            setCard.Add(dilateSlider);

            suffixField = new TextField("预设后缀") { value = AutoSuffix(DefaultColor) };
            suffixField.style.marginTop = 4;
            suffixField.RegisterValueChangedCallback(e => suffixAuto = e.newValue == AutoSuffix(colorField.value));
            setCard.Add(suffixField);

            warningBox = new HelpBox("", HelpBoxMessageType.Warning);
            warningBox.style.display = DisplayStyle.None;
            warningBox.style.marginTop = 4;
            setCard.Add(warningBox);

            // ---- 预览 ----
            var prevCard = MakeCard(content, "预览（近似效果，以实际材质为准）");
            previewBox = new VisualElement();
            previewBox.style.height = 96;
            previewBox.style.justifyContent = Justify.Center;
            previewBox.style.alignItems = Align.Center;
            previewBox.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            previewBox.style.borderTopLeftRadius = previewBox.style.borderTopRightRadius =
                previewBox.style.borderBottomLeftRadius = previewBox.style.borderBottomRightRadius = 6;
            previewLabel = new Label("金色描边 Sample 123");
            previewLabel.style.fontSize = PreviewFontSize;
            previewLabel.style.color = Color.white;
            previewLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewBox.Add(previewLabel);
            prevCard.Add(previewBox);

            var prevOptRow = new VisualElement();
            prevOptRow.style.flexDirection = FlexDirection.Row;
            prevOptRow.style.marginTop = 4;
            var prevText = new TextField { value = previewLabel.text };
            prevText.style.flexGrow = 1;
            prevText.RegisterValueChangedCallback(e => previewLabel.text = e.newValue);
            prevOptRow.Add(prevText);
            var bgToggle = new Toggle("浅色底") { value = false };
            bgToggle.style.marginLeft = 6;
            bgToggle.RegisterValueChangedCallback(e =>
            {
                previewBox.style.backgroundColor = e.newValue ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.13f, 0.13f, 0.13f);
                previewLabel.style.color = e.newValue ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
            });
            prevOptRow.Add(bgToggle);
            prevCard.Add(prevOptRow);

            // ---- 操作 ----
            generateBtn = new Button(GeneratePreset) { text = "生成描边预设" };
            generateBtn.style.height = 28;
            generateBtn.style.marginTop = 8;
            content.Add(generateBtn);
            var applyBtn = new Button(GenerateAndApplyToSelection)
            {
                text = "为 Hierarchy 选中的文本生成并应用",
                tooltip = "按每个文本自身的共享材质生成预设并应用，避免图集不匹配",
            };
            applyBtn.style.height = 24;
            applyBtn.style.marginTop = 2;
            content.Add(applyBtn);

            resultBox = new HelpBox("", HelpBoxMessageType.Info);
            resultBox.style.display = DisplayStyle.None;
            resultBox.style.marginTop = 6;
            content.Add(resultBox);
            pingBtn = new Button(() => { if (lastPreset != null) EditorGUIUtility.PingObject(lastPreset); }) { text = "在 Project 中定位" };
            pingBtn.style.display = DisplayStyle.None;
            content.Add(pingBtn);

            return pane;
        }

        private VisualElement MakeCard(VisualElement parent, string header)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.08f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            card.style.paddingLeft = card.style.paddingRight = 8;
            card.style.paddingTop = card.style.paddingBottom = 8;
            card.style.marginBottom = 8;
            var label = new Label(header);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12;
            label.style.marginBottom = 4;
            card.Add(label);
            parent.Add(card);
            return card;
        }

        // ---------- 选择与状态同步 ----------

        /// <summary>从 Project 右键菜单进入时定位到对应字体/材质；窗口未构建完成时挂起等 CreateGUI。</summary>
        public void SelectAsset(UnityEngine.Object obj)
        {
            if (content == null)
            {
                pendingSelect = obj;
                return;
            }
            if (obj is TMP_FontAsset font)
                SelectFont(font);
            else if (obj is Material mat && IsTMPMaterial(mat))
            {
                var owner = FindFontAsset(mat);
                if (owner != null)
                    SelectFont(owner, mat);
            }
        }

        private void SelectFont(TMP_FontAsset font, Material prefer = null)
        {
            selectedFont = font;
            int idx = shownFonts.IndexOf(font);
            fontListView.SetSelectionWithoutNotify(idx >= 0 ? new[] { idx } : Enumerable.Empty<int>());

            bool has = font != null;
            placeholder.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            content.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            if (!has)
                return;

            fontNameLabel.text = font.name;
            fontPathLabel.text = AssetDatabase.GetAssetPath(font);
            RefreshMaterialChoices(prefer);
            OnParamsChanged();
        }

        /// <summary>候选源材质：字体默认材质 + 同目录下以字体名开头的 TMP 材质预设（TMP 预设命名约定）。</summary>
        private void RefreshMaterialChoices(Material prefer)
        {
            materialChoices.Clear();
            var names = new List<string>();
            if (selectedFont.material != null)
            {
                materialChoices.Add(selectedFont.material);
                names.Add($"{selectedFont.material.name}（字体默认）");
            }
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(selectedFont))?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (mat != null && mat != selectedFont.material && IsTMPMaterial(mat)
                        && mat.name.StartsWith(selectedFont.name))
                    {
                        materialChoices.Add(mat);
                        names.Add(mat.name);
                    }
                }
            }
            if (prefer != null && !materialChoices.Contains(prefer))
            {
                materialChoices.Insert(0, prefer);
                names.Insert(0, prefer.name);
            }
            materialDropdown.choices = names;
            int sel = prefer != null ? materialChoices.IndexOf(prefer) : 0;
            if (materialChoices.Count > 0)
                materialDropdown.index = Mathf.Max(0, sel);
            else
                materialDropdown.SetValueWithoutNotify("");
        }

        private Material CurrentSourceMaterial =>
            materialDropdown.index >= 0 && materialDropdown.index < materialChoices.Count
                ? materialChoices[materialDropdown.index]
                : null;

        private void SetColor(Color c)
        {
            c.a = 1f;
            colorField.SetValueWithoutNotify(c);
            hexField.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(c));
            if (suffixAuto)
                suffixField.SetValueWithoutNotify(AutoSuffix(c));
            OnParamsChanged();
        }

        private void ApplyHexInput()
        {
            if (ColorUtility.TryParseHtmlString("#" + hexField.value.TrimStart('#'), out var c))
                SetColor(c);
            else
                hexField.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(colorField.value));
        }

        private static string AutoSuffix(Color c) => $"Outline #{ColorUtility.ToHtmlStringRGB(c)}";

        private float CurrentDilate => pureOutlineToggle.value ? widthSlider.value : dilateSlider.value;

        private void OnParamsChanged()
        {
            // 预览：UIToolkit 的 -unity-text-outline 近似模拟 SDF 描边。
            previewLabel.style.unityTextOutlineColor = colorField.value;
            previewLabel.style.unityTextOutlineWidth = widthSlider.value * PreviewFontSize * 0.22f;
            // 本项目 TMP_FontAsset 不继承 TextCore 的 FontAsset，用源 TTF 预览，取不到则退回默认字体。
            if (selectedFont != null && selectedFont.sourceFontFile != null)
                previewLabel.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(selectedFont.sourceFontFile));

            bool clipRisk = widthSlider.value + Mathf.Max(0f, CurrentDilate) > 0.9f;
            warningBox.text = "描边宽度 + Face Dilate 接近 SDF 上限，可能被裁切/发虚；必要时调高字体 Atlas Padding 后重新生成 Font Asset。";
            warningBox.style.display = clipRisk ? DisplayStyle.Flex : DisplayStyle.None;

            generateBtn.SetEnabled(CurrentSourceMaterial != null);
        }

        private void GrabFromSelection(bool showHint = false)
        {
            var texts = Selection.GetFiltered<TMP_Text>(SelectionMode.Deep);
            if (texts.Length == 0 || texts[0].font == null)
            {
                if (showHint)
                    ShowResult("请先在 Hierarchy 里选中带 TMP_Text 的物体。", HelpBoxMessageType.Warning);
                return;
            }
            var text = texts[0];
            SelectFont(text.font, IsTMPMaterial(text.fontSharedMaterial) ? text.fontSharedMaterial : null);
        }

        /// <summary>材质通常是字体资产的子资源或同目录预设，尝试反查字体。</summary>
        private static TMP_FontAsset FindFontAsset(Material mat)
        {
            string path = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(path))
                return null;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
                return font;
            string dir = Path.GetDirectoryName(path);
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { dir.Replace('\\', '/') }))
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && mat.name.StartsWith(font.name))
                    return font;
            }
            return null;
        }

        // ---------- 生成 ----------

        private void GeneratePreset()
        {
            var source = CurrentSourceMaterial;
            var preset = CreatePreset(source);
            if (preset != null)
            {
                Selection.activeObject = preset;
                EditorGUIUtility.PingObject(preset);
                RefreshMaterialChoices(source); // 新预设进入候选列表，选中项保持源材质
            }
        }

        private void GenerateAndApplyToSelection()
        {
            var texts = Selection.GetFiltered<TMP_Text>(SelectionMode.Deep);
            if (texts.Length == 0)
            {
                ShowResult("请先在 Hierarchy 里选中带 TMP_Text 的物体。", HelpBoxMessageType.Warning);
                return;
            }

            // 同一个共享材质只生成一份预设，复用以避免增加 Draw Call。
            var cache = new Dictionary<Material, Material>();
            int applied = 0;
            foreach (var text in texts)
            {
                var source = text.fontSharedMaterial;
                if (source == null || !IsTMPMaterial(source))
                    continue;
                if (!cache.TryGetValue(source, out var preset))
                {
                    preset = CreatePreset(source);
                    cache[source] = preset;
                }
                if (preset != null)
                {
                    Undo.RecordObject(text, "Apply TMP Outline Preset");
                    text.fontSharedMaterial = preset;
                    EditorUtility.SetDirty(text);
                    applied++;
                }
            }
            ShowResult($"已为 {applied} 个文本应用描边预设，生成 {cache.Count} 份材质。", HelpBoxMessageType.Info);
        }

        private Material CreatePreset(Material source)
        {
            if (source == null || !IsTMPMaterial(source))
            {
                ShowResult("请先指定一个 TMP 字体材质。", HelpBoxMessageType.Warning);
                return null;
            }
            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath))
            {
                ShowResult("源材质不是项目资源，无法生成预设。", HelpBoxMessageType.Error);
                return null;
            }

            string dir = Path.GetDirectoryName(srcPath);
            string baseName = Path.GetFileNameWithoutExtension(srcPath);
            string suffix = string.IsNullOrWhiteSpace(suffixField.value) ? AutoSuffix(colorField.value) : suffixField.value.Trim();
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(dir, $"{baseName} - {suffix}.mat").Replace('\\', '/'));

            // 复制源材质，保留字体图集贴图与所有原有属性，仅改描边。
            var preset = new Material(source);
            preset.SetColor(ShaderUtilities.ID_OutlineColor, colorField.value);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, widthSlider.value);
            preset.SetFloat(ShaderUtilities.ID_FaceDilate, CurrentDilate);

            AssetDatabase.CreateAsset(preset, targetPath);
            AssetDatabase.SaveAssets();

            lastPreset = preset;
            ShowResult($"已生成预设：{targetPath}", HelpBoxMessageType.Info);
            return preset;
        }

        private void ShowResult(string msg, HelpBoxMessageType type)
        {
            resultBox.text = msg;
            resultBox.messageType = type;
            resultBox.style.display = DisplayStyle.Flex;
            pingBtn.style.display = lastPreset != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsTMPMaterial(Material mat)
        {
            // TMP 的 SDF shader 都带有 _OutlineColor / _OutlineWidth 属性。
            return mat != null && mat.shader != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth);
        }
    }
}
