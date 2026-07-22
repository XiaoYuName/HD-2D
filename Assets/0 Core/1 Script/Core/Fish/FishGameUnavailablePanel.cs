using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
#endif

namespace XFramework.Fish
{
    /// <summary>
    /// 钓鱼老人离开后的提示面板。
    /// 后续只需制作同名预制、绑定以下引用，并把它加入 UIPageData 配置即可。
    /// </summary>
    public class FishGameUnavailablePanel : UIBase
    {
        [SerializeField] LocalizeStringEvent titleText, messageText, confirmText;
        [SerializeField] Button confirmButton;

#if UNITY_EDITOR
        [PropertySpace(8)]
        [Button("一键生成不可继续钓鱼提示 UI", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
        void CreateUI()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                Debug.LogError("[FishGameUnavailablePanel] 请先把脚本挂到带 RectTransform 的 UI 物体上。", this);
                return;
            }

            // 这是脚手架生成器，会重建当前面板的全部子节点。
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);

            SetStretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 全屏遮罩：阻止点击穿透到场景和已经关闭的钓鱼面板。
            RectTransform mask = CreateRect("Mask", root);
            SetStretch(mask, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image maskImage = AddImage(mask, new Color(0f, 0f, 0f, 0.68f));
            maskImage.raycastTarget = true;

            // 提示窗口。
            RectTransform window = CreateRect("Window", root);
            SetBox(window, new Vector2(0.5f, 0.5f), new Vector2(720f, 360f), Vector2.zero);
            AddImage(window, new Color(0.96f, 0.93f, 0.84f, 1f));
            TweenerRoot = window;

            RectTransform titleRt = CreateRect("Title", window);
            SetStretch(titleRt, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(48f, -90f), new Vector2(-48f, -28f));
            titleText = AddLocalizedText(titleRt, "提示", 38f, TextAlignmentOptions.Center);
            TextMeshProUGUI title = titleRt.GetComponent<TextMeshProUGUI>();
            title.fontStyle = FontStyles.Bold;

            RectTransform divider = CreateRect("Divider", window);
            SetStretch(divider, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(56f, -108f), new Vector2(-56f, -104f));
            AddImage(divider, new Color(0.62f, 0.51f, 0.35f, 0.55f));

            RectTransform messageRt = CreateRect("MessageText", window);
            SetStretch(messageRt, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(72f, 116f), new Vector2(-72f, -126f));
            messageText = AddLocalizedText(messageRt,
                "钓鱼老人已经离开，当前时段无法继续钓鱼。",
                30f, TextAlignmentOptions.Center);

            RectTransform buttonRt = CreateRect("ConfirmButton", window);
            SetBox(buttonRt, new Vector2(0.5f, 0f), new Vector2(220f, 64f), new Vector2(0f, 32f));
            Image buttonImage = AddImage(buttonRt, new Color(0.28f, 0.58f, 0.52f, 1f));
            confirmButton = buttonRt.gameObject.AddComponent<Button>();
            confirmButton.targetGraphic = buttonImage;

            ColorBlock colors = confirmButton.colors;
            colors.highlightedColor = new Color(0.34f, 0.68f, 0.61f, 1f);
            colors.pressedColor = new Color(0.20f, 0.46f, 0.41f, 1f);
            confirmButton.colors = colors;

            RectTransform buttonTextRt = CreateRect("Text", buttonRt);
            SetStretch(buttonTextRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            confirmText = AddLocalizedText(buttonTextRt, "确定", 28f, TextAlignmentOptions.Center);
            TextMeshProUGUI buttonText = buttonTextRt.GetComponent<TextMeshProUGUI>();
            buttonText.color = Color.white;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.raycastTarget = false;

            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            Debug.Log("[FishGameUnavailablePanel] UI 已生成并自动绑定，请按美术需求替换颜色、字体和底图后保存预制体。", this);
        }

        static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject go = new(objectName, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        static void SetStretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void SetBox(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        static Image AddImage(RectTransform rt, Color color)
        {
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static TextMeshProUGUI AddText(RectTransform rt, string content, float fontSize,
            TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.18f, 0.16f, 0.13f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        static LocalizeStringEvent AddLocalizedText(RectTransform rt, string placeholder,
            float fontSize, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = AddText(rt, placeholder, fontSize, alignment);
            LocalizeStringEvent localize = rt.gameObject.AddComponent<LocalizeStringEvent>();
            UnityEventTools.AddPersistentListener<string>(
                localize.OnUpdateString, new UnityAction<string>(text.SetText));
            return localize;
        }
#endif

        public override void Init()
        {
            confirmButton.onClick.AddListener(Close);
        }

        public override void Open()
        {
            base.Open();
            titleText.SetText(LocTableSet.Fish, LocVarSet.Fish.NpcUnavailableTitle);
            messageText.SetText(LocTableSet.Fish, LocVarSet.Fish.NpcUnavailable);
            confirmText.SetText(LocTableSet.Fish, LocVarSet.Fish.NpcUnavailableConfirm);
        }
    }
}
