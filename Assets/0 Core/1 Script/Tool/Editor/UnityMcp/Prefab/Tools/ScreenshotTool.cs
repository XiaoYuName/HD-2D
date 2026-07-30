using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 截取编辑器窗口并以 MCP image 内容返回。默认缩放 + JPEG，适合 AI 做视觉检查而不产生大段文本。
    /// </summary>
    static class ScreenshotTool
    {
        /// <summary>captureTarget 词表的唯一来源，工具表的 enum 直接引用。</summary>
        public static readonly string[] SupportedTargets = { "focusedWindow", "custom", "gameView", "sceneView" };

        public static string Capture(ScreenshotRequest command)
        {
            string mode = string.IsNullOrWhiteSpace(command.captureTarget) ? "focusedWindow" : command.captureTarget.Trim();
            bool custom = string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase);
            bool gameView = string.Equals(mode, "gameView", StringComparison.OrdinalIgnoreCase);
            bool sceneView = string.Equals(mode, "sceneView", StringComparison.OrdinalIgnoreCase);
            if (!custom && !gameView && !sceneView &&
                !string.Equals(mode, "focusedWindow", StringComparison.OrdinalIgnoreCase))
                return BridgeJson.Fail("captureTarget 只能是 " + string.Join("、", SupportedTargets));
            if (command.elementsOnly && (!gameView || !command.annotateUi))
                return BridgeJson.Fail("elementsOnly 需要 captureTarget=gameView 且 annotateUi=true");
            if (command.annotateUi && !gameView)
                return BridgeJson.Fail("annotateUi 只支持 captureTarget=gameView");
            if (command.frameContent && !sceneView)
                return BridgeJson.Fail("frameContent 只支持 captureTarget=sceneView");
            if (gameView)
                return CaptureGameView(command);
            if (sceneView)
                return CaptureSceneView(command);

            int screenHeight = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
            Rect source;
            if (custom)
            {
                if (command.widthPixels <= 0 || command.heightPixels <= 0)
                    return BridgeJson.Fail("custom 截图需要 widthPixels 和 heightPixels");
                source = new Rect(command.x, screenHeight - command.y - command.heightPixels,
                    command.widthPixels, command.heightPixels);
            }
            else
            {
                EditorWindow window = EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;
                if (window == null)
                    return BridgeJson.Fail("没有可截图的聚焦 Unity 窗口；请聚焦目标窗口，或使用 captureTarget=custom");
                Rect position = window.position;
                source = new Rect(position.x, screenHeight - position.y - position.height,
                    position.width, position.height);
            }

            int sourceWidth = Mathf.Max(1, Mathf.RoundToInt(source.width));
            int sourceHeight = Mathf.Max(1, Mathf.RoundToInt(source.height));
            InternalEditorUtility.RepaintAllViews();
            Texture2D texture = null;
            try
            {
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(
                    new Vector2(source.x, source.y), sourceWidth, sourceHeight);
                if (pixels == null || pixels.Length == 0)
                    return BridgeJson.Fail("Unity 没有返回截图像素");
                texture = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGB24, false);
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                texture = Resize(texture, command);

                int quality = command.jpegQuality > 0 ? Mathf.Clamp(command.jpegQuality, 20, 95) : 75;
                return BridgeJson.Serialize(new ScreenshotResponse
                {
                    message = "截图已捕获",
                    imageBase64 = Convert.ToBase64String(texture.EncodeToJPG(quality)),
                    imageMimeType = "image/jpeg",
                    imageWidth = texture.width,
                    imageHeight = texture.height,
                });
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Scene View 相机渲到离屏 RT：不要焦点、不要 PlayMode、不读桌面像素，后台也能出图，
        /// 是改完 Prefab 布局后唯一可靠的自校验手段。
        /// </summary>
        static string CaptureSceneView(ScreenshotRequest command)
        {
            SceneView view = SceneView.lastActiveSceneView
                ?? Resources.FindObjectsOfTypeAll<SceneView>().FirstOrDefault();
            if (view == null)
                return BridgeJson.Fail("没有 Scene View 窗口，请在 Unity 里打开 Window > General > Scene");

            if (command.frameContent)
            {
                string frameError = FrameContent(view);
                if (frameError != null)
                    return BridgeJson.Fail(frameError);
            }

            Camera camera = view.camera;
            if (camera == null)
                return BridgeJson.Fail("Scene View 相机不可用，请让 Scene 窗口重绘一次后重试");

            int width = Mathf.Clamp(command.widthPixels > 0 ? command.widthPixels
                : Mathf.RoundToInt(view.position.width), 64, 4096);
            int height = Mathf.Clamp(command.heightPixels > 0 ? command.heightPixels
                : Mathf.RoundToInt(view.position.height), 64, 4096);

            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                texture = Resize(texture, command);

                int quality = command.jpegQuality > 0 ? Mathf.Clamp(command.jpegQuality, 20, 95) : 75;
                StageInfo stage = StatusTools.GetStageInfo();
                return BridgeJson.Serialize(new ScreenshotResponse
                {
                    message = stage == null
                        ? "Scene View 已捕获（当前不在 Prefab 编辑态，画面是场景内容）"
                        : "Scene View 已捕获：" + stage.prefabPath,
                    imageBase64 = Convert.ToBase64String(texture.EncodeToJPG(quality)),
                    imageMimeType = "image/jpeg",
                    imageWidth = texture.width,
                    imageHeight = texture.height,
                });
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>把视角对准 Prefab 编辑态的内容（没有编辑态时对准当前选中物体）。</summary>
        static string FrameContent(SceneView view)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            GameObject target = stage != null ? stage.prefabContentsRoot : Selection.activeGameObject;
            if (target == null)
                return "frameContent 需要 Prefab 编辑态或场景里的选中物体";

            if (!TryGetBounds(target, out Bounds bounds))
                return $"{target.name} 上没有可用于取景的 Renderer/RectTransform";
            view.Frame(bounds, true);
            return null;
        }

        /// <summary>UI 预制体没有 Renderer，取 RectTransform 的世界角点。</summary>
        static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                    continue;
                bounds = any ? Encapsulate(bounds, renderer.bounds) : renderer.bounds;
                any = true;
            }
            if (any)
                return true;

            var corners = new Vector3[4];
            foreach (RectTransform rtf in target.GetComponentsInChildren<RectTransform>())
            {
                rtf.GetWorldCorners(corners);
                var rectBounds = new Bounds(corners[0], Vector3.zero);
                for (int i = 1; i < corners.Length; i++)
                    rectBounds.Encapsulate(corners[i]);
                bounds = any ? Encapsulate(bounds, rectBounds) : rectBounds;
                any = true;
            }
            return any;
        }

        static Bounds Encapsulate(Bounds bounds, Bounds other)
        {
            bounds.Encapsulate(other);
            return bounds;
        }

        static string CaptureGameView(ScreenshotRequest command)
        {
            if (!EditorApplication.isPlaying)
                return BridgeJson.Fail("gameView 截图需要 PlayMode");

            UiMarker[] markers = command.annotateUi
                ? PlayModeUi.GetMarkers(command.maxUiElements)
                : null;
            if (command.elementsOnly)
                return BridgeJson.Serialize(new ScreenshotResponse
                {
                    message = $"找到 {markers.Length} 个交互 UI 元素",
                    coordinateSystem = "playModeScreen",
                    screenWidth = Screen.width,
                    screenHeight = Screen.height,
                    uiElements = markers.Length > 0 ? markers : null,
                });

            InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null)
                return BridgeJson.Fail("Game View RenderTexture 不可用，请打开 Window > General > Game 并等待一帧后重试");
            try
            {
                if (markers != null)
                    DrawMarkers(texture, markers);
                texture = Resize(texture, command);
                int quality = command.jpegQuality > 0 ? Mathf.Clamp(command.jpegQuality, 20, 95) : 75;
                return BridgeJson.Serialize(new ScreenshotResponse
                {
                    message = markers == null ? "PlayMode 画面已捕获" : $"已标记 {markers.Length} 个交互 UI 元素",
                    imageBase64 = Convert.ToBase64String(texture.EncodeToJPG(quality)),
                    imageMimeType = "image/jpeg",
                    imageWidth = texture.width,
                    imageHeight = texture.height,
                    coordinateSystem = "playModeScreen",
                    screenWidth = Screen.width,
                    screenHeight = Screen.height,
                    uiElements = markers != null && markers.Length > 0 ? markers : null,
                });
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static Texture2D Resize(Texture2D texture, ScreenshotRequest command)
        {
            int maxWidth = command.maxWidth > 0 ? Mathf.Clamp(command.maxWidth, 64, 4096) : 1600;
            int maxHeight = command.maxHeight > 0 ? Mathf.Clamp(command.maxHeight, 64, 4096) : 1200;
            if (texture.width <= maxWidth && texture.height <= maxHeight)
                return texture;

            float scale = Mathf.Min((float)maxWidth / texture.width, (float)maxHeight / texture.height);
            var resized = new Texture2D(
                Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(texture.height * scale)),
                TextureFormat.RGB24, false);
            Graphics.ConvertTexture(texture, resized);
            UnityEngine.Object.DestroyImmediate(texture);
            return resized;
        }

        static readonly Color32[] MarkerColors =
        {
            new Color32(0, 229, 255, 255),
            new Color32(255, 234, 0, 255),
            new Color32(255, 64, 129, 255),
            new Color32(105, 240, 174, 255),
            new Color32(179, 136, 255, 255),
            new Color32(255, 145, 0, 255),
        };

        static readonly string[] DigitPixels =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111",
            "101101111001001", "111100111001111", "111100111101111", "111001001001001",
            "111101111101111", "111101111001111",
        };

        static void DrawMarkers(Texture2D texture, UiMarker[] markers)
        {
            float scaleX = (float)texture.width / Screen.width;
            float scaleY = (float)texture.height / Screen.height;
            for (int i = 0; i < markers.Length; i++)
            {
                UiMarker marker = markers[i];
                int minX = Mathf.RoundToInt(marker.minX * scaleX);
                int maxX = Mathf.RoundToInt(marker.maxX * scaleX);
                int minY = texture.height - Mathf.RoundToInt(marker.maxY * scaleY);
                int maxY = texture.height - Mathf.RoundToInt(marker.minY * scaleY);
                DrawRect(texture, minX, minY, maxX, maxY, new Color32(0, 0, 0, 255), 7);
                Color32 color = MarkerColors[i % MarkerColors.Length];
                DrawRect(texture, minX, minY, maxX, maxY, color, 3);
                DrawLabel(texture, marker.label, minX + 4, maxY - 14, color);
            }
            texture.Apply(false, false);
        }

        static void DrawRect(Texture2D texture, int minX, int minY, int maxX, int maxY,
            Color32 color, int thickness)
        {
            for (int offset = 0; offset < thickness; offset++)
            {
                for (int x = minX - offset; x <= maxX + offset; x++)
                {
                    SetPixel(texture, x, minY - offset, color);
                    SetPixel(texture, x, maxY + offset, color);
                }
                for (int y = minY - offset; y <= maxY + offset; y++)
                {
                    SetPixel(texture, minX - offset, y, color);
                    SetPixel(texture, maxX + offset, y, color);
                }
            }
        }

        static void DrawLabel(Texture2D texture, string label, int x, int y, Color32 color)
        {
            int width = label.Length * 8 + 4;
            for (int px = 0; px < width; px++)
                for (int py = 0; py < 14; py++)
                    SetPixel(texture, x + px, y + py, new Color32(0, 0, 0, 220));

            for (int digitIndex = 0; digitIndex < label.Length; digitIndex++)
            {
                string pixels = DigitPixels[label[digitIndex] - '0'];
                for (int py = 0; py < 5; py++)
                    for (int px = 0; px < 3; px++)
                        if (pixels[(4 - py) * 3 + px] == '1')
                            for (int sx = 0; sx < 2; sx++)
                                for (int sy = 0; sy < 2; sy++)
                                    SetPixel(texture, x + 3 + digitIndex * 8 + px * 2 + sx,
                                        y + 2 + py * 2 + sy, color);
            }
        }

        static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
                texture.SetPixel(x, y, color);
        }
    }
}
