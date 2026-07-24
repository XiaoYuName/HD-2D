using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

/// <summary>
/// 面向 MCP 的低 token Prefab 查询与编辑命令。
/// 使用 sibling-index objectId，避免重名节点导致错误绑定。
/// 结构化批量编辑（prefab.edit）在 PrefabMcpEditCommands.cs 中。
/// </summary>
public static partial class BridgeCommands
{
    [Serializable]
    sealed class BridgeEnvelope
    {
        public string action;
        public string expectedProjectPath;
    }

    [Serializable]
    sealed class ScreenshotRequest
    {
        public string action;
        public string expectedProjectPath;
        public string captureTarget;
        public int x;
        public int y;
        public int widthPixels;
        public int heightPixels;
        public int maxWidth;
        public int maxHeight;
        public int jpegQuality;
    }

    [Serializable]
    class RequestBase
    {
        public string action;
        public string expectedProjectPath;
    }

    [Serializable]
    class TargetRequest : RequestBase
    {
        public string targetMode;
        public string sceneRootName;
        public string prefabPath;
    }

    [Serializable]
    sealed class FindPrefabsRequest : RequestBase
    {
        public string query;
        public string[] searchFolders;
        public int maxResults;
    }

    [Serializable]
    sealed class PrefabTreeRequest : TargetRequest
    {
        public string rootObjectId;
        public int maxDepth;
        public bool includeComponents;
        public bool compact;
        public string nameFilter;
        public string componentTypeFilter;
        public int maxResults;
    }

    [Serializable]
    sealed class ComponentFieldsRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string fieldNameFilter;
        public bool onlyObjectReferences;
        public bool onlyUnassigned;
        public int maxResults;
    }

    [Serializable]
    sealed class BindingCandidatesRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string candidateRootObjectId;
        public int maxResults;
    }

    [Serializable]
    sealed class ObjectReferenceRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string sourceObjectId;
        public int sourceComponentIndex;
        public bool clear;
        public bool apply;
    }

    [Serializable]
    sealed class AssetCandidatesRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string query;
        public string[] searchFolders;
        public int maxResults;
    }

    [Serializable]
    sealed class AssetReferenceRequest : TargetRequest
    {
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string assetPath;
        public string assetName;
        public string assetType;
        public long assetLocalId;
        public string assetObjectId;
        public int assetComponentIndex;
        public bool apply;
    }

    [Serializable]
    sealed class CreateUiRequest : TargetRequest
    {
        public string parentObjectId;
        public string elementType;
        public string elementName;
        public string label;
        public float width;
        public float height;
        public bool apply;
    }

    [Serializable]
    sealed class EditRequest : TargetRequest
    {
        public bool apply;
        public EditOp[] operations;
    }

    [Serializable]
    sealed class ValidateRequest : TargetRequest
    {
        public int maxResults;
        public bool includeUnityComponents;
    }

    [Serializable]
    sealed class RefreshRequest : RequestBase
    {
        public bool refreshOnly;
    }

    [Serializable]
    sealed class CompileStatusRequest : RequestBase
    {
        public bool excludeMessages;
        public int maxResults;
    }

    [Serializable]
    class ResponseBase
    {
        public bool ok;
        public string error;
        public string message;
    }

    [Serializable] sealed class StatusResponse : ResponseBase
    {
        public string unityVersion;
        public string projectPath;
        public bool compiling;
    }

    [Serializable] sealed class CompileStatusResponse : ResponseBase
    {
        public CompileStatusInfo compileStatus;
        public bool compiling;
    }

    [Serializable] sealed class SettingsResponse : ResponseBase
    {
        public SettingsInfo settings;
    }

    [Serializable] sealed class PrefabListResponse : ResponseBase
    {
        public PrefabInfo[] prefabs;
    }

    [Serializable] sealed class TreeResponse : ResponseBase
    {
        public NodeInfo[] nodes;
    }

    [Serializable] sealed class FieldsResponse : ResponseBase
    {
        public FieldInfoDto[] fields;
    }

    [Serializable] sealed class CandidatesResponse : ResponseBase
    {
        public CandidateInfo[] candidates;
    }

    [Serializable] sealed class AssetCandidatesResponse : ResponseBase
    {
        public AssetCandidateInfo[] assetCandidates;
    }

    [Serializable] sealed class IssuesResponse : ResponseBase
    {
        public IssueInfo[] issues;
    }

    [Serializable] sealed class AssignmentResponse : ResponseBase
    {
        public AssignmentInfo assignment;
    }

    [Serializable] sealed class CreationResponse : ResponseBase
    {
        public CreationInfo creation;
    }

    [Serializable] sealed class EditResponse : ResponseBase
    {
        public EditInfo edit;
    }

    [Serializable] sealed class ScreenshotResponse : ResponseBase
    {
        public string imageBase64;
        public string imageMimeType;
        public int imageWidth;
        public int imageHeight;
    }

    [Serializable]
    sealed class PrefabInfo
    {
        public string path;
        public string name;
        public string guid;
    }

    [Serializable]
    sealed class NodeInfo
    {
        public string objectId;
        public string hierarchyPath;
        public string name;
        public int depth;
        public bool activeSelf;
        public ComponentInfo[] components;
    }

    [Serializable]
    sealed class ComponentInfo
    {
        public int componentIndex;
        public string type;
        public string shortType;
        public bool missing;
    }

    [Serializable]
    sealed class FieldInfoDto
    {
        public string propertyPath;
        public string displayName;
        public string propertyType;
        public string serializedType;
        public string fieldType;
        public bool objectReference;
        public string value;
    }

    [Serializable]
    sealed class CandidateInfo
    {
        public string objectId;
        public string hierarchyPath;
        public string name;
        public int sourceComponentIndex;
        public string sourceType;
    }

    [Serializable]
    sealed class AssetCandidateInfo
    {
        public string assetPath;
        public string assetName;
        public string assetType;
        public long assetLocalId;
        public string assetObjectId;
        public int assetComponentIndex;
    }

    [Serializable]
    sealed class SettingsInfo
    {
        public bool autoStartServer;
        public int port;
        public int requestTimeoutMilliseconds;
        public int maxRequestBytes;
        public bool allowPrefabWrites;
        public bool createBackupBeforeWrite;
        public string backupDirectory;
        public string[] allowedPrefabWriteRoots;
        public int defaultQueryLimit;
        public int maximumQueryLimit;
        public string[] defaultAssetSearchFolders;
        public string assetPath;
        public float defaultUiWidth;
        public float defaultUiHeight;
        public string defaultTmpFont;
        public float defaultTmpFontSize;
        public string defaultTextColor;
        public string defaultImageColor;
    }

    [Serializable]
    sealed class IssueInfo
    {
        public string kind;
        public string hierarchyPath;
        public string objectId;
        public int componentIndex;
        public string componentType;
        public string propertyPath;
        public string message;
    }

    [Serializable]
    sealed class CompileStatusInfo
    {
        public bool isCompiling;
        public bool isUpdating;
        public bool hasResult;
        public bool succeeded;
        public int errorCount;
        public int warningCount;
        public int domainReloadCount;
        public string finishedAtUtc;
        public InspectorBridgeCompileMessage[] messages;
    }

    [Serializable]
    sealed class AssignmentInfo
    {
        public string target;
        public string source;
        public string previousValue;
        public string newValue;
        public bool applied;
        public string backupPath;
    }

    [Serializable]
    sealed class CreationInfo
    {
        public string elementType;
        public string parentPath;
        public string objectId;
        public string hierarchyPath;
        public string[] createdObjects;
        public bool applied;
        public string backupPath;
    }

    /// <summary>
    /// 统一封装一次编辑的目标来源，屏蔽三种 targetMode 的差异：
    /// prefabAsset（LoadPrefabContents 的离屏副本，SaveAsPrefabAsset 落盘 + 可备份）、
    /// prefabStage（当前打开的 Prefab 编辑态，改后标脏由用户保存）、
    /// openScene（当前场景里某个根物体子树，改后标脏由用户保存）。
    /// objectId/hierarchyPath 一律相对 <see cref="Root"/> 解析，与来源无关。
    /// </summary>
    sealed class EditTarget
    {
        public Transform Root;
        bool isAsset;
        string assetPath;          // prefabAsset：落盘/备份/写入白名单校验都用它
        GameObject loadedContents; // prefabAsset：需要 Unload
        Scene dirtyScene;          // prefabStage/openScene：标脏用
        string policyPath;         // 写入白名单校验用的路径（asset 路径或场景/stage 资产路径）

        public static EditTarget ForAsset(GameObject contents, string prefabPath) => new EditTarget
        {
            Root = contents.transform,
            isAsset = true,
            assetPath = prefabPath,
            loadedContents = contents,
            policyPath = prefabPath,
        };

        public static EditTarget ForStage(Transform root, Scene scene, string stageAssetPath) => new EditTarget
        {
            Root = root,
            dirtyScene = scene,
            policyPath = stageAssetPath,
        };

        public static EditTarget ForScene(Transform root, Scene scene) => new EditTarget
        {
            Root = root,
            dirtyScene = scene,
            policyPath = scene.path,
        };

        public string PolicyPath => policyPath;

        /// <summary>true=离屏 Prefab 副本（可 SaveAsPrefabAsset 落盘、可备份、支持内存预演）；false=实时 stage/场景对象。</summary>
        public bool IsAsset => isAsset;

        public string CreateBackup() =>
            isAsset ? PrefabMcpSettings.GetOrCreate().CreatePrefabBackup(assetPath) : null;

        /// <summary>持久化改动，返回错误信息或 null。</summary>
        public string Save()
        {
            if (isAsset)
            {
                if (PrefabUtility.SaveAsPrefabAsset(loadedContents, assetPath) == null)
                    return "Prefab 保存失败，Unity 未返回已保存资源";
                AssetDatabase.SaveAssets();
                return null;
            }
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            return null;
        }

        public void Dispose()
        {
            if (loadedContents != null)
                PrefabUtility.UnloadPrefabContents(loadedContents);
        }
    }

    static bool TryGetEditTarget(TargetRequest command, out EditTarget target, out string error)
    {
        target = null;
        error = null;
        switch (string.IsNullOrEmpty(command.targetMode) ? "prefabAsset" : command.targetMode.Trim())
        {
            case "prefabAsset":
                error = ValidatePrefabPath(command.prefabPath);
                if (error != null)
                    return false;
                target = EditTarget.ForAsset(PrefabUtility.LoadPrefabContents(command.prefabPath), command.prefabPath);
                return true;
            case "prefabStage":
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null)
                {
                    error = "当前没有打开 Prefab 编辑模式（双击进入某个 Prefab 后再试）";
                    return false;
                }
                target = EditTarget.ForStage(stage.prefabContentsRoot.transform, stage.scene, stage.assetPath);
                return true;
            }
            case "openScene":
            {
                if (string.IsNullOrWhiteSpace(command.sceneRootName))
                {
                    error = "targetMode=openScene 需要 sceneRootName（场景里某个根物体的名字）";
                    return false;
                }
                Scene scene = SceneManager.GetActiveScene();
                GameObject rootGo = scene.GetRootGameObjects().FirstOrDefault(go => go.name == command.sceneRootName);
                if (rootGo == null)
                {
                    error = $"当前打开的场景里找不到根物体 {command.sceneRootName}";
                    return false;
                }
                target = EditTarget.ForScene(rootGo.transform, scene);
                return true;
            }
            default:
                error = $"未知 targetMode: {command.targetMode}（可选 prefabAsset/prefabStage/openScene）";
                return false;
        }
    }

    /// <summary>改动对象前的写入策略校验：总开关 + 目录白名单。返回错误信息或 null。</summary>
    static string ValidateWriteAllowed(EditTarget target)
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        if (!settings.AllowPrefabWrites)
            return $"写入已被项目配置禁止。请在 Project Settings > Unity Prefab MCP 中启用；配置资产: {PrefabMcpSettings.AssetPath}";
        if (!string.IsNullOrEmpty(target.PolicyPath) && !settings.IsPrefabWritePathAllowed(target.PolicyPath))
            return $"目标不在允许写入的目录中: {target.PolicyPath}";
        return null;
    }

    /// <summary>改动对象后落盘/标脏：先备份再保存。返回错误信息或 null。</summary>
    static string CommitTarget(EditTarget target, out string backupPath)
    {
        backupPath = target.CreateBackup();
        return target.Save();
    }

    public static string Dispatch(string requestJson)
    {
        BridgeEnvelope envelope;
        try
        {
            envelope = JsonConvert.DeserializeObject<BridgeEnvelope>(requestJson);
        }
        catch (Exception e)
        {
            return Fail("请求 JSON 解析失败: " + e.Message);
        }

        if (envelope == null)
            return Fail("请求为空");

        string projectError = ValidateProject(envelope.expectedProjectPath);
        if (projectError != null)
            return Fail(projectError);

        try
        {
            if (envelope.action == "editor.screenshot")
                return CaptureEditorScreenshot(JsonConvert.DeserializeObject<ScreenshotRequest>(requestJson));

            var command = envelope;
            switch (envelope.action)
            {
                case "prefab.status": return Status();
                case "prefab.settings": return GetSettings();
                case "prefab.find": return FindPrefabs(JsonConvert.DeserializeObject<FindPrefabsRequest>(requestJson));
                case "prefab.tree": return GetPrefabTree(JsonConvert.DeserializeObject<PrefabTreeRequest>(requestJson));
                case "prefab.fields": return GetComponentFields(JsonConvert.DeserializeObject<ComponentFieldsRequest>(requestJson));
                case "prefab.candidates": return FindBindingCandidates(JsonConvert.DeserializeObject<BindingCandidatesRequest>(requestJson));
                case "prefab.assign": return AssignObjectReference(JsonConvert.DeserializeObject<ObjectReferenceRequest>(requestJson));
                case "prefab.assetCandidates": return FindAssetCandidates(JsonConvert.DeserializeObject<AssetCandidatesRequest>(requestJson));
                case "prefab.assignAsset": return AssignAssetReference(JsonConvert.DeserializeObject<AssetReferenceRequest>(requestJson));
                case "prefab.createUi": return CreateUiElement(JsonConvert.DeserializeObject<CreateUiRequest>(requestJson));
                case "prefab.edit": return EditPrefab(JsonConvert.DeserializeObject<EditRequest>(requestJson));
                case "prefab.validate": return ValidatePrefab(JsonConvert.DeserializeObject<ValidateRequest>(requestJson));
                case "unity.refresh": return RefreshUnityAssets(JsonConvert.DeserializeObject<RefreshRequest>(requestJson));
                case "unity.compileStatus": return GetUnityCompileStatus(JsonConvert.DeserializeObject<CompileStatusRequest>(requestJson));
                default: return Fail("未知 Prefab MCP action: " + command.action);
            }
        }
        catch (Exception e)
        {
            return Fail(e.ToString());
        }
    }

    static string CaptureEditorScreenshot(ScreenshotRequest command)
    {
        string target = string.IsNullOrWhiteSpace(command.captureTarget) ? "focusedWindow" : command.captureTarget.Trim();
        if (!string.Equals(target, "custom", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(target, "focusedWindow", StringComparison.OrdinalIgnoreCase))
            return Fail("captureTarget 只能是 focusedWindow 或 custom");

        int screenHeight = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
        Rect source;
        if (string.Equals(target, "custom", StringComparison.OrdinalIgnoreCase))
        {
            if (command.widthPixels <= 0 || command.heightPixels <= 0)
                return Fail("custom 截图需要 widthPixels 和 heightPixels");
            source = new Rect(command.x, screenHeight - command.y - command.heightPixels,
                command.widthPixels, command.heightPixels);
        }
        else
        {
            EditorWindow window = EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;
            if (window == null)
                return Fail("没有可截图的聚焦 Unity 窗口；请聚焦目标窗口，或使用 captureTarget=custom");
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
                return Fail("Unity 没有返回截图像素");
            texture = new Texture2D(sourceWidth, sourceHeight, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply(false, false);

            int maxWidth = command.maxWidth > 0 ? Mathf.Clamp(command.maxWidth, 64, 4096) : 1600;
            int maxHeight = command.maxHeight > 0 ? Mathf.Clamp(command.maxHeight, 64, 4096) : 1200;
            if (texture.width > maxWidth || texture.height > maxHeight)
            {
                float scale = Mathf.Min((float)maxWidth / texture.width, (float)maxHeight / texture.height);
                Texture2D resized = new Texture2D(
                    Mathf.Max(1, Mathf.RoundToInt(texture.width * scale)),
                    Mathf.Max(1, Mathf.RoundToInt(texture.height * scale)),
                    TextureFormat.RGB24, false);
                Graphics.ConvertTexture(texture, resized);
                UnityEngine.Object.DestroyImmediate(texture);
                texture = resized;
            }

            int quality = command.jpegQuality > 0 ? Mathf.Clamp(command.jpegQuality, 20, 95) : 75;
            return ToJson(new ScreenshotResponse
            {
                ok = true,
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

    static string Status()
    {
        return ToJson(new StatusResponse
        {
            ok = true,
            message = "Unity Prefab MCP bridge is ready",
            unityVersion = Application.unityVersion,
            projectPath = ProjectPath(),
            compiling = EditorApplication.isCompiling,
        });
    }

    static string RefreshUnityAssets(RefreshRequest command)
    {
        InspectorBridgeCompileTracker.RequestRefresh(!command.refreshOnly);
        return ToJson(new ResponseBase
        {
            ok = true,
            message = command.refreshOnly
                ? "已安排 Unity 刷新 AssetDatabase"
                : "已安排 Unity 刷新 AssetDatabase 并请求脚本编译",
        });
    }

    static string GetUnityCompileStatus(CompileStatusRequest command)
    {
        int limit = ConfiguredLimit(command.maxResults, 200);
        InspectorBridgeCompileSnapshot snapshot =
            InspectorBridgeCompileTracker.GetSnapshot(command.excludeMessages ? 0 : limit);
        return ToJson(new CompileStatusResponse
        {
            ok = true,
            message = snapshot.isCompiling
                ? "Unity 正在编译脚本"
                : snapshot.hasResult
                    ? snapshot.succeeded ? "最近一次脚本编译成功" : "最近一次脚本编译失败"
                    : "当前会话尚无可用的脚本编译结果",
            compiling = snapshot.isCompiling,
            compileStatus = new CompileStatusInfo
            {
                isCompiling = snapshot.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                hasResult = snapshot.hasResult,
                succeeded = snapshot.succeeded,
                errorCount = snapshot.errorCount,
                warningCount = snapshot.warningCount,
                domainReloadCount = InspectorBridgeCompileTracker.DomainReloadCount,
                finishedAtUtc = snapshot.finishedAtUtc,
                messages = snapshot.messages,
            },
        });
    }

    static string GetSettings()
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        return ToJson(new SettingsResponse
        {
            ok = true,
            message = "Prefab MCP 项目配置",
            settings = new SettingsInfo
            {
                autoStartServer = settings.AutoStartServer,
                port = settings.Port,
                requestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds,
                maxRequestBytes = settings.MaxRequestBytes,
                allowPrefabWrites = settings.AllowPrefabWrites,
                createBackupBeforeWrite = settings.CreateBackupBeforeWrite,
                backupDirectory = settings.BackupDirectory,
                allowedPrefabWriteRoots = settings.AllowedPrefabWriteRoots,
                defaultQueryLimit = settings.DefaultQueryLimit,
                maximumQueryLimit = settings.MaximumQueryLimit,
                defaultAssetSearchFolders = settings.DefaultAssetSearchFolders,
                assetPath = PrefabMcpSettings.AssetPath,
                defaultUiWidth = settings.DefaultUiSize.x,
                defaultUiHeight = settings.DefaultUiSize.y,
                defaultTmpFont = settings.DefaultTmpFont == null ? null : AssetDatabase.GetAssetPath(settings.DefaultTmpFont),
                defaultTmpFontSize = settings.DefaultTmpFontSize,
                defaultTextColor = ColorUtility.ToHtmlStringRGBA(settings.DefaultTextColor),
                defaultImageColor = ColorUtility.ToHtmlStringRGBA(settings.DefaultImageColor),
            },
        });
    }

    static string FindPrefabs(FindPrefabsRequest command)
    {
        int limit = ConfiguredLimit(command.maxResults, 200);
        string filter = string.IsNullOrWhiteSpace(command.query)
            ? "t:Prefab"
            : command.query.Trim() + " t:Prefab";

        string[] folders = NormalizeSearchFolders(command.searchFolders);
        string[] guids = folders == null
            ? AssetDatabase.FindAssets(filter)
            : AssetDatabase.FindAssets(filter, folders);

        PrefabInfo[] prefabs = guids
            .Select(guid => new { guid, path = AssetDatabase.GUIDToAssetPath(guid) })
            .Where(item => item.path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(item => new PrefabInfo
            {
                guid = item.guid,
                path = item.path,
                name = Path.GetFileNameWithoutExtension(item.path),
            })
            .ToArray();

        return ToJson(new PrefabListResponse
        {
            ok = true,
            message = $"找到 {prefabs.Length} 个 Prefab（最多返回 {limit} 个）",
            prefabs = prefabs,
        });
    }

    static string GetPrefabTree(PrefabTreeRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget target, out string error))
            return Fail(error);

        int maxDepth = command.maxDepth <= 0 ? 4 : Math.Min(command.maxDepth, 64);
        int limit = ConfiguredLimit(command.maxResults, 1000);
        try
        {
            if (!TryResolveObject(target.Root, command.rootObjectId, out Transform queryRoot, out error))
                return Fail(error);

            var nodes = new List<NodeInfo>();
            CollectNodes(queryRoot, target.Root, 0, maxDepth, limit, command.includeComponents,
                command.compact, command.nameFilter, command.componentTypeFilter, nodes);
            string scope = string.IsNullOrEmpty(command.rootObjectId) ? "0" : command.rootObjectId;
            return ToJson(new TreeResponse
            {
                ok = true,
                message = $"返回子树 {scope} 的 {nodes.Count} 个节点，最大相对深度 {maxDepth}，最多 {limit} 个",
                nodes = nodes.ToArray(),
            });
        }
        finally
        {
            target.Dispose();
        }
    }

    static string GetComponentFields(ComponentFieldsRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        try
        {
            if (!TryResolveObject(editTarget.Root, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);

            var serializedObject = new SerializedObject(component);
            if (!string.IsNullOrEmpty(command.propertyPath))
            {
                var childFields = new List<FieldInfoDto>();
                string childError = CollectChildProperties(serializedObject, component, command, editTarget.Root, childFields);
                if (childError != null)
                    return Fail(childError);
                return ToJson(new FieldsResponse
                {
                    ok = true,
                    message = $"{command.propertyPath} 展开为 {childFields.Count} 个子属性",
                    fields = childFields.ToArray(),
                });
            }

            var fields = new List<FieldInfoDto>();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.depth != 0 || iterator.propertyPath == "m_Script")
                    continue;
                if (command.onlyObjectReferences && iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (command.onlyUnassigned &&
                    (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue != null))
                    continue;
                if (!string.IsNullOrEmpty(command.fieldNameFilter) &&
                    iterator.propertyPath.IndexOf(command.fieldNameFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    iterator.displayName.IndexOf(command.fieldNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                System.Reflection.FieldInfo reflected = FindField(component.GetType(), iterator.name);
                fields.Add(new FieldInfoDto
                {
                    propertyPath = iterator.propertyPath,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    serializedType = iterator.type,
                    fieldType = reflected == null ? null : FriendlyTypeName(reflected.FieldType),
                    objectReference = iterator.propertyType == SerializedPropertyType.ObjectReference,
                    value = DescribePropertyValue(iterator, editTarget.Root),
                });
            }

            return ToJson(new FieldsResponse
            {
                ok = true,
                message = $"{component.GetType().Name} 有 {fields.Count} 个顶层序列化字段",
                fields = fields.ToArray(),
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static string FindBindingCandidates(BindingCandidatesRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 200);
        try
        {
            if (!TryResolveObject(editTarget.Root, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(command.propertyPath);
            if (property == null)
                return Fail($"找不到序列化字段 {command.propertyPath}");
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return Fail($"字段 {command.propertyPath} 不是对象引用字段");

            System.Reflection.FieldInfo reflected = FindField(component.GetType(), RootFieldName(command.propertyPath));
            if (reflected == null)
                return Fail($"无法确定字段 {command.propertyPath} 的 C# 类型；MVP 暂只支持顶层对象引用字段");

            Type expectedType = reflected.FieldType;
            if (!typeof(UnityEngine.Object).IsAssignableFrom(expectedType))
                return Fail($"字段类型 {FriendlyTypeName(expectedType)} 不是 UnityEngine.Object 引用");

            Transform candidateRoot = editTarget.Root;
            if (!string.IsNullOrEmpty(command.candidateRootObjectId) &&
                !TryResolveObject(editTarget.Root, command.candidateRootObjectId, out candidateRoot, out error))
                return Fail(error);

            var candidates = new List<CandidateInfo>();
            foreach (Transform node in EnumerateHierarchy(candidateRoot))
            {
                if (expectedType.IsAssignableFrom(typeof(GameObject)))
                {
                    candidates.Add(CreateCandidate(editTarget.Root, node, -1, node.gameObject.GetType()));
                }

                Component[] components = node.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component candidate = components[i];
                    if (candidate != null && expectedType.IsAssignableFrom(candidate.GetType()))
                        candidates.Add(CreateCandidate(editTarget.Root, node, i, candidate.GetType()));
                }
            }

            CandidateInfo[] result = candidates
                .OrderByDescending(item => CandidateScore(command.propertyPath, item))
                .ThenBy(item => item.hierarchyPath, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();

            return ToJson(new CandidatesResponse
            {
                ok = true,
                message = $"字段类型 {FriendlyTypeName(expectedType)}，返回 {result.Length} 个候选项",
                candidates = result,
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static string AssignObjectReference(ObjectReferenceRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        try
        {
            if (!TryResolveObject(editTarget.Root, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.FindProperty(command.propertyPath);
            if (property == null)
                return Fail($"找不到序列化字段 {command.propertyPath}");
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return Fail($"字段 {command.propertyPath} 不是对象引用字段");

            System.Reflection.FieldInfo reflected = FindField(component.GetType(), RootFieldName(command.propertyPath));
            Type expectedType = reflected == null ? null : reflected.FieldType;
            UnityEngine.Object source = null;

            if (!command.clear)
            {
                if (string.IsNullOrEmpty(command.sourceObjectId))
                    return Fail("clear=false 时必须显式提供 sourceObjectId；根节点请传 0");
                if (!TryResolveObject(editTarget.Root, command.sourceObjectId, out Transform sourceTransform, out error))
                    return Fail(error);

                if (command.sourceComponentIndex < 0)
                {
                    source = sourceTransform.gameObject;
                }
                else
                {
                    if (!TryResolveComponent(sourceTransform, command.sourceComponentIndex, out Component sourceComponent, out error))
                        return Fail(error);
                    source = sourceComponent;
                }

                if (expectedType != null && !expectedType.IsInstanceOfType(source))
                    return Fail($"类型不兼容：字段需要 {FriendlyTypeName(expectedType)}，候选项是 {FriendlyTypeName(source.GetType())}");
            }

            string previous = DescribeObjectReference(property.objectReferenceValue, editTarget.Root);
            string next = DescribeObjectReference(source, editTarget.Root);
            var assignment = new AssignmentInfo
            {
                target = $"{GetHierarchyPath(target, editTarget.Root)} :: {component.GetType().Name}[{command.componentIndex}].{command.propertyPath}",
                source = next,
                previousValue = previous,
                newValue = next,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(editTarget);
                if (error != null)
                    return Fail(error);
                property.objectReferenceValue = source;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                error = CommitTarget(editTarget, out string backupPath);
                if (error != null)
                    return Fail(error);
                assignment.backupPath = backupPath;
            }

            return ToJson(new AssignmentResponse
            {
                ok = true,
                message = command.apply ? "引用已写入并保存" : "预检通过；apply=false，未修改 Prefab",
                assignment = assignment,
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static string FindAssetCandidates(AssetCandidatesRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 500);
        try
        {
            if (!TryResolveObject(editTarget.Root, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);
            if (!TryGetObjectReferenceProperty(component, command.propertyPath, out SerializedObject _,
                    out SerializedProperty _, out Type expectedType, out error))
                return Fail(error);

            string[] configuredFolders = command.searchFolders == null || command.searchFolders.Length == 0
                ? PrefabMcpSettings.GetOrCreate().DefaultAssetSearchFolders
                : command.searchFolders;
            string[] folders = NormalizeSearchFolders(configuredFolders);
            var candidates = new List<AssetCandidateInfo>();

            if (typeof(Component).IsAssignableFrom(expectedType) || expectedType == typeof(GameObject))
            {
                string filter = string.IsNullOrWhiteSpace(command.query)
                    ? "t:Prefab"
                    : command.query.Trim() + " t:Prefab";
                string[] guids = folders == null ? AssetDatabase.FindAssets(filter) : AssetDatabase.FindAssets(filter, folders);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;

                    if (expectedType == typeof(GameObject))
                    {
                        candidates.Add(CreateAssetCandidate(prefab, path, "0", -1));
                    }
                    else
                    {
                        foreach (Transform node in EnumerateHierarchy(prefab.transform))
                        {
                            Component[] components = node.GetComponents<Component>();
                            for (int index = 0; index < components.Length; index++)
                            {
                                Component candidate = components[index];
                                if (candidate != null && expectedType.IsInstanceOfType(candidate))
                                    candidates.Add(CreateAssetCandidate(candidate, path,
                                        GetObjectId(node, prefab.transform), index));
                            }
                        }
                    }

                    if (candidates.Count >= limit * 4)
                        break;
                }
            }
            else
            {
                string typeFilter = expectedType.Name;
                string filter = string.IsNullOrWhiteSpace(command.query)
                    ? "t:" + typeFilter
                    : command.query.Trim() + " t:" + typeFilter;
                string[] guids = folders == null ? AssetDatabase.FindAssets(filter) : AssetDatabase.FindAssets(filter, folders);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset != null && expectedType.IsInstanceOfType(asset))
                            candidates.Add(CreateAssetCandidate(asset, path, null, -1));
                    }
                    if (candidates.Count >= limit * 4)
                        break;
                }
            }

            AssetCandidateInfo[] result = candidates
                .GroupBy(item => $"{item.assetPath}|{item.assetLocalId}|{item.assetObjectId}|{item.assetComponentIndex}")
                .Select(group => group.First())
                .OrderByDescending(item => AssetCandidateScore(command.propertyPath, item))
                .ThenBy(item => item.assetPath, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();

            return ToJson(new AssetCandidatesResponse
            {
                ok = true,
                message = $"字段类型 {FriendlyTypeName(expectedType)}，返回 {result.Length} 个项目资产候选项",
                assetCandidates = result,
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static string AssignAssetReference(AssetReferenceRequest command)
    {
        if (string.IsNullOrEmpty(command.assetPath))
            return Fail("assetPath 不能为空");
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        try
        {
            if (!TryResolveObject(editTarget.Root, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);
            if (!TryGetObjectReferenceProperty(component, command.propertyPath, out SerializedObject serializedObject,
                    out SerializedProperty property, out Type expectedType, out error))
                return Fail(error);

            UnityEngine.Object source = ResolveAssetReference(command, expectedType, out error);
            if (source == null)
                return Fail(error ?? "找不到指定资产对象");
            if (!expectedType.IsInstanceOfType(source))
                return Fail($"类型不兼容：字段需要 {FriendlyTypeName(expectedType)}，资产是 {FriendlyTypeName(source.GetType())}");

            string previous = DescribeObjectReference(property.objectReferenceValue, editTarget.Root);
            string next = DescribeObjectReference(source, editTarget.Root);
            var assignment = new AssignmentInfo
            {
                target = $"{GetHierarchyPath(target, editTarget.Root)} :: {component.GetType().Name}[{command.componentIndex}].{command.propertyPath}",
                source = next,
                previousValue = previous,
                newValue = next,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(editTarget);
                if (error != null)
                    return Fail(error);
                property.objectReferenceValue = source;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                error = CommitTarget(editTarget, out string backupPath);
                if (error != null)
                    return Fail(error);
                assignment.backupPath = backupPath;
            }

            return ToJson(new AssignmentResponse
            {
                ok = true,
                message = command.apply ? "资产引用已写入并保存" : "资产引用预检通过；apply=false，未修改 Prefab",
                assignment = assignment,
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static string CreateUiElement(CreateUiRequest command)
    {
        string type = (command.elementType ?? string.Empty).Trim().ToLowerInvariant();
        string[] supported = { "container", "image", "button", "tmptext", "verticallayout", "scrollview" };
        if (!supported.Contains(type))
            return Fail("不支持的 elementType。可选: " + string.Join(", ", supported));

        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        float width = command.width > 0f ? command.width : settings.DefaultUiSize.x;
        float height = command.height > 0f ? command.height : settings.DefaultUiSize.y;
        string objectName = string.IsNullOrWhiteSpace(command.elementName)
            ? DefaultUiElementName(type)
            : command.elementName.Trim();

        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);
        try
        {
            if (!TryResolveObject(editTarget.Root, command.parentObjectId, out Transform parent, out error))
                return Fail(error);
            if (!(parent is RectTransform))
                return Fail($"父节点 {GetHierarchyPath(parent, editTarget.Root)} 没有 RectTransform，不适合作为 UI 父节点");

            var creation = new CreationInfo
            {
                elementType = type,
                parentPath = GetHierarchyPath(parent, editTarget.Root),
                hierarchyPath = GetHierarchyPath(parent, editTarget.Root) + "/" + objectName,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(editTarget);
                if (error != null)
                    return Fail(error);

                GameObject created = BuildUiElement(type, objectName, command.label, parent, width, height, settings);
                creation.objectId = GetObjectId(created.transform, editTarget.Root);
                creation.hierarchyPath = GetHierarchyPath(created.transform, editTarget.Root);
                creation.createdObjects = EnumerateHierarchy(created.transform)
                    .Select(item => GetHierarchyPath(item, editTarget.Root))
                    .ToArray();

                error = CommitTarget(editTarget, out string backupPath);
                if (error != null)
                    return Fail(error);
                creation.backupPath = backupPath;
            }
            else
            {
                creation.createdObjects = type == "button"
                    ? new[] { creation.hierarchyPath, creation.hierarchyPath + "/Label" }
                    : type == "scrollview"
                        ? new[] { creation.hierarchyPath, creation.hierarchyPath + "/Viewport", creation.hierarchyPath + "/Viewport/Content" }
                        : new[] { creation.hierarchyPath };
            }

            return ToJson(new CreationResponse
            {
                ok = true,
                message = command.apply ? "UI 节点已创建并保存" : "UI 创建预检通过；apply=false，未修改 Prefab",
                creation = creation,
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static GameObject BuildUiElement(string type, string objectName, string label, Transform parent,
        float width, float height, PrefabMcpSettings settings)
    {
        GameObject gameObject = CreateUiObject(objectName, parent, width, height);
        switch (type)
        {
            case "container":
                break;
            case "image":
                AddImage(gameObject, settings.DefaultImageColor);
                break;
            case "button":
            {
                Image image = AddImage(gameObject, settings.DefaultImageColor);
                Button button = gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                GameObject labelObject = CreateUiObject("Label", gameObject.transform, width, height);
                StretchToParent((RectTransform)labelObject.transform);
                TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
                ConfigureTmpText(text, string.IsNullOrEmpty(label) ? objectName : label, settings);
                text.alignment = TextAlignmentOptions.Center;
                break;
            }
            case "tmptext":
            {
                TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
                ConfigureTmpText(text, string.IsNullOrEmpty(label) ? objectName : label, settings);
                break;
            }
            case "verticallayout":
            {
                VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                break;
            }
            case "scrollview":
                BuildScrollView(gameObject, width, height, settings);
                break;
        }
        return gameObject;
    }

    static GameObject CreateUiObject(string name, Transform parent, float width, float height)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rectTransform = (RectTransform)gameObject.transform;
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = Vector2.zero;
        return gameObject;
    }

    static Image AddImage(GameObject gameObject, Color color)
    {
        if (gameObject.GetComponent<CanvasRenderer>() == null)
            gameObject.AddComponent<CanvasRenderer>();
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static void ConfigureTmpText(TextMeshProUGUI text, string value, PrefabMcpSettings settings)
    {
        text.text = value;
        text.fontSize = settings.DefaultTmpFontSize;
        text.color = settings.DefaultTextColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (settings.DefaultTmpFont != null)
            text.font = settings.DefaultTmpFont;
    }

    static void BuildScrollView(GameObject root, float width, float height, PrefabMcpSettings settings)
    {
        AddImage(root, settings.DefaultImageColor);
        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        GameObject viewportObject = CreateUiObject("Viewport", root.transform, width, height);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        StretchToParent(viewport);
        AddImage(viewportObject, Color.white);
        Mask mask = viewportObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = CreateUiObject("Content", viewport, width, height);
        RectTransform content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, height);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
    }

    static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    static string DefaultUiElementName(string type)
    {
        switch (type)
        {
            case "tmptext": return "Text";
            case "verticallayout": return "VerticalLayout";
            case "scrollview": return "ScrollView";
            default: return char.ToUpperInvariant(type[0]) + type.Substring(1);
        }
    }

    static string ValidatePrefab(ValidateRequest command)
    {
        if (!TryGetEditTarget(command, out EditTarget editTarget, out string error))
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 500);
        try
        {
            var issues = new List<IssueInfo>();
            foreach (Transform node in EnumerateHierarchy(editTarget.Root))
            {
                Component[] components = node.GetComponents<Component>();
                for (int index = 0; index < components.Length && issues.Count < limit; index++)
                {
                    Component component = components[index];
                    if (component == null)
                    {
                        issues.Add(new IssueInfo
                        {
                            kind = "missingScript",
                            hierarchyPath = GetHierarchyPath(node, editTarget.Root),
                            objectId = GetObjectId(node, editTarget.Root),
                            componentIndex = index,
                            message = "组件脚本丢失",
                        });
                        continue;
                    }

                    if (component is MonoBehaviour &&
                        (command.includeUnityComponents || IsProjectScript((MonoBehaviour)component)))
                        CollectUnassignedReferences(component, index, node, editTarget.Root, issues, limit);
                }
                if (issues.Count >= limit)
                    break;
            }

            return ToJson(new IssuesResponse
            {
                ok = true,
                message = issues.Count == 0
                    ? "未发现丢失脚本或项目脚本中未赋值的顶层对象引用"
                    : $"发现 {issues.Count} 项（默认仅检查 Assets 下的项目脚本；null 引用可能是可选字段）",
                issues = issues.ToArray(),
            });
        }
        finally
        {
            editTarget.Dispose();
        }
    }

    static void CollectNodes(Transform node, Transform root, int depth, int maxDepth, int limit,
        bool includeComponents, bool compact, string nameFilter, string componentTypeFilter, List<NodeInfo> output)
    {
        if (output.Count >= limit)
            return;

        Component[] components = node.GetComponents<Component>();
        bool nameMatches = string.IsNullOrEmpty(nameFilter) ||
            node.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        bool componentMatches = string.IsNullOrEmpty(componentTypeFilter) || components.Any(component =>
            component != null &&
            (component.GetType().Name.IndexOf(componentTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
             (component.GetType().FullName ?? string.Empty).IndexOf(componentTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0));

        var info = new NodeInfo
        {
            objectId = GetObjectId(node, root),
            hierarchyPath = compact ? null : GetHierarchyPath(node, root),
            name = node.name,
            depth = depth,
            activeSelf = node.gameObject.activeSelf,
        };

        if (includeComponents)
        {
            info.components = components.Select((component, index) => new ComponentInfo
            {
                componentIndex = index,
                missing = component == null,
                type = compact || component == null ? null : component.GetType().FullName,
                shortType = component == null ? "MissingScript" : component.GetType().Name,
            }).ToArray();
        }

        if (nameMatches && componentMatches)
            output.Add(info);
        if (depth >= maxDepth)
            return;
        for (int i = 0; i < node.childCount; i++)
            CollectNodes(node.GetChild(i), root, depth + 1, maxDepth, limit, includeComponents,
                compact, nameFilter, componentTypeFilter, output);
    }

    static IEnumerable<Transform> EnumerateHierarchy(Transform root)
    {
        yield return root;
        for (int i = 0; i < root.childCount; i++)
        {
            foreach (Transform child in EnumerateHierarchy(root.GetChild(i)))
                yield return child;
        }
    }

    static CandidateInfo CreateCandidate(Transform root, Transform node, int componentIndex, Type type)
    {
        return new CandidateInfo
        {
            objectId = GetObjectId(node, root),
            hierarchyPath = GetHierarchyPath(node, root),
            name = node.name,
            sourceComponentIndex = componentIndex,
            sourceType = FriendlyTypeName(type),
        };
    }

    static int CandidateScore(string propertyPath, CandidateInfo candidate)
    {
        string field = RootFieldName(propertyPath).ToLowerInvariant();
        string name = candidate.name.ToLowerInvariant();
        string type = candidate.sourceType.ToLowerInvariant();
        int score = 0;
        if (field == name) score += 100;
        if (field.Contains(name) || name.Contains(field)) score += 40;
        string trimmed = field.Replace("m_", string.Empty).Replace("_", string.Empty);
        if (trimmed.EndsWith(type.Replace("unityengine.", string.Empty).ToLowerInvariant())) score += 20;
        return score;
    }

    static AssetCandidateInfo CreateAssetCandidate(UnityEngine.Object asset, string assetPath,
        string objectId, int componentIndex)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId);
        return new AssetCandidateInfo
        {
            assetPath = assetPath,
            assetName = asset.name,
            assetType = FriendlyTypeName(asset.GetType()),
            assetLocalId = localId,
            assetObjectId = objectId,
            assetComponentIndex = componentIndex,
        };
    }

    static int AssetCandidateScore(string propertyPath, AssetCandidateInfo candidate)
    {
        string field = RootFieldName(propertyPath).ToLowerInvariant().Replace("_", string.Empty);
        string name = (candidate.assetName ?? string.Empty).ToLowerInvariant().Replace("_", string.Empty);
        string fileName = Path.GetFileNameWithoutExtension(candidate.assetPath).ToLowerInvariant().Replace("_", string.Empty);
        int score = 0;
        if (field == name || field == fileName) score += 100;
        if (field.Contains(name) || name.Contains(field)) score += 40;
        if (field.Contains(fileName) || fileName.Contains(field)) score += 30;
        return score;
    }

    static bool TryGetObjectReferenceProperty(Component component, string propertyPath,
        out SerializedObject serializedObject, out SerializedProperty property, out Type expectedType, out string error)
    {
        serializedObject = new SerializedObject(component);
        property = serializedObject.FindProperty(propertyPath);
        expectedType = null;
        if (property == null)
        {
            error = $"找不到序列化字段 {propertyPath}";
            return false;
        }
        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            error = $"字段 {propertyPath} 不是对象引用字段";
            return false;
        }

        System.Reflection.FieldInfo reflected = FindField(component.GetType(), RootFieldName(propertyPath));
        if (reflected == null || !typeof(UnityEngine.Object).IsAssignableFrom(reflected.FieldType))
        {
            error = $"无法确定字段 {propertyPath} 的 UnityEngine.Object 类型；当前只支持顶层对象引用字段";
            return false;
        }

        expectedType = reflected.FieldType;
        error = null;
        return true;
    }

    static UnityEngine.Object ResolveAssetReference(AssetReferenceRequest command, Type expectedType, out string error)
    {
        error = null;
        string path = (command.assetPath ?? string.Empty).Replace('\\', '/');
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
        {
            error = "找不到项目资产: " + command.assetPath;
            return null;
        }

        if (typeof(Component).IsAssignableFrom(expectedType))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error = "组件资产引用必须来自 Prefab: " + path;
                return null;
            }
            if (string.IsNullOrEmpty(command.assetObjectId) || command.assetComponentIndex < 0)
            {
                error = "组件资产候选必须提供 assetObjectId 和 assetComponentIndex";
                return null;
            }
            if (!TryResolveObject(prefab.transform, command.assetObjectId, out Transform target, out error))
                return null;
            if (!TryResolveComponent(target, command.assetComponentIndex, out Component component, out error))
                return null;
            return component;
        }

        if (expectedType == typeof(GameObject))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error = "GameObject 资产引用必须来自 Prefab: " + path;
                return null;
            }
            if (string.IsNullOrEmpty(command.assetObjectId))
            {
                error = "GameObject 资产候选必须提供 assetObjectId；根节点为 0";
                return null;
            }
            return TryResolveObject(prefab.transform, command.assetObjectId, out Transform target, out error)
                ? target.gameObject
                : null;
        }

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset == null || !expectedType.IsInstanceOfType(asset))
                continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId);
            if (command.assetLocalId != 0 && localId != command.assetLocalId)
                continue;
            if (!string.IsNullOrEmpty(command.assetName) && asset.name != command.assetName)
                continue;
            if (!string.IsNullOrEmpty(command.assetType) && FriendlyTypeName(asset.GetType()) != command.assetType)
                continue;
            return asset;
        }

        error = $"资产 {path} 中找不到类型为 {FriendlyTypeName(expectedType)} 的匹配对象";
        return null;
    }

    static void CollectUnassignedReferences(Component component, int componentIndex, Transform node, Transform root, List<IssueInfo> issues, int limit)
    {
        var serializedObject = new SerializedObject(component);
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (issues.Count < limit && iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.depth != 0 || iterator.propertyPath == "m_Script")
                continue;
            if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue != null)
                continue;

            System.Reflection.FieldInfo field = FindField(component.GetType(), iterator.propertyPath);
            if (field == null)
                continue;

            issues.Add(new IssueInfo
            {
                kind = "unassignedReference",
                hierarchyPath = GetHierarchyPath(node, root),
                objectId = GetObjectId(node, root),
                componentIndex = componentIndex,
                componentType = component.GetType().FullName,
                propertyPath = iterator.propertyPath,
                message = "对象引用未赋值（可能是可选字段）",
            });
        }
    }

    static bool IsProjectScript(MonoBehaviour component)
    {
        MonoScript script = MonoScript.FromMonoBehaviour(component);
        string path = script == null ? null : AssetDatabase.GetAssetPath(script);
        return !string.IsNullOrEmpty(path) &&
            path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
    }

    static string DescribePropertyValue(SerializedProperty property, Transform root)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                return DescribeObjectReference(property.objectReferenceValue, root);
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Integer:
                return property.longValue.ToString();
            case SerializedPropertyType.Float:
                return property.doubleValue.ToString("G");
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[property.enumValueIndex]
                    : property.enumValueIndex.ToString();
            case SerializedPropertyType.Color:
                return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);
            case SerializedPropertyType.Vector2:
            {
                Vector2 v = property.vector2Value;
                return FormatFloats(v.x, v.y);
            }
            case SerializedPropertyType.Vector3:
            {
                Vector3 v = property.vector3Value;
                return FormatFloats(v.x, v.y, v.z);
            }
            case SerializedPropertyType.Vector4:
            {
                Vector4 v = property.vector4Value;
                return FormatFloats(v.x, v.y, v.z, v.w);
            }
            case SerializedPropertyType.Quaternion:
            {
                Quaternion q = property.quaternionValue;
                return FormatFloats(q.x, q.y, q.z, q.w);
            }
            case SerializedPropertyType.Rect:
            {
                Rect r = property.rectValue;
                return FormatFloats(r.x, r.y, r.width, r.height);
            }
            case SerializedPropertyType.Vector2Int:
            {
                Vector2Int v = property.vector2IntValue;
                return v.x + "," + v.y;
            }
            case SerializedPropertyType.Vector3Int:
            {
                Vector3Int v = property.vector3IntValue;
                return v.x + "," + v.y + "," + v.z;
            }
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString();
            case SerializedPropertyType.Character:
                return ((char)property.intValue).ToString();
            default:
                return property.isArray ? $"array(size={property.arraySize})" : "<" + property.propertyType + ">";
        }
    }

    static string DescribeObjectReference(UnityEngine.Object value, Transform root)
    {
        if (value == null)
            return "null";
        string assetPath = AssetDatabase.GetAssetPath(value);
        if (!string.IsNullOrEmpty(assetPath))
            return "asset:" + assetPath;
        if (value is Component component && component.transform.root == root)
            return $"prefab:{GetObjectId(component.transform, root)}#{GetComponentIndex(component)}:{component.GetType().Name}";
        if (value is GameObject gameObject && gameObject.transform.root == root)
            return $"prefab:{GetObjectId(gameObject.transform, root)}#-1:GameObject";
        return value.name + ":" + value.GetType().Name;
    }

    static int GetComponentIndex(Component component)
    {
        Component[] components = component.gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] == component)
                return i;
        return -1;
    }

    static bool TryResolveObject(Transform root, string objectId, out Transform result, out string error)
    {
        result = root;
        error = null;
        if (string.IsNullOrEmpty(objectId) || objectId == "0")
            return true;

        string[] segments = objectId.Split('/');
        if (segments.Length == 0 || segments[0] != "0")
        {
            error = $"objectId 格式无效: {objectId}（根节点必须为 0）";
            return false;
        }

        for (int i = 1; i < segments.Length; i++)
        {
            if (!int.TryParse(segments[i], out int childIndex) || childIndex < 0 || childIndex >= result.childCount)
            {
                error = $"objectId 不存在或层级已变化: {objectId}";
                result = null;
                return false;
            }
            result = result.GetChild(childIndex);
        }
        return true;
    }

    static bool TryResolveComponent(Transform target, int componentIndex, out Component component, out string error)
    {
        Component[] components = target.GetComponents<Component>();
        if (componentIndex < 0 || componentIndex >= components.Length)
        {
            component = null;
            error = $"节点 {target.name} 上不存在 componentIndex={componentIndex}";
            return false;
        }
        component = components[componentIndex];
        if (component == null)
        {
            error = $"节点 {target.name} 的 componentIndex={componentIndex} 是丢失脚本";
            return false;
        }
        error = null;
        return true;
    }

    static string GetObjectId(Transform transform, Transform root)
    {
        var indices = new Stack<int>();
        Transform current = transform;
        while (current != root)
        {
            if (current.parent == null)
                return null;
            indices.Push(current.GetSiblingIndex());
            current = current.parent;
        }
        return indices.Count == 0 ? "0" : "0/" + string.Join("/", indices);
    }

    static string GetHierarchyPath(Transform transform, Transform root)
    {
        var names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
                break;
            current = current.parent;
        }
        return string.Join("/", names);
    }

    static System.Reflection.FieldInfo FindField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            System.Reflection.FieldInfo field = current.GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
                return field;
        }
        return null;
    }

    static string RootFieldName(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return propertyPath;
        int dot = propertyPath.IndexOf('.');
        return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
    }

    static string FriendlyTypeName(Type type)
    {
        return type.FullName ?? type.Name;
    }

    static string[] NormalizeSearchFolders(string[] folders)
    {
        if (folders == null || folders.Length == 0)
            return null;
        var valid = new List<string>();
        foreach (string folder in folders)
        {
            string normalized = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(normalized))
                throw new ArgumentException("无效的搜索目录: " + folder);
            valid.Add(normalized);
        }
        return valid.ToArray();
    }

    static string ValidatePrefabPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "prefabPath 不能为空";
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return "prefabPath 必须是 Assets/ 下的 .prefab 路径";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            return "找不到 Prefab: " + path;
        return null;
    }

    static string ValidateProject(string expectedProjectPath)
    {
        if (string.IsNullOrEmpty(expectedProjectPath))
            return null;
        string expected = Path.GetFullPath(expectedProjectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actual = ProjectPath();
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"连接到了错误的 Unity 项目。期望: {expected}，实际: {actual}";
    }

    static string ProjectPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    static int ConfiguredLimit(int value, int hardMaximum)
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        int maximum = Math.Min(settings.MaximumQueryLimit, hardMaximum);
        int fallback = Math.Min(settings.DefaultQueryLimit, maximum);
        return value <= 0 ? fallback : Math.Min(value, maximum);
    }

    static string ToJson(object response)
    {
        return JsonConvert.SerializeObject(response);
    }

    static string Fail(string error)
    {
        return ToJson(new ResponseBase { ok = false, error = error });
    }
}
