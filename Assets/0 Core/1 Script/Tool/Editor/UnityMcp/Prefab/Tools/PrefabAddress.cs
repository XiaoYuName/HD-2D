using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 寻址与取值：objectId（sibling index 路径）、组件下标、字段的 C# 类型推断，
    /// 以及序列化值 ↔ 协议字符串的互转。
    /// 读出来的对象引用形如 <c>object:0/2#3:Button</c>，可原样回填给 setValue。
    /// </summary>
    static class PrefabAddress
    {
        // ---- 层级寻址 ----

        public static IEnumerable<Transform> EnumerateHierarchy(Transform rootTf)
        {
            yield return rootTf;
            for (int i = 0; i < rootTf.childCount; i++)
            {
                foreach (Transform child in EnumerateHierarchy(rootTf.GetChild(i)))
                    yield return child;
            }
        }

        /// <summary>按名字寻址的前缀：<c>path:Root/Child/Grandchild</c>，同名兄弟用 <c>Child[1]</c> 区分。</summary>
        public const string PathPrefix = "path:";

        public static bool TryGetObject(Transform rootTf, string objectId, out Transform result, out string error)
        {
            result = rootTf;
            error = null;
            if (string.IsNullOrEmpty(objectId) || objectId == "0")
                return true;

            // sibling index 路径一旦有插入/重排就全部失效；名字路径不受同级顺序影响，可以跨调用复用。
            if (objectId.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase))
                return TryGetByHierarchyPath(rootTf, objectId.Substring(PathPrefix.Length), out result, out error);

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

        /// <summary>
        /// 解析 <c>path:</c> 后面的名字路径。可带根节点名（get_prefab_tree 的 hierarchyPath 原样可用），
        /// 同名兄弟写 <c>名字[i]</c>（i 是同名节点中的序号，不是 sibling index）；歧义时报错并列出候选。
        /// </summary>
        static bool TryGetByHierarchyPath(Transform rootTf, string path, out Transform result, out string error)
        {
            result = null;
            error = null;
            string[] segments = NormalizeSlashes(path).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                result = rootTf;
                return true;
            }

            Transform current = rootTf;
            int start = SegmentMatchesName(segments[0], rootTf.name) ? 1 : 0;
            for (int i = start; i < segments.Length; i++)
            {
                SplitNameIndex(segments[i], out string name, out int occurrence);
                var matches = new List<Transform>();
                for (int child = 0; child < current.childCount; child++)
                {
                    Transform childTf = current.GetChild(child);
                    if (string.Equals(childTf.name, name, StringComparison.OrdinalIgnoreCase))
                        matches.Add(childTf);
                }

                if (matches.Count == 0)
                {
                    error = $"path 寻址失败：{GetHierarchyPath(current, rootTf)} 下没有名为 {name} 的子节点" +
                        (current.childCount == 0 ? "（该节点没有子节点）" : "；可选: " + string.Join(", ",
                            Enumerable.Range(0, current.childCount).Select(c => current.GetChild(c).name).Distinct().Take(12)));
                    return false;
                }
                if (occurrence < 0 && matches.Count > 1)
                {
                    error = $"path 寻址有歧义：{GetHierarchyPath(current, rootTf)} 下有 {matches.Count} 个名为 {name} 的子节点，" +
                        $"请写 {name}[0] ~ {name}[{matches.Count - 1}]，或改用 sibling 路径（" +
                        string.Join(", ", matches.Select(m => GetObjectId(m, rootTf))) + "）";
                    return false;
                }
                int index = occurrence < 0 ? 0 : occurrence;
                if (index >= matches.Count)
                {
                    error = $"path 寻址失败：{name} 只有 {matches.Count} 个同名节点，取不到 [{index}]";
                    return false;
                }
                current = matches[index];
            }

            result = current;
            return true;
        }

        /// <summary>拆出 <c>名字[i]</c> 里的名字和序号；没写序号返回 -1。</summary>
        static void SplitNameIndex(string segment, out string name, out int occurrence)
        {
            name = segment.Trim();
            occurrence = -1;
            if (!name.EndsWith("]", StringComparison.Ordinal))
                return;
            int open = name.LastIndexOf('[');
            if (open <= 0)
                return;
            if (!int.TryParse(name.Substring(open + 1, name.Length - open - 2).Trim(), out occurrence) || occurrence < 0)
            {
                occurrence = -1;
                return;
            }
            name = name.Substring(0, open).Trim();
        }

        static bool SegmentMatchesName(string segment, string name)
        {
            SplitNameIndex(segment, out string segmentName, out int _);
            return string.Equals(segmentName, name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>逗号或竖线分隔的多个关键词，命中任意一个即算匹配；过滤串为空视为不过滤。</summary>
        public static bool MatchesFilter(string filter, params string[] texts)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;
            foreach (string term in filter.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = term.Trim();
                if (trimmed.Length == 0)
                    continue;
                foreach (string text in texts)
                {
                    if (text != null && text.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public static bool TryGetComponentAt(Transform targetTf, int componentIndex, out Component component, out string error)
        {
            Component[] components = targetTf.GetComponents<Component>();
            if (componentIndex < 0 || componentIndex >= components.Length)
            {
                component = null;
                error = $"节点 {targetTf.name} 上不存在 componentIndex={componentIndex}";
                return false;
            }
            component = components[componentIndex];
            if (component == null)
            {
                error = $"节点 {targetTf.name} 的 componentIndex={componentIndex} 是丢失脚本";
                return false;
            }
            error = null;
            return true;
        }

        public static string GetObjectId(Transform tf, Transform rootTf)
        {
            var indices = new Stack<int>();
            Transform current = tf;
            while (current != rootTf)
            {
                if (current.parent == null)
                    return null;
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }
            return indices.Count == 0 ? "0" : "0/" + string.Join("/", indices);
        }

        public static string GetHierarchyPath(Transform tf, Transform rootTf)
        {
            var names = new Stack<string>();
            Transform current = tf;
            while (current != null)
            {
                names.Push(current.name);
                if (current == rootTf)
                    break;
                current = current.parent;
            }
            return string.Join("/", names);
        }

        public static int GetComponentIndex(Component component)
        {
            Component[] components = component.gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == component)
                    return i;
            }
            return -1;
        }

        // ---- 字段类型推断 ----

        public static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }
            return null;
        }

        public static string RootFieldName(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
                return propertyPath;
            int dot = propertyPath.IndexOf('.');
            return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
        }

        /// <summary>沿 propertyPath（含 Array.data[i]）取出字段的实际 C# 类型；取不到返回 null。</summary>
        public static Type GetFieldPathType(Type componentType, string propertyPath)
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

        /// <summary>取出一个对象引用字段的 SerializedProperty 及其期望类型。</summary>
        public static bool TryGetObjectReferenceProperty(Component component, string propertyPath,
            out SerializedObject serializedObject, out SerializedProperty property, out Type expectedType, out string error)
        {
            serializedObject = new SerializedObject(component);
            property = string.IsNullOrEmpty(propertyPath) ? null : serializedObject.FindProperty(propertyPath);
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

            Type resolved = GetFieldPathType(component.GetType(), propertyPath);
            if (resolved == null || !typeof(UnityEngine.Object).IsAssignableFrom(resolved))
            {
                error = $"无法确定字段 {propertyPath} 的 UnityEngine.Object 类型";
                return false;
            }

            expectedType = resolved;
            error = null;
            return true;
        }

        public static string FriendlyTypeName(Type type) => type.FullName ?? type.Name;

        // ---- 序列化值 ↔ 协议字符串 ----

        public static string GetPropertyValueText(SerializedProperty property, Transform rootTf)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return GetObjectReferenceText(property.objectReferenceValue, rootTf);
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

        /// <summary>
        /// 对象引用的可回填写法：<c>asset:路径</c> 或 <c>object:objectId#componentIndex:类型名</c>。
        /// 末尾的 :类型名 只是给人看的可读后缀，setValue 会忽略它。
        /// </summary>
        public static string GetObjectReferenceText(UnityEngine.Object value, Transform rootTf)
        {
            if (value == null)
                return "null";
            string assetPath = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(assetPath))
                return "asset:" + assetPath;
            if (value is Component component && component.transform.root == rootTf)
                return $"object:{GetObjectId(component.transform, rootTf)}#{GetComponentIndex(component)}:{component.GetType().Name}";
            if (value is GameObject go && go.transform.root == rootTf)
                return $"object:{GetObjectId(go.transform, rootTf)}#-1:GameObject";
            return value.name + ":" + value.GetType().Name;
        }

        public static string FormatFloats(params float[] values) =>
            string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

        public static string[] SplitFloatParts(string text) =>
            (text ?? string.Empty).Trim().Trim('(', ')').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        public static bool TryParseFloats(string text, int expected, out float[] values, out string error)
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

        public static bool TryParseBool(string text, out bool value) =>
            bool.TryParse((text ?? string.Empty).Trim(), out value);

        // ---- 资产路径 ----

        public static string NormalizeSlashes(string path) => (path ?? string.Empty).Replace('\\', '/').Trim();

        public static bool TryNormalizePrefabPath(string path, out string normalized, out string error)
        {
            normalized = NormalizeSlashes(path);
            error = null;
            if (string.IsNullOrEmpty(normalized))
            {
                error = "prefabPath 不能为空";
                return false;
            }
            if (Path.IsPathRooted(normalized) || normalized.IndexOf(':') >= 0)
            {
                error = "prefabPath 必须是 Assets/ 下的项目相对路径，不能是绝对路径: " + path;
                return false;
            }

            string[] segments = normalized.Split('/');
            if (segments.Length < 2 || segments[0] != "Assets" ||
                segments.Any(segment => string.IsNullOrEmpty(segment) || segment == "." || segment == ".."))
            {
                error = "prefabPath 必须是规范的 Assets/... 路径，不能包含空段、. 或 ..: " + path;
                return false;
            }
            normalized = string.Join("/", segments);
            if (!normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error = "prefabPath 必须以 .prefab 结尾";
                return false;
            }
            return true;
        }

        public static string ValidatePrefabPathSyntax(string path)
        {
            return TryNormalizePrefabPath(path, out _, out string error) ? null : error;
        }

        public static string ValidatePrefabPath(string path)
        {
            if (!TryNormalizePrefabPath(path, out string normalized, out string error))
                return error;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(normalized) == null)
                return "找不到 Prefab: " + normalized;
            return null;
        }

        /// <summary>空数组返回 null（表示不限目录）；含非法目录时抛异常，由路由统一转成错误响应。</summary>
        public static string[] NormalizeSearchFolders(string[] folders)
        {
            if (folders == null || folders.Length == 0)
                return null;
            var valid = new List<string>();
            foreach (string folder in folders)
            {
                string normalized = NormalizeSlashes(folder).TrimEnd('/');
                if (!normalized.StartsWith("Assets", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(normalized))
                    throw new ArgumentException("无效的搜索目录: " + folder);
                valid.Add(normalized);
            }
            return valid.ToArray();
        }
    }
}
