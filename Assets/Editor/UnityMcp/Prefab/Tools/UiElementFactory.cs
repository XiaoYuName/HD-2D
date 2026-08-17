using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMcp
{
    /// <summary>
    /// 带项目默认字体/颜色/尺寸的 UI 节点预设。作为 edit_prefab 的 createUi 操作被调用，
    /// 因此创建完可以在同一批次里继续 setValue 配置它，不需要再往返一次读层级。
    /// </summary>
    static class UiElementFactory
    {
        public static readonly string[] SupportedTypes =
            { "container", "image", "button", "tmpText", "verticalLayout", "scrollView" };

        public static bool IsSupported(string elementType) =>
            SupportedTypes.Any(type => string.Equals(type, elementType, StringComparison.OrdinalIgnoreCase));

        public static string DefaultName(string elementType)
        {
            switch (Normalize(elementType))
            {
                case "tmptext": return "Text";
                case "verticallayout": return "VerticalLayout";
                case "scrollview": return "ScrollView";
                default: return char.ToUpperInvariant(elementType[0]) + elementType.Substring(1);
            }
        }

        static string Normalize(string elementType) => (elementType ?? string.Empty).Trim().ToLowerInvariant();

        public static GameObject Build(string elementType, string objectName, string label, Transform parentTf,
            float width, float height, UnityMcpSettings settings)
        {
            GameObject go = CreateUiObject(objectName, parentTf, width, height);
            switch (Normalize(elementType))
            {
                case "container":
                    break;
                case "image":
                    AddImage(go, settings.DefaultImageColor);
                    break;
                case "button":
                {
                    Image image = AddImage(go, settings.DefaultImageColor);
                    Button button = go.AddComponent<Button>();
                    button.targetGraphic = image;
                    GameObject labelGo = CreateUiObject("Label", go.transform, width, height);
                    StretchToParent((RectTransform)labelGo.transform);
                    TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
                    ConfigureText(text, string.IsNullOrEmpty(label) ? objectName : label, settings);
                    text.alignment = TextAlignmentOptions.Center;
                    break;
                }
                case "tmptext":
                {
                    TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
                    ConfigureText(text, string.IsNullOrEmpty(label) ? objectName : label, settings);
                    break;
                }
                case "verticallayout":
                {
                    ConfigureVerticalLayout(go);
                    break;
                }
                case "scrollview":
                    BuildScrollView(go, width, height, settings);
                    break;
            }
            return go;
        }

        public static GameObject CreateUiObject(string name, Transform parentTf, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rtf = (RectTransform)go.transform;
            rtf.SetParent(parentTf, false);
            rtf.anchorMin = new Vector2(0.5f, 0.5f);
            rtf.anchorMax = new Vector2(0.5f, 0.5f);
            rtf.pivot = new Vector2(0.5f, 0.5f);
            rtf.sizeDelta = new Vector2(width, height);
            rtf.anchoredPosition = Vector2.zero;
            return go;
        }

        static Image AddImage(GameObject go, Color color)
        {
            if (go.GetComponent<CanvasRenderer>() == null)
                go.AddComponent<CanvasRenderer>();
            Image image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static void ConfigureText(TextMeshProUGUI text, string value, UnityMcpSettings settings)
        {
            text.text = value;
            text.fontSize = settings.DefaultTmpFontSize;
            text.color = settings.DefaultTextColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            if (settings.DefaultTmpFont != null)
                text.font = settings.DefaultTmpFont;
        }

        static void ConfigureVerticalLayout(GameObject go)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void BuildScrollView(GameObject rootGo, float width, float height, UnityMcpSettings settings)
        {
            AddImage(rootGo, settings.DefaultImageColor);
            ScrollRect scrollRect = rootGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            GameObject viewportGo = CreateUiObject("Viewport", rootGo.transform, width, height);
            var viewportRtf = (RectTransform)viewportGo.transform;
            StretchToParent(viewportRtf);
            AddImage(viewportGo, Color.white);
            Mask mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentGo = CreateUiObject("Content", viewportRtf, width, height);
            var contentRtf = (RectTransform)contentGo.transform;
            contentRtf.anchorMin = new Vector2(0f, 1f);
            contentRtf.anchorMax = new Vector2(1f, 1f);
            contentRtf.pivot = new Vector2(0.5f, 1f);
            contentRtf.anchoredPosition = Vector2.zero;
            contentRtf.sizeDelta = new Vector2(0f, height);
            ConfigureVerticalLayout(contentGo);

            scrollRect.viewport = viewportRtf;
            scrollRect.content = contentRtf;
        }

        static void StretchToParent(RectTransform rtf)
        {
            rtf.anchorMin = Vector2.zero;
            rtf.anchorMax = Vector2.one;
            rtf.pivot = new Vector2(0.5f, 0.5f);
            rtf.anchoredPosition = Vector2.zero;
            rtf.sizeDelta = Vector2.zero;
        }
    }
}
