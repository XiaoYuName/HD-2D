using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// Unity MCP 的项目级配置（纯数据 + 写入策略判断）。资产放在 Assets 里，便于团队通过版本控制共享。
    /// 设置面板见 <see cref="UnityMcpSettingsUI"/>，客户端配置写入见 <see cref="McpConfigInstaller"/>。
    /// </summary>
    public sealed class UnityMcpSettings : ScriptableObject
    {
        const string AssetName = "UnityMcpSettings.asset";
        const string McpScriptName = "unity-mcp.ps1";

        internal const string ProjectSettingsPath = "Project/Unity MCP";

        static UnityMcpSettings instance;

        [Header("Client")]
        [SerializeField] DefaultAsset mcpScriptAsset;
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

        public static string AssetPath => instance != null
            ? AssetDatabase.GetAssetPath(instance)
            : GetDefaultAssetPath();
        public DefaultAsset McpScriptAsset => GetMcpScriptAsset();
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

        public static UnityMcpSettings GetOrCreate()
        {
            if (instance != null)
                return instance;

            string[] guids = AssetDatabase.FindAssets("t:UnityMcpSettings");
            if (guids.Length > 0)
            {
                instance = AssetDatabase.LoadAssetAtPath<UnityMcpSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (instance != null)
                    return instance;
            }

            string assetPath = GetDefaultAssetPath();
            CreateAssetFolder(assetPath);
            instance = CreateInstance<UnityMcpSettings>();
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();
            return instance;
        }

        DefaultAsset GetMcpScriptAsset()
        {
            if (mcpScriptAsset != null &&
                string.Equals(Path.GetFileName(AssetDatabase.GetAssetPath(mcpScriptAsset)), McpScriptName,
                    StringComparison.OrdinalIgnoreCase))
                return mcpScriptAsset;

            string moduleRoot = Path.GetDirectoryName(GetDefaultAssetPath())?.Replace('\\', '/');
            moduleRoot = Path.GetDirectoryName(moduleRoot)?.Replace('\\', '/');
            foreach (string guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(McpScriptName),
                         string.IsNullOrEmpty(moduleRoot) ? null : new[] { moduleRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileName(path), McpScriptName, StringComparison.OrdinalIgnoreCase))
                    continue;
                mcpScriptAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                EditorUtility.SetDirty(this);
                return mcpScriptAsset;
            }
            return null;
        }

        static string GetDefaultAssetPath()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(UnityMcpSettings)} t:Script"))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script != null && script.GetClass() == typeof(UnityMcpSettings))
                {
                    string configFolder = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
                    string moduleFolder = Path.GetDirectoryName(configFolder)?.Replace('\\', '/');
                    return $"{moduleFolder}/Settings/{AssetName}";
                }
            }
            throw new InvalidOperationException($"找不到 {nameof(UnityMcpSettings)} 脚本资产。");
        }

        /// <summary>查询条数：请求值 &lt;= 0 时取配置默认值，且始终不超过配置上限与工具硬上限。</summary>
        public static int Limit(int requested, int hardMaximum)
        {
            UnityMcpSettings settings = GetOrCreate();
            int maximum = Math.Min(settings.MaximumQueryLimit, hardMaximum);
            int fallback = Math.Min(settings.DefaultQueryLimit, maximum);
            return requested <= 0 ? fallback : Math.Min(requested, maximum);
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
            string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_" +
                $"{Path.GetFileNameWithoutExtension(prefabPath)}_{guidPart}.prefab";
            string backupPath = Path.Combine(backupRoot, fileName);
            File.Copy(sourcePath, backupPath, false);
            return GetProjectRelativePath(projectPath, backupPath);
        }

        static string GetProjectRelativePath(string projectPath, string path)
        {
            string root = projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).Replace('\\', '/')
                : path.Replace('\\', '/');
        }

        static string NormalizeAssetPath(string path) => (path ?? string.Empty).Replace('\\', '/').Trim();

        static string NormalizeProjectRelativeDirectory(string path, string fallback)
        {
            string normalized = NormalizeAssetPath(path).Trim('/');
            if (string.IsNullOrEmpty(normalized) || normalized.Contains("..") || Path.IsPathRooted(normalized))
                return fallback;
            return normalized;
        }

        /// <summary>按配置资产路径逐级补齐目录。</summary>
        static void CreateAssetFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length - 1; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        void OnValidate()
        {
            GetMcpScriptAsset();
            port = Mathf.Clamp(port, 1024, 65535);
            requestTimeoutSeconds = Mathf.Clamp(requestTimeoutSeconds, 1, 120);
            maxRequestBytes = Mathf.Max(4096, maxRequestBytes);
            maximumQueryLimit = Mathf.Clamp(maximumQueryLimit, 1, 5000);
            defaultQueryLimit = Mathf.Clamp(defaultQueryLimit, 1, maximumQueryLimit);
            defaultUiSize.x = Mathf.Max(1f, defaultUiSize.x);
            defaultUiSize.y = Mathf.Max(1f, defaultUiSize.y);
            defaultTmpFontSize = Mathf.Max(1f, defaultTmpFontSize);
        }
    }
}
