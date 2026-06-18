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
}
#endif
