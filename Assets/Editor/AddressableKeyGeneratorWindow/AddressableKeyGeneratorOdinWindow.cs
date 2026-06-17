#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class AddressableKeyGeneratorOdinWindow : OdinEditorWindow
{
    [MenuItem("Tools/XFramework/Addressable Key 生成器")]
    private static void OpenWindow()
    {
        var window = GetWindow<AddressableKeyGeneratorOdinWindow>();
        window.titleContent = new GUIContent("Addressable Key 生成器");
        window.minSize = new Vector2(620, 420);
        window.Show();
    }

    [Title("Addressable Key 常量生成器")]

    [BoxGroup("路径设置")]
    [LabelText("资源文件夹")]
    [FolderPath(RequireExistingPath = true)]
    [ValidateInput(nameof(IsValidAssetFolder), "必须选择 Assets 目录下的文件夹")]
    public string TargetFolder = "Assets/AddressableAssets/Remote";

    [BoxGroup("路径设置")]
    [LabelText("输出文件夹")]
    [FolderPath(RequireExistingPath = true)]
    [ValidateInput(nameof(IsValidAssetFolder), "必须选择 Assets 目录下的文件夹")]
    public string OutputFolder = "Assets/Scripts/Generated";

    [BoxGroup("生成设置")]
    [LabelText("命名空间")]
    public string NamespaceName = "XFramework";

    [BoxGroup("生成设置")]
    [LabelText("类名")]
    public string ClassName = "AssetKeys";

    [BoxGroup("生成设置")]
    [LabelText("常量后缀")]
    public string ConstSuffix = "Prefab";

    [BoxGroup("生成设置")]
    [LabelText("包含子文件夹")]
    public bool IncludeSubFolders = true;

    [BoxGroup("生成设置")]
    [LabelText("只生成 Prefab")]
    public bool OnlyPrefab = true;

    [BoxGroup("生成设置")]
    [LabelText("覆盖同名文件")]
    public bool OverwriteFile = true;

    [BoxGroup("生成设置")]
    [LabelText("使用完整路径生成常量名")]
    [InfoBox("关闭时只使用资源文件名生成常量名，例如 SceneCharacterPrefab。开启时会根据路径生成更长的名字，减少重名。")]
    public bool UseFullPathAsConstName = false;

    [BoxGroup("预览")]
    [LabelText("找到的资源数量")]
    [ReadOnly]
    public int AssetCount;

    [BoxGroup("预览")]
    [LabelText("输出路径")]
    [ReadOnly]
    [ShowInInspector]
    private string OutputFilePath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(OutputFolder) || string.IsNullOrWhiteSpace(ClassName))
            {
                return string.Empty;
            }

            return $"{OutputFolder}/{ClassName}.cs";
        }
    }

    [BoxGroup("操作")]
    [Button("刷新预览", ButtonSizes.Medium)]
    private void RefreshPreview()
    {
        AssetCount = FindAssetPaths().Count;
        Debug.Log($"找到资源数量: {AssetCount}");
    }

    [BoxGroup("操作")]
    [Button("生成常量类", ButtonSizes.Large)]
    [GUIColor(0.3f, 0.8f, 0.4f)]
    private void Generate()
    {
        if (!IsValidAssetFolder(TargetFolder))
        {
            Debug.LogError($"资源文件夹无效: {TargetFolder}");
            return;
        }

        if (!IsValidAssetFolder(OutputFolder))
        {
            Debug.LogError($"输出文件夹无效: {OutputFolder}");
            return;
        }

        if (string.IsNullOrWhiteSpace(ClassName))
        {
            Debug.LogError("类名不能为空");
            return;
        }

        var assetPaths = FindAssetPaths();

        if (assetPaths.Count == 0)
        {
            Debug.LogWarning($"没有找到可生成的资源: {TargetFolder}");
            return;
        }

        string outputPath = $"{OutputFolder}/{ClassName}.cs";

        if (File.Exists(outputPath) && !OverwriteFile)
        {
            Debug.LogError($"文件已存在，且未开启覆盖: {outputPath}");
            return;
        }

        string scriptContent = BuildScript(assetPaths);

        string directory = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, scriptContent, Encoding.UTF8);
        AssetDatabase.Refresh();

        AssetCount = assetPaths.Count;

        Debug.Log($"生成成功: {outputPath}\n共生成 {assetPaths.Count} 条资源常量");
    }

    private List<string> FindAssetPaths()
    {
        List<string> result = new List<string>();

        if (!IsValidAssetFolder(TargetFolder))
        {
            return result;
        }

        string filter = OnlyPrefab ? "t:Prefab" : string.Empty;
        string[] guids = AssetDatabase.FindAssets(filter, new[] { TargetFolder });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (!IncludeSubFolders)
            {
                string directory = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");

                if (directory != TargetFolder)
                {
                    continue;
                }
            }

            if (OnlyPrefab && !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(assetPath);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private string BuildScript(List<string> assetPaths)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// ------------------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     此文件由 AddressableKeyGeneratorOdinWindow 自动生成。");
        sb.AppendLine("//     请不要手动修改，重新生成会覆盖内容。");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// ------------------------------------------------------------------------------");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(NamespaceName))
        {
            sb.AppendLine($"namespace {NamespaceName}");
            sb.AppendLine("{");
        }

        string indent = string.IsNullOrWhiteSpace(NamespaceName) ? "" : "    ";
        string memberIndent = string.IsNullOrWhiteSpace(NamespaceName) ? "    " : "        ";

        sb.AppendLine($"{indent}public static class {ClassName}");
        sb.AppendLine($"{indent}{{");

        HashSet<string> usedConstNames = new HashSet<string>();

        foreach (string assetPath in assetPaths)
        {
            string constName = UseFullPathAsConstName
                ? PathToConstName(assetPath)
                : FileNameToConstName(assetPath);

            constName += ConstSuffix;
            constName = MakeUniqueName(constName, usedConstNames);

            sb.AppendLine($"{memberIndent}public const string {constName} = \"{assetPath}\";");
        }

        sb.AppendLine($"{indent}}}");

        if (!string.IsNullOrWhiteSpace(NamespaceName))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private string FileNameToConstName(string assetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        return ToPascalCase(fileName);
    }

    private string PathToConstName(string assetPath)
    {
        string path = Path.ChangeExtension(assetPath, null);
        path = path.Replace("\\", "/");

        if (path.StartsWith("Assets/"))
        {
            path = path.Substring("Assets/".Length);
        }

        string[] parts = path.Split('/');

        StringBuilder sb = new StringBuilder();

        foreach (string part in parts)
        {
            sb.Append(ToPascalCase(part));
        }

        return sb.ToString();
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        StringBuilder sb = new StringBuilder();
        bool nextUpper = true;

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (sb.Length == 0 && char.IsDigit(c))
                {
                    sb.Append('_');
                }

                sb.Append(nextUpper ? char.ToUpperInvariant(c) : c);
                nextUpper = false;
            }
            else
            {
                nextUpper = true;
            }
        }

        return sb.Length == 0 ? "Unnamed" : sb.ToString();
    }

    private static string MakeUniqueName(string baseName, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(baseName))
        {
            usedNames.Add(baseName);
            return baseName;
        }

        int index = 2;

        while (true)
        {
            string newName = baseName + index;

            if (!usedNames.Contains(newName))
            {
                usedNames.Add(newName);
                return newName;
            }

            index++;
        }
    }

    private static bool IsValidAssetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        path = path.Replace("\\", "/");

        if (!path.StartsWith("Assets"))
        {
            return false;
        }

        return AssetDatabase.IsValidFolder(path);
    }
}

#endif