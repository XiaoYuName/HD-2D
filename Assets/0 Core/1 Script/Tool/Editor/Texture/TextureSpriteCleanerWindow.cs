using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UIImage = UnityEngine.UI.Image;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine.UIElements;

/// <summary>清理 Project 文件夹或 Hierarchy 选中层级中的 Sprite 资源，并反查哪些资源引用了它们。</summary>
public sealed class TextureSpriteCleanerWindow : EditorWindow
{
    private enum ScanSource
    {
        FolderSprites,   // 文件夹（含子文件夹）内的 Sprite/贴图资源
        FolderPrefabs,   // 文件夹内 Prefab 上 Image.Sprite 用到的资源
        Hierarchy        // Hierarchy 选中对象及其子节点的 Image.Sprite
    }

    // 反查引用时会扫描这些类型的资源
    private static readonly string[] ReferenceFilters = { "t:Prefab", "t:SceneAsset", "t:ScriptableObject", "t:Material", "t:AnimationClip", "t:SpriteAtlas" };

    private readonly List<string> spritePaths = new();
    private readonly HashSet<string> spritePathSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> prefabReferences = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> targetFolders = new();
    private ScanSource source = ScanSource.FolderSprites;
    private bool includeSubFolders = true;
    private bool spriteOnly = true;
    private bool autoScanReferences = true;
    private VisualElement folderList;
    private VisualElement results;
    private Label summary;
    private ProgressBar scanProgress;
    private Button cancelScanButton;
    private string[] referenceCandidates;
    private int scanIndex;
    private bool pendingDelete;
    private bool referencesScanned;

    [MenuItem(EditorMenuSet.Texture2D + "/Sprite Cleaner")]
    private static void Open() => TextureToolsHomeWindow.OpenTool("sprite-cleaner");

    public void BuildEmbedded(VisualElement host)
    {
        host.Clear();
        TextureToolsTheme.Apply(host);
        host.style.flexGrow = 1f;
        host.style.paddingLeft = 22f;
        host.style.paddingRight = 22f;
        host.style.paddingTop = 18f;
        host.style.paddingBottom = 18f;
        host.Add(new Label("Sprite 清理") { style = { fontSize = 22f, unityFontStyleAndWeight = FontStyle.Bold } });
        host.Add(new Label("拖入或选中 Project 文件夹后可递归扫描其中的 Sprite，并反查 Prefab / 场景 / 材质 / 动画 等引用。") { style = { marginTop = 4f, color = new Color(1f, 1f, 1f, 0.5f), whiteSpace = WhiteSpace.Normal } });

        host.Add(BuildFolderPicker());

        EnumField sourceField = new("扫描来源", source) { style = { marginTop = 10f } };
        sourceField.RegisterValueChangedCallback(evt => { source = (ScanSource)evt.newValue; RefreshOptionState(); });
        host.Add(sourceField);

        VisualElement options = new() { style = { flexDirection = FlexDirection.Row, marginTop = 6f, flexWrap = Wrap.Wrap } };
        options.Add(CreateToggle("含子文件夹", includeSubFolders, value => includeSubFolders = value));
        options.Add(CreateToggle("仅 Sprite 类型", spriteOnly, value => spriteOnly = value));
        options.Add(CreateToggle("扫描后自动统计引用", autoScanReferences, value => autoScanReferences = value));
        host.Add(options);

        VisualElement actions = new() { style = { flexDirection = FlexDirection.Row, marginTop = 12f } };
        actions.Add(CreateButton("扫描资源", () => ScanSelection(true)));
        actions.Add(CreateButton("统计引用", ScanPrefabReferences));
        actions.Add(CreateButton("删除这些图片文件", DeleteSpriteFiles));
        cancelScanButton = CreateButton("取消扫描", CancelScan);
        cancelScanButton.SetEnabled(false);
        actions.Add(cancelScanButton);
        host.Add(actions);

        scanProgress = new ProgressBar { title = "", style = { marginTop = 8f } };
        scanProgress.style.display = DisplayStyle.None;
        host.Add(scanProgress);
        summary = new Label("尚未扫描。请拖入 Project 文件夹，或在 Project / Hierarchy 中选中后点击「使用 Project 选中」。") { style = { marginTop = 12f, color = new Color(1f, 1f, 1f, 0.7f) } };
        host.Add(summary);
        results = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1f, marginTop = 8f } };
        host.Add(results);
        RefreshFolderList();
    }

    private VisualElement BuildFolderPicker()
    {
        VisualElement box = new() { style = { marginTop = 12f, paddingLeft = 8f, paddingRight = 8f, paddingTop = 6f, paddingBottom = 6f } };
        box.AddToClassList("texture-tools-card");

        VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        header.Add(new Label("目标文件夹（可拖拽多个）") { style = { flexGrow = 1f, fontSize = 11f, color = new Color(1f, 1f, 1f, 0.7f) } });
        header.Add(CreateButton("使用 Project 选中", AddSelectedFolders));
        header.Add(CreateButton("清空", () => { targetFolders.Clear(); RefreshFolderList(); }));
        box.Add(header);

        folderList = new VisualElement { style = { marginTop = 4f, minHeight = 26f } };
        box.Add(folderList);

        // 整个卡片作为文件夹拖放区
        box.RegisterCallback<DragUpdatedEvent>(_ => DragAndDrop.visualMode = HasDraggedFolder() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected);
        box.RegisterCallback<DragPerformEvent>(_ =>
        {
            foreach (string path in DragAndDrop.paths) AddFolder(path);
            DragAndDrop.AcceptDrag();
            RefreshFolderList();
        });
        return box;
    }

    private static bool HasDraggedFolder()
    {
        foreach (string path in DragAndDrop.paths)
            if (AssetDatabase.IsValidFolder(path)) return true;
        return false;
    }

    private void AddFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!AssetDatabase.IsValidFolder(path)) path = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;
        foreach (string existing in targetFolders)
            if (string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) return;
        targetFolders.Add(path);
    }

    private void AddSelectedFolders()
    {
        foreach (UnityEngine.Object selected in Selection.objects) AddFolder(AssetDatabase.GetAssetPath(selected));
        RefreshFolderList();
        if (targetFolders.Count == 0) summary.text = "Project 中没有选中文件夹（Hierarchy 选中请把扫描来源切到 Hierarchy）。";
    }

    private void RefreshFolderList()
    {
        if (folderList == null) return;
        folderList.Clear();
        if (targetFolders.Count == 0)
        {
            folderList.Add(new Label("（未指定，Hierarchy 模式下无需指定）") { style = { fontSize = 10f, color = new Color(1f, 1f, 1f, 0.35f) } });
            return;
        }
        foreach (string folder in new List<string>(targetFolders))
        {
            string captured = folder;
            VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label(captured) { style = { flexGrow = 1f, fontSize = 10f } });
            Button remove = new(() => { targetFolders.Remove(captured); RefreshFolderList(); }) { text = "×" };
            remove.style.width = 20f;
            remove.style.height = 18f;
            row.Add(remove);
            folderList.Add(row);
        }
    }

    private void RefreshOptionState()
    {
        if (summary != null && source != ScanSource.Hierarchy && targetFolders.Count == 0)
            summary.text = "请先指定目标文件夹。";
    }

    private static Toggle CreateToggle(string text, bool value, Action<bool> onChange)
    {
        Toggle toggle = new(text) { value = value, style = { marginRight = 14f } };
        toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
        return toggle;
    }

    private static Button CreateButton(string text, Action action)
    {
        Button button = new(action) { text = text };
        button.AddToClassList("texture-tools-button");
        button.style.marginRight = 8f;
        button.style.height = 28f;
        return button;
    }

    private void ScanSelection(bool userTriggered)
    {
        CancelScan();
        spritePaths.Clear();
        spritePathSet.Clear();
        prefabReferences.Clear();
        referencesScanned = false;

        if (source == ScanSource.Hierarchy)
            CollectFromHierarchy();
        else
        {
            if (targetFolders.Count == 0) AddSelectedFolders();
            if (targetFolders.Count == 0)
            {
                summary.text = "没有目标文件夹：请拖入文件夹，或在 Project 中选中后点「使用 Project 选中」。";
                results.Clear();
                return;
            }
            string[] folders = targetFolders.ToArray();
            if (source == ScanSource.FolderSprites) CollectFolderSprites(folders);
            else CollectFolderPrefabSprites(folders);
        }

        RefreshResults();
        if (userTriggered && autoScanReferences && spritePaths.Count > 0) ScanPrefabReferences();
    }

    private void CollectFromHierarchy()
    {
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is not GameObject gameObject) continue;
            foreach (UIImage image in gameObject.GetComponentsInChildren<UIImage>(true)) AddSprite(image.sprite);
        }
        if (spritePaths.Count == 0) summary.text = "Hierarchy 选中对象中没有带 Sprite 的 Image。";
    }

    private void CollectFolderSprites(string[] folders)
    {
        string filter = spriteOnly ? "t:Sprite" : "t:Texture2D";
        foreach (string guid in AssetDatabase.FindAssets(filter, folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsInTargetFolders(path)) continue;
            AddPath(path);
        }
    }

    private void CollectFolderPrefabSprites(string[] folders)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsInTargetFolders(prefabPath)) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;
            foreach (UIImage image in prefab.GetComponentsInChildren<UIImage>(true)) AddSprite(image.sprite);
        }
    }

    /// <summary>includeSubFolders 关闭时只保留直接位于目标文件夹下的资源。</summary>
    private bool IsInTargetFolders(string assetPath)
    {
        if (includeSubFolders) return true;
        string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        foreach (string folder in targetFolders)
            if (string.Equals(directory, folder.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void AddSprite(Sprite sprite)
    {
        if (sprite != null) AddPath(AssetDatabase.GetAssetPath(sprite));
    }

    private void AddPath(string path)
    {
        if (!string.IsNullOrEmpty(path) && spritePathSet.Add(path)) spritePaths.Add(path);
    }

    private void ScanPrefabReferences()
    {
        if (spritePaths.Count == 0) { ScanSelection(false); if (spritePaths.Count == 0) return; }
        CancelScan();
        prefabReferences.Clear();
        referencesScanned = false;
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
        foreach (string filter in ReferenceFilters)
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !spritePathSet.Contains(path)) candidates.Add(path);
            }
        referenceCandidates = new string[candidates.Count];
        candidates.CopyTo(referenceCandidates);
        scanIndex = 0;
        StartScanProgress();
    }

    private void StartScanProgress()
    {
        scanProgress.style.display = DisplayStyle.Flex;
        scanProgress.lowValue = 0f;
        scanProgress.highValue = referenceCandidates.Length;
        scanProgress.value = 0f;
        scanProgress.title = $"反查引用（0/{referenceCandidates.Length}）";
        cancelScanButton.SetEnabled(true);
        EditorApplication.update += ScanNextBatch;
    }

    /// <summary>每帧按时间片处理若干候选资源，避免大工程卡死。</summary>
    private void ScanNextBatch()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (scanIndex < referenceCandidates.Length && stopwatch.ElapsedMilliseconds < 25L)
        {
            string assetPath = referenceCandidates[scanIndex++];
            foreach (string dependency in AssetDatabase.GetDependencies(assetPath, true))
            {
                if (!spritePathSet.Contains(dependency)) continue;
                if (!prefabReferences.TryGetValue(dependency, out List<string> references)) prefabReferences[dependency] = references = new List<string>();
                references.Add(assetPath);
            }
        }
        scanProgress.value = scanIndex;
        scanProgress.title = $"反查引用（{scanIndex}/{referenceCandidates.Length}）";
        if (scanIndex >= referenceCandidates.Length) FinishScan(false);
    }

    private void CancelScan()
    {
        if (referenceCandidates == null) return;
        FinishScan(true);
    }

    private void FinishScan(bool cancelled)
    {
        EditorApplication.update -= ScanNextBatch;
        referenceCandidates = null;
        scanProgress.style.display = DisplayStyle.None;
        cancelScanButton.SetEnabled(false);
        referencesScanned = !cancelled;
        RefreshResults();
        int referenced = prefabReferences.Count;
        summary.text = (cancelled ? "扫描已取消（保留已扫描结果）· " : "引用反查完成 · ") + $"{spritePaths.Count} 个资源，其中 {referenced} 个被引用，{spritePaths.Count - referenced} 个未被引用";
        if (pendingDelete)
        {
            if (cancelled) pendingDelete = false;
            else ConfirmDelete();
        }
    }

    private void DeleteSpriteFiles()
    {
        if (spritePaths.Count == 0) ScanSelection(false);
        if (spritePaths.Count == 0) { EditorUtility.DisplayDialog("没有可删除的资源", "未找到 Sprite 资源。", "确定"); return; }
        pendingDelete = true;
        ScanPrefabReferences();
    }

    private void ConfirmDelete()
    {
        pendingDelete = false;
        int references = 0;
        foreach (List<string> paths in prefabReferences.Values) references += paths.Count;
        string warning = $"将永久删除 {spritePaths.Count} 个资源文件。\n" + (references > 0 ? $"检测到 {references} 处引用，删除后这些引用会丢失。\n" : "未检测到引用。\n") + "确定继续吗？";
        if (!EditorUtility.DisplayDialog("删除 Sprite 文件", warning, "永久删除", "取消")) return;
        int deleted = 0;
        foreach (string path in spritePaths) if (File.Exists(Path.GetFullPath(path)) && AssetDatabase.DeleteAsset(path)) deleted++;
        AssetDatabase.Refresh();
        spritePaths.Clear(); spritePathSet.Clear(); prefabReferences.Clear(); RefreshResults();
        Debug.Log($"[TextureSpriteCleaner] 已删除 {deleted} 个 Sprite 资源文件。", this);
    }

    /// <summary>把单个资源连同 .meta 移到系统回收站，不做二次确认。</summary>
    private void DeleteSingle(string path)
    {
        if (!AssetDatabase.MoveAssetToTrash(path))
        {
            Debug.LogWarning($"[TextureSpriteCleaner] 移到回收站失败：{path}", this);
            return;
        }
        spritePaths.Remove(path);
        spritePathSet.Remove(path);
        prefabReferences.Remove(path);
        AssetDatabase.Refresh();
        RefreshResults();
        int referenced = prefabReferences.Count;
        summary.text = $"已移到回收站：{Path.GetFileName(path)} · 剩余 {spritePaths.Count} 个资源" + (referencesScanned ? $"，其中 {referenced} 个被引用，{spritePaths.Count - referenced} 个未被引用" : string.Empty);
        Debug.Log($"[TextureSpriteCleaner] 已移到回收站：{path}", this);
    }

    private void RefreshResults()
    {
        if (summary == null) return;
        results.Clear();
        if (spritePaths.Count == 0) { summary.text = "未找到 Sprite 资源。"; return; }
        summary.text = $"找到 {spritePaths.Count} 个资源文件";
        VisualElement grid = new();
        grid.AddToClassList("texture-tools-grid");
        foreach (string path in spritePaths) grid.Add(BuildCard(path));
        results.Add(grid);
    }

    private VisualElement BuildCard(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Texture2D texture = sprite != null ? sprite.texture : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        VisualElement card = new() { style = { width = 168f, marginRight = 8f, marginBottom = 8f, paddingLeft = 6f, paddingRight = 6f, paddingTop = 6f, paddingBottom = 6f, overflow = Overflow.Hidden } };
        card.AddToClassList("texture-tools-card");
        VisualElement preview = new() { style = { width = 154f, height = 95f, backgroundImage = texture == null ? StyleKeyword.None : new StyleBackground(texture) } };
        preview.AddToClassList("texture-tools-preview");
        card.Add(preview);
        Label name = new(Path.GetFileName(path)) { tooltip = path, style = { fontSize = 10f, marginTop = 4f, whiteSpace = WhiteSpace.Normal } };
        card.Add(name);

        List<string> found = prefabReferences.TryGetValue(path, out List<string> value) ? value : null;

        VisualElement row = new() { style = { flexDirection = FlexDirection.Row, marginTop = 2f } };
        Button locate = new(() => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path))) { text = "定位" };
        locate.style.height = 18f;
        locate.style.fontSize = 9f;
        locate.style.flexGrow = 1f;
        locate.style.marginLeft = 0f;
        row.Add(locate);
        Button deleteOne = new(() => DeleteSingle(path)) { text = "删除", tooltip = $"连同 .meta 移到回收站：{path}" };
        deleteOne.style.height = 18f;
        deleteOne.style.fontSize = 9f;
        deleteOne.style.flexGrow = 1f;
        deleteOne.style.marginRight = 0f;
        deleteOne.style.color = found == null && referencesScanned ? new Color(1f, 0.55f, 0.5f) : new Color(1f, 1f, 1f, 0.8f);
        row.Add(deleteOne);
        card.Add(row);

        string text = found != null ? $"引用：{found.Count}" : referencesScanned ? "无引用（可安全删除）" : "未统计引用";
        Color color = found != null ? new Color(1f, 0.72f, 0.35f) : referencesScanned ? new Color(0.5f, 0.85f, 0.55f) : new Color(1f, 1f, 1f, 0.5f);
        card.Add(new Label(text) { style = { fontSize = 9f, marginTop = 3f, color = color } });

        if (found == null) return card;
        int shown = Mathf.Min(found.Count, 6);
        for (int i = 0; i < shown; i++)
        {
            string referencePath = found[i];
            Button ping = new(() => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(referencePath)))
            {
                text = Path.GetFileNameWithoutExtension(referencePath),
                tooltip = referencePath
            };
            ping.style.height = 18f;
            ping.style.fontSize = 9f;
            ping.style.marginLeft = 0f;
            ping.style.marginRight = 0f;
            ping.style.overflow = Overflow.Hidden;
            ping.style.textOverflow = TextOverflow.Ellipsis;
            card.Add(ping);
        }
        if (found.Count > shown)
        {
            Button more = new(() => Debug.Log($"[TextureSpriteCleaner] {path} 的全部引用：\n{string.Join("\n", found)}")) { text = $"+{found.Count - shown} 更多（输出到 Console）" };
            more.style.height = 18f;
            more.style.fontSize = 9f;
            card.Add(more);
        }
        return card;
    }

    private void OnDisable() => CancelScan();
}
