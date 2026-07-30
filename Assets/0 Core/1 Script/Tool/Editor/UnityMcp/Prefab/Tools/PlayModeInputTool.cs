using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnityMcp
{
    /// <summary>
    /// PlayMode 输入只开放固定动作：uGUI 走 EventSystem，键盘走 Input System。
    /// 不执行动态代码，也不向运行时场景挂常驻辅助组件。
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeInputTool
    {
        public static readonly string[] SupportedActions =
            { "click", "drag", "keyPress", "keyDown", "keyUp" };
        public static readonly string[] SupportedPlayModeActions =
            { "start", "stop", "pause", "resume" };

        static readonly HashSet<Key> HeldKeys = new HashSet<Key>();
        static readonly List<KeyRelease> KeyReleases = new List<KeyRelease>();

        static PlayModeInputTool()
        {
            EditorApplication.update += UpdateKeyReleases;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    ClearKeys();
            };
        }

        public static string Simulate(PlayModeInputRequest command)
        {
            if (!EditorApplication.isPlaying)
                return BridgeJson.Fail("PlayMode 未运行");
            if (EditorApplication.isPaused)
                return BridgeJson.Fail("PlayMode 已暂停，请先恢复");

            string action = command.inputAction?.Trim();
            if (!SupportedActions.Contains(action))
                return BridgeJson.Fail($"inputAction 只能是 {string.Join("、", SupportedActions)}");

            try
            {
                return action == "click" || action == "drag"
                    ? SimulatePointer(command, action)
                    : SimulateKey(command, action);
            }
            catch (Exception e)
            {
                return BridgeJson.Fail(e.Message);
            }
        }

        public static string ControlPlayMode(PlayModeRequest command)
        {
            string action = command.playModeAction?.Trim();
            if (!SupportedPlayModeActions.Contains(action))
                return BridgeJson.Fail($"playModeAction 只能是 {string.Join("、", SupportedPlayModeActions)}");

            if (action == "start")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return BridgeJson.Fail("Unity 已在 PlayMode 或正在进入 PlayMode");
                EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
            else if (action == "stop")
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    return BridgeJson.Fail("Unity 当前不在 PlayMode");
                EditorApplication.delayCall += EditorApplication.ExitPlaymode;
            }
            else
            {
                if (!EditorApplication.isPlaying)
                    return BridgeJson.Fail("pause/resume 需要 PlayMode");
                EditorApplication.isPaused = action == "pause";
            }

            return BridgeJson.Serialize(new PlayModeResponse
            {
                message = action == "start" || action == "stop" ? $"PlayMode {action} 已排队" : $"PlayMode 已{(action == "pause" ? "暂停" : "恢复")}",
                playModeAction = action,
                playing = EditorApplication.isPlaying.OrNull(),
                paused = EditorApplication.isPaused.OrNull(),
            });
        }

        static string SimulatePointer(PlayModeInputRequest command, string action)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return BridgeJson.Fail("当前场景没有 EventSystem");

            PointerEventData.InputButton button = GetButton(command.button);
            Vector2 targetPos = GetInputPosition(command.targetPath, command.x, command.y);
            Vector2 startPos = action == "drag"
                ? GetInputPosition(command.targetPath, command.fromX, command.fromY)
                : targetPos;
            GameObject startObject = GetRaycastObject(eventSystem, startPos);
            if (startObject == null)
                return BridgeJson.Fail($"坐标 ({startPos.x:0.#}, {startPos.y:0.#}) 没有命中可射线检测的 UI");

            if (!string.IsNullOrWhiteSpace(command.targetPath))
            {
                GameObject expected = PlayModeUi.GetGameObject(command.targetPath);
                if (startObject != expected &&
                    !startObject.transform.IsChildOf(expected.transform) &&
                    !expected.transform.IsChildOf(startObject.transform))
                    return BridgeJson.Fail($"目标 {command.targetPath} 被 {PlayModeUi.GetPath(startObject.transform)} 遮挡");
            }

            var pointer = new PointerEventData(eventSystem)
            {
                button = button,
                position = ToEventPosition(startPos),
                pressPosition = ToEventPosition(startPos),
                delta = Vector2.zero,
            };

            if (action == "click")
                Click(startObject, pointer);
            else
                Drag(startObject, pointer, targetPos);

            return BridgeJson.Serialize(new PlayModeInputResponse
            {
                message = action == "click" ? "UI 点击已发送" : "UI 拖拽已发送",
                inputAction = action,
                targetPath = string.IsNullOrWhiteSpace(command.targetPath)
                    ? PlayModeUi.GetPath(startObject.transform)
                    : command.targetPath,
                x = targetPos.x,
                y = targetPos.y,
            });
        }

        static string SimulateKey(PlayModeInputRequest command, string action)
        {
            if (string.IsNullOrWhiteSpace(command.key))
                return BridgeJson.Fail($"{action} 需要 key");
            string keyName = string.Equals(command.key, "Return", StringComparison.OrdinalIgnoreCase)
                ? "Enter"
                : command.key.Trim();
            if (!Enum.TryParse(keyName, true, out Key key) || key == Key.None)
                return BridgeJson.Fail($"无效的 Input System Key：{command.key}");
            if (Keyboard.current == null)
                return BridgeJson.Fail("Input System 当前没有 Keyboard 设备");

            if (action == "keyUp")
            {
                if (!HeldKeys.Remove(key))
                    return BridgeJson.Fail($"按键 {key} 当前不是由 MCP 按住的");
                KeyReleases.RemoveAll(item => item.key == key);
                SetKeyboardState();
            }
            else
            {
                if (!HeldKeys.Add(key))
                    return BridgeJson.Fail($"按键 {key} 已由 MCP 按住");
                SetKeyboardState();
                if (action == "keyPress")
                {
                    float duration = command.durationSeconds > 0f
                        ? Mathf.Min(command.durationSeconds, 10f)
                        : 0.1f;
                    KeyReleases.Add(new KeyRelease
                    {
                        key = key,
                        releaseTime = EditorApplication.timeSinceStartup + duration,
                    });
                }
            }

            return BridgeJson.Serialize(new PlayModeInputResponse
            {
                message = action == "keyPress"
                    ? $"按键 {key} 已按下，将自动释放"
                    : action == "keyDown" ? $"按键 {key} 已按住" : $"按键 {key} 已释放",
                inputAction = action,
                key = key.ToString(),
            });
        }

        static Vector2 GetInputPosition(string targetPath, float? x, float? y)
        {
            if (x.HasValue != y.HasValue)
                throw new ArgumentException("x 和 y 必须同时提供");
            if (x.HasValue)
                return new Vector2(x.Value, y.Value);
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("需要 targetPath，或同时提供 x、y");

            UiMarker marker = PlayModeUi.GetMarker(PlayModeUi.GetGameObject(targetPath));
            return new Vector2(marker.centerX, marker.centerY);
        }

        static PointerEventData.InputButton GetButton(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, "left", StringComparison.OrdinalIgnoreCase))
                return PointerEventData.InputButton.Left;
            if (string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
                return PointerEventData.InputButton.Right;
            if (string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase))
                return PointerEventData.InputButton.Middle;
            throw new ArgumentException("button 只能是 left、right 或 middle");
        }

        static GameObject GetRaycastObject(EventSystem eventSystem, Vector2 inputPos)
        {
            var pointer = new PointerEventData(eventSystem) { position = ToEventPosition(inputPos) };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        static Vector2 ToEventPosition(Vector2 inputPos) =>
            new Vector2(inputPos.x, Screen.height - inputPos.y);

        static void Click(GameObject hitObject, PointerEventData pointer)
        {
            ExecuteEvents.ExecuteHierarchy(hitObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hitObject, pointer, ExecuteEvents.pointerUpHandler);
            GameObject clickObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
            if (clickObject == null)
                throw new InvalidOperationException($"{PlayModeUi.GetPath(hitObject.transform)} 没有点击处理器");
            ExecuteEvents.Execute(clickObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        static void Drag(GameObject hitObject, PointerEventData pointer, Vector2 targetPos)
        {
            GameObject dragObject = ExecuteEvents.GetEventHandler<IDragHandler>(hitObject);
            if (dragObject == null)
                throw new InvalidOperationException($"{PlayModeUi.GetPath(hitObject.transform)} 没有拖拽处理器");

            ExecuteEvents.ExecuteHierarchy(hitObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.beginDragHandler);
            Vector2 destination = ToEventPosition(targetPos);
            pointer.delta = destination - pointer.position;
            pointer.position = destination;
            ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(dragObject, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(hitObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        static void SetKeyboardState()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(HeldKeys.ToArray()));
        }

        static void UpdateKeyReleases()
        {
            if (KeyReleases.Count == 0)
                return;
            double now = EditorApplication.timeSinceStartup;
            bool changed = false;
            for (int i = KeyReleases.Count - 1; i >= 0; i--)
            {
                if (KeyReleases[i].releaseTime > now)
                    continue;
                changed |= HeldKeys.Remove(KeyReleases[i].key);
                KeyReleases.RemoveAt(i);
            }
            if (changed && EditorApplication.isPlaying && Keyboard.current != null)
                SetKeyboardState();
        }

        static void ClearKeys()
        {
            HeldKeys.Clear();
            KeyReleases.Clear();
            if (Keyboard.current != null)
                SetKeyboardState();
        }

        sealed class KeyRelease
        {
            public Key key;
            public double releaseTime;
        }
    }

    /// <summary>交互 UI 的路径和 PlayMode 屏幕坐标，是截图与输入之间的稳定契约。</summary>
    static class PlayModeUi
    {
        static readonly string[] Colors =
            { "#00E5FFFF", "#FFEA00FF", "#FF4081FF", "#69F0AEFF", "#B388FFFF", "#FF9100FF" };

        public static UiMarker[] GetMarkers(int maxResults)
        {
            int limit = maxResults > 0 ? Mathf.Clamp(maxResults, 1, 100) : 30;
            return Selectable.allSelectablesArray
                .Where(item => item != null && item.IsActive() && item.interactable)
                .OrderByDescending(item => GetSortingOrder(item.gameObject))
                .ThenByDescending(item => item.transform.GetSiblingIndex())
                .Select(item => GetMarker(item.gameObject))
                .Where(item => item.maxX > 0 && item.maxY > 0 &&
                    item.minX < Screen.width && item.minY < Screen.height)
                .Take(limit)
                .Select((item, index) =>
                {
                    item.label = (index + 1).ToString();
                    item.color = Colors[index % Colors.Length];
                    return item;
                })
                .ToArray();
        }

        public static UiMarker GetMarker(GameObject gameObject)
        {
            var rtf = gameObject.transform as RectTransform;
            if (rtf == null)
                throw new InvalidOperationException($"{GetPath(gameObject.transform)} 不是 RectTransform UI");

            Canvas canvas = rtf.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;
            var corners = new Vector3[4];
            rtf.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = Screen.height - first.y;
            float maxY = minY;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, Screen.height - point.y);
                maxY = Mathf.Max(maxY, Screen.height - point.y);
            }

            Selectable selectable = gameObject.GetComponent<Selectable>();
            return new UiMarker
            {
                path = GetPath(gameObject.transform),
                type = selectable != null ? selectable.GetType().Name : "UI",
                interaction = selectable is Slider || selectable is Scrollbar ? "drag" : "click",
                centerX = (minX + maxX) * 0.5f,
                centerY = (minY + maxY) * 0.5f,
                minX = minX,
                minY = minY,
                maxX = maxX,
                maxY = maxY,
            };
        }

        public static GameObject GetGameObject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("targetPath 不能为空");
            GameObject found = GameObject.Find(path);
            if (found != null)
                return found;

            string[] names = path.Split('/');
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                foreach (GameObject root in SceneManager.GetSceneAt(sceneIndex).GetRootGameObjects())
                {
                    if (root.name != names[0])
                        continue;
                    Transform current = root.transform;
                    for (int i = 1; i < names.Length && current != null; i++)
                        current = GetChild(current, names[i]);
                    if (current != null)
                        return current.gameObject;
                }
            }
            throw new InvalidOperationException($"场景中找不到 UI 路径：{path}");
        }

        public static string GetPath(Transform tf)
        {
            var names = new Stack<string>();
            while (tf != null)
            {
                names.Push(tf.name);
                tf = tf.parent;
            }
            return string.Join("/", names);
        }

        static Transform GetChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }

        static int GetSortingOrder(GameObject gameObject)
        {
            Canvas canvas = gameObject.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.sortingOrder : 0;
        }
    }
}
