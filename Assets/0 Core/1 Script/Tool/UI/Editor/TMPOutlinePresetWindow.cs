using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorTools
{
    /// <summary>
    /// TMP 描边预设生成器（UIToolkit）。
    /// 基于源材质复制生成带自定义颜色描边的 Material Preset，
    /// 描边是材质层面的设置，单独建预设避免污染同字体的其他文本。
    /// 菜单：Tools/TMP/描边预设生成器
    /// </summary>
    public class TMPOutlinePresetWindow : EditorWindow
    {
        private const float PreviewFontSize = 34f;
        private static readonly Color DefaultColor = new Color32(0xC9, 0x9D, 0x45, 0xFF); // #C99D45
        private static readonly (string hex, string name)[] SwatchColors =
        {
            ("C99D45", "金"), ("FFFFFF", "白"), ("000000", "黑"), ("8B2F2F", "红"),
        };

        private ObjectField _materialField;
        private ColorField _colorField;
        private TextField _hexField;
        private Slider _widthSlider;
        private Toggle _pureOutlineToggle;
        private Slider _dilateSlider;
        private TextField _suffixField;
        private Label _previewLabel;
        private VisualElement _previewBox;
        private HelpBox _warningBox;
        private HelpBox _resultBox;
        private Button _generateBtn;
        private Button _applyBtn;
        private Button _pingBtn;

        private TMP_FontAsset _previewFont;
        private Material _lastPreset;
        private bool _suffixAuto = true;

        [MenuItem("Tools/TMP/描边预设生成器")]
        public static void Open()
        {
            var win = GetWindow<TMPOutlinePresetWindow>();
            win.titleContent = new GUIContent("TMP 描边预设");
            win.minSize = new Vector2(380, 600);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            var scroll = new ScrollView();
            root.Add(scroll);

            // ---- 标题 ----
            var title = new Label("TMP 描边预设生成器");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 2;
            scroll.Add(title);
            var subtitle = new Label("复制源材质生成带描边的 Material Preset，不影响原材质。");
            subtitle.style.fontSize = 11;
            subtitle.style.opacity = 0.6f;
            subtitle.style.marginBottom = 8;
            scroll.Add(subtitle);

            // ---- 源材质 ----
            var srcCard = MakeCard(scroll, "源材质");
            _materialField = new ObjectField("字体材质") { objectType = typeof(Material) };
            _materialField.RegisterValueChangedCallback(_ => OnMaterialChanged());
            srcCard.Add(_materialField);
            var grabBtn = new Button(GrabFromSelection) { text = "从 Hierarchy 选中的文本抓取" };
            grabBtn.style.marginTop = 4;
            srcCard.Add(grabBtn);

            // ---- 描边设置 ----
            var setCard = MakeCard(scroll, "描边设置");

            var colorRow = new VisualElement();
            colorRow.style.flexDirection = FlexDirection.Row;
            _colorField = new ColorField("描边颜色") { value = DefaultColor, showAlpha = false };
            _colorField.style.flexGrow = 1;
            _colorField.RegisterValueChangedCallback(e => SetColor(e.newValue));
            colorRow.Add(_colorField);
            _hexField = new TextField { value = "C99D45", maxLength = 6 };
            _hexField.style.width = 64;
            _hexField.RegisterCallback<FocusOutEvent>(_ => ApplyHexInput());
            _hexField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ApplyHexInput();
            });
            colorRow.Add(_hexField);
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

            _widthSlider = new Slider("描边宽度", 0f, 1f) { value = 0.2f, showInputField = true };
            _widthSlider.style.marginTop = 6;
            _widthSlider.RegisterValueChangedCallback(_ => OnParamsChanged());
            setCard.Add(_widthSlider);

            // 纯外描边：Face Dilate 向外推等量，字身不被描边吃细。
            _pureOutlineToggle = new Toggle("纯外描边（Face Dilate 同步宽度）") { value = true };
            _pureOutlineToggle.RegisterValueChangedCallback(e =>
            {
                _dilateSlider.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                OnParamsChanged();
            });
            setCard.Add(_pureOutlineToggle);

            _dilateSlider = new Slider("Face Dilate", -1f, 1f) { value = 0f, showInputField = true };
            _dilateSlider.style.display = DisplayStyle.None;
            _dilateSlider.RegisterValueChangedCallback(_ => OnParamsChanged());
            setCard.Add(_dilateSlider);

            _suffixField = new TextField("预设后缀") { value = AutoSuffix(DefaultColor) };
            _suffixField.style.marginTop = 4;
            _suffixField.RegisterValueChangedCallback(e => _suffixAuto = e.newValue == AutoSuffix(_colorField.value));
            setCard.Add(_suffixField);

            _warningBox = new HelpBox("", HelpBoxMessageType.Warning);
            _warningBox.style.display = DisplayStyle.None;
            _warningBox.style.marginTop = 4;
            setCard.Add(_warningBox);

            // ---- 预览 ----
            var prevCard = MakeCard(scroll, "预览（近似效果，以实际材质为准）");
            _previewBox = new VisualElement();
            _previewBox.style.height = 96;
            _previewBox.style.justifyContent = Justify.Center;
            _previewBox.style.alignItems = Align.Center;
            _previewBox.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            _previewBox.style.borderTopLeftRadius = _previewBox.style.borderTopRightRadius =
                _previewBox.style.borderBottomLeftRadius = _previewBox.style.borderBottomRightRadius = 6;
            _previewLabel = new Label("金色描边 Sample 123");
            _previewLabel.style.fontSize = PreviewFontSize;
            _previewLabel.style.color = Color.white;
            _previewLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _previewBox.Add(_previewLabel);
            prevCard.Add(_previewBox);

            var prevOptRow = new VisualElement();
            prevOptRow.style.flexDirection = FlexDirection.Row;
            prevOptRow.style.marginTop = 4;
            var prevText = new TextField { value = _previewLabel.text };
            prevText.style.flexGrow = 1;
            prevText.RegisterValueChangedCallback(e => _previewLabel.text = e.newValue);
            prevOptRow.Add(prevText);
            var bgToggle = new Toggle("浅色底") { value = false };
            bgToggle.style.marginLeft = 6;
            bgToggle.RegisterValueChangedCallback(e =>
            {
                _previewBox.style.backgroundColor = e.newValue ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.13f, 0.13f, 0.13f);
                _previewLabel.style.color = e.newValue ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
            });
            prevOptRow.Add(bgToggle);
            prevCard.Add(prevOptRow);

            // ---- 操作 ----
            _generateBtn = new Button(GeneratePreset) { text = "生成描边预设" };
            _generateBtn.style.height = 28;
            _generateBtn.style.marginTop = 8;
            scroll.Add(_generateBtn);
            _applyBtn = new Button(GenerateAndApplyToSelection) { text = "为 Hierarchy 选中的文本生成并应用" };
            _applyBtn.style.height = 24;
            _applyBtn.style.marginTop = 2;
            scroll.Add(_applyBtn);

            _resultBox = new HelpBox("", HelpBoxMessageType.Info);
            _resultBox.style.display = DisplayStyle.None;
            _resultBox.style.marginTop = 6;
            scroll.Add(_resultBox);
            _pingBtn = new Button(() => { if (_lastPreset != null) EditorGUIUtility.PingObject(_lastPreset); }) { text = "在 Project 中定位" };
            _pingBtn.style.display = DisplayStyle.None;
            scroll.Add(_pingBtn);

            GrabFromSelection();
            OnParamsChanged();
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

        // ---------- 状态同步 ----------

        private void SetColor(Color c)
        {
            c.a = 1f;
            _colorField.SetValueWithoutNotify(c);
            _hexField.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(c));
            if (_suffixAuto)
                _suffixField.SetValueWithoutNotify(AutoSuffix(c));
            OnParamsChanged();
        }

        private void ApplyHexInput()
        {
            if (ColorUtility.TryParseHtmlString("#" + _hexField.value.TrimStart('#'), out var c))
                SetColor(c);
            else
                _hexField.SetValueWithoutNotify(ColorUtility.ToHtmlStringRGB(_colorField.value));
        }

        private static string AutoSuffix(Color c) => $"Outline #{ColorUtility.ToHtmlStringRGB(c)}";

        private float CurrentDilate => _pureOutlineToggle.value ? _widthSlider.value : _dilateSlider.value;

        private void OnParamsChanged()
        {
            // 预览：UIToolkit 的 -unity-text-outline 近似模拟 SDF 描边。
            _previewLabel.style.unityTextOutlineColor = _colorField.value;
            _previewLabel.style.unityTextOutlineWidth = _widthSlider.value * PreviewFontSize * 0.22f;
            // 本项目 TMP_FontAsset 不继承 TextCore 的 FontAsset，用源 TTF 预览，取不到则退回默认字体。
            if (_previewFont != null && _previewFont.sourceFontFile != null)
                _previewLabel.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_previewFont.sourceFontFile));

            bool clipRisk = _widthSlider.value + Mathf.Max(0f, CurrentDilate) > 0.9f;
            _warningBox.text = "描边宽度 + Face Dilate 接近 SDF 上限，可能被裁切/发虚；必要时调高字体 Atlas Padding 后重新生成 Font Asset。";
            _warningBox.style.display = clipRisk ? DisplayStyle.Flex : DisplayStyle.None;

            bool valid = _materialField.value is Material mat && IsTMPMaterial(mat);
            _generateBtn.SetEnabled(valid);
        }

        private void OnMaterialChanged()
        {
            var mat = _materialField.value as Material;
            if (mat != null && !IsTMPMaterial(mat))
            {
                ShowResult("所选材质不是 TMP SDF 材质（缺少 _OutlineWidth 属性）。", HelpBoxMessageType.Error);
                _materialField.SetValueWithoutNotify(null);
                mat = null;
            }
            if (mat != null)
                _previewFont = FindFontAsset(mat) ?? _previewFont;
            OnParamsChanged();
        }

        private void GrabFromSelection()
        {
            var texts = Selection.GetFiltered<TMP_Text>(SelectionMode.Deep);
            if (texts.Length == 0)
                return;
            var text = texts[0];
            if (text.fontSharedMaterial != null && IsTMPMaterial(text.fontSharedMaterial))
            {
                _materialField.SetValueWithoutNotify(text.fontSharedMaterial);
                _previewFont = text.font;
                OnParamsChanged();
            }
        }

        /// <summary>材质通常是字体资产的子资源或同目录预设，尝试反查字体用于预览。</summary>
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
            var source = _materialField.value as Material;
            var preset = CreatePreset(source);
            if (preset != null)
            {
                Selection.activeObject = preset;
                EditorGUIUtility.PingObject(preset);
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
            string suffix = string.IsNullOrWhiteSpace(_suffixField.value) ? AutoSuffix(_colorField.value) : _suffixField.value.Trim();
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(dir, $"{baseName} - {suffix}.mat").Replace('\\', '/'));

            // 复制源材质，保留字体图集贴图与所有原有属性，仅改描边。
            var preset = new Material(source);
            preset.SetColor(ShaderUtilities.ID_OutlineColor, _colorField.value);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, _widthSlider.value);
            preset.SetFloat(ShaderUtilities.ID_FaceDilate, CurrentDilate);

            AssetDatabase.CreateAsset(preset, targetPath);
            AssetDatabase.SaveAssets();

            _lastPreset = preset;
            ShowResult($"已生成预设：{targetPath}", HelpBoxMessageType.Info);
            return preset;
        }

        private void ShowResult(string msg, HelpBoxMessageType type)
        {
            _resultBox.text = msg;
            _resultBox.messageType = type;
            _resultBox.style.display = DisplayStyle.Flex;
            _pingBtn.style.display = _lastPreset != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsTMPMaterial(Material mat)
        {
            // TMP 的 SDF shader 都带有 _OutlineColor / _OutlineWidth 属性。
            return mat != null && mat.shader != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth);
        }
    }
}
