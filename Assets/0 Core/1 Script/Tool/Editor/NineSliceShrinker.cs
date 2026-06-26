using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 把“边框 + 大片纯色填充”这类没有压缩到最小的 UI 背景图，
/// 自动收缩成可九宫格(9-slice)拉伸的最小贴图，并写入 SpriteBorder。
///
/// 原理：
///   逐列 / 逐行比较，找出最长的一段“相邻且相同(容差内)的列 / 行”——
///   这段就是可以被拉伸的区域(九宫格的中缝)。把它压成 centerKeep 像素，
///   两侧保留下来的就是九宫格的固定边(border)。每个输出像素都直接取自
///   原图对应的九宫格位置，所以四角、四边的样子完全保留。
///
/// 用法：
///   1. 菜单 Tools/2D/9-Slice Shrinker 打开窗口，先“预览”看结果，再“收缩”。
///   2. 或在 Project 选中 PNG，菜单 Tools/2D/Shrink 9-Slice (Auto) 用默认参数直接处理。
///
/// 注意：默认就地覆盖原 PNG（请确保已提交 git，以便回退）。
///       处理后还需把使用处的 Image 组件 Image Type 设为 Sliced，才会真正按九宫格绘制。
/// </summary>
public static class NineSliceShrinker
{
    public struct Options
    {
        public int tolerance;    // 每通道允许的最大差值，0 = 完全相同；图有抗锯齿 / 噪点时调大
        public int centerKeep;   // 中缝(可拉伸区)每轴保留的像素数，默认 1
        public bool writeBorder; // 处理后是否自动把检测到的 9-slice 边框写进导入设置
        public bool inPlace;     // true = 覆盖原文件；false = 输出到 *_9s.png

        public static Options Default => new ()
        {
            tolerance = 0,
            centerKeep = 1,
            writeBorder = true,
            inPlace = true,
        };
    }

    public struct Analysis
    {
        public bool ok;           // 是否成功解码并分析
        public string message;    // 失败 / 跳过原因或提示
        public int oldW, oldH, newW, newH;
        public Vector4 border;    // x=left y=bottom z=right w=top
        public bool shrank;       // 是否真的发生了收缩
        public Texture2D preview; // 内存中的收缩结果（调用方负责 DestroyImmediate）；无收缩为 null
        public string outputPath; // 写盘目标路径

        public float SavedPercent =>
            oldW * oldH == 0 ? 0f : 100f * (1f - (float)(newW * newH) / (oldW * oldH));
    }

    // ---------------------------------------------------------------- menu ----

    private const string ShrinkMenu = EditorMenuSet.Texture2D + "/Shrink 9-Slice (Auto)";

    [MenuItem(ShrinkMenu, true)]
    private static bool ShrinkSelectedValidate() => CollectTexturePaths().Count > 0;

    [MenuItem(ShrinkMenu)]
    private static void ShrinkSelectedMenu() => ShrinkSelection(Options.Default);

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

    /// <summary>收缩当前 Selection 中的所有贴图（写盘 + 写入九宫格 border）。</summary>
    public static void ShrinkSelection(Options options)
    {
        var paths = CollectTexturePaths();
        if (paths.Count == 0)
        {
            Debug.LogWarning("[NineSliceShrinker] 未选中任何贴图。");
            return;
        }

        var bordersToWrite = new List<KeyValuePair<string, Vector4>>();
        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Count; i++)
            {
                string name = Path.GetFileName(paths[i]);
                EditorUtility.DisplayProgressBar("9-Slice Shrinker", name, (float)i / paths.Count);

                Analysis a = Process(paths[i], options);
                if (!a.ok)
                {
                    Debug.LogWarning($"[NineSliceShrinker] 跳过 {name}：{a.message}");
                    continue;
                }
                if (!a.shrank)
                {
                    Debug.Log($"[NineSliceShrinker] {name}：{a.message}");
                    continue;
                }

                changed++;
                Debug.Log($"[NineSliceShrinker] {name}: {a.oldW}x{a.oldH} → {a.newW}x{a.newH}  " +
                          $"border(L{(int)a.border.x} B{(int)a.border.y} R{(int)a.border.z} T{(int)a.border.w})" +
                          (options.inPlace ? "" : $"  -> {a.outputPath}"));

                if (options.writeBorder)
                    bordersToWrite.Add(new KeyValuePair<string, Vector4>(a.outputPath, a.border));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // 第二遍：此时新的 PNG 内容已重新导入，再写入九宫格 border 并重导。
        foreach (var kv in bordersToWrite)
            ApplySpriteBorder(kv.Key, kv.Value);

        if (bordersToWrite.Count > 0) AssetDatabase.Refresh();
        Debug.Log($"[NineSliceShrinker] 完成：{changed}/{paths.Count} 张已收缩。");
    }

    /// <summary>
    /// 分析单张贴图，并在内存中生成收缩结果，不写磁盘。
    /// 用于面板预览；调用方用完后需 Object.DestroyImmediate(result.preview)。
    /// </summary>
    public static Analysis Analyze(string assetPath, Options options)
    {
        options.centerKeep = Mathf.Max(1, options.centerKeep);
        options.tolerance = Mathf.Max(0, options.tolerance);

        var a = new Analysis { outputPath = options.inPlace ? assetPath : NewPath(assetPath) };

        if (Path.GetExtension(assetPath).ToLowerInvariant() != ".png")
        {
            a.message = "非 PNG 文件";
            return a;
        }

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!src.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath))))
            {
                a.message = "无法解码";
                return a;
            }

            int w = src.width, h = src.height;
            Color32[] px = src.GetPixels32(); // 行优先，原点左下

            // 找最长的“相同列”与“相同行”游程（锚点比较，保证整段都在容差内）。
            FindRun(px, w, h, true, options.tolerance, out int colStart, out int colEnd);
            FindRun(px, w, h, false, options.tolerance, out int rowStart, out int rowEnd);

            // 保留索引：游程压成 centerKeep 个代表，其余原样保留。
            List<int> keepCols = BuildKeep(w, colStart, colEnd, options.centerKeep);
            List<int> keepRows = BuildKeep(h, rowStart, rowEnd, options.centerKeep);
            int newW = keepCols.Count, newH = keepRows.Count;

            // 只有该轴真正发生了收缩，才把游程两侧记为九宫格固定边。
            bool shrankX = colEnd - colStart + 1 > options.centerKeep;
            bool shrankY = rowEnd - rowStart + 1 > options.centerKeep;

            a.ok = true;
            a.oldW = w; a.oldH = h;
            a.newW = newW; a.newH = newH;
            a.border = new Vector4(
                shrankX ? colStart : 0,
                shrankY ? rowStart : 0,
                shrankX ? (w - 1 - colEnd) : 0,
                shrankY ? (h - 1 - rowEnd) : 0);
            a.shrank = newW != w || newH != h;

            if (!a.shrank)
            {
                a.message = "无可收缩的纯色区域，保持原样";
                a.outputPath = assetPath;
                return a;
            }

            a.preview = Sample(px, w, keepCols, keepRows);
            return a;
        }
        finally
        {
            Object.DestroyImmediate(src);
        }
    }

    /// <summary>分析并收缩单张贴图（写盘）。返回的 Analysis.preview 已释放为 null。</summary>
    public static Analysis Process(string assetPath, Options options)
    {
        Analysis a = Analyze(assetPath, options);
        if (!a.ok || !a.shrank) return a; // preview 本就为 null

        try
        {
            File.WriteAllBytes(Path.GetFullPath(a.outputPath), a.preview.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(a.preview);
            a.preview = null;
        }
        return a;
    }

    /// <summary>按保留索引采样出收缩后的贴图（每个像素直接取自原图对应的九宫格位置）。</summary>
    private static Texture2D Sample(Color32[] px, int w, List<int> keepCols, List<int> keepRows)
    {
        int newW = keepCols.Count, newH = keepRows.Count;
        var dst = new Color32[newW * newH];
        for (int y = 0; y < newH; y++)
        {
            int srcRow = keepRows[y] * w;
            int dstRow = y * newW;
            for (int x = 0; x < newW; x++)
                dst[dstRow + x] = px[srcRow + keepCols[x]];
        }

        var tex = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
        tex.SetPixels32(dst);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------- algorithm ----

    /// <summary>
    /// 找出最长的一段“互相相同”的列(columns=true)或行(columns=false)。
    /// 用锚点比较：段内每一列 / 行都与该段第一列 / 行比较，避免容差下的渐变误判。
    /// </summary>
    private static void FindRun(Color32[] px, int w, int h, bool columns, int tol,
        out int bestStart, out int bestEnd)
    {
        int n = columns ? w : h;
        bestStart = 0; bestEnd = 0;
        int anchor = 0, curStart = 0;

        for (int i = 1; i < n; i++)
        {
            bool same = columns
                ? ColumnsEqual(px, w, h, i, anchor, tol)
                : RowsEqual(px, w, i, anchor, tol);

            if (same)
            {
                if (i - curStart > bestEnd - bestStart) { bestStart = curStart; bestEnd = i; }
            }
            else
            {
                anchor = i; curStart = i;
            }
        }
    }

    /// <summary>构造要保留的索引列表：游程 [start..end] 只保留 keep 个代表，其余原样。</summary>
    private static List<int> BuildKeep(int n, int start, int end, int keep)
    {
        var list = new List<int>(n);
        if (end - start + 1 <= keep) // 不值得收缩
        {
            for (int i = 0; i < n; i++) list.Add(i);
            return list;
        }

        for (int i = 0; i < start; i++) list.Add(i);        // 左 / 下 固定区
        for (int k = 0; k < keep; k++) list.Add(start + k); // 中缝代表(游程内皆相同)
        for (int i = end + 1; i < n; i++) list.Add(i);      // 右 / 上 固定区
        return list;
    }

    private static bool ColumnsEqual(Color32[] px, int w, int h, int x1, int x2, int tol)
    {
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            if (!Near(px[row + x1], px[row + x2], tol)) return false;
        }
        return true;
    }

    private static bool RowsEqual(Color32[] px, int w, int y1, int y2, int tol)
    {
        int r1 = y1 * w, r2 = y2 * w;
        for (int x = 0; x < w; x++)
            if (!Near(px[r1 + x], px[r2 + x], tol)) return false;
        return true;
    }

    private static bool Near(Color32 a, Color32 b, int tol)
    {
        return Mathf.Abs(a.r - b.r) <= tol
            && Mathf.Abs(a.g - b.g) <= tol
            && Mathf.Abs(a.b - b.b) <= tol
            && Mathf.Abs(a.a - b.a) <= tol;
    }

    // -------------------------------------------------------------- import ----

    private static void ApplySpriteBorder(string assetPath, Vector4 border)
    {
        if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter ti))
        {
            Debug.LogWarning($"[NineSliceShrinker] 不是贴图导入器，未写入 border：{assetPath}");
            return;
        }

        if (ti.textureType != TextureImporterType.Sprite)
        {
            Debug.Log($"[NineSliceShrinker] {Path.GetFileName(assetPath)} 不是 Sprite 类型，已跳过写 border。");
            return;
        }

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteBorder = border; // 仅单图模式生效，保持原有 spriteMode 不动
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    private static string NewPath(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(assetPath);
        return $"{dir}/{name}_9s.png";
    }
}

/// <summary>九宫格收缩窗口（UIToolkit）：在面板内实时预览收缩结果与九宫格还原效果。</summary>
public class NineSliceShrinkerWindow : EditorWindow
{
    private const float BoxH = 150f;

    private SliderInt tolerance;
    private IntegerField centerKeep;
    private Toggle writeBorder;
    private Toggle inPlace;

    private Label summary;        // 已选数量 / 当前预览的文件名
    private Label stats;          // 尺寸、节省、border 数值
    private Image origImage;      // 原图（拉伸填充）
    private VisualElement resultBox;   // 修改后：有 border 走九宫格还原，否则用 resultFallback 铺满
    private Image resultFallback;      // 修改后的回退显示（无九宫格时拉伸铺满，避免空白）
    private Button shrinkBtn;

    private Texture2D previewTex; // 内存预览贴图，需手动释放

    [MenuItem(EditorMenuSet.Texture2D + "/9-Slice Shrinker")]
    private static void Open()
    {
        GetWindow<NineSliceShrinkerWindow>("9-Slice Shrinker").minSize = new Vector2(460, 600);
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        SetPadding(root, 10f);

        var body = new ScrollView();
        root.Add(body);

        body.Add(new HelpBox(
            "把“边框 + 大片纯色”的图收缩成可九宫格拉伸的最小贴图，并写入 SpriteBorder。\n" +
            "默认就地覆盖原 PNG —— 请确保已提交 git，便于回退。", HelpBoxMessageType.Info));

        // ---- 参数（改变即实时刷新预览）----
        tolerance = new SliderInt("容差", 0, 32)
        {
            value = 0,
            showInputField = true,
            tooltip = "每通道允许的最大差值；图有抗锯齿 / 噪点时调大，纯色描边用 0",
        };
        centerKeep = new IntegerField("中缝保留(px)")
        {
            value = 1,
            tooltip = "可拉伸区域保留的像素数，通常 1 即可",
        };
        writeBorder = new Toggle("写入九宫格 Border")
        {
            value = true,
            tooltip = "处理后自动设置 Sprite 的 9-slice 边框",
        };
        inPlace = new Toggle("覆盖原文件")
        {
            value = true,
            tooltip = "关闭则输出到 *_9s.png（会断开原有引用）",
        };

        tolerance.RegisterValueChangedCallback(_ => Refresh());
        centerKeep.RegisterValueChangedCallback(e =>
        {
            if (e.newValue < 1) centerKeep.SetValueWithoutNotify(1);
            Refresh();
        });

        body.Add(tolerance);
        body.Add(centerKeep);
        body.Add(writeBorder);
        body.Add(inPlace);

        // ---- 实时预览 ----
        body.Add(Divider());
        var head = new Label("分析预览");
        head.style.unityFontStyleAndWeight = FontStyle.Bold;
        head.style.marginBottom = 2f;
        body.Add(head);

        summary = NormalLabel();
        stats = NormalLabel();
        stats.style.marginTop = 2f;
        stats.style.marginBottom = 8f;
        body.Add(summary);
        body.Add(stats);

        origImage = new Image { scaleMode = ScaleMode.StretchToFill };

        resultBox = new VisualElement();
        resultFallback = new Image { scaleMode = ScaleMode.StretchToFill };
        resultFallback.style.position = Position.Absolute;
        resultFallback.style.left = 0f;
        resultFallback.style.right = 0f;
        resultFallback.style.top = 0f;
        resultFallback.style.bottom = 0f;
        resultBox.Add(resultFallback);

        var compare = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        compare.Add(LabeledColumn("原图", origImage));
        compare.Add(LabeledColumn("修改后（九宫格还原）", resultBox));
        body.Add(compare);

        // ---- 操作 ----
        shrinkBtn = new Button(Apply) { style = { height = 32f, marginTop = 12f } };
        body.Add(shrinkBtn);

        body.Add(new HelpBox(
            "提示：处理后，把使用该图的 Image 组件 Image Type 设为 Sliced，才会按九宫格绘制。",
            HelpBoxMessageType.None));

        Selection.selectionChanged -= Refresh;
        Selection.selectionChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Refresh;
        ReleasePreview();
    }

    private NineSliceShrinker.Options CurrentOptions() => new()
    {
        tolerance = tolerance.value,
        centerKeep = Mathf.Max(1, centerKeep.value),
        writeBorder = writeBorder.value,
        inPlace = inPlace.value,
    };

    private void Apply()
    {
        NineSliceShrinker.ShrinkSelection(CurrentOptions());
        Refresh(); // 资源已变，刷新预览
    }

    /// <summary>重新分析当前选中的第一张贴图并刷新整个预览区。</summary>
    private void Refresh()
    {
        ReleasePreview();

        Texture2D primary = PrimaryTexture(out int count);
        shrinkBtn.SetEnabled(count > 0);
        shrinkBtn.text = count > 0 ? $"收缩 {count} 张贴图" : "收缩（未选中贴图）";

        if (primary == null)
        {
            summary.text = "请先在 Project 窗口选中至少一张 PNG。";
            stats.text = "";
            origImage.image = null;
            ShowResult(null, Vector4.zero);
            return;
        }

        string path = AssetDatabase.GetAssetPath(primary);
        summary.text = count > 1
            ? $"已选 {count} 张 · 预览第 1 张：{Path.GetFileName(path)}"
            : Path.GetFileName(path);
        origImage.image = primary;

        NineSliceShrinker.Analysis a = NineSliceShrinker.Analyze(path, CurrentOptions());

        if (!a.ok)
        {
            stats.text = $"无法分析：{a.message}";
            ShowResult(primary, GetImporterBorder(path)); // 仍显示原图，不留空白
            return;
        }
        if (!a.shrank)
        {
            stats.text = $"{a.oldW} × {a.oldH}　已是最小，无需收缩";
            ShowResult(primary, GetImporterBorder(path)); // 用当前导入设置里的 border 还原
            return;
        }

        previewTex = a.preview;
        previewTex.filterMode = FilterMode.Point;

        stats.text =
            $"{a.oldW} × {a.oldH}　→　{a.newW} × {a.newH}　（面积 -{a.SavedPercent:0.##}%）\n" +
            $"九宫格 Border：左 {(int)a.border.x}　下 {(int)a.border.y}　右 {(int)a.border.z}　上 {(int)a.border.w}";

        ShowResult(previewTex, a.border);
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

    /// <summary>在“修改后”框里显示一张贴图：有 border 走九宫格还原，否则铺满拉伸（永不空白）。</summary>
    private void ShowResult(Texture2D tex, Vector4 border)
    {
        bool sliced = tex != null &&
            (border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f);

        if (sliced)
        {
            resultFallback.style.display = DisplayStyle.None;
            resultBox.style.backgroundImage = tex;
            resultBox.style.unitySliceLeft = (int)border.x;
            resultBox.style.unitySliceBottom = (int)border.y;
            resultBox.style.unitySliceRight = (int)border.z;
            resultBox.style.unitySliceTop = (int)border.w;
        }
        else
        {
            resultBox.style.backgroundImage = StyleKeyword.None;
            resultFallback.image = tex;
            resultFallback.style.display = tex != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    /// <summary>读取贴图当前导入设置里的九宫格 border（无则为 0）。</summary>
    private static Vector4 GetImporterBorder(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti)
        {
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            return s.spriteBorder;
        }
        return Vector4.zero;
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

    private static VisualElement Divider() => new VisualElement
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

        // 两列等分窗口宽度，并随窗口缩放（更方便查看）。
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
