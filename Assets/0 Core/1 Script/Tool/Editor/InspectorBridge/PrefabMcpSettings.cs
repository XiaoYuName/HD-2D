using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Unity Prefab MCP 的项目级配置。资产位于 Assets 中，便于团队通过版本控制共享。
/// </summary>
public sealed class PrefabMcpSettings : ScriptableObject
{
    public const string AssetPath = "Assets/0 Core/1 Script/Tool/Editor/InspectorBridge/Settings/PrefabMcpSettings.asset";
    const string ProjectSettingsPath = "Project/Unity Prefab MCP";

    static PrefabMcpSettings instance;

    [Header("Server")]
    [SerializeField] bool autoStartServer = true;
    [SerializeField, Range(1024, 65535)] int port = 58732;
    [SerializeField, Range(1, 120)] int requestTimeoutSeconds = 20;
    [SerializeField, Min(4096)] int maxRequestBytes = 1024 * 1024;

    [Header("Write Safety")]
    [SerializeField] bool allowPrefabWrites = true;
    [SerializeField] bool createBackupBeforeWrite = true;
    [SerializeField] string backupDirectory = "Library/PrefabMcpBackups";
    [SerializeField] string[] allowedPrefabWriteRoots = { "Assets" };

    [Header("Query Defaults")]
    [SerializeField, Range(1, 1000)] int defaultQueryLimit = 50;
    [SerializeField, Range(1, 5000)] int maximumQueryLimit = 500;
    [SerializeField] string[] defaultAssetSearchFolders = { "Assets" };

    [Header("UI Defaults")]
    [SerializeField] Vector2 defaultUiSize = new Vector2(200f, 60f);
    [SerializeField] TMP_FontAsset defaultTmpFont;
    [SerializeField, Min(1f)] float defaultTmpFontSize = 24f;
    [SerializeField] Color defaultTextColor = Color.white;
    [SerializeField] Color defaultImageColor = Color.white;

    public bool AutoStartServer => autoStartServer;
    public int Port => Mathf.Clamp(port, 1024, 65535);
    public int RequestTimeoutMilliseconds => Mathf.Clamp(requestTimeoutSeconds, 1, 120) * 1000;
    public int MaxRequestBytes => Mathf.Max(4096, maxRequestBytes);
    public bool AllowPrefabWrites => allowPrefabWrites;
    public bool CreateBackupBeforeWrite => createBackupBeforeWrite;
    public string BackupDirectory => NormalizeProjectRelativeDirectory(backupDirectory, "Library/PrefabMcpBackups");
    public string[] AllowedPrefabWriteRoots => allowedPrefabWriteRoots ?? Array.Empty<string>();
    public int DefaultQueryLimit => Mathf.Clamp(defaultQueryLimit, 1, MaximumQueryLimit);
    public int MaximumQueryLimit => Mathf.Clamp(maximumQueryLimit, 1, 5000);
    public string[] DefaultAssetSearchFolders => defaultAssetSearchFolders ?? Array.Empty<string>();
    public Vector2 DefaultUiSize => defaultUiSize;
    public TMP_FontAsset DefaultTmpFont => defaultTmpFont;
    public float DefaultTmpFontSize => Mathf.Max(1f, defaultTmpFontSize);
    public Color DefaultTextColor => defaultTextColor;
    public Color DefaultImageColor => defaultImageColor;

    public static PrefabMcpSettings GetOrCreate()
    {
        if (instance != null)
            return instance;

        instance = AssetDatabase.LoadAssetAtPath<PrefabMcpSettings>(AssetPath);
        if (instance != null)
            return instance;

        string[] guids = AssetDatabase.FindAssets("t:PrefabMcpSettings");
        if (guids.Length > 0)
        {
            instance = AssetDatabase.LoadAssetAtPath<PrefabMcpSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (instance != null)
                return instance;
        }

        EnsureAssetFolder();
        instance = CreateInstance<PrefabMcpSettings>();
        AssetDatabase.CreateAsset(instance, AssetPath);
        AssetDatabase.SaveAssets();
        return instance;
    }

    public bool IsPrefabWritePathAllowed(string assetPath)
    {
        string normalizedPath = NormalizeAssetPath(assetPath).TrimEnd('/');
        foreach (string configuredRoot in AllowedPrefabWriteRoots)
        {
            string root = NormalizeAssetPath(configuredRoot).TrimEnd('/');
            if (string.IsNullOrEmpty(root))
                continue;
            if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public string CreatePrefabBackup(string prefabPath)
    {
        if (!CreateBackupBeforeWrite)
            return null;

        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sourcePath = Path.GetFullPath(Path.Combine(projectPath, prefabPath));
        string backupRoot = Path.GetFullPath(Path.Combine(projectPath, BackupDirectory));
        Directory.CreateDirectory(backupRoot);

        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        string guidPart = string.IsNullOrEmpty(guid) ? "noguid" : guid.Substring(0, Math.Min(8, guid.Length));
        string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Path.GetFileNameWithoutExtension(prefabPath)}_{guidPart}.prefab";
        string backupPath = Path.Combine(backupRoot, fileName);
        File.Copy(sourcePath, backupPath, false);
        return MakeProjectRelative(projectPath, backupPath);
    }

    static string MakeProjectRelative(string projectPath, string path)
    {
        string root = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(root.Length).Replace('\\', '/')
            : path.Replace('\\', '/');
    }

    static string NormalizeAssetPath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim();
    }

    static string NormalizeProjectRelativeDirectory(string path, string fallback)
    {
        string normalized = NormalizeAssetPath(path).Trim('/');
        if (string.IsNullOrEmpty(normalized) || normalized.Contains("..") || Path.IsPathRooted(normalized))
            return fallback;
        return normalized;
    }

    static void EnsureAssetFolder()
    {
        string current = "Assets";
        foreach (string segment in "Editor/PrefabMcp".Split('/'))
        {
            string next = current + "/" + segment;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segment);
            current = next;
        }
    }

    void OnValidate()
    {
        port = Mathf.Clamp(port, 1024, 65535);
        requestTimeoutSeconds = Mathf.Clamp(requestTimeoutSeconds, 1, 120);
        maxRequestBytes = Mathf.Max(4096, maxRequestBytes);
        maximumQueryLimit = Mathf.Clamp(maximumQueryLimit, 1, 5000);
        defaultQueryLimit = Mathf.Clamp(defaultQueryLimit, 1, maximumQueryLimit);
        defaultUiSize.x = Mathf.Max(1f, defaultUiSize.x);
        defaultUiSize.y = Mathf.Max(1f, defaultUiSize.y);
        defaultTmpFontSize = Mathf.Max(1f, defaultTmpFontSize);
    }

    [SettingsProvider]
    static SettingsProvider CreateSettingsProvider()
    {
        SerializedObject serializedSettings = null;
        return new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
        {
            label = "Unity Prefab MCP",
            keywords = new[] { "Unity", "Prefab", "MCP", "AI", "Inspector Bridge" },
            guiHandler = _ =>
            {
                PrefabMcpSettings settings = GetOrCreate();
                if (serializedSettings == null || serializedSettings.targetObject != settings)
                    serializedSettings = new SerializedObject(settings);

                serializedSettings.Update();
                EditorGUILayout.HelpBox("端口或自动启动设置变更后，请重启 Inspector Bridge。写入仍需 MCP 调用显式传 apply=true。", MessageType.Info);
                DrawProperty(serializedSettings, "autoStartServer");
                DrawProperty(serializedSettings, "port");
                DrawProperty(serializedSettings, "requestTimeoutSeconds");
                DrawProperty(serializedSettings, "maxRequestBytes");
                EditorGUILayout.Space();
                DrawProperty(serializedSettings, "allowPrefabWrites");
                DrawProperty(serializedSettings, "createBackupBeforeWrite");
                DrawProperty(serializedSettings, "backupDirectory");
                DrawProperty(serializedSettings, "allowedPrefabWriteRoots", true);
                EditorGUILayout.Space();
                DrawProperty(serializedSettings, "defaultQueryLimit");
                DrawProperty(serializedSettings, "maximumQueryLimit");
                DrawProperty(serializedSettings, "defaultAssetSearchFolders", true);
                EditorGUILayout.Space();
                DrawProperty(serializedSettings, "defaultUiSize");
                DrawProperty(serializedSettings, "defaultTmpFont");
                DrawProperty(serializedSettings, "defaultTmpFontSize");
                DrawProperty(serializedSettings, "defaultTextColor");
                DrawProperty(serializedSettings, "defaultImageColor");
                serializedSettings.ApplyModifiedProperties();

                EditorGUILayout.Space();
                PrefabMcpSettingsControls.DrawServerControls();
                PrefabMcpSettingsControls.DrawCodexConfigControls(settings);
                if (GUILayout.Button("在 Project 中选中配置资产"))
                    Selection.activeObject = settings;
            },
        };
    }

    static void DrawProperty(SerializedObject serializedObject, string propertyName, bool includeChildren = false)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), includeChildren);
    }

    [MenuItem(InspectorBridgeMenu.Root + "/设置")]
    static void OpenSettings()
    {
        SettingsService.OpenProjectSettings(ProjectSettingsPath);
    }
}

/// <summary>Project Settings 与 SO Inspector 共用的服务控制按钮。</summary>
static class PrefabMcpSettingsControls
{
    public static void DrawServerControls()
    {
        bool running = InspectorBridgeServer.IsRunning;
        string status = running
            ? $"服务运行中 · 127.0.0.1:{InspectorBridgeServer.ActivePort}"
            : "服务已停止";
        EditorGUILayout.HelpBox(status, running ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(running))
        {
            if (GUILayout.Button("启动服务"))
                InspectorBridgeServer.Start();
        }
        using (new EditorGUI.DisabledScope(!running))
        {
            if (GUILayout.Button("停止服务"))
                InspectorBridgeServer.Stop();
            if (GUILayout.Button("重启服务"))
                InspectorBridgeServer.Restart();
        }
        EditorGUILayout.EndHorizontal();
    }

    public static void DrawCodexConfigControls(PrefabMcpSettings settings)
    {
        EditorGUILayout.Space();
        bool configured = CodexMcpConfigInstaller.IsConfigured();
        EditorGUILayout.HelpBox(
            configured
                ? "已检测到项目级 Codex MCP 配置。端口或插件路径变化后可点击更新。"
                : "尚未配置 Codex。可自动生成项目级 .codex/config.toml，让 Codex 连接当前项目的 Inspector Bridge。",
            configured ? MessageType.Info : MessageType.Warning);

        if (!GUILayout.Button(configured ? "更新 Codex MCP 配置" : "初始化 Codex MCP 配置"))
            return;

        try
        {
            CodexMcpConfigInstaller.InstallOrUpdate(settings.Port);
            EditorUtility.DisplayDialog(
                "Inspector Bridge",
                "Codex MCP 配置已写入 .codex/config.toml。\n\n请重启 Codex 会话以加载或刷新 MCP 工具。",
                "确定");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Inspector Bridge", $"Codex MCP 配置失败：\n{e.Message}", "确定");
        }
    }
}

/// <summary>安全创建或更新项目级 .codex/config.toml 中由 Inspector Bridge 管理的 MCP 段落。</summary>
static class CodexMcpConfigInstaller
{
    const string ConfigRelativePath = ".codex/config.toml";
    const string McpScriptAssetPath = "Assets/0 Core/1 Script/Tool/Editor/InspectorBridge/McpServer/unity-prefab-mcp.ps1";
    const string SectionHeader = "[mcp_servers.unity_prefab]";
    const string BeginMarker = "# BEGIN InspectorBridge Codex MCP (generated)";
    const string EndMarker = "# END InspectorBridge Codex MCP (generated)";

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string ConfigPath => Path.Combine(ProjectRoot, ConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public static bool IsConfigured()
    {
        return File.Exists(ConfigPath) &&
               File.ReadAllText(ConfigPath).IndexOf(SectionHeader, StringComparison.Ordinal) >= 0;
    }

    public static void InstallOrUpdate(int port)
    {
        string configPath = ConfigPath;
        string mcpScriptPath = Path.Combine(ProjectRoot, McpScriptAssetPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(mcpScriptPath))
            throw new FileNotFoundException("找不到 Inspector Bridge MCP 启动脚本。", mcpScriptPath);

        Directory.CreateDirectory(Path.GetDirectoryName(configPath));

        string current = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;
        string newline = current.Contains("\r\n") ? "\r\n" : "\n";
        string generatedBlock = BuildGeneratedBlock(Mathf.Clamp(port, 1024, 65535), newline);
        string updated = ReplaceGeneratedOrServerSection(current, generatedBlock, newline);

        if (string.Equals(current, updated, StringComparison.Ordinal))
            return;

        File.WriteAllText(configPath, updated, new UTF8Encoding(false));
        Debug.Log($"[InspectorBridge] Codex MCP 配置已写入：{ConfigRelativePath}");
    }

    static string BuildGeneratedBlock(int port, string newline)
    {
        string command = "$projectRoot = (& git rev-parse --show-toplevel).Trim(); " +
                         $"& (Join-Path $projectRoot '{McpScriptAssetPath}') -ProjectPath $projectRoot -Port {port}";

        string[] lines =
        {
            BeginMarker,
            SectionHeader,
            "enabled = true",
            "required = false",
            "command = \"powershell.exe\"",
            "args = [",
            "  \"-NoLogo\",",
            "  \"-NoProfile\",",
            "  \"-ExecutionPolicy\",",
            "  \"Bypass\",",
            "  \"-Command\",",
            $"  \"{EscapeTomlBasicString(command)}\",",
            "]",
            "startup_timeout_sec = 15",
            "tool_timeout_sec = 60",
            EndMarker,
        };
        return string.Join(newline, lines);
    }

    static string ReplaceGeneratedOrServerSection(string current, string generatedBlock, string newline)
    {
        Match generatedMatch = Regex.Match(
            current,
            $@"(?ms)^\s*{Regex.Escape(BeginMarker)}.*?^\s*{Regex.Escape(EndMarker)}\s*(?:\r?\n)?");
        if (generatedMatch.Success)
            return current.Remove(generatedMatch.Index, generatedMatch.Length)
                .Insert(generatedMatch.Index, generatedBlock + newline);

        Match sectionMatch = Regex.Match(current, $@"(?m)^\s*{Regex.Escape(SectionHeader)}\s*(?:\r?\n|$)");
        if (sectionMatch.Success)
        {
            Match nextSection = new Regex(@"^\s*\[[^\r\n]+\]", RegexOptions.Multiline)
                .Match(current, sectionMatch.Index + sectionMatch.Length);
            int end = nextSection.Success ? nextSection.Index : current.Length;
            return current.Remove(sectionMatch.Index, end - sectionMatch.Index)
                .Insert(sectionMatch.Index, generatedBlock + newline + (nextSection.Success ? newline : string.Empty));
        }

        if (string.IsNullOrWhiteSpace(current))
            return generatedBlock + newline;

        return current.TrimEnd('\r', '\n') + newline + newline + generatedBlock + newline;
    }

    static string EscapeTomlBasicString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

[CustomEditor(typeof(PrefabMcpSettings))]
public sealed class PrefabMcpSettingsInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        PrefabMcpSettingsControls.DrawServerControls();
        PrefabMcpSettingsControls.DrawCodexConfigControls((PrefabMcpSettings)target);
        EditorGUILayout.HelpBox("端口变更后请重启服务，并让 MCP 客户端使用相同的 -Port 参数。", MessageType.None);
    }
}
