using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>只读查询：找 Prefab、读层级、读字段、找绑定候选、体检。</summary>
    static class QueryTools
    {
        /// <summary>compact 模式剔除的组件：没有可读写字段，却几乎每个 UI 节点都有。</summary>
        static readonly string[] NoiseComponentTypes = { "CanvasRenderer" };

        public static string FindPrefabs(FindPrefabsRequest command)
        {
            int limit = PrefabMcpSettings.Limit(command.maxResults, 200);
            string filter = string.IsNullOrWhiteSpace(command.query)
                ? "t:Prefab"
                : command.query.Trim() + " t:Prefab";

            string[] folders = PrefabAddress.NormalizeSearchFolders(command.searchFolders);
            string[] guids = folders == null
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, folders);

            string[] prefabs = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();

            return BridgeJson.Serialize(new PrefabListResponse
            {
                message = $"找到 {prefabs.Length} 个 Prefab（最多返回 {limit} 个）",
                prefabs = prefabs.Length == 0 ? null : prefabs,
            });
        }

        public static string GetTree(PrefabTreeRequest command)
        {
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);
            using (target)
            {
                if (!PrefabAddress.TryGetObject(target.RootTf, command.rootObjectId, out Transform queryRootTf, out error))
                    return BridgeJson.Fail(error);

                int maxDepth = command.maxDepth <= 0 ? 4 : Math.Min(command.maxDepth, 64);
                int limit = PrefabMcpSettings.Limit(command.maxResults, 1000);
                bool compact = command.compact ?? true;
                // 显式按噪音组件过滤时要保留它，否则查询看起来什么都没命中。
                bool keepNoise = !string.IsNullOrEmpty(command.componentTypeFilter) &&
                    NoiseComponentTypes.Any(noise => PrefabAddress.MatchesFilter(command.componentTypeFilter, noise));

                var nodes = new List<NodeInfo>();
                CollectNodes(queryRootTf, target.RootTf, 0, maxDepth, limit, command.includeComponents ?? true,
                    compact, keepNoise, command.nameFilter, command.componentTypeFilter, nodes);

                string scope = string.IsNullOrEmpty(command.rootObjectId) ? "0" : command.rootObjectId;
                return BridgeJson.Serialize(new TreeResponse
                {
                    message = $"返回子树 {scope} 的 {nodes.Count} 个节点，最大相对深度 {maxDepth}，最多 {limit} 个",
                    nodes = nodes.OrNull(),
                });
            }
        }

        static void CollectNodes(Transform nodeTf, Transform rootTf, int depth, int maxDepth, int limit,
            bool includeComponents, bool compact, bool keepNoise, string nameFilter, string componentTypeFilter,
            List<NodeInfo> output)
        {
            if (output.Count >= limit)
                return;

            Component[] components = nodeTf.GetComponents<Component>();
            bool nameMatches = PrefabAddress.MatchesFilter(nameFilter, nodeTf.name);
            bool componentMatches = string.IsNullOrEmpty(componentTypeFilter) || components.Any(component =>
                component != null &&
                PrefabAddress.MatchesFilter(componentTypeFilter, component.GetType().Name, component.GetType().FullName));

            var info = new NodeInfo
            {
                objectId = PrefabAddress.GetObjectId(nodeTf, rootTf),
                hierarchyPath = compact ? null : PrefabAddress.GetHierarchyPath(nodeTf, rootTf),
                name = nodeTf.name,
                depth = compact ? (int?)null : depth,
                activeSelf = nodeTf.gameObject.activeSelf ? (bool?)null : false,
            };

            if (includeComponents)
            {
                var infos = new List<ComponentInfo>();
                for (int index = 0; index < components.Length; index++)
                {
                    Component component = components[index];
                    bool missing = component == null;
                    // 剔除噪音组件不影响其余条目的 componentIndex：下标随每条一起发。
                    if (!missing && compact && !keepNoise && NoiseComponentTypes.Contains(component.GetType().Name))
                        continue;
                    infos.Add(new ComponentInfo
                    {
                        componentIndex = index,
                        missing = missing.OrNull(),
                        type = compact || missing ? null : component.GetType().FullName,
                        shortType = missing ? "MissingScript" : component.GetType().Name,
                    });
                }
                info.components = infos.OrNull();
            }

            if (nameMatches && componentMatches)
                output.Add(info);
            if (depth >= maxDepth)
                return;
            for (int i = 0; i < nodeTf.childCount; i++)
                CollectNodes(nodeTf.GetChild(i), rootTf, depth + 1, maxDepth, limit, includeComponents,
                    compact, keepNoise, nameFilter, componentTypeFilter, output);
        }

        public static string GetComponentFields(ComponentFieldsRequest command)
        {
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);
            using (target)
            {
                int limit = PrefabMcpSettings.Limit(command.maxResults, 500);
                bool compact = command.compact ?? true;

                // 单目标沿用扁平 fields 响应；targets 非空时走批量，一次调用读多个组件。
                if (command.targets == null || command.targets.Length == 0)
                {
                    if (!PrefabAddress.TryGetObject(target.RootTf, command.objectId, out Transform targetTf, out error))
                        return BridgeJson.Fail(error);
                    if (!PrefabAddress.TryGetComponentAt(targetTf, command.componentIndex, out Component component, out error))
                        return BridgeJson.Fail(error);

                    var fields = new List<FieldDto>();
                    error = CollectFields(component, command, command.propertyPath, compact, limit, target.RootTf, fields);
                    if (error != null)
                        return BridgeJson.Fail(error);
                    return BridgeJson.Serialize(new FieldsResponse
                    {
                        message = FieldsMessage(component, command.propertyPath, fields.Count, limit),
                        fields = fields.OrNull(),
                    });
                }

                var componentFields = new List<ComponentFieldsInfo>();
                int total = 0;
                foreach (ComponentFieldsTarget item in command.targets)
                {
                    if (!PrefabAddress.TryGetObject(target.RootTf, item.objectId, out Transform targetTf, out error))
                    {
                        componentFields.Add(new ComponentFieldsInfo
                        {
                            objectId = item.objectId,
                            componentIndex = -1,
                            error = error,
                        });
                        continue;
                    }

                    Component[] components = targetTf.GetComponents<Component>();
                    int first = item.componentIndex < 0 ? 0 : item.componentIndex;
                    int last = item.componentIndex < 0 ? components.Length - 1 : item.componentIndex;
                    for (int index = first; index <= last && total < limit; index++)
                    {
                        if (!PrefabAddress.TryGetComponentAt(targetTf, index, out Component component, out error))
                        {
                            componentFields.Add(new ComponentFieldsInfo
                            {
                                objectId = item.objectId,
                                componentIndex = index,
                                error = error,
                            });
                            continue;
                        }

                        var fields = new List<FieldDto>();
                        string fieldError = CollectFields(component, command, item.propertyPath, compact,
                            limit - total, target.RootTf, fields);
                        total += fields.Count;
                        componentFields.Add(new ComponentFieldsInfo
                        {
                            objectId = item.objectId,
                            componentIndex = index,
                            componentType = component.GetType().Name,
                            error = fieldError,
                            fields = fields.OrNull(),
                        });
                    }
                }

                return BridgeJson.Serialize(new FieldsResponse
                {
                    message = total >= limit
                        ? $"{componentFields.Count} 个组件，共 {total} 个字段，已达上限 {limit}（结果可能被截断，请缩小 targets 或加过滤）"
                        : $"{componentFields.Count} 个组件，共 {total} 个字段",
                    componentFields = componentFields.OrNull(),
                });
            }
        }

        /// <summary>读一个组件的字段：propertyPath 非空时展开该属性，否则列顶层字段。返回错误信息或 null。</summary>
        static string CollectFields(Component component, ComponentFieldsRequest command, string propertyPath,
            bool compact, int limit, Transform rootTf, List<FieldDto> fields)
        {
            var serializedObject = new SerializedObject(component);
            if (!string.IsNullOrEmpty(propertyPath))
                return CollectChildProperties(serializedObject, component, propertyPath, compact, limit, rootTf, fields);

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && fields.Count < limit)
            {
                enterChildren = false;
                if (iterator.depth != 0 || iterator.propertyPath == "m_Script")
                    continue;
                if (command.onlyObjectReferences && iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                if (command.onlyUnassigned &&
                    (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue != null))
                    continue;
                if (!PrefabAddress.MatchesFilter(command.fieldNameFilter, iterator.propertyPath, iterator.displayName))
                    continue;

                fields.Add(ToFieldDto(iterator, component, rootTf, compact));
            }
            return null;
        }

        /// <summary>展开嵌套结构或数组（数组返回 size + 各元素）。</summary>
        static string CollectChildProperties(SerializedObject serializedObject, Component component, string propertyPath,
            bool compact, int limit, Transform rootTf, List<FieldDto> fields)
        {
            SerializedProperty parentProperty = serializedObject.FindProperty(propertyPath);
            if (parentProperty == null)
                return $"找不到序列化字段 {propertyPath}";

            if (parentProperty.isArray && parentProperty.propertyType == SerializedPropertyType.Generic)
            {
                fields.Add(new FieldDto
                {
                    propertyPath = parentProperty.propertyPath + ".Array.size",
                    displayName = compact ? null : "Size",
                    propertyType = SerializedPropertyType.ArraySize.ToString(),
                    serializedType = compact ? null : "int",
                    fieldType = compact ? null : "int",
                    value = parentProperty.arraySize.ToString(),
                });
                for (int i = 0; i < parentProperty.arraySize && fields.Count < limit; i++)
                    fields.Add(ToFieldDto(parentProperty.GetArrayElementAtIndex(i), component, rootTf, compact));
                return null;
            }

            if (!parentProperty.hasVisibleChildren)
            {
                fields.Add(ToFieldDto(parentProperty, component, rootTf, compact));
                return null;
            }

            SerializedProperty end = parentProperty.GetEndProperty();
            SerializedProperty child = parentProperty.Copy();
            if (!child.NextVisible(true))
                return null;
            while (!SerializedProperty.EqualContents(child, end) && fields.Count < limit)
            {
                fields.Add(ToFieldDto(child, component, rootTf, compact));
                if (!child.NextVisible(false))
                    break;
            }
            return null;
        }

        /// <summary>
        /// compact=true 只保留改字段真正需要的 propertyPath / propertyType / value，
        /// 以及对象引用字段的 fieldType（绑定时要知道目标类型）；displayName 可由 propertyPath 推出，
        /// serializedType 与 fieldType 表达同一件事，都省掉。
        /// </summary>
        static FieldDto ToFieldDto(SerializedProperty property, Component component, Transform rootTf, bool compact)
        {
            bool isObjectReference = property.propertyType == SerializedPropertyType.ObjectReference;
            Type fieldType = compact && !isObjectReference
                ? null
                : PrefabAddress.GetFieldPathType(component.GetType(), property.propertyPath);
            return new FieldDto
            {
                propertyPath = property.propertyPath,
                displayName = compact ? null : property.displayName,
                propertyType = property.propertyType.ToString(),
                serializedType = compact ? null : property.type,
                fieldType = fieldType == null ? null : PrefabAddress.FriendlyTypeName(fieldType),
                value = PrefabAddress.GetPropertyValueText(property, rootTf),
            };
        }

        static string FieldsMessage(Component component, string propertyPath, int count, int limit)
        {
            string scope = string.IsNullOrEmpty(propertyPath)
                ? $"{component.GetType().Name} 有 {count} 个顶层序列化字段"
                : $"{propertyPath} 展开为 {count} 个子属性";
            return count >= limit ? scope + $"（已达上限 {limit}，结果可能被截断）" : scope;
        }

        /// <summary>
        /// 找一个对象引用字段能填什么。scope=auto 时按字段类型自动选：
        /// Component/GameObject 找当前层级内的节点，其余（ScriptableObject、Sprite…）找项目资产。
        /// 返回的 value 可直接交给 edit_prefab 的 setValue。
        /// </summary>
        public static string FindCandidates(CandidatesRequest command)
        {
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);
            using (target)
            {
                int limit = PrefabMcpSettings.Limit(command.maxResults, 500);
                if (!PrefabAddress.TryGetObject(target.RootTf, command.objectId, out Transform targetTf, out error))
                    return BridgeJson.Fail(error);
                if (!PrefabAddress.TryGetComponentAt(targetTf, command.componentIndex, out Component component, out error))
                    return BridgeJson.Fail(error);
                if (!PrefabAddress.TryGetObjectReferenceProperty(component, command.propertyPath,
                        out SerializedObject _, out SerializedProperty _, out Type expectedType, out error))
                    return BridgeJson.Fail(error);

                bool sceneType = typeof(Component).IsAssignableFrom(expectedType) || expectedType == typeof(GameObject);
                string scope = string.IsNullOrWhiteSpace(command.scope) ? "auto" : command.scope.Trim();
                if (scope == "auto")
                    scope = sceneType ? "prefab" : "asset";
                if (scope != "prefab" && scope != "asset")
                    return BridgeJson.Fail($"未知 scope: {command.scope}（可选 auto/prefab/asset）");

                List<CandidateInfo> candidates = scope == "prefab"
                    ? CollectHierarchyCandidates(target, command, expectedType, out error)
                    : CollectAssetCandidates(command, expectedType, limit);
                if (candidates == null)
                    return BridgeJson.Fail(error);

                string field = PrefabAddress.RootFieldName(command.propertyPath);
                CandidateInfo[] result = candidates
                    .GroupBy(item => item.value)
                    .Select(group => group.First())
                    .OrderByDescending(item => Score(field, item))
                    .ThenBy(item => item.value, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .ToArray();

                return BridgeJson.Serialize(new CandidatesResponse
                {
                    message = $"字段类型 {PrefabAddress.FriendlyTypeName(expectedType)}，" +
                        $"在 {scope} 范围返回 {result.Length} 个候选项（value 可直接用于 edit_prefab setValue）",
                    candidates = result.Length == 0 ? null : result,
                });
            }
        }

        static List<CandidateInfo> CollectHierarchyCandidates(EditTarget target, CandidatesRequest command,
            Type expectedType, out string error)
        {
            Transform candidateRootTf = target.RootTf;
            if (!string.IsNullOrEmpty(command.candidateRootObjectId) &&
                !PrefabAddress.TryGetObject(target.RootTf, command.candidateRootObjectId, out candidateRootTf, out error))
                return null;

            error = null;
            var candidates = new List<CandidateInfo>();
            foreach (Transform nodeTf in PrefabAddress.EnumerateHierarchy(candidateRootTf))
            {
                if (!string.IsNullOrEmpty(command.query) &&
                    nodeTf.name.IndexOf(command.query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string objectId = PrefabAddress.GetObjectId(nodeTf, target.RootTf);
                if (expectedType.IsAssignableFrom(typeof(GameObject)))
                    candidates.Add(new CandidateInfo
                    {
                        value = $"object:{objectId}#-1",
                        name = nodeTf.name,
                        type = nameof(GameObject),
                    });

                Component[] components = nodeTf.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component candidate = components[i];
                    if (candidate != null && expectedType.IsAssignableFrom(candidate.GetType()))
                        candidates.Add(new CandidateInfo
                        {
                            value = $"object:{objectId}#{i}",
                            name = nodeTf.name,
                            type = candidate.GetType().Name,
                        });
                }
            }
            return candidates;
        }

        static List<CandidateInfo> CollectAssetCandidates(CandidatesRequest command, Type expectedType, int limit)
        {
            string[] configuredFolders = command.searchFolders == null || command.searchFolders.Length == 0
                ? PrefabMcpSettings.GetOrCreate().DefaultAssetSearchFolders
                : command.searchFolders;
            string[] folders = PrefabAddress.NormalizeSearchFolders(configuredFolders);
            var candidates = new List<CandidateInfo>();

            bool wantsComponent = typeof(Component).IsAssignableFrom(expectedType);
            bool wantsGameObject = expectedType == typeof(GameObject);
            string typeFilter = wantsComponent || wantsGameObject ? "Prefab" : expectedType.Name;
            string filter = string.IsNullOrWhiteSpace(command.query)
                ? "t:" + typeFilter
                : command.query.Trim() + " t:" + typeFilter;
            string[] guids = folders == null
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, folders);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (wantsGameObject)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                        candidates.Add(new CandidateInfo
                        {
                            value = "asset:" + path,
                            name = prefab.name,
                            type = nameof(GameObject),
                        });
                }
                else if (wantsComponent)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        continue;
                    foreach (Transform nodeTf in PrefabAddress.EnumerateHierarchy(prefab.transform))
                    {
                        Component[] components = nodeTf.GetComponents<Component>();
                        for (int index = 0; index < components.Length; index++)
                        {
                            Component candidate = components[index];
                            if (candidate == null || !expectedType.IsInstanceOfType(candidate))
                                continue;
                            // @objectId#componentIndex 精确定位到 Prefab 内部的组件，不受重名影响。
                            candidates.Add(new CandidateInfo
                            {
                                value = $"asset:{path}@{PrefabAddress.GetObjectId(nodeTf, prefab.transform)}#{index}",
                                name = nodeTf.name,
                                type = candidate.GetType().Name,
                            });
                        }
                    }
                }
                else
                {
                    UnityEngine.Object[] matches = AssetDatabase.LoadAllAssetsAtPath(path)
                        .Where(asset => asset != null && expectedType.IsInstanceOfType(asset))
                        .ToArray();
                    foreach (UnityEngine.Object asset in matches)
                    {
                        // 单个匹配就不带子资产名；多个（图集里的 Sprite 等）才需要 #名字 区分。
                        candidates.Add(new CandidateInfo
                        {
                            value = matches.Length == 1 ? "asset:" + path : $"asset:{path}#{asset.name}",
                            name = asset.name,
                            type = asset.GetType().Name,
                        });
                    }
                }

                if (candidates.Count >= limit * 4)
                    break;
            }
            return candidates;
        }

        /// <summary>字段名与候选名/类型的相似度，越像越靠前。</summary>
        static int Score(string propertyPath, CandidateInfo candidate)
        {
            string field = PrefabAddress.RootFieldName(propertyPath).ToLowerInvariant()
                .Replace("m_", string.Empty).Replace("_", string.Empty);
            string name = (candidate.name ?? string.Empty).ToLowerInvariant().Replace("_", string.Empty);
            string type = (candidate.type ?? string.Empty).ToLowerInvariant();
            int score = 0;
            if (field == name)
                score += 100;
            if (name.Length > 0 && (field.Contains(name) || name.Contains(field)))
                score += 40;
            if (type.Length > 0 && field.EndsWith(type, StringComparison.Ordinal))
                score += 20;
            return score;
        }

        public static string Validate(ValidateRequest command)
        {
            if (!EditTarget.TryResolve(command, out EditTarget target, out string error))
                return BridgeJson.Fail(error);
            using (target)
            {
                int limit = PrefabMcpSettings.Limit(command.maxResults, 500);
                var issues = new List<IssueInfo>();
                foreach (Transform nodeTf in PrefabAddress.EnumerateHierarchy(target.RootTf))
                {
                    Component[] components = nodeTf.GetComponents<Component>();
                    for (int index = 0; index < components.Length && issues.Count < limit; index++)
                    {
                        Component component = components[index];
                        if (component == null)
                        {
                            issues.Add(new IssueInfo
                            {
                                kind = "missingScript",
                                objectId = PrefabAddress.GetObjectId(nodeTf, target.RootTf),
                                hierarchyPath = PrefabAddress.GetHierarchyPath(nodeTf, target.RootTf),
                                componentIndex = index,
                            });
                            continue;
                        }

                        if (component is MonoBehaviour behaviour &&
                            (command.includeUnityComponents || IsProjectScript(behaviour)))
                            CollectUnassigned(behaviour, index, nodeTf, target.RootTf, issues, limit);
                    }
                    if (issues.Count >= limit)
                        break;
                }

                return BridgeJson.Serialize(new IssuesResponse
                {
                    message = issues.Count == 0
                        ? "未发现丢失脚本或项目脚本中未赋值的顶层对象引用"
                        : $"发现 {issues.Count} 项（默认仅检查 Assets 下的项目脚本；unassignedReference 也可能是业务允许的可选字段）",
                    issues = issues.OrNull(),
                });
            }
        }

        static void CollectUnassigned(Component component, int componentIndex, Transform nodeTf, Transform rootTf,
            List<IssueInfo> issues, int limit)
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
                if (PrefabAddress.FindField(component.GetType(), iterator.propertyPath) == null)
                    continue;

                issues.Add(new IssueInfo
                {
                    kind = "unassignedReference",
                    objectId = PrefabAddress.GetObjectId(nodeTf, rootTf),
                    hierarchyPath = PrefabAddress.GetHierarchyPath(nodeTf, rootTf),
                    componentIndex = componentIndex,
                    componentType = component.GetType().Name,
                    propertyPath = iterator.propertyPath,
                });
            }
        }

        static bool IsProjectScript(MonoBehaviour component)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(component);
            string path = script == null ? null : AssetDatabase.GetAssetPath(script);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
