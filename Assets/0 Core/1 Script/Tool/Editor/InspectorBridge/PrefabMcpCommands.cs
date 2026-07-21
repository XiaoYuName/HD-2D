using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 面向 MCP 的低 token Prefab 查询与编辑命令。
/// 使用 sibling-index objectId，避免重名节点导致错误绑定。
/// 结构化批量编辑（prefab.edit）在 PrefabMcpEditCommands.cs 中。
/// </summary>
public static partial class PrefabMcpCommands
{
    [Serializable]
    sealed class Command
    {
        public string action;
        public string expectedProjectPath;
        public string query;
        public string[] searchFolders;
        public int maxResults;
        public string prefabPath;
        public int maxDepth;
        public bool includeComponents;
        public string nameFilter;
        public string componentTypeFilter;
        public string objectId;
        public int componentIndex;
        public string propertyPath;
        public string fieldNameFilter;
        public bool onlyObjectReferences;
        public string candidateRootObjectId;
        public string sourceObjectId;
        public int sourceComponentIndex;
        public bool clear;
        public bool apply;
        public bool onlyUnassigned;
        public string assetPath;
        public string assetName;
        public string assetType;
        public long assetLocalId;
        public string assetObjectId;
        public int assetComponentIndex;
        public string parentObjectId;
        public string elementType;
        public string elementName;
        public string label;
        public float width;
        public float height;
        public EditOp[] operations;
    }

    [Serializable]
    sealed class Response
    {
        public bool ok;
        public string error;
        public string message;
        public string unityVersion;
        public string projectPath;
        public bool compiling;
        public PrefabInfo[] prefabs;
        public NodeInfo[] nodes;
        public FieldInfoDto[] fields;
        public CandidateInfo[] candidates;
        public AssetCandidateInfo[] assetCandidates;
        public IssueInfo[] issues;
        public AssignmentInfo assignment;
        public SettingsInfo settings;
        public CreationInfo creation;
        public EditInfo edit;
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

    public static string Dispatch(string requestJson)
    {
        Command command;
        try
        {
            command = JsonUtility.FromJson<Command>(requestJson);
        }
        catch (Exception e)
        {
            return Fail("请求 JSON 解析失败: " + e.Message);
        }

        if (command == null)
            return Fail("请求为空");

        string projectError = ValidateProject(command.expectedProjectPath);
        if (projectError != null)
            return Fail(projectError);

        try
        {
            switch (command.action)
            {
                case "prefab.status": return Status();
                case "prefab.settings": return GetSettings();
                case "prefab.find": return FindPrefabs(command);
                case "prefab.tree": return GetPrefabTree(command);
                case "prefab.fields": return GetComponentFields(command);
                case "prefab.candidates": return FindBindingCandidates(command);
                case "prefab.assign": return AssignObjectReference(command);
                case "prefab.assetCandidates": return FindAssetCandidates(command);
                case "prefab.assignAsset": return AssignAssetReference(command);
                case "prefab.createUi": return CreateUiElement(command);
                case "prefab.edit": return EditPrefab(command);
                case "prefab.validate": return ValidatePrefab(command);
                default: return Fail("未知 Prefab MCP action: " + command.action);
            }
        }
        catch (Exception e)
        {
            return Fail(e.ToString());
        }
    }

    static string Status()
    {
        return ToJson(new Response
        {
            ok = true,
            message = "Unity Prefab MCP bridge is ready",
            unityVersion = Application.unityVersion,
            projectPath = ProjectPath(),
            compiling = EditorApplication.isCompiling,
        });
    }

    static string GetSettings()
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        return ToJson(new Response
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

    static string FindPrefabs(Command command)
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

        return ToJson(new Response
        {
            ok = true,
            message = $"找到 {prefabs.Length} 个 Prefab（最多返回 {limit} 个）",
            prefabs = prefabs,
        });
    }

    static string GetPrefabTree(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        int maxDepth = command.maxDepth <= 0 ? 4 : Math.Min(command.maxDepth, 64);
        int limit = ConfiguredLimit(command.maxResults, 1000);
        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            var nodes = new List<NodeInfo>();
            CollectNodes(root.transform, root.transform, 0, maxDepth, limit, command.includeComponents,
                command.nameFilter, command.componentTypeFilter, nodes);
            return ToJson(new Response
            {
                ok = true,
                message = $"返回 {nodes.Count} 个节点，最大深度 {maxDepth}，最多 {limit} 个",
                nodes = nodes.ToArray(),
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string GetComponentFields(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.objectId, out Transform target, out error))
                return Fail(error);
            if (!TryResolveComponent(target, command.componentIndex, out Component component, out error))
                return Fail(error);

            var serializedObject = new SerializedObject(component);
            if (!string.IsNullOrEmpty(command.propertyPath))
            {
                var childFields = new List<FieldInfoDto>();
                string childError = CollectChildProperties(serializedObject, component, command, root.transform, childFields);
                if (childError != null)
                    return Fail(childError);
                return ToJson(new Response
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
                    value = DescribePropertyValue(iterator, root.transform),
                });
            }

            return ToJson(new Response
            {
                ok = true,
                message = $"{component.GetType().Name} 有 {fields.Count} 个顶层序列化字段",
                fields = fields.ToArray(),
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string FindBindingCandidates(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 200);
        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.objectId, out Transform target, out error))
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

            Transform candidateRoot = root.transform;
            if (!string.IsNullOrEmpty(command.candidateRootObjectId) &&
                !TryResolveObject(root.transform, command.candidateRootObjectId, out candidateRoot, out error))
                return Fail(error);

            var candidates = new List<CandidateInfo>();
            foreach (Transform node in EnumerateHierarchy(candidateRoot))
            {
                if (expectedType.IsAssignableFrom(typeof(GameObject)))
                {
                    candidates.Add(CreateCandidate(root.transform, node, -1, node.gameObject.GetType()));
                }

                Component[] components = node.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component candidate = components[i];
                    if (candidate != null && expectedType.IsAssignableFrom(candidate.GetType()))
                        candidates.Add(CreateCandidate(root.transform, node, i, candidate.GetType()));
                }
            }

            CandidateInfo[] result = candidates
                .OrderByDescending(item => CandidateScore(command.propertyPath, item))
                .ThenBy(item => item.hierarchyPath, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();

            return ToJson(new Response
            {
                ok = true,
                message = $"字段类型 {FriendlyTypeName(expectedType)}，返回 {result.Length} 个候选项",
                candidates = result,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string AssignObjectReference(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.objectId, out Transform target, out error))
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
                if (!TryResolveObject(root.transform, command.sourceObjectId, out Transform sourceTransform, out error))
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

            string previous = DescribeObjectReference(property.objectReferenceValue, root.transform);
            string next = DescribeObjectReference(source, root.transform);
            var assignment = new AssignmentInfo
            {
                target = $"{GetHierarchyPath(target, root.transform)} :: {component.GetType().Name}[{command.componentIndex}].{command.propertyPath}",
                source = next,
                previousValue = previous,
                newValue = next,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(command.prefabPath);
                if (error != null)
                    return Fail(error);
                assignment.backupPath = PrefabMcpSettings.GetOrCreate().CreatePrefabBackup(command.prefabPath);
                property.objectReferenceValue = source;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, command.prefabPath);
                if (saved == null)
                    return Fail("Prefab 保存失败，Unity 未返回已保存资源");
                AssetDatabase.SaveAssets();
            }

            return ToJson(new Response
            {
                ok = true,
                message = command.apply ? "引用已写入并保存" : "预检通过；apply=false，未修改 Prefab",
                assignment = assignment,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string FindAssetCandidates(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 500);
        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.objectId, out Transform target, out error))
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

            return ToJson(new Response
            {
                ok = true,
                message = $"字段类型 {FriendlyTypeName(expectedType)}，返回 {result.Length} 个项目资产候选项",
                assetCandidates = result,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string AssignAssetReference(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);
        if (string.IsNullOrEmpty(command.assetPath))
            return Fail("assetPath 不能为空");

        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.objectId, out Transform target, out error))
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

            string previous = DescribeObjectReference(property.objectReferenceValue, root.transform);
            string next = DescribeObjectReference(source, root.transform);
            var assignment = new AssignmentInfo
            {
                target = $"{GetHierarchyPath(target, root.transform)} :: {component.GetType().Name}[{command.componentIndex}].{command.propertyPath}",
                source = next,
                previousValue = previous,
                newValue = next,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(command.prefabPath);
                if (error != null)
                    return Fail(error);
                assignment.backupPath = PrefabMcpSettings.GetOrCreate().CreatePrefabBackup(command.prefabPath);
                property.objectReferenceValue = source;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, command.prefabPath);
                if (saved == null)
                    return Fail("Prefab 保存失败，Unity 未返回已保存资源");
                AssetDatabase.SaveAssets();
            }

            return ToJson(new Response
            {
                ok = true,
                message = command.apply ? "资产引用已写入并保存" : "资产引用预检通过；apply=false，未修改 Prefab",
                assignment = assignment,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string CreateUiElement(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

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

        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            if (!TryResolveObject(root.transform, command.parentObjectId, out Transform parent, out error))
                return Fail(error);
            if (!(parent is RectTransform))
                return Fail($"父节点 {GetHierarchyPath(parent, root.transform)} 没有 RectTransform，不适合作为 UI 父节点");

            var creation = new CreationInfo
            {
                elementType = type,
                parentPath = GetHierarchyPath(parent, root.transform),
                hierarchyPath = GetHierarchyPath(parent, root.transform) + "/" + objectName,
                applied = command.apply,
            };

            if (command.apply)
            {
                error = ValidateWriteAllowed(command.prefabPath);
                if (error != null)
                    return Fail(error);
                creation.backupPath = settings.CreatePrefabBackup(command.prefabPath);

                GameObject created = BuildUiElement(type, objectName, command.label, parent, width, height, settings);
                creation.objectId = GetObjectId(created.transform, root.transform);
                creation.hierarchyPath = GetHierarchyPath(created.transform, root.transform);
                creation.createdObjects = EnumerateHierarchy(created.transform)
                    .Select(item => GetHierarchyPath(item, root.transform))
                    .ToArray();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, command.prefabPath);
                if (saved == null)
                    return Fail("Prefab 保存失败，Unity 未返回已保存资源");
                AssetDatabase.SaveAssets();
            }
            else
            {
                creation.createdObjects = type == "button"
                    ? new[] { creation.hierarchyPath, creation.hierarchyPath + "/Label" }
                    : type == "scrollview"
                        ? new[] { creation.hierarchyPath, creation.hierarchyPath + "/Viewport", creation.hierarchyPath + "/Viewport/Content" }
                        : new[] { creation.hierarchyPath };
            }

            return ToJson(new Response
            {
                ok = true,
                message = command.apply ? "UI 节点已创建并保存" : "UI 创建预检通过；apply=false，未修改 Prefab",
                creation = creation,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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

    static string ValidatePrefab(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);

        int limit = ConfiguredLimit(command.maxResults, 500);
        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            var issues = new List<IssueInfo>();
            foreach (Transform node in EnumerateHierarchy(root.transform))
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
                            hierarchyPath = GetHierarchyPath(node, root.transform),
                            objectId = GetObjectId(node, root.transform),
                            componentIndex = index,
                            message = "组件脚本丢失",
                        });
                        continue;
                    }

                    // Unity 内置组件包含大量允许为空的内部引用；MVP 只检查业务脚本，减少噪音。
                    if (component is MonoBehaviour)
                        CollectUnassignedReferences(component, index, node, root.transform, issues, limit);
                }
                if (issues.Count >= limit)
                    break;
            }

            return ToJson(new Response
            {
                ok = true,
                message = issues.Count == 0
                    ? "未发现丢失脚本或未赋值的顶层对象引用"
                    : $"发现 {issues.Count} 项（null 引用可能是有意保留，请结合业务判断）",
                issues = issues.ToArray(),
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CollectNodes(Transform node, Transform root, int depth, int maxDepth, int limit,
        bool includeComponents, string nameFilter, string componentTypeFilter, List<NodeInfo> output)
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
            hierarchyPath = GetHierarchyPath(node, root),
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
                type = component == null ? null : component.GetType().FullName,
                shortType = component == null ? "MissingScript" : component.GetType().Name,
            }).ToArray();
        }

        if (nameMatches && componentMatches)
            output.Add(info);
        if (depth >= maxDepth)
            return;
        for (int i = 0; i < node.childCount; i++)
            CollectNodes(node.GetChild(i), root, depth + 1, maxDepth, limit, includeComponents,
                nameFilter, componentTypeFilter, output);
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

    static UnityEngine.Object ResolveAssetReference(Command command, Type expectedType, out string error)
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

    static string ValidateWriteAllowed(string prefabPath)
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        if (!settings.AllowPrefabWrites)
            return $"Prefab 写入已被项目配置禁止。请在 Project Settings > Unity Prefab MCP 中启用；配置资产: {PrefabMcpSettings.AssetPath}";
        if (!settings.IsPrefabWritePathAllowed(prefabPath))
            return $"Prefab 不在允许写入的目录中: {prefabPath}";
        return null;
    }

    static int ConfiguredLimit(int value, int hardMaximum)
    {
        PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
        int maximum = Math.Min(settings.MaximumQueryLimit, hardMaximum);
        int fallback = Math.Min(settings.DefaultQueryLimit, maximum);
        return value <= 0 ? fallback : Math.Min(value, maximum);
    }

    static string ToJson(Response response)
    {
        return JsonUtility.ToJson(response);
    }

    static string Fail(string error)
    {
        return ToJson(new Response { ok = false, error = error });
    }
}
