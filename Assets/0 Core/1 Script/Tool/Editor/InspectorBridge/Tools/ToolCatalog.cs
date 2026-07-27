using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// MCP 工具表（名字 + 描述 + inputSchema）的唯一来源，和 <see cref="BridgeRouter"/> 的处理函数一一对应。
    /// 放在 C# 里是为了让契约和实现同居一处：旧实现把 schema 手写在 ps1 里，
    /// 加字段要改两处、拼错也没人报错。MCP 服务通过 action=mcp.tools 取这张表。
    /// 枚举取值同理，一律引用实现方的词表（<see cref="EditTools.SupportedOps"/> 等），不在这里再抄一份。
    ///
    /// 另外每次域重载都把它导出到 <see cref="CacheFileName"/>，这样 Unity 没开时
    /// MCP 服务仍能回一份 tools/list（否则客户端会以为这个服务没有任何工具）。
    /// </summary>
    static class ToolCatalog
    {
        public const string CacheFileName = "Library/PrefabMcpTools.json";

        public static string Respond() => BridgeJson.Serialize(new ToolsResponse
        {
            message = "Unity Prefab MCP 工具表",
            tools = Build(),
        });

        [InitializeOnLoadMethod]
        static void Export()
        {
            // 资源导入 worker 也会跑到这里，让它写同一个文件只会和编辑器抢文件锁。
            if (Application.isBatchMode)
                return;

            try
            {
                JArray tools = Build();
                // schema 和处理函数必须一一对应：漏了处理函数的工具会在 AI 调用时才报"未知 action"。
                var handled = new HashSet<string>(BridgeRouter.ToolActions);
                string missing = string.Join(", ", tools
                    .Select(tool => (string)tool["name"])
                    .Where(name => !handled.Remove(name)));
                if (missing.Length > 0)
                    Debug.LogError($"[InspectorBridge] 工具表里有 BridgeRouter 未注册的工具: {missing}");
                if (handled.Count > 0)
                    Debug.LogError($"[InspectorBridge] BridgeRouter 注册了工具表里没有的 action: {string.Join(", ", handled)}");

                string path = Path.Combine(BridgeRouter.ProjectPath(),
                    CacheFileName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = tools.ToString(Newtonsoft.Json.Formatting.None);
                // 内容没变就不写：文件 mtime 是 MCP 服务判断「要不要发 list_changed」的依据。
                if (File.Exists(path) && File.ReadAllText(path) == json)
                    return;
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InspectorBridge] 导出 MCP 工具表失败（Unity 关闭时客户端将拿不到工具列表）: {e.Message}");
            }
        }

        public static JArray Build() => new JArray(new List<JObject>
        {
            Tool("unity_prefab_status",
                "Check that the correct Unity project is open and its Prefab bridge is ready. Also reports prefabStage: the Prefab currently open in Prefab edit mode, if any, and whether it has unsaved changes.",
                new JObject()),

            Tool("open_prefab_stage",
                "Enter Prefab edit mode for a Prefab, exactly like double-clicking it in the Project window. Use this before editing with targetMode=prefabStage, or to make a Prefab visible for capture_unity_screenshot. Rejected when the current stage has unsaved changes, because Unity would show a modal save dialog and block the bridge.",
                new JObject { ["prefabPath"] = Str("Assets/.../*.prefab path to open.") },
                "prefabPath"),

            Tool("refresh_unity_assets",
                "Ask Unity to refresh AssetDatabase and request script compilation. The request returns before refresh starts; poll get_unity_compile_status afterward.",
                new JObject
                {
                    ["refreshOnly"] = Bool(false, "Refresh assets without explicitly requesting script compilation."),
                }),

            Tool("get_unity_compile_status",
                "Read current compilation state and the persisted errors/warnings from the latest script compilation, including after an assembly reload. Check compileStatus.resultStale: true means the result predates your refresh_unity_assets call and Unity has not recompiled yet (it defers compilation while the Editor is in the background), so do not trust errorCount yet.",
                new JObject
                {
                    ["excludeMessages"] = Bool(false, "Return counts only."),
                    ["maxResults"] = Int(1, 200, 50, "Maximum errors/warnings to return."),
                }),

            Tool("capture_unity_screenshot",
                "Capture a Unity window or the PlayMode game rendering as a downscaled JPEG. gameView uses PlayMode screen coordinates shared with simulate_playmode_input. annotateUi draws numbered bounds around interactive uGUI elements and returns their paths and coordinates; elementsOnly skips image capture.",
                new JObject
                {
                    ["captureTarget"] = Enum(new[] { "focusedWindow", "custom", "gameView" }, "focusedWindow"),
                    ["annotateUi"] = Bool(false, "gameView only: mark interactive uGUI elements and return reusable paths and bounds."),
                    ["elementsOnly"] = Bool(false, "gameView only: return UI element metadata without an image. Requires annotateUi=true."),
                    ["maxUiElements"] = Int(1, 100, 30, "Maximum marked UI elements, frontmost first."),
                    ["x"] = Int(description: "Custom rectangle left coordinate in desktop pixels."),
                    ["y"] = Int(description: "Custom rectangle top coordinate in desktop pixels."),
                    ["widthPixels"] = Int(1, description: "Custom rectangle width."),
                    ["heightPixels"] = Int(1, description: "Custom rectangle height."),
                    ["maxWidth"] = Int(64, 4096, 1600),
                    ["maxHeight"] = Int(64, 4096, 1200),
                    ["jpegQuality"] = Int(20, 95, 75),
                }),

            Tool("simulate_playmode_input",
                "Inject focused PlayMode input without arbitrary code execution. click/drag follow the uGUI EventSystem route and can target an annotated targetPath or PlayMode screen coordinates (top-left origin). keyPress/keyDown/keyUp use Unity Input System; keyPress releases automatically after durationSeconds.",
                new JObject
                {
                    ["inputAction"] = Enum(PlayModeInputTool.SupportedActions),
                    ["targetPath"] = Str("click/drag: hierarchy path returned by capture_unity_screenshot annotateUi. The element must still win the EventSystem raycast; blockers are reported."),
                    ["x"] = Number("click: target X; drag: destination X. PlayMode screen pixels, top-left origin."),
                    ["y"] = Number("click: target Y; drag: destination Y. PlayMode screen pixels, top-left origin."),
                    ["fromX"] = Number("drag start X. Omit with targetPath to start at the element center."),
                    ["fromY"] = Number("drag start Y. Omit with targetPath to start at the element center."),
                    ["button"] = Enum(new[] { "left", "right", "middle" }, "left", "click/drag pointer button."),
                    ["key"] = Str("keyPress/keyDown/keyUp: Unity Input System Key name, e.g. W, Space, LeftShift, Enter."),
                    ["durationSeconds"] = Number("keyPress hold duration, 0 to 10 seconds. Defaults to 0.1.", 0, 10, 0.1),
                },
                "inputAction"),

            Tool("control_unity_play_mode",
                "Start, stop, pause, or resume Unity PlayMode without relying on Editor window focus. start/stop are queued so the MCP response can finish before Unity reloads assemblies.",
                new JObject
                {
                    ["playModeAction"] = Enum(PlayModeInputTool.SupportedPlayModeActions),
                },
                "playModeAction"),

            Tool("get_prefab_mcp_settings",
                "Read the project-shared ScriptableObject settings, including write policy, backups, limits, folders, and server port.",
                new JObject()),

            Tool("find_prefabs",
                "Find Prefab assets without reading their YAML. Returns a plain array of Assets/... paths.",
                new JObject
                {
                    ["query"] = Str("Unity AssetDatabase search text, usually a prefab name fragment."),
                    ["searchFolders"] = StrArray("Optional Assets/... folders to search."),
                    ["maxResults"] = Int(1, 200, 20),
                }),

            TargetTool("get_prefab_tree",
                "Get a compact hierarchy. objectId is sibling-index based (e.g. 0/2/1) and should be used by later calls; re-read the tree after structural changes. For large Prefabs, filter first, then pass rootObjectId to fetch a subtree.",
                new JObject
                {
                    ["rootObjectId"] = Str("Optional subtree root objectId. Returned objectIds remain relative to the root."),
                    ["maxDepth"] = Int(1, 64, 4),
                    ["includeComponents"] = Bool(true, "Include the per-node component index. Default true."),
                    ["compact"] = Bool(true, "Default true: omit hierarchyPath, depth, full component type names, and pure-noise components (CanvasRenderer). Pass false only when you need hierarchyPath or full type names."),
                    ["nameFilter"] = Str("Optional case-insensitive node-name filter."),
                    ["componentTypeFilter"] = Str("Optional short or full component-type filter. Filtering by a noise type keeps it in the result."),
                    ["maxResults"] = Int(1, 1000, 100),
                }),

            TargetTool("get_component_fields",
                "List the top-level Unity-serialized fields of one component, or of many components in ONE call via targets. Prefer targets when reading a whole panel: it is far cheaper than one call per component.",
                new JObject
                {
                    ["objectId"] = Str("Single-target mode: objectId from get_prefab_tree; root is 0."),
                    ["componentIndex"] = Int(0, description: "Single-target mode: component index."),
                    ["propertyPath"] = Str("Optional: expand the children of this property instead of listing top-level fields. Works for nested structs and arrays (arrays return size + elements)."),
                    ["targets"] = ArrayOf(Object(new JObject
                        {
                            ["objectId"] = Str("Node id; root is 0."),
                            ["componentIndex"] = Int(-1, description: "Component index, or -1 for every component on that node."),
                            ["propertyPath"] = Str("Optional property to expand for this target."),
                        }, "objectId", "componentIndex"),
                        "Batch mode: read several components in one call. Response returns componentFields instead of a flat fields list."),
                    ["fieldNameFilter"] = Str("Optional case-insensitive property/display-name filter."),
                    ["onlyObjectReferences"] = Bool(false),
                    ["onlyUnassigned"] = Bool(false),
                    ["compact"] = Bool(true, "Default true: drop displayName and serializedType, and keep fieldType only for object-reference fields. Pass false only when you need the Inspector display names."),
                    ["maxResults"] = Int(1, 500, description: "Maximum fields in total; the message says so when results are truncated."),
                }),

            TargetTool("find_binding_candidates",
                "Find what can be assigned to one serialized object-reference field. Each candidate returns a ready-to-use value string for edit_prefab setValue, so there is no separate assign tool. scope=auto (default) searches the current hierarchy for Component/GameObject fields and project assets (ScriptableObject, Sprite, ...) otherwise.",
                new JObject
                {
                    ["objectId"] = Str("Node that owns the field; root is 0."),
                    ["componentIndex"] = Int(0, description: "Component index of the field owner."),
                    ["propertyPath"] = Str("SerializedProperty.propertyPath returned by get_component_fields."),
                    ["scope"] = Enum(new[] { "auto", "prefab", "asset" }, "auto", "auto picks by field type; prefab searches the current hierarchy; asset searches project assets."),
                    ["query"] = Str("Optional name filter (node name for scope=prefab, AssetDatabase search text for scope=asset)."),
                    ["searchFolders"] = StrArray("scope=asset only. Defaults to the SO-configured asset folders."),
                    ["candidateRootObjectId"] = Str("scope=prefab only: limit the search to this subtree."),
                    ["maxResults"] = Int(1, 500, 30),
                },
                "objectId", "componentIndex", "propertyPath"),

            TargetTool("edit_prefab",
                "Run an ordered, transactional batch of edits in ONE call. Any failing op aborts the batch and nothing is saved; apply=false dry-runs every op in memory (prefabAsset only) and reports per-op results. Later ops must use objectIds valid after earlier structural ops - or just reference an earlier op's node with \"$n\" (n = 1-based op number), which is what you want after createObject/createUi/duplicate/instantiatePrefab. Prefer one batch over many single-op calls.",
                new JObject
                {
                    ["apply"] = Bool(false, "false dry-runs the whole batch; true backs up once, runs, and saves once. Live targets (prefabStage/openScene) require true."),
                    ["operations"] = ArrayOf(Object(new JObject
                    {
                        ["op"] = Enum(EditTools.SupportedOps),
                        ["objectId"] = Str("Target node id from get_prefab_tree (root is 0), or \"$n\" for the node produced by op n."),
                        ["parentObjectId"] = Str("reparent/createObject/createUi/instantiatePrefab: parent node id; also accepts \"$n\"."),
                        ["newName"] = Str("rename (required) / duplicate / createObject / createUi / instantiatePrefab (optional)."),
                        ["active"] = Enum(new[] { "true", "false" }, description: "setActive only; pass as string."),
                        ["siblingIndex"] = Str("Optional 0-based child index as a string, e.g. '2'. Required by setSiblingIndex."),
                        ["componentType"] = Str("addComponent: short or full type name; ambiguous short names are rejected with the full-name list."),
                        ["componentIndex"] = Int(description: "setValue/removeComponent: component index from get_prefab_tree. Transform (index 0) cannot be removed."),
                        ["propertyPath"] = Str("setValue: SerializedProperty path, e.g. m_AnchoredPosition, m_SizeDelta, items.Array.data[2].label, items.Array.size."),
                        ["value"] = Str("setValue only, always a string. Formats: numbers/strings literal; bool true|false; enum name or int; Color #RRGGBBAA; Vector2 x,y; Vector3 x,y,z; Vector4/Quaternion x,y,z,w (Quaternion also accepts euler x,y,z); Rect x,y,w,h; object reference: null | asset:Assets/path[#subAssetName] | asset:Assets/path@objectId[#componentIndex] | object:<objectId>[#componentIndex] (-1 = GameObject). Values read by get_component_fields and find_binding_candidates can be pasted back as-is."),
                        ["sourcePrefabPath"] = Str("instantiatePrefab: Assets/.../*.prefab to nest under parentObjectId."),
                        ["elementType"] = Enum(UiElementFactory.SupportedTypes, description: "createUi only: preset to build with the project's default font/color/size."),
                        ["label"] = Str("createUi only: button label or TMP text content."),
                        ["width"] = Str("createUi only, as a string. Defaults to the SO UI width."),
                        ["height"] = Str("createUi only, as a string. Defaults to the SO UI height."),
                    }, "op")),
                },
                "operations", "apply"),

            TargetTool("validate_prefab",
                "Report missing scripts and unassigned top-level object references. By default, reference checks only inspect project scripts under Assets to avoid Unity UI/internal-field noise. A null reference may still be an optional field by design.",
                new JObject
                {
                    ["maxResults"] = Int(1, 500, 100),
                    ["includeUnityComponents"] = Bool(false, "Also inspect package and Unity-provided MonoBehaviour components; this can be noisy."),
                }),
        });

        // ---- schema 构造小工具 ----

        static JObject Tool(string name, string description, JObject properties, params string[] required) =>
            new JObject
            {
                ["name"] = name,
                ["description"] = description,
                ["inputSchema"] = Object(properties, required),
            };

        /// <summary>目标感知型工具：prefabPath / targetMode / sceneRootName 语义统一，在这里注入，避免逐个重复。</summary>
        static JObject TargetTool(string name, string description, JObject properties, params string[] required)
        {
            properties["prefabPath"] = Str("Assets/.../*.prefab path. Required when targetMode=prefabAsset (the default).");
            properties["targetMode"] = Enum(EditTarget.SupportedModes, EditTarget.SupportedModes[0],
                "Edit target. prefabAsset (default) reads/writes the on-disk prefab and supports apply=false dry-run; prefabStage uses the currently open Prefab stage; openScene uses sceneRootName in the active scene. Live stage/scene targets mutate real objects and only mark the scene dirty (save with Ctrl+S), with the whole batch registered as one Undo step.");
            properties["sceneRootName"] = Str("targetMode=openScene only: name of a root GameObject in the active scene. objectId 0 refers to that root object.");
            return Tool(name, description, properties, required);
        }

        static JObject Object(JObject properties, params string[] required)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };
            if (required != null && required.Length > 0)
                schema["required"] = new JArray(required);
            schema["additionalProperties"] = false;
            return schema;
        }

        static JObject Str(string description) => Described(new JObject { ["type"] = "string" }, description);

        static JObject StrArray(string description) => ArrayOf(new JObject { ["type"] = "string" }, description, 0);

        static JObject ArrayOf(JObject items, string description = null, int minItems = 1)
        {
            var schema = new JObject { ["type"] = "array" };
            if (minItems > 0)
                schema["minItems"] = minItems;
            schema["items"] = items;
            return Described(schema, description);
        }

        static JObject Bool(bool? defaultValue = null, string description = null)
        {
            var schema = new JObject { ["type"] = "boolean" };
            if (defaultValue.HasValue)
                schema["default"] = defaultValue.Value;
            return Described(schema, description);
        }

        static JObject Int(int? minimum = null, int? maximum = null, int? defaultValue = null, string description = null)
        {
            var schema = new JObject { ["type"] = "integer" };
            if (minimum.HasValue)
                schema["minimum"] = minimum.Value;
            if (maximum.HasValue)
                schema["maximum"] = maximum.Value;
            if (defaultValue.HasValue)
                schema["default"] = defaultValue.Value;
            return Described(schema, description);
        }

        static JObject Number(string description, double? minimum = null, double? maximum = null,
            double? defaultValue = null)
        {
            var schema = new JObject { ["type"] = "number" };
            if (minimum.HasValue)
                schema["minimum"] = minimum.Value;
            if (maximum.HasValue)
                schema["maximum"] = maximum.Value;
            if (defaultValue.HasValue)
                schema["default"] = defaultValue.Value;
            return Described(schema, description);
        }

        static JObject Enum(string[] values, string defaultValue = null, string description = null)
        {
            var schema = new JObject
            {
                ["type"] = "string",
                ["enum"] = new JArray(values),
            };
            if (defaultValue != null)
                schema["default"] = defaultValue;
            return Described(schema, description);
        }

        static JObject Described(JObject schema, string description)
        {
            if (!string.IsNullOrEmpty(description))
                schema["description"] = description;
            return schema;
        }
    }
}
