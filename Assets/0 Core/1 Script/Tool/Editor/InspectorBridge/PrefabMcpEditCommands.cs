using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// prefab.edit：单次请求内按顺序执行一批结构化编辑（重命名、增删移动节点、增删组件、改序列化值）。
/// 任一步失败即中止且不保存；apply=false 时全部在内存中预演后丢弃，是真正的 dry-run。
/// 目标是让 AI 用最少的往返和 token 完成一组改动：一次调用、一次备份、一次保存。
/// </summary>
public static partial class PrefabMcpCommands
{
    [Serializable]
    sealed class EditOp
    {
        // JsonUtility 反序列化不执行字段初始化器，可选标量一律用 string，空串/缺省表示"未提供"。
        public string op;
        public string objectId;
        public string parentObjectId;
        public string newName;
        public string active;        // "true" | "false"
        public string siblingIndex;  // 非空时解析为 int
        public string componentType;
        public int componentIndex;
        public string propertyPath;
        public string value;
        public string sourcePrefabPath;
    }

    [Serializable]
    sealed class EditInfo
    {
        public int operationCount;
        public int completed;
        public bool applied;
        public string backupPath;
        public EditOpResult[] results;
    }

    [Serializable]
    sealed class EditOpResult
    {
        public string op;
        public bool ok;
        public string error;
        public string objectId;
        public string hierarchyPath;
        public int componentIndex = -1;
        public string detail;
    }

    static string EditPrefab(Command command)
    {
        string error = ValidatePrefabPath(command.prefabPath);
        if (error != null)
            return Fail(error);
        if (command.operations == null || command.operations.Length == 0)
            return Fail("operations 不能为空");

        GameObject root = PrefabUtility.LoadPrefabContents(command.prefabPath);
        try
        {
            var results = new List<EditOpResult>();
            foreach (EditOp op in command.operations)
            {
                EditOpResult result = ExecuteEditOp(root.transform, op, command.prefabPath);
                results.Add(result);
                if (!result.ok)
                    break;
            }

            var edit = new EditInfo
            {
                operationCount = command.operations.Length,
                completed = results.Count(item => item.ok),
                results = results.ToArray(),
            };

            EditOpResult last = results[results.Count - 1];
            if (!last.ok)
                return ToJson(new Response
                {
                    ok = false,
                    error = $"操作 {results.Count}/{command.operations.Length}（{last.op}）失败: {last.error}；批次已中止，Prefab 未保存",
                    edit = edit,
                });

            if (command.apply)
            {
                error = ValidateWriteAllowed(command.prefabPath);
                if (error != null)
                    return Fail(error);
                edit.backupPath = PrefabMcpSettings.GetOrCreate().CreatePrefabBackup(command.prefabPath);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, command.prefabPath);
                if (saved == null)
                    return Fail("Prefab 保存失败，Unity 未返回已保存资源");
                AssetDatabase.SaveAssets();
                edit.applied = true;
            }

            return ToJson(new Response
            {
                ok = true,
                message = command.apply
                    ? $"已执行 {edit.completed} 个操作并保存"
                    : $"{edit.completed} 个操作预演成功；apply=false，未保存",
                edit = edit,
            });
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static EditOpResult ExecuteEditOp(Transform root, EditOp op, string prefabPath)
    {
        var result = new EditOpResult { op = op.op };
        try
        {
            return ExecuteEditOpCore(root, op, prefabPath, result);
        }
        catch (Exception e)
        {
            return FailOp(result, e.Message);
        }
    }

    static EditOpResult ExecuteEditOpCore(Transform root, EditOp op, string prefabPath, EditOpResult result)
    {
        string error;
        switch ((op.op ?? string.Empty).Trim())
        {
            case "rename":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (string.IsNullOrWhiteSpace(op.newName))
                    return FailOp(result, "rename 需要 newName");
                result.detail = target.name + " -> " + op.newName.Trim();
                target.name = op.newName.Trim();
                return OkOp(result, target, root);
            }
            case "setActive":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (!TryParseBool(op.active, out bool active))
                    return FailOp(result, "setActive 需要 active=\"true\"|\"false\"");
                target.gameObject.SetActive(active);
                result.detail = "activeSelf=" + (active ? "true" : "false");
                return OkOp(result, target, root);
            }
            case "reparent":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (target == root)
                    return FailOp(result, "不能移动 Prefab 根节点");
                if (string.IsNullOrEmpty(op.parentObjectId))
                    return FailOp(result, "reparent 需要 parentObjectId（根节点为 0）");
                if (!TryResolveObject(root, op.parentObjectId, out Transform parent, out error))
                    return FailOp(result, error);
                if (parent == target || parent.IsChildOf(target))
                    return FailOp(result, "不能把节点移动到它自己或它的子级下");
                string previous = GetHierarchyPath(target, root);
                target.SetParent(parent, false);
                if (!ApplySiblingIndex(target, op.siblingIndex, out error))
                    return FailOp(result, error);
                result.detail = previous + " -> " + GetHierarchyPath(target, root);
                return OkOp(result, target, root);
            }
            case "setSiblingIndex":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (target == root)
                    return FailOp(result, "不能调整 Prefab 根节点");
                if (string.IsNullOrWhiteSpace(op.siblingIndex))
                    return FailOp(result, "setSiblingIndex 需要 siblingIndex");
                if (!ApplySiblingIndex(target, op.siblingIndex, out error))
                    return FailOp(result, error);
                result.detail = "siblingIndex=" + target.GetSiblingIndex();
                return OkOp(result, target, root);
            }
            case "delete":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (target == root)
                    return FailOp(result, "不能删除 Prefab 根节点");
                result.hierarchyPath = GetHierarchyPath(target, root);
                result.detail = "已删除";
                UnityEngine.Object.DestroyImmediate(target.gameObject);
                result.ok = true;
                return result;
            }
            case "duplicate":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (target == root)
                    return FailOp(result, "不能复制 Prefab 根节点");
                GameObject copy = UnityEngine.Object.Instantiate(target.gameObject, target.parent);
                copy.name = string.IsNullOrWhiteSpace(op.newName) ? target.name : op.newName.Trim();
                copy.transform.SetSiblingIndex(target.GetSiblingIndex() + 1);
                if (!ApplySiblingIndex(copy.transform, op.siblingIndex, out error))
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    return FailOp(result, error);
                }
                result.detail = "复制自 " + GetHierarchyPath(target, root);
                return OkOp(result, copy.transform, root);
            }
            case "createObject":
            {
                if (!TryResolveObject(root, op.parentObjectId, out Transform parent, out error))
                    return FailOp(result, error);
                string name = string.IsNullOrWhiteSpace(op.newName) ? "GameObject" : op.newName.Trim();
                GameObject created;
                if (parent is RectTransform)
                {
                    PrefabMcpSettings settings = PrefabMcpSettings.GetOrCreate();
                    created = CreateUiObject(name, parent, settings.DefaultUiSize.x, settings.DefaultUiSize.y);
                }
                else
                {
                    created = new GameObject(name);
                    created.transform.SetParent(parent, false);
                }
                if (!ApplySiblingIndex(created.transform, op.siblingIndex, out error))
                {
                    UnityEngine.Object.DestroyImmediate(created);
                    return FailOp(result, error);
                }
                result.detail = parent is RectTransform ? "已创建（含 RectTransform）" : "已创建";
                return OkOp(result, created.transform, root);
            }
            case "instantiatePrefab":
            {
                if (!TryResolveObject(root, op.parentObjectId, out Transform parent, out error))
                    return FailOp(result, error);
                string sourceError = ValidatePrefabPath(op.sourcePrefabPath);
                if (sourceError != null)
                    return FailOp(result, sourceError);
                if (string.Equals(NormalizeSlashes(op.sourcePrefabPath), NormalizeSlashes(prefabPath), StringComparison.OrdinalIgnoreCase))
                    return FailOp(result, "不能把 Prefab 嵌套进它自己");
                GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(op.sourcePrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceAsset, parent);
                if (instance == null)
                    return FailOp(result, "实例化失败: " + op.sourcePrefabPath);
                if (!string.IsNullOrWhiteSpace(op.newName))
                    instance.name = op.newName.Trim();
                if (!ApplySiblingIndex(instance.transform, op.siblingIndex, out error))
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    return FailOp(result, error);
                }
                result.detail = "来自 " + op.sourcePrefabPath;
                return OkOp(result, instance.transform, root);
            }
            case "addComponent":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (!TryResolveComponentType(op.componentType, out Type type, out error))
                    return FailOp(result, error);
                Component added = target.gameObject.AddComponent(type);
                if (added == null)
                    return FailOp(result, $"AddComponent {type.FullName} 失败（可能与现有组件冲突，如 DisallowMultipleComponent）");
                result.componentIndex = GetComponentIndex(added);
                result.detail = "已添加 " + type.FullName;
                return OkOp(result, target, root);
            }
            case "removeComponent":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (!TryResolveComponent(target, op.componentIndex, out Component component, out error))
                    return FailOp(result, error + "（丢失脚本请用 removeMissingScripts）");
                if (component is Transform)
                    return FailOp(result, "不能移除 Transform/RectTransform");
                string typeName = component.GetType().FullName;
                UnityEngine.Object.DestroyImmediate(component);
                if (component != null)
                    return FailOp(result, $"移除 {typeName} 失败（可能被其他组件 RequireComponent 依赖）");
                result.detail = "已移除 " + typeName;
                return OkOp(result, target, root);
            }
            case "removeMissingScripts":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target.gameObject);
                result.detail = $"移除了 {removed} 个丢失脚本";
                return OkOp(result, target, root);
            }
            case "setValue":
            {
                if (!TryResolveObject(root, op.objectId, out Transform target, out error))
                    return FailOp(result, error);
                if (!TryResolveComponent(target, op.componentIndex, out Component component, out error))
                    return FailOp(result, error);
                var serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.FindProperty(op.propertyPath);
                if (property == null)
                    return FailOp(result, $"组件 {component.GetType().Name} 上找不到序列化字段 {op.propertyPath}");
                string previous = DescribePropertyValue(property, root);
                if (!TrySetPropertyValue(property, component, root, op.value, out error))
                    return FailOp(result, error);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                result.componentIndex = op.componentIndex;
                result.detail = $"{component.GetType().Name}.{op.propertyPath}: {previous} -> {DescribePropertyValue(property, root)}";
                return OkOp(result, target, root);
            }
            default:
                return FailOp(result, "未知 op: " + op.op +
                    "。支持: rename, setActive, reparent, setSiblingIndex, delete, duplicate, createObject, instantiatePrefab, addComponent, removeComponent, removeMissingScripts, setValue");
        }
    }

    // ---- prefab.fields 的子属性展开（嵌套结构 / 数组钻取） ----

    static string CollectChildProperties(SerializedObject serializedObject, Component component, Command command,
        Transform root, List<FieldInfoDto> fields)
    {
        SerializedProperty parent = serializedObject.FindProperty(command.propertyPath);
        if (parent == null)
            return $"找不到序列化字段 {command.propertyPath}";

        int limit = ConfiguredLimit(command.maxResults, 500);
        if (parent.isArray && parent.propertyType == SerializedPropertyType.Generic)
        {
            fields.Add(new FieldInfoDto
            {
                propertyPath = parent.propertyPath + ".Array.size",
                displayName = "Size",
                propertyType = SerializedPropertyType.ArraySize.ToString(),
                serializedType = "int",
                fieldType = "int",
                value = parent.arraySize.ToString(),
            });
            for (int i = 0; i < parent.arraySize && fields.Count < limit; i++)
                fields.Add(DescribeProperty(parent.GetArrayElementAtIndex(i), component, root));
            return null;
        }

        if (!parent.hasVisibleChildren)
        {
            fields.Add(DescribeProperty(parent, component, root));
            return null;
        }

        SerializedProperty end = parent.GetEndProperty();
        SerializedProperty child = parent.Copy();
        if (!child.NextVisible(true))
            return null;
        while (!SerializedProperty.EqualContents(child, end) && fields.Count < limit)
        {
            fields.Add(DescribeProperty(child, component, root));
            if (!child.NextVisible(false))
                break;
        }
        return null;
    }

    static FieldInfoDto DescribeProperty(SerializedProperty property, Component component, Transform root)
    {
        Type fieldType = ResolveFieldPathType(component.GetType(), property.propertyPath);
        return new FieldInfoDto
        {
            propertyPath = property.propertyPath,
            displayName = property.displayName,
            propertyType = property.propertyType.ToString(),
            serializedType = property.type,
            fieldType = fieldType == null ? null : FriendlyTypeName(fieldType),
            objectReference = property.propertyType == SerializedPropertyType.ObjectReference,
            value = DescribePropertyValue(property, root),
        };
    }

    // ---- setValue 的通用值解析 ----

    static bool TrySetPropertyValue(SerializedProperty property, Component component, Transform root,
        string value, out string error)
    {
        error = null;
        string text = value ?? string.Empty;
        switch (property.propertyType)
        {
            case SerializedPropertyType.String:
                property.stringValue = text;
                return true;
            case SerializedPropertyType.Boolean:
            {
                if (!TryParseBool(text, out bool parsed))
                {
                    error = "需要 true/false: " + text;
                    return false;
                }
                property.boolValue = parsed;
                return true;
            }
            case SerializedPropertyType.Integer:
            {
                if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                {
                    error = "需要整数: " + text;
                    return false;
                }
                property.longValue = parsed;
                return true;
            }
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.LayerMask:
            {
                if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 0)
                {
                    error = "需要不小于 0 的整数: " + text;
                    return false;
                }
                property.intValue = parsed;
                return true;
            }
            case SerializedPropertyType.Float:
            {
                if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    error = "需要数字: " + text;
                    return false;
                }
                property.doubleValue = parsed;
                return true;
            }
            case SerializedPropertyType.Enum:
            {
                string trimmed = text.Trim();
                string[] names = property.enumNames;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        property.enumValueIndex = i;
                        return true;
                    }
                }
                if (int.TryParse(trimmed, out int enumValue))
                {
                    property.intValue = enumValue;
                    return true;
                }
                error = "枚举值无效: " + text + "。可选: " + string.Join(", ", names);
                return false;
            }
            case SerializedPropertyType.Color:
            {
                string trimmed = text.Trim();
                if (!ColorUtility.TryParseHtmlString(trimmed, out Color color) &&
                    !ColorUtility.TryParseHtmlString("#" + trimmed, out color))
                {
                    error = "颜色格式无效，需要 #RRGGBB / #RRGGBBAA 或颜色名: " + text;
                    return false;
                }
                property.colorValue = color;
                return true;
            }
            case SerializedPropertyType.Vector2:
            {
                if (!TryParseFloats(text, 2, out float[] v, out error))
                    return false;
                property.vector2Value = new Vector2(v[0], v[1]);
                return true;
            }
            case SerializedPropertyType.Vector3:
            {
                if (!TryParseFloats(text, 3, out float[] v, out error))
                    return false;
                property.vector3Value = new Vector3(v[0], v[1], v[2]);
                return true;
            }
            case SerializedPropertyType.Vector4:
            {
                if (!TryParseFloats(text, 4, out float[] v, out error))
                    return false;
                property.vector4Value = new Vector4(v[0], v[1], v[2], v[3]);
                return true;
            }
            case SerializedPropertyType.Quaternion:
            {
                string[] parts = SplitFloatParts(text);
                if (parts.Length == 3 && TryParseFloats(text, 3, out float[] euler, out error))
                {
                    property.quaternionValue = Quaternion.Euler(euler[0], euler[1], euler[2]);
                    return true;
                }
                if (!TryParseFloats(text, 4, out float[] q, out error))
                {
                    error = "四元数需要 x,y,z,w 或欧拉角 x,y,z: " + text;
                    return false;
                }
                property.quaternionValue = new Quaternion(q[0], q[1], q[2], q[3]);
                return true;
            }
            case SerializedPropertyType.Rect:
            {
                if (!TryParseFloats(text, 4, out float[] r, out error))
                    return false;
                property.rectValue = new Rect(r[0], r[1], r[2], r[3]);
                return true;
            }
            case SerializedPropertyType.Vector2Int:
            {
                if (!TryParseFloats(text, 2, out float[] v, out error))
                    return false;
                property.vector2IntValue = new Vector2Int((int)v[0], (int)v[1]);
                return true;
            }
            case SerializedPropertyType.Vector3Int:
            {
                if (!TryParseFloats(text, 3, out float[] v, out error))
                    return false;
                property.vector3IntValue = new Vector3Int((int)v[0], (int)v[1], (int)v[2]);
                return true;
            }
            case SerializedPropertyType.Character:
            {
                if (text.Length == 0)
                {
                    error = "字符字段需要一个字符";
                    return false;
                }
                property.intValue = text[0];
                return true;
            }
            case SerializedPropertyType.ObjectReference:
                return TrySetObjectReference(property, component, root, text, out error);
            default:
                error = $"setValue 暂不支持 {property.propertyType} 类型；请用 get_component_fields 的 propertyPath 展开后改叶子字段";
                return false;
        }
    }

    static bool TrySetObjectReference(SerializedProperty property, Component component, Transform root,
        string text, out string error)
    {
        error = null;
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            property.objectReferenceValue = null;
            return true;
        }

        Type expectedType = ResolveFieldPathType(component.GetType(), property.propertyPath);
        if (expectedType == null || !typeof(UnityEngine.Object).IsAssignableFrom(expectedType))
            expectedType = typeof(UnityEngine.Object);

        UnityEngine.Object resolved;
        if (trimmed.StartsWith("asset:", StringComparison.OrdinalIgnoreCase))
        {
            string spec = trimmed.Substring("asset:".Length);
            string subName = null;
            int hash = spec.IndexOf('#');
            if (hash >= 0)
            {
                subName = spec.Substring(hash + 1).Trim();
                spec = spec.Substring(0, hash);
            }
            resolved = LoadAssetForType(NormalizeSlashes(spec), subName, expectedType, out error);
            if (resolved == null)
                return false;
        }
        else if (trimmed.StartsWith("object:", StringComparison.OrdinalIgnoreCase))
        {
            string spec = trimmed.Substring("object:".Length).Trim();
            string idPart = spec;
            int componentIndex = int.MinValue;
            int hash = spec.IndexOf('#');
            if (hash >= 0)
            {
                idPart = spec.Substring(0, hash);
                if (!int.TryParse(spec.Substring(hash + 1), out componentIndex))
                {
                    error = "object 引用的 componentIndex 无效: " + spec;
                    return false;
                }
            }
            if (!TryResolveObject(root, idPart, out Transform node, out error))
                return false;
            if (componentIndex == int.MinValue)
            {
                if (expectedType == typeof(GameObject))
                {
                    resolved = node.gameObject;
                }
                else if (typeof(Component).IsAssignableFrom(expectedType))
                {
                    resolved = node.GetComponent(expectedType);
                    if (resolved == null)
                    {
                        error = $"{node.name} 上没有组件 {FriendlyTypeName(expectedType)}";
                        return false;
                    }
                }
                else
                {
                    error = "无法推断引用类型，请用 object:<objectId>#<componentIndex>（-1 表示 GameObject）";
                    return false;
                }
            }
            else if (componentIndex < 0)
            {
                resolved = node.gameObject;
            }
            else
            {
                if (!TryResolveComponent(node, componentIndex, out Component resolvedComponent, out error))
                    return false;
                resolved = resolvedComponent;
            }
        }
        else
        {
            error = "对象引用字段的 value 必须是 null、asset:<Assets 路径>[#子资产名] 或 object:<objectId>[#componentIndex]";
            return false;
        }

        if (expectedType != typeof(UnityEngine.Object) && !expectedType.IsInstanceOfType(resolved))
        {
            error = $"类型不兼容：字段需要 {FriendlyTypeName(expectedType)}，得到 {FriendlyTypeName(resolved.GetType())}";
            return false;
        }
        property.objectReferenceValue = resolved;
        return true;
    }

    static UnityEngine.Object LoadAssetForType(string path, string subAssetName, Type expectedType, out string error)
    {
        error = null;
        if (!path.StartsWith("Assets/", StringComparison.Ordinal))
        {
            error = "资产路径必须以 Assets/ 开头: " + path;
            return null;
        }

        if (!string.IsNullOrEmpty(subAssetName))
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset != null && asset.name == subAssetName && expectedType.IsInstanceOfType(asset))
                    return asset;
            }
            error = $"资产 {path} 中找不到名为 {subAssetName} 且类型为 {FriendlyTypeName(expectedType)} 的子资产";
            return null;
        }

        UnityEngine.Object direct = AssetDatabase.LoadAssetAtPath(path, expectedType);
        if (direct != null)
            return direct;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset != null && expectedType.IsInstanceOfType(asset))
                return asset;
        }
        error = $"在 {path} 找不到类型为 {FriendlyTypeName(expectedType)} 的资产";
        return null;
    }

    /// <summary>沿 propertyPath（含 Array.data[i]）解析出字段的实际 C# 类型；解析失败返回 null。</summary>
    static Type ResolveFieldPathType(Type componentType, string propertyPath)
    {
        Type current = componentType;
        foreach (string token in (propertyPath ?? string.Empty).Split('.'))
        {
            if (token == "Array")
                continue;
            if (token == "size")
                return typeof(int);
            if (token.StartsWith("data[", StringComparison.Ordinal))
            {
                if (current.IsArray)
                    current = current.GetElementType();
                else if (current.IsGenericType && current.GetGenericArguments().Length == 1)
                    current = current.GetGenericArguments()[0];
                else
                    return null;
                continue;
            }
            FieldInfo field = FindField(current, token);
            if (field == null)
                return null;
            current = field.FieldType;
        }
        return current;
    }

    static bool TryResolveComponentType(string name, out Type type, out string error)
    {
        type = null;
        error = null;
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            error = "componentType 不能为空";
            return false;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type exact = assembly.GetType(trimmed, false);
            if (exact != null)
            {
                if (!IsAddableComponentType(exact, out error))
                    return false;
                type = exact;
                return true;
            }
        }

        var matches = new List<Type>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch { continue; }
            foreach (Type candidate in types)
            {
                if (candidate.Name == trimmed && typeof(Component).IsAssignableFrom(candidate) &&
                    !candidate.IsAbstract && !candidate.IsGenericTypeDefinition)
                    matches.Add(candidate);
            }
        }

        if (matches.Count == 0)
        {
            error = "找不到组件类型: " + trimmed;
            return false;
        }
        if (matches.Count > 1)
        {
            error = "组件类型名有歧义，请用全名: " +
                string.Join(", ", matches.Take(5).Select(FriendlyTypeName));
            return false;
        }
        type = matches[0];
        return true;
    }

    static bool IsAddableComponentType(Type type, out string error)
    {
        error = null;
        if (!typeof(Component).IsAssignableFrom(type))
        {
            error = $"{FriendlyTypeName(type)} 不是 Component";
            return false;
        }
        if (type.IsAbstract || type.IsGenericTypeDefinition)
        {
            error = $"{FriendlyTypeName(type)} 是抽象或泛型定义，不能 AddComponent";
            return false;
        }
        return true;
    }

    // ---- 小工具 ----

    static EditOpResult FailOp(EditOpResult result, string error)
    {
        result.ok = false;
        result.error = error;
        return result;
    }

    static EditOpResult OkOp(EditOpResult result, Transform target, Transform root)
    {
        result.ok = true;
        result.objectId = GetObjectId(target, root);
        result.hierarchyPath = GetHierarchyPath(target, root);
        return result;
    }

    static bool TryParseBool(string text, out bool value)
    {
        return bool.TryParse((text ?? string.Empty).Trim(), out value);
    }

    static bool ApplySiblingIndex(Transform target, string siblingIndex, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(siblingIndex))
            return true;
        if (!int.TryParse(siblingIndex.Trim(), out int index) || index < 0)
        {
            error = "siblingIndex 必须是不小于 0 的整数: " + siblingIndex;
            return false;
        }
        int maxIndex = target.parent == null ? 0 : target.parent.childCount - 1;
        target.SetSiblingIndex(Mathf.Clamp(index, 0, Math.Max(0, maxIndex)));
        return true;
    }

    static string[] SplitFloatParts(string text)
    {
        return (text ?? string.Empty).Trim().Trim('(', ')')
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }

    static bool TryParseFloats(string text, int expected, out float[] values, out string error)
    {
        values = null;
        error = null;
        string[] parts = SplitFloatParts(text);
        if (parts.Length != expected)
        {
            error = $"需要 {expected} 个以逗号分隔的数字: {text}";
            return false;
        }
        values = new float[expected];
        for (int i = 0; i < expected; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                error = $"第 {i + 1} 个数字无效: {parts[i]}";
                return false;
            }
        }
        return true;
    }

    static string FormatFloats(params float[] values)
    {
        return string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
    }

    static string NormalizeSlashes(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim();
    }
}
