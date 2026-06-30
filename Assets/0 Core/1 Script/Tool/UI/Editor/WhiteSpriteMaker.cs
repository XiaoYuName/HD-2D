using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements; // ColorField
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 把一张带颜色的贴图“漂白”成白色（保留 Alpha 轮廓），
/// 之后在 UI 的 Image 组件上用 color 染成任意颜色复用，而不必为每种配色单独出图。
///
/// 两种模式：
///   纯白(PureWhite)：RGB 全部置白，只保留 Alpha —— 适合纯色图标，染色后等于 Image.color。
///   灰度(Grayscale) ：RGB 取亮度灰阶，保留明暗 —— 适合有渐变/高光的图，染色后保留体积感。
///
/// 用法：
///   1. 在 Project 选中一张或多张 PNG，菜单 Tools/2D/Make White Sprite（默认纯白、生成副本）。
///   2. 或菜单 Tools/2D/White Sprite Maker 打开窗口，选模式 + 染色预览后再生成。
///
/// 默认生成 *_White.png 副本（不动原图），并把原图的导入设置(Sprite类型/PPU/边框等)拷贝过去，
/// 让白图能直接顶替原 Sprite 使用。仅处理 PNG。
/// </summary>
public static class WhiteSpriteMaker
{
    public enum WhiteMode
    {
        PureWhite, // RGB 置白，保留 Alpha
        Grayscale, // RGB 取亮度灰阶，保留 Alpha
    }

    public struct Options
    {
        public WhiteMode mode;
        public bool inPlace;            // true = 覆盖原文件；false = 输出到 *_White.png
        public bool copyImportSettings; // 生成副本时，把原图的贴图导入设置拷给副本

        public static Options Default => new()
        {
            mode = WhiteMode.PureWhite,
            inPlace = false,
            copyImportSettings = true,
        };
    }

    public struct Result
    {
        public bool ok;
        public string message;
        public string outputPath;
    }

    // ---------------------------------------------------------------- menu ----

    private const string MakeWhiteMenu = EditorMenuSet.Texture2D + "/Make White Sprite";

    [MenuItem(MakeWhiteMenu, true)]
    private static bool MakeSelectedValidate() => CollectTexturePaths().Count > 0;

    [MenuItem(MakeWhiteMenu)]
    private static void MakeSelectedMenu() => MakeSelection(Options.Default);

    // ----------------------------------------------------------- processing ----

    /// <summary>当前 Selection 中所有贴图的资源路径。</summary>
    private static List<string> CollectTexturePaths()
    {
        var paths = new List<string>();
        foreach (var obj in Selection.objects)
        {
            if (obj is Texture2D)
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(p)) paths.Add(p);
            }
        }
        return paths;
    }

    /// <summary>漂白当前 Selection 中的所有贴图。</summary>
    public static void MakeSelection(Options options)
    {
        var paths = CollectTexturePaths();
        if (paths.Count == 0)
        {
            Debug.LogWarning("[WhiteSpriteMaker] 未选中任何贴图。");
            return;
        }

        var toCopy = new List<KeyValuePair<string, string>>(); // src -> dst，需在 Refresh 后拷贝导入设置
        var outputs = new List<string>();
        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Count; i++)
            {
                EditorUtility.DisplayProgressBar("White Sprite Maker",
                    Path.GetFileName(paths[i]), (float)i / paths.Count);

                Result r = Process(paths[i], options);
                if (!r.ok)
                {
                    Debug.LogWarning($"[WhiteSpriteMaker] 跳过 {Path.GetFileName(paths[i])}：{r.message}");
                    continue;
                }

                changed++;
                outputs.Add(r.outputPath);
                Debug.Log($"[WhiteSpriteMaker] {Path.GetFileName(paths[i])} → {r.outputPath}（{options.mode}）");

                if (options.copyImportSettings && !options.inPlace)
                    toCopy.Add(new KeyValuePair<string, string>(paths[i], r.outputPath));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // 第二遍：副本已重新导入、有了 importer，再把原图的导入设置拷过去。
        foreach (var kv in toCopy)
            CopyImportSettings(kv.Key, kv.Value);
        if (toCopy.Count > 0) AssetDatabase.Refresh();

        // 选中生成结果，便于直接拖入使用。
        if (outputs.Count > 0)
        {
            var objs = new List<Object>(outputs.Count);
            foreach (var p in outputs)
            {
                var o = AssetDatabase.LoadMainAssetAtPath(p);
                if (o != null) objs.Add(o);
            }
            Selection.objects = objs.ToArray();
        }

        Debug.Log($"[WhiteSpriteMaker] 完成：{changed}/{paths.Count} 张已漂白。");
    }

    /// <summary>漂白单张贴图并写盘。</summary>
    public static Result Process(string assetPath, Options options)
    {
        var r = new Result { outputPath = options.inPlace ? assetPath : NewPath(assetPath) };

        Color32[] px = LoadPixels(assetPath, out int w, out int h);
        if (px == null)
        {
            r.message = "非 PNG 或无法解码";
            return r;
        }

        Whiten(px, options.mode);

        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        try
        {
            dst.SetPixels32(px);
            dst.Apply();
            File.WriteAllBytes(Path.GetFullPath(r.outputPath), dst.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(dst);
        }

        r.ok = true;
        return r;
    }

    /// <summary>解码 PNG 到 Color32 数组（行优先，原点左下）。失败返回 null。</summary>
    public static Color32[] LoadPixels(string assetPath, out int w, out int h)
    {
        w = h = 0;
        if (Path.GetExtension(assetPath).ToLowerInvariant() != ".png")
            return null;

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!src.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath))))
                return null;
            w = src.width;
            h = src.height;
            return src.GetPixels32();
        }
        finally
        {
            Object.DestroyImmediate(src);
        }
    }

    /// <summary>就地漂白像素数组：只改 RGB，Alpha 原样保留。</summary>
    public static void Whiten(Color32[] px, WhiteMode mode)
    {
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            if (mode == WhiteMode.PureWhite)
            {
                c.r = c.g = c.b = 255;
            }
            else // Grayscale：Rec.601 亮度，保留明暗
            {
                byte l = (byte)Mathf.Clamp(c.r * 0.299f + c.g * 0.587f + c.b * 0.114f + 0.5f, 0f, 255f);
                c.r = c.g = c.b = l;
            }
            px[i] = c;
        }
    }

    /// <summary>把原贴图的导入设置拷给生成的副本，使其能直接顶替原 Sprite。</summary>
    private static void CopyImportSettings(string srcPath, string dstPath)
    {
        if (AssetImporter.GetAtPath(srcPath) is not TextureImporter src ||
            AssetImporter.GetAtPath(dstPath) is not TextureImporter dst)
            return;

        var s = new TextureImporterSettings();
        src.ReadTextureSettings(s);        // 含 textureType / spriteMode / 对齐 / PPU / border / alphaIsTransparency 等
        dst.SetTextureSettings(s);
        dst.SetPlatformTextureSettings(src.GetDefaultPlatformTextureSettings()); // 尺寸/压缩

#pragma warning disable 0618 // 多图集模式下，连同子 Sprite 切片一起拷贝
        if (src.spriteImportMode == SpriteImportMode.Multiple)
            dst.spritesheet = src.spritesheet;
#pragma warning restore 0618

        dst.SaveAndReimport();
    }

    private static string NewPath(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(assetPath);
        return AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_White.png");
    }
}

/// <summary>漂白窗口（UIToolkit）：选模式 + 染色预览，确认后批量生成。</summary>
public class WhiteSpriteMakerWindow : EditorWindow
{
    private const float BoxH = 160f;

    private EnumField mode;
    private Toggle inPlace;
    private Toggle copySettings;
    private ColorField tint;

    private Label summary;
    private Image origImage;
    private Image resultImage;
    private Button makeBtn;

    private Texture2D previewTex; // 内存预览贴图，需手动释放

    [MenuItem(EditorMenuSet.Texture2D + "/White Sprite Maker")]
    private static void Open()
    {
        GetWindow<WhiteSpriteMakerWindow>("White Sprite Maker").minSize = new Vector2(440, 520);
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        SetPadding(root, 10f);

        var body = new ScrollView();
        root.Add(body);

        body.Add(new HelpBox(
            "把彩色图漂白成白图，之后在 Image 组件上用 color 染色复用。\n" +
            "默认生成 *_White.png 副本并继承原图导入设置；仅处理 PNG。", HelpBoxMessageType.Info));

        mode = new EnumField("模式", WhiteSpriteMaker.WhiteMode.PureWhite)
        {
            tooltip = "纯白：RGB 全白只留 Alpha（纯色图标）；灰度：取亮度保留明暗（带渐变/高光的图）",
        };
        inPlace = new Toggle("覆盖原文件")
        {
            value = false,
            tooltip = "关闭则输出到 *_White.png（推荐，保留原彩色图）",
        };
        copySettings = new Toggle("继承导入设置")
        {
            value = true,
            tooltip = "把原图的 Sprite 类型 / PPU / 九宫格边框等拷给副本，方便直接顶替使用",
        };
        tint = new ColorField("染色预览")
        {
            value = Color.white,
            tooltip = "仅预览：模拟在 Image.color 上染色后的效果，不影响生成结果",
        };

        mode.RegisterValueChangedCallback(_ => Refresh());
        inPlace.RegisterValueChangedCallback(_ => copySettings.SetEnabled(!inPlace.value));
        tint.RegisterValueChangedCallback(e =>
        {
            if (resultImage != null) resultImage.tintColor = e.newValue;
        });

        body.Add(mode);
        body.Add(inPlace);
        body.Add(copySettings);
        body.Add(tint);

        body.Add(Divider());
        var head = new Label("预览");
        head.style.unityFontStyleAndWeight = FontStyle.Bold;
        head.style.marginBottom = 4f;
        body.Add(head);

        summary = NormalLabel();
        summary.style.marginBottom = 6f;
        body.Add(summary);

        origImage = new Image { scaleMode = ScaleMode.ScaleToFit };
        resultImage = new Image { scaleMode = ScaleMode.ScaleToFit, tintColor = tint.value };

        var compare = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        compare.Add(LabeledColumn("原图", origImage));
        compare.Add(LabeledColumn("漂白后（染色预览）", resultImage));
        body.Add(compare);

        makeBtn = new Button(Apply) { style = { height = 32f, marginTop = 12f } };
        body.Add(makeBtn);

        Selection.selectionChanged -= Refresh;
        Selection.selectionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Refresh;
        ReleasePreview();
    }

    private WhiteSpriteMaker.Options CurrentOptions() => new()
    {
        mode = (WhiteSpriteMaker.WhiteMode)mode.value,
        inPlace = inPlace.value,
        copyImportSettings = copySettings.value,
    };

    private void Apply()
    {
        WhiteSpriteMaker.MakeSelection(CurrentOptions());
        Refresh();
    }

    /// <summary>重新生成当前选中第一张贴图的预览。</summary>
    private void Refresh()
    {
        ReleasePreview();

        Texture2D primary = PrimaryTexture(out int count);
        makeBtn.SetEnabled(count > 0);
        makeBtn.text = count > 0
            ? (inPlace.value ? $"漂白 {count} 张（覆盖原图）" : $"生成 {count} 张白色副本")
            : "漂白（未选中贴图）";

        if (primary == null)
        {
            summary.text = "请先在 Project 窗口选中至少一张 PNG。";
            origImage.image = null;
            resultImage.image = null;
            return;
        }

        string path = AssetDatabase.GetAssetPath(primary);
        summary.text = count > 1
            ? $"已选 {count} 张 · 预览第 1 张：{Path.GetFileName(path)}"
            : Path.GetFileName(path);
        origImage.image = primary;

        Color32[] px = WhiteSpriteMaker.LoadPixels(path, out int w, out int h);
        if (px == null)
        {
            summary.text += "（仅支持 PNG，无法预览）";
            resultImage.image = null;
            return;
        }

        WhiteSpriteMaker.Whiten(px, (WhiteSpriteMaker.WhiteMode)mode.value);
        previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        previewTex.SetPixels32(px);
        previewTex.Apply();
        resultImage.image = previewTex;
        resultImage.tintColor = tint.value;
    }

    private static Texture2D PrimaryTexture(out int count)
    {
        count = 0;
        Texture2D first = null;
        foreach (var obj in Selection.objects)
            if (obj is Texture2D t)
            {
                if (first == null) first = t;
                count++;
            }
        return first;
    }

    private void ReleasePreview()
    {
        if (previewTex != null)
        {
            Object.DestroyImmediate(previewTex);
            previewTex = null;
        }
    }

    // ---- 小型 UI 构造 helper ----

    private static Label NormalLabel()
    {
        var l = new Label();
        l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

    private static VisualElement Divider() => new()
    {
        style =
        {
            height = 1f,
            marginTop = 8f,
            marginBottom = 8f,
            backgroundColor = new Color(1f, 1f, 1f, 0.08f),
        },
    };

    private static VisualElement LabeledColumn(string caption, VisualElement preview)
    {
        StyleBox(preview);

        var label = new Label(caption);
        label.style.fontSize = 11f;
        label.style.marginTop = 3f;
        label.style.unityTextAlign = TextAnchor.UpperCenter;
        label.style.whiteSpace = WhiteSpace.Normal;

        var col = new VisualElement { style = { flexGrow = 1f, flexBasis = 0f, marginRight = 10f } };
        col.Add(preview);
        col.Add(label);
        return col;
    }

    private static void SetPadding(VisualElement e, float p)
    {
        e.style.paddingTop = p;
        e.style.paddingBottom = p;
        e.style.paddingLeft = p;
        e.style.paddingRight = p;
    }

    private static void StyleBox(VisualElement e)
    {
        e.style.height = BoxH;
        // 半透明棋盘灰底，便于看清白色像素与透明区域。
        e.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        var c = new Color(0f, 0f, 0f, 0.4f);
        e.style.borderTopWidth = 1f;
        e.style.borderBottomWidth = 1f;
        e.style.borderLeftWidth = 1f;
        e.style.borderRightWidth = 1f;
        e.style.borderTopColor = c;
        e.style.borderBottomColor = c;
        e.style.borderLeftColor = c;
        e.style.borderRightColor = c;
    }
}
