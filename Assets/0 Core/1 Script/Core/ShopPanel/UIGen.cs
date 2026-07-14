#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// 商店帮忙小游戏的编辑器建 UI 辅助（仅编辑器）：供 <see cref="ShopHelpPanel"/> / <see cref="ShopHelpSettlePanel"/>
/// 的「创建界面 UI」按钮复用。多语言文本默认走 <see cref="LocTableSet.ShopHelpPanel"/> 表。
/// </summary>
public static class UIGen
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

    // 纯文本（运行时直接赋 text，无需多语言）
    public static TMP_Text Text(string name, Transform parent, string text, int fontSize, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Node(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // 多语言文本（表：ShopHelpPanel）
    public static LocalizeStringEvent Loc(string name, Transform parent, string key, int fontSize, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = Node(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        LocalizeStringEvent lse = rt.gameObject.AddComponent<LocalizeStringEvent>();
        lse.StringReference.SetReference(LocTableSet.ShopHelpPanel, key);
        UnityAction<string> setText = tmp.SetText;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(lse.OnUpdateString, setText);
        return lse;
    }

    public static Button Button(string name, Transform parent, string labelKey, Color bgColor, Color textColor)
    {
        Image img = Img(name, parent, bgColor);
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        LocalizeStringEvent label = Loc(name + "Text", img.transform, labelKey, 30, textColor, TextAlignmentOptions.Center);
        Stretch(label.GetComponent<RectTransform>());
        return btn;
    }

    public static GameObject InstantiatePrefab(string guid, Transform parent)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if(string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[UIGen] 未找到预制体 guid={guid}");
            return null;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab == null ? null : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    public static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, float w, float h, float x, float y)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2((aMin.x + aMax.x) * 0.5f, (aMin.y + aMax.y) * 0.5f);
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
