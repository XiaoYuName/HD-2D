using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 编辑器下一键搭建 UI 的通用工具：创建带 RectTransform 的节点、Image、文本，以及拉伸铺满等常用操作。
/// 用于各类「一键创建UI」按钮，减少重复的节点创建样板代码。
/// </summary>
public static class UICreateTool
{
#if UNITY_EDITOR
    // 创建一个带 RectTransform 的空节点并挂到父物体下（注册 Undo，支持编辑器撤销）
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create UI");
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    // 创建一个 Image 节点（默认不接收射线），便于纯表现用途
    public static Image CreateImage(string name, Transform parent, Color color)
    {
        Image img = CreateRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // 创建一个 TextMeshProUGUI 文本节点（默认居中、深灰色、不接收射线）
    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize)
    {
        TextMeshProUGUI t = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        t.raycastTarget = false;
        return t;
    }

    // 创建一个按钮：背景 Image（接收射线）+ 居中文本子节点（铺满、不接收射线）
    public static Button CreateButton(string name, Transform parent, string text, Color color, out TextMeshProUGUI label)
    {
        Image img = CreateImage(name, parent, color);
        img.raycastTarget = true;   // 按钮背景需接收点击
        Button btn = img.gameObject.AddComponent<Button>();
        label = CreateText("Text", img.rectTransform, text, 24f);
        label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        UIAnchorTool.Stretch(label.rectTransform);
        return btn;
    }
#endif
}
