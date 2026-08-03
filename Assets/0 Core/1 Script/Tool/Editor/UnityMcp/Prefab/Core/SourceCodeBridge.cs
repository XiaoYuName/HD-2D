using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace UnityMcp
{
    /// <summary>
    /// 将 UnityMcp 内置的 SourceCodeMcp 暴露给 Unity MCP。
    /// 内部发行版不依赖项目根 Tools 目录，后台转发也不会阻塞 Unity 主线程。
    /// </summary>
    static class SourceCodeBridge
    {
        const int RequestTimeoutMilliseconds = 120000;
        const int StopTimeoutMilliseconds = 500;
        const int MaxErrorChars = 2000;

        static readonly object ProcessSync = new();
        static readonly object ErrorSync = new();
        static JArray toolDefinitions;
        static HashSet<string> toolNames;
        static Process process;
        static StreamWriter input;
        static StreamReader output;
        static long requestId;
        static string lastError;

        public static IEnumerable<string> ToolNames
        {
            get
            {
                LoadDefinitions();
                return toolNames;
            }
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        public static JArray GetToolDefinitions()
        {
            LoadDefinitions();
            return (JArray)toolDefinitions.DeepClone();
        }

        public static bool Handles(string toolName)
        {
            LoadDefinitions();
            return toolNames.Contains(toolName);
        }

        public static string Dispatch(JObject payload)
        {
            JObject arguments = (JObject)payload.DeepClone();
            arguments.Remove("action");
            arguments.Remove("expectedProjectPath");

            lock (ProcessSync)
            {
                try
                {
                    Start();
                    long id = ++requestId;
                    JObject request = new()
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = id,
                        ["method"] = "tools/call",
                        ["params"] = new JObject
                        {
                            ["name"] = (string)payload["action"],
                            ["arguments"] = arguments,
                        },
                    };
                    input.WriteLine(request.ToString(Formatting.None));

                    Task<string> readTask = output.ReadLineAsync();
                    if (!readTask.Wait(RequestTimeoutMilliseconds))
                    {
                        StopProcess();
                        return BridgeJson.Fail("SourceCodeMcp 请求超时。");
                    }

                    string line = readTask.GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(line))
                        throw new IOException("SourceCodeMcp 已退出。");

                    JObject response = JObject.Parse(line);
                    if (response["error"] != null)
                        return BridgeJson.Fail("SourceCodeMcp JSON-RPC 错误: " + response["error"].ToString(Formatting.None));

                    string text = (string)response.SelectToken("result.content[0].text");
                    return string.IsNullOrEmpty(text)
                        ? BridgeJson.Fail("SourceCodeMcp 返回了空结果。")
                        : text;
                }
                catch (Exception e)
                {
                    StopProcess();
                    return BridgeJson.Fail("SourceCodeMcp 调用失败: " + e.Message + GetErrorSuffix());
                }
            }
        }

        static void LoadDefinitions()
        {
            if (toolDefinitions != null)
                return;

            string path = GetDefinitionPath();
            toolDefinitions = JArray.Parse(File.ReadAllText(path, Encoding.UTF8));
            toolNames = new(StringComparer.Ordinal);
            foreach (JToken definition in toolDefinitions)
                toolNames.Add((string)definition["name"]);
        }

        static void Start()
        {
            if (process != null && !process.HasExited)
                return;

            StopProcess();
            lock (ErrorSync)
                lastError = null;
            process = CreateProcess();
            input = process.StandardInput;
            input.AutoFlush = true;
            output = process.StandardOutput;
        }

        static Process CreateProcess()
        {
            string launcher = GetLauncherPath();
            ProcessStartInfo startInfo = new()
            {
                FileName = "pwsh",
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " +
                            Quote(launcher) + " -ProjectPath " + Quote(BridgeRouter.ProjectPath()),
                WorkingDirectory = BridgeRouter.ProjectPath(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process created = new() { StartInfo = startInfo, EnableRaisingEvents = true };
            created.ErrorDataReceived += (_, args) => SetLastError(args.Data);
            if (!created.Start())
                throw new InvalidOperationException("无法启动 SourceCodeMcp。");
            created.BeginErrorReadLine();
            return created;
        }

        static void Stop()
        {
            lock (ProcessSync)
                StopProcess();
        }

        static void StopProcess()
        {
            if (process == null)
                return;

            try { input?.Close(); } catch { }
            try
            {
                if (!process.HasExited && !process.WaitForExit(StopTimeoutMilliseconds))
                    process.Kill();
            }
            catch { }
            try { process.Dispose(); } catch { }
            process = null;
            input = null;
            output = null;
        }

        static void SetLastError(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            lock (ErrorSync)
                lastError = value.Length <= MaxErrorChars ? value : value.Substring(value.Length - MaxErrorChars);
        }

        static string GetErrorSuffix()
        {
            lock (ErrorSync)
                return string.IsNullOrEmpty(lastError) ? string.Empty : " stderr: " + lastError;
        }

        static string GetDefinitionPath([CallerFilePath] string sourceFilePath = null) =>
            Path.Combine(GetSourceRoot(sourceFilePath), "source-code-tools.json");

        static string GetLauncherPath([CallerFilePath] string sourceFilePath = null) =>
            Path.Combine(GetSourceRoot(sourceFilePath), "source-code-mcp.ps1");

        static string GetSourceRoot([CallerFilePath] string sourceFilePath = null)
        {
            // 本文件位于 .../UnityMcp/Prefab/Core/SourceCodeBridge.cs
            // SourceCodeMcp~ 位于 .../UnityMcp/SourceCodeMcp~
            string directory = Path.GetDirectoryName(sourceFilePath);
            string root = Path.GetDirectoryName(Path.GetDirectoryName(directory));
            return Path.Combine(root, "SourceCodeMcp~");
        }

        static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
