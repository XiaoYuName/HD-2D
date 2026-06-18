#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// 工厂模块各面板「一键生成界面」共用的 UGUI 生成辅助（仅编辑器）。多语言统一走 <see cref="LocalizeTableSet.Factory"/> 表。
/// 与 CrashSprintPanel 的内置生成器同构，抽出避免在多个面板里重复。
/// </summary>
public static class FactoryUIGen
{
    public static RectTransform Node(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Img(string name, Transform parent, Color color)
    {
        RectTransform rt = Node(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TMP_Text Text(string name, Transform parent, string text, int size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Node(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    public static LocalizeStringEvent Loc(string name, Transform parent, string key, int size, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = (TextMeshProUGUI)Text(name, parent, string.Empty, size, color, align);
        LocalizeStringEvent lse = tmp.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.SetReference(LocalizeTableSet.Factory, key);
        UnityAction<string> setText = tmp.SetText;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(lse.OnUpdateString, setText);
        return lse;
    }

    public static Button Btn(string name, Transform parent, string key, Color bg, Color textColor)
    {
        Image img = Img(name, parent, bg);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        LocalizeStringEvent label = Loc(name + "Text", img.transform, key, 30, textColor, TextAlignmentOptions.Center);
        Stretch(label.GetComponent<RectTransform>());
        return btn;
    }

    // 锚点定位：anchorMin/Max + 尺寸 + 相对锚点偏移
    public static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, float w, float h, float x, float y)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = aMin;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    public static void Center(RectTransform rt, float w, float h, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 在 <paramref name="content"/> 外层就地插入一个垂直 ScrollRect（含 Viewport 裁剪 + 右侧滑条），并把它设为内容。
    /// content 改为顶部拉伸 + ContentSizeFitter，元素多时可上下滑动；其上的 GridLayoutGroup 自动改为顶部对齐。
    /// 已在 ScrollRect 内则原样返回，避免重复包裹。
    /// </summary>
    public static ScrollRect WrapInScrollView(RectTransform content, float scrollbarWidth = 14f)
    {
        ScrollRect exist = content.GetComponentInParent<ScrollRect>();
        if(exist != null)
            return exist;

        Transform parent = content.parent;
        int sibling = content.GetSiblingIndex();

        // ScrollView 根：占据 content 原本的位置 / 尺寸
        RectTransform root = Node(content.name + "ScrollView", parent);
        root.SetSiblingIndex(sibling);
        CopyRect(content, root);

        ScrollRect sr = root.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // Viewport：裁剪可视区，右侧留出滑条宽度
        RectTransform viewport = Node("Viewport", root);
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-scrollbarWidth - 4f, 0f);
        viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        // content 作为内容：顶部拉伸 + 按内容撑高
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        if(content.GetComponent<ContentSizeFitter>() == null)
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if(content.GetComponent<GridLayoutGroup>() is GridLayoutGroup glg)
            glg.childAlignment = TextAnchor.UpperCenter;
        sr.content = content;

        sr.verticalScrollbar = MakeVerticalScrollbar(root, scrollbarWidth);
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return sr;
    }

    static void CopyRect(RectTransform src, RectTransform dst)
    {
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.sizeDelta = src.sizeDelta;
        dst.anchoredPosition = src.anchoredPosition;
    }

    // 右侧垂直滑条（底到顶）
    static Scrollbar MakeVerticalScrollbar(Transform parent, float width)
    {
        RectTransform sb = Node("Scrollbar", parent);
        sb.anchorMin = new Vector2(1f, 0f);
        sb.anchorMax = new Vector2(1f, 1f);
        sb.pivot = new Vector2(1f, 1f);
        sb.sizeDelta = new Vector2(width, 0f);
        sb.anchoredPosition = Vector2.zero;
        sb.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

        Scrollbar scrollbar = sb.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform area = Node("Sliding Area", sb);
        Stretch(area);
        area.offsetMin = new Vector2(2f, 2f);
        area.offsetMax = new Vector2(-2f, -2f);
        RectTransform handle = Node("Handle", area);
        handle.sizeDelta = Vector2.zero;
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.55f, 0.55f, 0.6f, 0.9f);

        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImg;
        return scrollbar;
    }
}
#endif
