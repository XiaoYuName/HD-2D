#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class OdinUIBindFromFieldsGenerator : OdinEditorWindow
{
    [Title("根据字段自动生成 UI 绑定代码")]

    [LabelText("UI 根节点")]
    [Required]
    public GameObject uiRoot;

    [LabelText("目标脚本")]
    [Required]
    public MonoScript targetScript;

    [LabelText("包含 private 字段")]
    public bool includePrivateFields = true;

    [LabelText("包含 protected 字段")]
    public bool includeProtectedFields = true;

    [LabelText("包含 public 字段")]
    public bool includePublicFields = false;

    [LabelText("只生成 Component 类型字段")]
    public bool onlyComponentFields = true;

    [LabelText("生成 this.")]
    public bool useThisPrefix = false;

    [LabelText("生成 public override void Init()")]
    public bool generateInitMethodWrapper = true;

    [LabelText("匹配失败时生成注释")]
    public bool generateMissingComment = true;

    [LabelText("使用字段名后缀辅助匹配")]
    public bool useFieldSuffixMatch = true;

    [LabelText("生成结果")]
    [TextArea(15, 40)]
    [HideLabel]
    public string resultCode;

    [MenuItem("Tools/UI/根据字段生成 UI 绑定代码")]
    private static void Open()
    {
        GetWindow<OdinUIBindFromFieldsGenerator>("字段绑定生成器").Show();
    }

    [Button("使用当前选中物体作为 UI 根节点", ButtonSizes.Large)]
    private void UseSelectionAsRoot()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("当前没有选中 GameObject");
            return;
        }

        uiRoot = Selection.activeGameObject;
    }

    [Button("生成绑定代码", ButtonSizes.Large)]
    private void GenerateBindCode()
    {
        if (uiRoot == null)
        {
            Debug.LogError("请先设置 UI 根节点");
            return;
        }

        if (targetScript == null)
        {
            Debug.LogError("请先设置目标脚本");
            return;
        }

        Type targetType = targetScript.GetClass();

        if (targetType == null)
        {
            Debug.LogError("无法获取目标脚本 Class，请确认脚本没有编译错误，并且文件名和类名一致");
            return;
        }

        FieldInfo[] fields = GetTargetFields(targetType);

        if (fields.Length == 0)
        {
            Debug.LogWarning("目标脚本中没有找到可绑定字段");
            return;
        }

        Transform root = uiRoot.transform;

        List<Transform> allChildren = root
            .GetComponentsInChildren<Transform>(true)
            .Where(t => t != root)
            .ToList();

        StringBuilder sb = new StringBuilder();

        if (generateInitMethodWrapper)
        {
            sb.AppendLine("public override void Init()");
            sb.AppendLine("{");
        }

        string indent = generateInitMethodWrapper ? "    " : "";
        string prefix = useThisPrefix ? "this." : "";

        foreach (FieldInfo field in fields)
        {
            Type fieldType = field.FieldType;

            Transform match = FindBestMatch(root, allChildren, field);

            if (match == null)
            {
                if (generateMissingComment)
                {
                    sb.AppendLine($"{indent}// 未找到匹配节点：{field.Name} : {GetTypeName(fieldType)}");
                }

                continue;
            }

            string path = GetTransformPath(root, match);

            sb.AppendLine($"{indent}{prefix}{field.Name} = Get<{GetTypeName(fieldType)}>(\"{path}\");");
        }

        if (generateInitMethodWrapper)
        {
            sb.AppendLine("}");
        }

        resultCode = sb.ToString();

        GUIUtility.systemCopyBuffer = resultCode;

        Debug.Log("绑定代码已生成，并已复制到剪贴板");
    }

    [Button("复制生成结果", ButtonSizes.Medium)]
    private void CopyResult()
    {
        if (string.IsNullOrEmpty(resultCode))
        {
            Debug.LogWarning("当前没有可复制的代码");
            return;
        }

        GUIUtility.systemCopyBuffer = resultCode;
        Debug.Log("已复制到剪贴板");
    }

    private FieldInfo[] GetTargetFields(Type targetType)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.DeclaredOnly;

        if (includePrivateFields || includeProtectedFields)
            flags |= BindingFlags.NonPublic;

        if (includePublicFields)
            flags |= BindingFlags.Public;

        FieldInfo[] fields = targetType.GetFields(flags);

        return fields
            .Where(field => !field.IsStatic)
            .Where(field =>
            {
                if (field.IsPrivate)
                    return includePrivateFields;

                if (field.IsFamily)
                    return includeProtectedFields;

                if (field.IsPublic)
                    return includePublicFields;

                return false;
            })
            .Where(field =>
            {
                if (!onlyComponentFields)
                    return true;

                return typeof(Component).IsAssignableFrom(field.FieldType);
            })
            .ToArray();
    }

    private Transform FindBestMatch(Transform root, List<Transform> allChildren, FieldInfo field)
    {
        Type fieldType = field.FieldType;

        string normalizedFieldName = NormalizeName(field.Name);
        string simplifiedFieldName = SimplifyFieldName(field.Name, fieldType);

        Transform bestTransform = null;
        int bestScore = int.MinValue;

        foreach (Transform child in allChildren)
        {
            Component component = child.GetComponent(fieldType);

            if (component == null)
                continue;

            string normalizedChildName = NormalizeName(child.name);

            int score = 0;

            // 完全匹配
            if (normalizedChildName == normalizedFieldName)
                score += 10000;

            // 去掉 Img、Button、String、Text 等后缀后匹配
            if (normalizedChildName == simplifiedFieldName)
                score += 8000;

            // 互相包含匹配
            if (normalizedChildName.Contains(simplifiedFieldName))
                score += 4000;

            if (simplifiedFieldName.Contains(normalizedChildName))
                score += 3000;

            // 根据字段名关键词匹配
            score += GetKeywordScore(field.Name, child.name);

            // 根据类型关键词匹配
            score += GetTypeKeywordScore(fieldType, child.name);

            // 路径越短，稍微优先
            score -= GetDepth(root, child) * 10;

            if (score > bestScore)
            {
                bestScore = score;
                bestTransform = child;
            }
        }

        return bestTransform;
    }

    private int GetKeywordScore(string fieldName, string objectName)
    {
        string field = fieldName.ToLower();
        string obj = objectName.ToLower();

        int score = 0;

        if (field.Contains("icon") && obj.Contains("icon"))
            score += 1000;

        if (field.Contains("name") && obj.Contains("name"))
            score += 1000;

        if (field.Contains("price") && obj.Contains("price"))
            score += 1000;

        if (field.Contains("description") && obj.Contains("des"))
            score += 1000;

        if (field.Contains("des") && obj.Contains("des"))
            score += 1000;

        if (field.Contains("number") && obj.Contains("number"))
            score += 1000;

        if (field.Contains("count") && obj.Contains("count"))
            score += 1000;

        if (field.Contains("add") && obj.Contains("add"))
            score += 1000;

        if (field.Contains("remove") && obj.Contains("remove"))
            score += 1000;

        if (field.Contains("buy") && obj.Contains("buy"))
            score += 800;

        return score;
    }

    private int GetTypeKeywordScore(Type fieldType, string objectName)
    {
        string obj = objectName.ToLower();
        string typeName = fieldType.Name.ToLower();

        int score = 0;

        if (typeName.Contains("image"))
        {
            if (obj.Contains("image"))
                score += 300;

            if (obj.Contains("icon"))
                score += 600;
        }

        if (typeName.Contains("button"))
        {
            if (obj.Contains("button"))
                score += 600;

            if (obj.Contains("btn"))
                score += 600;
        }

        if (typeName.Contains("localizestringevent"))
        {
            if (obj.Contains("text"))
                score += 400;

            if (obj.Contains("tmp"))
                score += 400;

            if (obj.Contains("tex"))
                score += 400;
        }

        return score;
    }

    private string SimplifyFieldName(string fieldName, Type fieldType)
    {
        string result = NormalizeName(fieldName);

        if (!useFieldSuffixMatch)
            return result;

        string[] commonSuffixes =
        {
            "img",
            "image",
            "button",
            "btn",
            "string",
            "text",
            "txt",
            "tmp",
            "event",
            "localize",
            "localizestringevent",
            "scrollrect",
            "slider",
            "toggle",
            "inputfield"
        };

        foreach (string suffix in commonSuffixes)
        {
            if (result.EndsWith(suffix))
            {
                result = result.Substring(0, result.Length - suffix.Length);
                break;
            }
        }

        return result;
    }

    private string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        return name
            .ToLower()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    private string GetTransformPath(Transform root, Transform target)
    {
        if (root == target)
            return string.Empty;

        Stack<string> pathStack = new Stack<string>();

        Transform current = target;

        while (current != null && current != root)
        {
            pathStack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", pathStack);
    }

    private int GetDepth(Transform root, Transform target)
    {
        int depth = 0;

        Transform current = target;

        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private string GetTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        string typeName = type.Name;
        int index = typeName.IndexOf('`');

        if (index >= 0)
            typeName = typeName.Substring(0, index);

        string genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));

        return $"{typeName}<{genericArgs}>";
    }
}

#endif