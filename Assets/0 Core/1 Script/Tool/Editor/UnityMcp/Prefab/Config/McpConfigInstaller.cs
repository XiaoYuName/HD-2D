using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp
{
    /// <summary>
    /// 把本项目的 MCP 服务写进各客户端的配置文件，只覆盖本服务那一段，保留用户的其他配置。
    /// 端口不写死在配置里也能用：桥会把实际端口写到 Library/PrefabMcpPort.txt，ps1 每次请求都优先读它。
    /// </summary>
    static class McpConfigInstaller
    {
        public const string McpScriptAssetPath =
            "Assets/0 Core/1 Script/Tool/Editor/UnityMcp/Prefab/McpServer/unity-prefab-mcp.ps1";

        const string ServerKey = "unity_prefab";
        const string ClaudeConfigRelativePath = ".mcp.json";
        const string CodexConfigRelativePath = ".codex/config.toml";
        const string CodexSectionHeader = "[mcp_servers.unity_prefab]";
        const string CodexBeginMarker = "# BEGIN UnityMcp Codex MCP (generated)";
        const string CodexEndMarker = "# END UnityMcp Codex MCP (generated)";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string ClaudeConfigPath => Path.Combine(ProjectRoot, ClaudeConfigRelativePath);
        static string CodexConfigPath => Path.Combine(ProjectRoot,
            CodexConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));

        public static void InstallOrUpdateAll(int port)
        {
            int clamped = Mathf.Clamp(port, 1024, 65535);
            InstallOrUpdateCodex(clamped);
            InstallOrUpdateClaude(clamped);
        }

        public static void InstallOrUpdateOnStartup(int port)
        {
            try
            {
                InstallOrUpdateAll(port);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityMcp] 启动时同步 MCP 配置失败：{e.Message}");
            }
        }

        // ---- Claude Code / 其他读 .mcp.json 的客户端 ----

        public static bool IsClaudeConfigured() =>
            File.Exists(ClaudeConfigPath) &&
            JObject.Parse(File.ReadAllText(ClaudeConfigPath))["mcpServers"]?[ServerKey] != null;

        static void InstallOrUpdateClaude(int port)
        {
            string current = File.Exists(ClaudeConfigPath) ? File.ReadAllText(ClaudeConfigPath) : null;
            JObject root = current != null ? JObject.Parse(current) : new JObject();
            if (root["mcpServers"] is not JObject servers)
            {
                servers = new JObject();
                root["mcpServers"] = servers;
            }

            servers[ServerKey] = new JObject
            {
                // pwsh 而非 powershell.exe：5.1 读无 BOM 文件按 ANSI 代码页解码，中文会吃掉代码行。
                ["command"] = "pwsh",
                ["args"] = new JArray(
                    "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass",
                    // 项目相对路径：客户端以项目根为工作目录启动，换机器换克隆目录都不用改。
                    "-File", McpScriptAssetPath,
                    "-Port", port.ToString()),
            };

            string updated = root.ToString(Formatting.Indented);
            if (string.Equals(current, updated, StringComparison.Ordinal))
                return;

            WriteTextAtomically(ClaudeConfigPath, updated);
            Debug.Log($"[UnityMcp] Claude Code MCP 配置已写入：{ClaudeConfigRelativePath}");
        }

        // ---- Codex ----

        public static bool IsCodexConfigured() =>
            File.Exists(CodexConfigPath) &&
            File.ReadAllText(CodexConfigPath).IndexOf(CodexSectionHeader, StringComparison.Ordinal) >= 0;

        static void InstallOrUpdateCodex(int port)
        {
            string scriptPath = Path.Combine(ProjectRoot, McpScriptAssetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("找不到 Unity MCP 启动脚本。", scriptPath);

            Directory.CreateDirectory(Path.GetDirectoryName(CodexConfigPath));
            string current = File.Exists(CodexConfigPath) ? File.ReadAllText(CodexConfigPath) : string.Empty;
            string newline = current.Contains("\r\n") ? "\r\n" : "\n";
            string updated = ReplaceGeneratedOrSection(current, BuildCodexBlock(port, newline), newline);
            if (string.Equals(current, updated, StringComparison.Ordinal))
                return;

            WriteTextAtomically(CodexConfigPath, updated);
            Debug.Log($"[UnityMcp] Codex MCP 配置已写入：{CodexConfigRelativePath}");
        }

        static string BuildCodexBlock(int port, string newline)
        {
            string command = "$projectRoot = (& git rev-parse --show-toplevel).Trim(); " +
                $"& (Join-Path $projectRoot '{McpScriptAssetPath}') -ProjectPath $projectRoot -Port {port}";

            string[] lines =
            {
                CodexBeginMarker,
                CodexSectionHeader,
                "enabled = true",
                "required = false",
                "command = \"pwsh\"",
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
                CodexEndMarker,
            };
            return string.Join(newline, lines);
        }

        static string ReplaceGeneratedOrSection(string current, string generatedBlock, string newline)
        {
            Match generatedMatch = Regex.Match(current,
                $@"(?ms)^\s*{Regex.Escape(CodexBeginMarker)}.*?^\s*{Regex.Escape(CodexEndMarker)}\s*(?:\r?\n)?");
            if (generatedMatch.Success)
                return current.Remove(generatedMatch.Index, generatedMatch.Length)
                    .Insert(generatedMatch.Index, generatedBlock + newline);

            Match sectionMatch = Regex.Match(current, $@"(?m)^\s*{Regex.Escape(CodexSectionHeader)}\s*(?:\r?\n|$)");
            if (sectionMatch.Success)
            {
                Match nextSection = new Regex(@"^\s*\[[^\r\n]+\]", RegexOptions.Multiline)
                    .Match(current, sectionMatch.Index + sectionMatch.Length);
                int end = nextSection.Success ? nextSection.Index : current.Length;
                return current.Remove(sectionMatch.Index, end - sectionMatch.Index)
                    .Insert(sectionMatch.Index,
                        generatedBlock + newline + (nextSection.Success ? newline : string.Empty));
            }

            if (string.IsNullOrWhiteSpace(current))
                return generatedBlock + newline;
            return current.TrimEnd('\r', '\n') + newline + newline + generatedBlock + newline;
        }

        static string EscapeTomlBasicString(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static void WriteTextAtomically(string path, string content)
        {
            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, content, new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// 查找 pwsh 并在缺失时引导安装。不能只查 PATH：Unity 的环境变量是启动时的快照，
    /// 刚装完的 pwsh 在重启 Unity 前不会出现在 PATH 里。
    /// </summary>
    static class PowerShell7Locator
    {
        public const string DownloadUrl = "https://github.com/PowerShell/PowerShell/releases/latest";

        const string WingetArguments =
            "install --id Microsoft.PowerShell --source winget " +
            "--accept-package-agreements --accept-source-agreements";

        public static string Find()
        {
            foreach (string candidate in EnumerateCandidates())
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        static System.Collections.Generic.IEnumerable<string> EnumerateCandidates()
        {
            foreach (string variable in new[] { "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)" })
            {
                string root = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrEmpty(root))
                    continue;
                // 7 之后的大版本各自独立安装（7 / 8 / …），倒序取最新的一个。
                string powerShellRoot = Path.Combine(root, "PowerShell");
                if (!Directory.Exists(powerShellRoot))
                    continue;
                string[] versionDirs = Directory.GetDirectories(powerShellRoot);
                Array.Sort(versionDirs, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(versionDirs);
                foreach (string versionDir in versionDirs)
                    yield return Path.Combine(versionDir, "pwsh.exe");
            }

            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
                yield return Path.Combine(localAppData, @"Microsoft\WindowsApps\pwsh.exe");

            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                yield break;
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(dir))
                    yield return Path.Combine(dir.Trim(), "pwsh.exe");
            }
        }

        public static void InstallViaWinget()
        {
            try
            {
                // 用可见的 cmd 窗口跑，安装过程和失败原因都留在屏幕上；/k 让窗口不自动关闭。
                var startInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/k winget {WingetArguments}")
                {
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(startInfo);
                EditorUtility.DisplayDialog("Unity MCP",
                    "已打开安装窗口。安装完成后请重启 Unity 和 MCP 客户端会话（Unity 的 PATH 是启动时快照）。\n\n" +
                    "如果提示找不到 winget，请改用「打开下载页」装 MSI。",
                    "确定");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Application.OpenURL(DownloadUrl);
            }
        }
    }
}
