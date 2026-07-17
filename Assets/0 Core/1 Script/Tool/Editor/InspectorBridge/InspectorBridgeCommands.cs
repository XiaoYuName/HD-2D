using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// JSON 命令的字段结构。用 UnityEngine.JsonUtility 解析，所以只能是扁平的可序列化字段，
/// 不支持字典/多态，未用到的字段留空即可。
/// </summary>
[Serializable]
public class InspectorCommand
{
    /// <summary>"setField" | "getField" | "listFields"</summary>
    public string action;

    /// <summary>"prefabAsset" | "prefabStage" | "openScene"</summary>
    public string targetMode;

    /// <summary>targetMode=prefabAsset 时必填：Prefab 的 Assets 相对路径</summary>
    public string targetPath;

    /// <summary>
    /// 目标 GameObject 相对于"根"的路径（用 "/" 分隔子级名字）。
    /// prefabAsset/prefabStage：相对 Prefab 根，空字符串＝根节点本身。
    /// openScene：相对场景，第一段是某个根物体的名字。
    /// </summary>
    public string hierarchyPath;

    /// <summary>要操作的组件类型名（不需要写命名空间，按类名匹配）</summary>
    public string component;

    /// <summary>要赋值/读取的字段名（和 [SerializeField] 的字段名完全一致）</summary>
    public string field;

    /// <summary>"asset" | "objectInTarget" | "null" | "string" | "float" | "int" | "bool"</summary>
    public string valueKind;

    /// <summary>valueKind=asset 时：资源的 Assets 相对路径</summary>
    public string valueAssetPath;

    /// <summary>valueKind=objectInTarget 时：另一个物体相对同一个根的路径，规则同 hierarchyPath</summary>
    public string valueHierarchyPath;

    /// <summary>valueKind=objectInTarget 且字段类型是组件时：该物体上要取的组件类型名；留空且字段类型是 GameObject 时直接用物体本身</summary>
    public string valueComponent;

    public string valueString;
    public float valueFloat;
    public int valueInt;
    public bool valueBool;
}

[Serializable]
public class InspectorResult
{
    public bool ok;
    public string error;
    public string info;
}

/// <summary>在主线程执行的实际逻辑：解析命令、定位对象、用 SerializedObject 赋值/读取字段。</summary>
public static class InspectorBridgeCommands
{
    public static string Dispatch(string requestJson)
    {
        InspectorCommand cmd;
        try
        {
            cmd = JsonUtility.FromJson<InspectorCommand>(requestJson);
        }
        catch (Exception e)
        {
            return Fail($"请求 JSON 解析失败: {e.Message}");
        }
        if (cmd == null)
            return Fail("请求体为空");

        try
        {
            if (cmd.action != null && cmd.action.StartsWith("prefab.", StringComparison.Ordinal))
                return PrefabMcpCommands.Dispatch(requestJson);

            switch (cmd.action)
            {
                case "setField": return SetField(cmd);
                case "getField": return GetField(cmd);
                case "listFields": return ListFields(cmd);
                default: return Fail($"未知 action: {cmd.action}");
            }
        }
        catch (Exception e)
        {
            return Fail(e.ToString());
        }
    }

    static string SetField(InspectorCommand cmd)
    {
        if (!TryResolveComponent(cmd, out Component target, out Action save, out string err))
            return Fail(err);

        var so = new SerializedObject(target);
        var prop = so.FindProperty(cmd.field);
        if (prop == null)
            return Fail($"组件 {cmd.component} 上找不到字段 {cmd.field}（是不是没加 [SerializeField]，或者名字不一致？）");

        FieldInfo fieldInfo = FindFieldInfo(target.GetType(), cmd.field);

        switch (cmd.valueKind)
        {
            case "null":
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    return Fail("valueKind=null 只能用于引用类型字段");
                prop.objectReferenceValue = null;
                break;

            case "asset":
            {
                if (fieldInfo == null)
                    return Fail("无法通过反射拿到字段类型，赋值资源失败");
                if (string.IsNullOrEmpty(cmd.valueAssetPath))
                    return Fail("valueKind=asset 需要提供 valueAssetPath");
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(cmd.valueAssetPath, fieldInfo.FieldType);
                if (asset == null)
                    return Fail($"在 {cmd.valueAssetPath} 找不到类型为 {fieldInfo.FieldType.Name} 的资源（注意 Sprite/Texture2D 要选对子资源类型）");
                prop.objectReferenceValue = asset;
                break;
            }

            case "objectInTarget":
            {
                if (fieldInfo == null)
                    return Fail("无法通过反射拿到字段类型，赋值失败");
                if (!ResolveTransform(cmd, cmd.valueHierarchyPath, out Transform other, out err))
                    return Fail(err);

                UnityEngine.Object value;
                if (!string.IsNullOrEmpty(cmd.valueComponent))
                {
                    Type compType = FindType(cmd.valueComponent);
                    if (compType == null)
                        return Fail($"找不到组件类型 {cmd.valueComponent}");
                    value = other.GetComponent(compType);
                    if (value == null)
                        return Fail($"{cmd.valueHierarchyPath} 上没有组件 {cmd.valueComponent}");
                }
                else if (fieldInfo.FieldType == typeof(GameObject))
                {
                    value = other.gameObject;
                }
                else if (typeof(Component).IsAssignableFrom(fieldInfo.FieldType))
                {
                    value = other.GetComponent(fieldInfo.FieldType);
                    if (value == null)
                        return Fail($"{cmd.valueHierarchyPath} 上没有组件 {fieldInfo.FieldType.Name}");
                }
                else
                {
                    return Fail($"字段类型 {fieldInfo.FieldType.Name} 不是 GameObject/Component，且未指定 valueComponent");
                }
                prop.objectReferenceValue = value;
                break;
            }

            case "string":
                if (prop.propertyType != SerializedPropertyType.String)
                    return Fail("字段不是 string 类型");
                prop.stringValue = cmd.valueString;
                break;

            case "float":
                if (prop.propertyType != SerializedPropertyType.Float)
                    return Fail("字段不是 float 类型");
                prop.floatValue = cmd.valueFloat;
                break;

            case "int":
                if (prop.propertyType != SerializedPropertyType.Integer && prop.propertyType != SerializedPropertyType.Enum)
                    return Fail("字段不是 int/enum 类型");
                prop.intValue = cmd.valueInt;
                break;

            case "bool":
                if (prop.propertyType != SerializedPropertyType.Boolean)
                    return Fail("字段不是 bool 类型");
                prop.boolValue = cmd.valueBool;
                break;

            default:
                return Fail($"未知 valueKind: {cmd.valueKind}");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        save();
        return Ok($"已设置 {cmd.component}.{cmd.field}");
    }

    static string GetField(InspectorCommand cmd)
    {
        if (!TryResolveComponent(cmd, out Component target, out _, out string err))
            return Fail(err);

        var so = new SerializedObject(target);
        var prop = so.FindProperty(cmd.field);
        if (prop == null)
            return Fail($"组件 {cmd.component} 上找不到字段 {cmd.field}");

        string text = prop.propertyType switch
        {
            SerializedPropertyType.ObjectReference => prop.objectReferenceValue == null
                ? "null"
                : DescribeObjectReference(prop.objectReferenceValue),
            SerializedPropertyType.String => prop.stringValue,
            SerializedPropertyType.Float => prop.floatValue.ToString(),
            SerializedPropertyType.Integer => prop.intValue.ToString(),
            SerializedPropertyType.Enum => prop.enumDisplayNames.Length > prop.enumValueIndex ? prop.enumDisplayNames[prop.enumValueIndex] : prop.intValue.ToString(),
            SerializedPropertyType.Boolean => prop.boolValue.ToString(),
            _ => $"<{prop.propertyType}>",
        };
        return Ok(text);
    }

    static string DescribeObjectReference(UnityEngine.Object obj)
    {
        string assetPath = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(assetPath))
            return $"asset:{assetPath}";
        if (obj is Component c)
            return $"sceneObject:{GetGameObjectPath(c.gameObject)} ({c.GetType().Name})";
        if (obj is GameObject go)
            return $"sceneObject:{GetGameObjectPath(go)}";
        return obj.ToString();
    }

    static string GetGameObjectPath(GameObject go)
    {
        var sb = new StringBuilder(go.name);
        Transform t = go.transform.parent;
        while (t != null)
        {
            sb.Insert(0, t.name + "/");
            t = t.parent;
        }
        return sb.ToString();
    }

    static string ListFields(InspectorCommand cmd)
    {
        Type compType = FindType(cmd.component);
        if (compType == null)
            return Fail($"找不到组件类型 {cmd.component}");

        var sb = new StringBuilder();
        foreach (var f in compType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            bool serialized = f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;
            if (!serialized)
                continue;
            sb.AppendLine($"{f.Name} : {f.FieldType.Name}");
        }
        return Ok(sb.ToString());
    }

    // ---- 定位 ----

    static bool TryResolveComponent(InspectorCommand cmd, out Component component, out Action save, out string error)
    {
        component = null;
        save = null;

        if (!ResolveTransform(cmd, cmd.hierarchyPath, out Transform target, out error))
            return false;

        Type compType = FindType(cmd.component);
        if (compType == null)
        {
            error = $"找不到组件类型 {cmd.component}";
            return false;
        }

        component = target.GetComponent(compType);
        if (component == null)
        {
            error = $"{cmd.hierarchyPath} 上没有组件 {cmd.component}";
            return false;
        }

        save = BuildSaveAction(cmd);
        return true;
    }

    static Action BuildSaveAction(InspectorCommand cmd)
    {
        switch (cmd.targetMode)
        {
            case "prefabAsset":
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(cmd.targetPath);
                return () =>
                {
                    PrefabUtility.SavePrefabAsset(prefabRoot);
                    AssetDatabase.SaveAssets();
                };
            case "prefabStage":
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                return () => EditorSceneManager.MarkSceneDirty(stage.scene);
            case "openScene":
                Scene scene = SceneManager.GetActiveScene();
                return () => EditorSceneManager.MarkSceneDirty(scene);
            default:
                return () => { };
        }
    }

    /// <summary>
    /// 按 targetMode 定位 path 对应的 Transform。path 为 hierarchyPath 或 valueHierarchyPath，
    /// 不会修改 cmd 本身，可以对同一个 cmd 反复调用（比如同时解析 hierarchyPath 和 valueHierarchyPath）。
    /// </summary>
    static bool ResolveTransform(InspectorCommand cmd, string path, out Transform result, out string error)
    {
        result = null;
        error = null;

        switch (cmd.targetMode)
        {
            case "prefabAsset":
            {
                if (string.IsNullOrEmpty(cmd.targetPath))
                {
                    error = "targetMode=prefabAsset 需要 targetPath";
                    return false;
                }
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(cmd.targetPath);
                if (prefabRoot == null)
                {
                    error = $"在 {cmd.targetPath} 找不到 Prefab";
                    return false;
                }
                result = FindByPath(prefabRoot.transform, path ?? string.Empty);
                break;
            }
            case "prefabStage":
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null)
                {
                    error = "当前没有打开 Prefab 编辑模式（双击进入 Prefab 后再试）";
                    return false;
                }
                result = FindByPath(stage.prefabContentsRoot.transform, path ?? string.Empty);
                break;
            }
            case "openScene":
            {
                if (string.IsNullOrEmpty(path))
                {
                    error = "targetMode=openScene 时路径第一段必须是场景里某个根物体的名字";
                    return false;
                }
                string[] segs = path.Split('/');
                Scene scene = SceneManager.GetActiveScene();
                GameObject rootGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == segs[0]);
                if (rootGo == null)
                {
                    error = $"当前打开的场景里找不到根物体 {segs[0]}";
                    return false;
                }
                result = FindByPath(rootGo.transform, string.Join("/", segs.Skip(1)));
                break;
            }
            default:
                error = $"未知 targetMode: {cmd.targetMode}";
                return false;
        }

        if (result == null)
            error ??= $"找不到路径 {path}";
        return result != null;
    }

    static Transform FindByPath(Transform root, string path)
    {
        if (string.IsNullOrEmpty(path))
            return root;
        Transform cur = root;
        foreach (string seg in path.Split('/'))
        {
            Transform next = null;
            for (int i = 0; i < cur.childCount; i++)
            {
                if (cur.GetChild(i).name == seg)
                {
                    next = cur.GetChild(i);
                    break;
                }
            }
            if (next == null)
                return null;
            cur = next;
        }
        return cur;
    }

    static Type FindType(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null)
                return t;
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }
            foreach (var t in types)
                if (t.Name == name)
                    return t;
        }
        return null;
    }

    static FieldInfo FindFieldInfo(Type type, string fieldName)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null)
                return f;
        }
        return null;
    }

    static string Ok(string info) => JsonUtility.ToJson(new InspectorResult { ok = true, info = info });
    static string Fail(string error) => JsonUtility.ToJson(new InspectorResult { ok = false, error = error });
}
