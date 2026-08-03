using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// setValue 的值解析：协议里所有标量都是字符串，这里按 SerializedPropertyType 转回真实值。
    /// 对象引用支持四种写法，且与 get_component_fields 读出来的格式一致（可原样回填）：
    /// <c>null</c> / <c>asset:路径[#子资产名]</c> / <c>asset:路径@objectId[#componentIndex]</c> /
    /// <c>object:objectId[#componentIndex]</c>（-1 表示 GameObject）/
    /// <c>component:componentRef</c>（edit_prefab 批内组件句柄）。
    /// </summary>
    static class SerializedValueWriter
    {
        /// <summary>节点寻址钩子：由 edit_prefab 传入，使 object: 引用也认批内的 $n。</summary>
        public delegate bool NodeLookup(string objectId, out Transform result, out string error);
        /// <summary>组件寻址钩子：使 component: 引用可用 addComponent/ensureComponent 的别名或 $n。</summary>
        public delegate bool ComponentLookup(string componentRef, out Component result, out string error);

        /// <summary>把字符串协议值写入序列化属性；返回错误信息或 null。</summary>
        public static string Write(SerializedProperty property, Component component, Transform rootTf, string value,
            NodeLookup lookup = null, ComponentLookup componentLookup = null)
        {
            string text = value ?? string.Empty;
            string error;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = text;
                    return null;
                case SerializedPropertyType.Boolean:
                {
                    if (!PrefabAddress.TryParseBool(text, out bool parsed))
                        return "需要 true/false: " + text;
                    property.boolValue = parsed;
                    return null;
                }
                case SerializedPropertyType.Integer:
                {
                    if (!long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                        return "需要整数: " + text;
                    property.longValue = parsed;
                    return null;
                }
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                {
                    if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 0)
                        return "需要不小于 0 的整数: " + text;
                    property.intValue = parsed;
                    return null;
                }
                case SerializedPropertyType.Float:
                {
                    if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                        return "需要数字: " + text;
                    property.doubleValue = parsed;
                    return null;
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
                            return null;
                        }
                    }
                    if (int.TryParse(trimmed, out int enumValue))
                    {
                        property.intValue = enumValue;
                        return null;
                    }
                    return "枚举值无效: " + text + "。可选: " + string.Join(", ", names);
                }
                case SerializedPropertyType.Color:
                {
                    string trimmed = text.Trim();
                    if (!ColorUtility.TryParseHtmlString(trimmed, out Color color) &&
                        !ColorUtility.TryParseHtmlString("#" + trimmed, out color))
                        return "颜色格式无效，需要 #RRGGBB / #RRGGBBAA 或颜色名: " + text;
                    property.colorValue = color;
                    return null;
                }
                case SerializedPropertyType.Vector2:
                {
                    if (!PrefabAddress.TryParseFloats(text, 2, out float[] v, out error))
                        return error;
                    property.vector2Value = new Vector2(v[0], v[1]);
                    return null;
                }
                case SerializedPropertyType.Vector3:
                {
                    if (!PrefabAddress.TryParseFloats(text, 3, out float[] v, out error))
                        return error;
                    property.vector3Value = new Vector3(v[0], v[1], v[2]);
                    return null;
                }
                case SerializedPropertyType.Vector4:
                {
                    if (!PrefabAddress.TryParseFloats(text, 4, out float[] v, out error))
                        return error;
                    property.vector4Value = new Vector4(v[0], v[1], v[2], v[3]);
                    return null;
                }
                case SerializedPropertyType.Quaternion:
                {
                    if (PrefabAddress.SplitFloatParts(text).Length == 3 &&
                        PrefabAddress.TryParseFloats(text, 3, out float[] euler, out error))
                    {
                        property.quaternionValue = Quaternion.Euler(euler[0], euler[1], euler[2]);
                        return null;
                    }
                    if (!PrefabAddress.TryParseFloats(text, 4, out float[] q, out error))
                        return "四元数需要 x,y,z,w 或欧拉角 x,y,z: " + text;
                    property.quaternionValue = new Quaternion(q[0], q[1], q[2], q[3]);
                    return null;
                }
                case SerializedPropertyType.Rect:
                {
                    if (!PrefabAddress.TryParseFloats(text, 4, out float[] r, out error))
                        return error;
                    property.rectValue = new Rect(r[0], r[1], r[2], r[3]);
                    return null;
                }
                case SerializedPropertyType.Vector2Int:
                {
                    if (!PrefabAddress.TryParseFloats(text, 2, out float[] v, out error))
                        return error;
                    property.vector2IntValue = new Vector2Int((int)v[0], (int)v[1]);
                    return null;
                }
                case SerializedPropertyType.Vector3Int:
                {
                    if (!PrefabAddress.TryParseFloats(text, 3, out float[] v, out error))
                        return error;
                    property.vector3IntValue = new Vector3Int((int)v[0], (int)v[1], (int)v[2]);
                    return null;
                }
                case SerializedPropertyType.Character:
                {
                    if (text.Length == 0)
                        return "字符字段需要一个字符";
                    property.intValue = text[0];
                    return null;
                }
                case SerializedPropertyType.ObjectReference:
                    return WriteObjectReference(property, component, rootTf, text, lookup, componentLookup);
                default:
                    return $"setValue 暂不支持 {property.propertyType} 类型；" +
                        "请用 get_component_fields 的 propertyPath 展开后改叶子字段";
            }
        }

        static string WriteObjectReference(SerializedProperty property, Component component, Transform rootTf,
            string text, NodeLookup lookup, ComponentLookup componentLookup)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
            {
                property.objectReferenceValue = null;
                return null;
            }

            Type expectedType = PrefabAddress.GetFieldPathType(component.GetType(), property.propertyPath);
            if (expectedType == null || !typeof(UnityEngine.Object).IsAssignableFrom(expectedType))
                expectedType = typeof(UnityEngine.Object);

            UnityEngine.Object resolved;
            string error;
            if (TryStripPrefix(trimmed, "asset:", out string assetSpec))
            {
                resolved = ResolveAsset(assetSpec, expectedType, out error);
            }
            else if (TryStripPrefix(trimmed, "object:", out string nodeSpec))
            {
                resolved = ResolveNode(rootTf, nodeSpec, expectedType, out error, lookup);
            }
            else if (TryStripPrefix(trimmed, "component:", out string componentSpec))
            {
                if (componentLookup == null)
                    return "component: 引用只支持 edit_prefab 批内组件句柄";
                if (!componentLookup(componentSpec, out Component referenced, out error))
                    return error;
                resolved = referenced;
            }
            else
            {
                return "对象引用字段的 value 必须是 null、asset:<Assets 路径>[#子资产名|@objectId[#componentIndex]] " +
                    "、object:<objectId|$n>[#componentIndex] 或 component:<componentRef|$n>";
            }

            if (resolved == null)
                return error;
            if (expectedType != typeof(UnityEngine.Object) && !expectedType.IsInstanceOfType(resolved))
                return $"类型不兼容：字段需要 {PrefabAddress.FriendlyTypeName(expectedType)}，" +
                    $"得到 {PrefabAddress.FriendlyTypeName(resolved.GetType())}";
            property.objectReferenceValue = resolved;
            return null;
        }

        /// <summary>asset:路径[#子资产名] 或 asset:路径@objectId[#componentIndex]（引用别的 Prefab 内部节点/组件）。</summary>
        static UnityEngine.Object ResolveAsset(string spec, Type expectedType, out string error)
        {
            int at = spec.IndexOf('@');
            if (at >= 0)
            {
                string prefabPath = PrefabAddress.NormalizeSlashes(spec.Substring(0, at));
                error = PrefabAddress.ValidatePrefabPath(prefabPath);
                if (error != null)
                    return null;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                // 目标是别的 Prefab 内部，$n 在那里没有意义，不透传 lookup。
                return ResolveNode(prefab.transform, spec.Substring(at + 1), expectedType, out error, null);
            }

            string path = spec;
            string subName = null;
            int hash = spec.IndexOf('#');
            if (hash >= 0)
            {
                subName = spec.Substring(hash + 1).Trim();
                path = spec.Substring(0, hash);
            }
            path = PrefabAddress.NormalizeSlashes(path);

            error = null;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "资产路径必须以 Assets/ 开头: " + path;
                return null;
            }

            if (!string.IsNullOrEmpty(subName))
            {
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset != null && asset.name == subName && expectedType.IsInstanceOfType(asset))
                        return asset;
                }
                error = $"资产 {path} 中找不到名为 {subName} 且类型为 " +
                    $"{PrefabAddress.FriendlyTypeName(expectedType)} 的子资产";
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
            error = $"在 {path} 找不到类型为 {PrefabAddress.FriendlyTypeName(expectedType)} 的资产";
            return null;
        }

        /// <summary>解析 objectId|$n[#componentIndex][:类型名]；末尾的类型名只是可读后缀，忽略。</summary>
        static UnityEngine.Object ResolveNode(Transform rootTf, string spec, Type expectedType, out string error,
            NodeLookup lookup)
        {
            string idPart = spec.Trim();
            int componentIndex = int.MinValue;
            int hash = idPart.IndexOf('#');
            if (hash >= 0)
            {
                string indexPart = idPart.Substring(hash + 1);
                idPart = idPart.Substring(0, hash);
                int colon = indexPart.IndexOf(':');
                if (colon >= 0)
                    indexPart = indexPart.Substring(0, colon);
                if (!int.TryParse(indexPart.Trim(), out componentIndex))
                {
                    error = "引用的 componentIndex 无效: " + spec;
                    return null;
                }
            }

            Transform nodeTf;
            bool found = lookup != null
                ? lookup(idPart, out nodeTf, out error)
                : PrefabAddress.TryGetObject(rootTf, idPart, out nodeTf, out error);
            if (!found)
                return null;

            if (componentIndex >= 0)
            {
                if (!PrefabAddress.TryGetComponentAt(nodeTf, componentIndex, out Component resolved, out error))
                    return null;
                return resolved;
            }
            if (componentIndex != int.MinValue)
                return nodeTf.gameObject; // -1 = GameObject

            // 没写 #componentIndex：按字段类型自动取。
            if (expectedType == typeof(GameObject))
                return nodeTf.gameObject;
            if (typeof(Component).IsAssignableFrom(expectedType))
            {
                Component resolved = nodeTf.GetComponent(expectedType);
                if (resolved == null)
                    error = $"{nodeTf.name} 上没有组件 {PrefabAddress.FriendlyTypeName(expectedType)}";
                return resolved;
            }
            error = "无法推断引用类型，请写成 objectId#componentIndex（-1 表示 GameObject）";
            return null;
        }

        static bool TryStripPrefix(string source, string prefix, out string rest)
        {
            if (source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                rest = source.Substring(prefix.Length).Trim();
                return true;
            }
            rest = null;
            return false;
        }
    }
}
