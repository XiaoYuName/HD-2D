using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TestSystem
{
    /// <summary>生成测试面板预制体（现代 WPF 深色风格），改布局改这里重新生成即可。</summary>
    public static class TestPanelPrefabCreator
    {
        const string PrefabPath = "Assets/Resources/Test/TestPanel.prefab";
        const string FontPath = "Assets/AddressableAssets/Local/Font/zh-cn SDF.asset";
        const float TitleBarHeight = 44f;
        const float SidebarWidth = 220f;
        static readonly Vector2 WindowSize = new (1100f, 640f);
        static readonly Color WindowColor = new Color32(0x1E, 0x1E, 0x1E, 0xFF);
        static readonly Color TitleBarColor = new Color32(0x2D, 0x2D, 0x30, 0xFF);
        static readonly Color SidebarColor = new Color32(0x25, 0x25, 0x26, 0xFF);
        static readonly Color CategoryColor = new Color32(0x33, 0x33, 0x37, 0xFF);
        static readonly Color ActionColor = new Color32(0x0E, 0x63, 0x9C, 0xFF);
        static readonly Color TextColor = new Color32(0xF0, 0xF0, 0xF0, 0xFF);
        static readonly Color HintColor = new Color32(0x9D, 0x9D, 0x9D, 0xFF);
        static readonly Color TitleAccentColor = new Color32(0x9C, 0xDC, 0xFE, 0xFF);
        static TMP_FontAsset font;

        /// <summary>预制体缺失时自动补一份，删掉预制体即可重新生成。</summary>
        [InitializeOnLoadMethod]
        static void CreateMissingPrefab()
        {
            if (File.Exists(PrefabPath))
                return;

            EditorApplication.delayCall += CreateTestPanelPrefab;
        }

        [MenuItem("Tools/测试面板/生成测试面板预制体")]
        public static void CreateTestPanelPrefab()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            GameObject rootGo = new ("TestPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new (1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            TestPanel testPanel = rootGo.AddComponent<TestPanel>();

            RectTransform windowRtf = CreateImage("Window", rootGo.transform, WindowColor).rectTransform;
            windowRtf.anchorMin = windowRtf.anchorMax = new (0.5f, 0.5f);
            windowRtf.sizeDelta = WindowSize;
            windowRtf.anchoredPosition = Vector2.zero;

            Button closeButton = CreateTitleBar(windowRtf);
            Transform categoryContainer = CreateSidebar(windowRtf);
            TextMeshProUGUI categoryTitleText = CreateContent(windowRtf, out Transform actionContainer);
            CreateTemplates(windowRtf, out Button categoryButtonPrefab, out Button actionButtonPrefab);

            testPanel.SetRef(
                categoryContainer,
                actionContainer,
                categoryTitleText,
                categoryButtonPrefab,
                actionButtonPrefab,
                closeButton);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);
            AssetDatabase.Refresh();
            Debug.Log($"测试面板预制体已生成：{PrefabPath}");
        }

        static Button CreateTitleBar(RectTransform windowRtf)
        {
            RectTransform titleBarRtf = CreateImage("TitleBar", windowRtf, TitleBarColor).rectTransform;
            SetTopStretch(titleBarRtf, TitleBarHeight);

            TextMeshProUGUI titleText = CreateText(
                "TitleText",
                titleBarRtf,
                "测试面板",
                24f,
                TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetStretch(titleText.rectTransform, 20f, 200f, 0f, 0f);

            TextMeshProUGUI hintText = CreateText(
                "HintText",
                titleBarRtf,
                "F1 开关面板",
                16f,
                HintColor,
                TextAlignmentOptions.MidlineRight);
            SetStretch(hintText.rectTransform, 0f, 60f, 0f, 0f);

            Button closeButton = CreateButton("CloseButton", titleBarRtf, TitleBarColor, "✕", 20f);
            RectTransform closeRtf = closeButton.image.rectTransform;
            closeRtf.anchorMin = closeRtf.anchorMax = closeRtf.pivot = new (1f, 1f);
            closeRtf.sizeDelta = new (TitleBarHeight, TitleBarHeight);
            closeRtf.anchoredPosition = Vector2.zero;
            return closeButton;
        }

        static Transform CreateSidebar(RectTransform windowRtf)
        {
            RectTransform sidebarRtf = CreateImage("Sidebar", windowRtf, SidebarColor).rectTransform;
            sidebarRtf.anchorMin = new (0f, 0f);
            sidebarRtf.anchorMax = new (0f, 1f);
            sidebarRtf.offsetMin = Vector2.zero;
            sidebarRtf.offsetMax = new (SidebarWidth, -TitleBarHeight);

            RectTransform containerRtf = CreateRect("CategoryContainer", sidebarRtf);
            SetStretch(containerRtf, 0f, 0f, 0f, 0f);
            VerticalLayoutGroup layout = containerRtf.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return containerRtf;
        }

        static TextMeshProUGUI CreateContent(RectTransform windowRtf, out Transform actionContainer)
        {
            RectTransform contentRtf = CreateImage("Content", windowRtf, WindowColor).rectTransform;
            contentRtf.anchorMin = new (0f, 0f);
            contentRtf.anchorMax = new (1f, 1f);
            contentRtf.offsetMin = new (SidebarWidth, 0f);
            contentRtf.offsetMax = new (0f, -TitleBarHeight);

            TextMeshProUGUI categoryTitleText = CreateText(
                "CategoryTitleText",
                contentRtf,
                "分类",
                22f,
                TitleAccentColor,
                TextAlignmentOptions.MidlineLeft);
            SetTopStretch(categoryTitleText.rectTransform, 48f, 24f, 24f);

            RectTransform actionRtf = CreateRect("ActionContainer", contentRtf);
            SetStretch(actionRtf, 24f, 24f, 48f, 24f);
            GridLayoutGroup grid = actionRtf.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new (270f, 48f);
            grid.spacing = new (14f, 14f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            actionContainer = actionRtf;
            return categoryTitleText;
        }

        static void CreateTemplates(
            RectTransform windowRtf,
            out Button categoryButtonPrefab,
            out Button actionButtonPrefab)
        {
            RectTransform templatesRtf = CreateRect("Templates", windowRtf);
            SetStretch(templatesRtf, 0f, 0f, 0f, 0f);
            templatesRtf.gameObject.SetActive(false);

            categoryButtonPrefab = CreateButton(
                "CategoryButtonPrefab",
                templatesRtf,
                CategoryColor,
                "分类",
                18f);
            categoryButtonPrefab.image.rectTransform.sizeDelta = new (SidebarWidth - 20f, 42f);
            LayoutElement layoutElement = categoryButtonPrefab.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 42f;

            actionButtonPrefab = CreateButton("ActionButtonPrefab", templatesRtf, ActionColor, "操作", 17f);
            actionButtonPrefab.image.rectTransform.sizeDelta = new (270f, 48f);
        }

        static Button CreateButton(
            string name,
            Transform parent,
            Color bgColor,
            string label,
            float fontSize)
        {
            Image image = CreateImage(name, parent, bgColor);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI text = CreateText(
                "Text",
                image.rectTransform,
                label,
                fontSize,
                TextColor,
                TextAlignmentOptions.Center);
            SetStretch(text.rectTransform, 10f, 10f, 0f, 0f);
            return button;
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new (name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void SetStretch(RectTransform rtf, float left, float right, float top, float bottom)
        {
            rtf.anchorMin = Vector2.zero;
            rtf.anchorMax = Vector2.one;
            rtf.offsetMin = new (left, bottom);
            rtf.offsetMax = new (-right, -top);
        }

        static void SetTopStretch(RectTransform rtf, float height, float left = 0f, float right = 0f)
        {
            rtf.anchorMin = new (0f, 1f);
            rtf.anchorMax = new (1f, 1f);
            rtf.pivot = new (0.5f, 1f);
            rtf.offsetMin = new (left, -height);
            rtf.offsetMax = new (-right, 0f);
        }
    }
}
