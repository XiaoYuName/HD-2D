#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>创建或补全可直接运行的刺绣棋盘预制体。</summary>
internal static class DressMakingEmbroideryLevelPrefabBuilder
{
    internal static GameObject CreateOrUpdate(DressMakingEmbroideryLevelData level, string path)
    {
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        GameObject root = exists
            ? PrefabUtility.LoadPrefabContents(path)
            : new GameObject($"EmbroideryLevel_{level.clothingId}", typeof(RectTransform));

        try
        {
            Configure(root, level.canvasSize);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
    }

    static void Configure(GameObject root, Vector2 canvasSize)
    {
        foreach (Transform tf in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tf.gameObject);
        RectTransform rootRtf = GetRtf(root);
        rootRtf.sizeDelta = canvasSize;
        rootRtf.localScale = Vector3.one;

        DressMakingEmbroiderySimulationGameBoard board =
            GetOrAdd<DressMakingEmbroiderySimulationGameBoard>(root);

        RectTransform gridRtf = GetChildRtf(rootRtf, "WavyGrid");
        Stretch(gridRtf);
        GetOrAdd<CanvasRenderer>(gridRtf.gameObject);
        DressMakingEmbroideryWavyGridGraphic grid =
            GetOrAdd<DressMakingEmbroideryWavyGridGraphic>(gridRtf.gameObject);
        grid.raycastTarget = false;
        gridRtf.SetSiblingIndex(0);

        RectTransform regionsRtf = GetChildRtf(rootRtf, "Regions");
        Stretch(regionsRtf);
        regionsRtf.SetSiblingIndex(1);

        RectTransform templateRtf = GetChildRtf(regionsRtf, "RegionTemplate");
        Stretch(templateRtf);
        DressMakingEmbroideryRegionView template =
            GetOrAdd<DressMakingEmbroideryRegionView>(templateRtf.gameObject);

        DressMakingEmbroideryPolygonGraphic baseGraphic = GetPolygonGraphic(templateRtf, "Base");
        DressMakingEmbroideryPolygonGraphic fillGraphic = GetPolygonGraphic(templateRtf, "Fill");

        RectTransform labelRtf = GetChildRtf(templateRtf, "Label");
        labelRtf.anchorMin = labelRtf.anchorMax = labelRtf.pivot = new Vector2(0.5f, 0.5f);
        labelRtf.anchoredPosition = Vector2.zero;
        labelRtf.sizeDelta = new Vector2(180f, 100f);
        GetOrAdd<CanvasRenderer>(labelRtf.gameObject);
        TextMeshProUGUI label = GetOrAdd<TextMeshProUGUI>(labelRtf.gameObject);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 48f;
        label.color = Color.white;
        label.raycastTarget = false;
        template.SetGraphics(baseGraphic, fillGraphic, label);
        template.gameObject.SetActive(false);

        RectTransform dividerRtf = GetChildRtf(rootRtf, "GridDividers");
        Stretch(dividerRtf);
        GetOrAdd<CanvasRenderer>(dividerRtf.gameObject);
        DressMakingEmbroideryGridDividerGraphic divider =
            GetOrAdd<DressMakingEmbroideryGridDividerGraphic>(dividerRtf.gameObject);
        divider.raycastTarget = false;
        dividerRtf.SetSiblingIndex(2);

        RectTransform inputRtf = GetChildRtf(rootRtf, "InputSurface");
        Stretch(inputRtf);
        GetOrAdd<CanvasRenderer>(inputRtf.gameObject);
        Image inputImage = GetOrAdd<Image>(inputRtf.gameObject);
        inputImage.color = Color.clear;
        inputImage.raycastTarget = true;
        inputRtf.SetAsLastSibling();

        RectTransform counterRtf = GetChildRtf(rootRtf, "DragCounter");
        counterRtf.anchorMin = counterRtf.anchorMax = counterRtf.pivot = new Vector2(0.5f, 0.5f);
        counterRtf.sizeDelta = new Vector2(56f, 56f);
        GetOrAdd<CanvasRenderer>(counterRtf.gameObject);
        DressMakingEmbroideryCounterGraphic counterGraphic =
            GetOrAdd<DressMakingEmbroideryCounterGraphic>(counterRtf.gameObject);
        counterGraphic.color = new Color(0.96f, 0.96f, 0.96f, 1f);
        counterGraphic.raycastTarget = false;
        Outline outline = GetOrAdd<Outline>(counterRtf.gameObject);
        outline.effectColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform counterTextRtf = GetChildRtf(counterRtf, "Count");
        Stretch(counterTextRtf);
        GetOrAdd<CanvasRenderer>(counterTextRtf.gameObject);
        TextMeshProUGUI counterText = GetOrAdd<TextMeshProUGUI>(counterTextRtf.gameObject);
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.fontSize = 28f;
        counterText.fontStyle = FontStyles.Bold;
        counterText.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        counterText.raycastTarget = false;
        counterRtf.gameObject.SetActive(false);
        counterRtf.SetAsLastSibling();

        board.SetReferences(
            rootRtf,
            regionsRtf,
            template,
            grid,
            divider,
            inputRtf,
            inputImage,
            counterRtf,
            counterText);
        EditorUtility.SetDirty(root);
    }

    static DressMakingEmbroideryPolygonGraphic GetPolygonGraphic(RectTransform parent, string name)
    {
        RectTransform rtf = GetChildRtf(parent, name);
        Stretch(rtf);
        GetOrAdd<CanvasRenderer>(rtf.gameObject);
        DressMakingEmbroideryPolygonGraphic graphic =
            GetOrAdd<DressMakingEmbroideryPolygonGraphic>(rtf.gameObject);
        graphic.raycastTarget = false;
        return graphic;
    }

    static RectTransform GetChildRtf(RectTransform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return GetRtf(child.gameObject);

        GameObject gameObject = new(name, typeof(RectTransform));
        RectTransform rtf = (RectTransform)gameObject.transform;
        rtf.SetParent(parent, false);
        return rtf;
    }

    static RectTransform GetRtf(GameObject gameObject)
        => gameObject.transform as RectTransform ?? gameObject.AddComponent<RectTransform>();

    static T GetOrAdd<T>(GameObject gameObject) where T : Component
        => gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();

    static void Stretch(RectTransform rtf)
    {
        rtf.anchorMin = Vector2.zero;
        rtf.anchorMax = Vector2.one;
        rtf.pivot = new Vector2(0.5f, 0.5f);
        rtf.anchoredPosition = Vector2.zero;
        rtf.offsetMin = Vector2.zero;
        rtf.offsetMax = Vector2.zero;
        rtf.localScale = Vector3.one;
    }
}
#endif
