using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMcp.Compile
{
    [Serializable]
    sealed class CompileDiagnostic
    {
        public string type;
        public string message;
        public string file;
        public int line;
        public int column;
    }

    [Serializable]
    sealed class CompileState
    {
        public int schemaVersion = 1;
        public int reporterVersion;
        public long generation;
        public int unityPid;
        public bool isCompiling;
        public bool hasResult;
        public bool succeeded;
        public int errorCount;
        public int warningCount;
        public string startedAtUtc;
        public string finishedAtUtc;
        public string status;
        public string sourceStamp;
        public CompileDiagnostic[] messages;
    }

    /// <summary>
    /// 将 Unity 编译生命周期写到 Library，供独立的外部 MCP 可靠判断成功和失败。
    /// </summary>
    [InitializeOnLoad]
    static class UnityCompileStateReporter
    {
        const int ReporterVersion = 4;
        const int StoredMessageLimit = 200;
        static readonly int unityPid = Process.GetCurrentProcess().Id;
        static readonly string statePath = Path.GetFullPath(
            Path.Combine(Application.dataPath, $"../Library/UnityCompileMcp/compile-state-{unityPid}.json"));
        static readonly string legacyStatePath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/UnityCompileMcp/compile-state.json"));
        static readonly List<CompileDiagnostic> messages = new List<CompileDiagnostic>();
        static CompileState state;

        static UnityCompileStateReporter()
        {
            if (Application.isBatchMode)
                return;

            CompilationPipeline.compilationStarted -= StartCompilation;
            CompilationPipeline.assemblyCompilationFinished -= EndAssemblyCompilation;
            CompilationPipeline.compilationFinished -= EndCompilation;
            CompilationPipeline.compilationStarted += StartCompilation;
            CompilationPipeline.assemblyCompilationFinished += EndAssemblyCompilation;
            CompilationPipeline.compilationFinished += EndCompilation;

            state = ReadState() ?? new CompileState();
            state.reporterVersion = ReporterVersion;
            if (!state.isCompiling)
            {
                state.unityPid = unityPid;
                SaveState();
                return;
            }

            if (state.unityPid == unityPid)
            {
                // compilationFinished 通常早于域重载；若某版本的回调顺序相反，重载后补齐最终状态。
                EditorApplication.delayCall += EndReloadedCompilation;
                return;
            }

            EndInterruptedCompilation();
        }

        static void StartCompilation(object context)
        {
            state = ReadState() ?? new CompileState();
            state.reporterVersion = ReporterVersion;
            state.generation++;
            state.unityPid = unityPid;
            state.isCompiling = true;
            state.hasResult = false;
            state.succeeded = false;
            state.errorCount = 0;
            state.warningCount = 0;
            state.sourceStamp = GetSourceStamp();
            state.startedAtUtc = DateTime.UtcNow.ToString("O");
            state.finishedAtUtc = string.Empty;
            state.status = "compiling";
            state.messages = Array.Empty<CompileDiagnostic>();
            messages.Clear();
            SaveState();
        }

        static void EndAssemblyCompilation(string assemblyPath, CompilerMessage[] compilerMessages)
        {
            foreach (CompilerMessage compilerMessage in compilerMessages)
            {
                if (compilerMessage.type == CompilerMessageType.Error)
                    state.errorCount++;
                else if (compilerMessage.type == CompilerMessageType.Warning)
                    state.warningCount++;
                else
                    continue;

                if (messages.Count >= StoredMessageLimit)
                    continue;
                messages.Add(new CompileDiagnostic
                {
                    type = compilerMessage.type == CompilerMessageType.Error ? "error" : "warning",
                    message = compilerMessage.message,
                    file = compilerMessage.file,
                    line = compilerMessage.line,
                    column = compilerMessage.column,
                });
            }
            state.messages = messages.ToArray();
            SaveState();
        }

        static void EndCompilation(object context)
        {
            state.isCompiling = false;
            state.hasResult = true;
            state.succeeded = state.errorCount == 0;
            state.finishedAtUtc = DateTime.UtcNow.ToString("O");
            state.status = state.succeeded ? "succeeded" : "failed";
            state.messages = messages.ToArray();
            SaveState();
        }

        static void EndReloadedCompilation()
        {
            if (!state.isCompiling || EditorApplication.isCompiling)
                return;
            state.isCompiling = false;
            state.hasResult = true;
            state.succeeded = state.errorCount == 0;
            state.finishedAtUtc = DateTime.UtcNow.ToString("O");
            state.status = state.succeeded ? "succeeded" : "failed";
            SaveState();
        }

        static void EndInterruptedCompilation()
        {
            state.unityPid = unityPid;
            state.isCompiling = false;
            state.hasResult = true;
            state.succeeded = false;
            state.finishedAtUtc = DateTime.UtcNow.ToString("O");
            state.status = "interrupted";
            SaveState();
        }

        static CompileState ReadState()
        {
            CompileState current = ReadStateFile(statePath);
            if (current != null)
                return current;
            CompileState legacy = ReadStateFile(legacyStatePath);
            return legacy != null && legacy.unityPid == unityPid ? legacy : null;
        }

        static CompileState ReadStateFile(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<CompileState>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string GetSourceStamp()
        {
            long newestTicks = DateTime.MinValue.Ticks;
            long totalLength = 0;
            int count = 0;
            foreach (string path in Directory.EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
            {
                var file = new FileInfo(path);
                newestTicks = Math.Max(newestTicks, file.LastWriteTimeUtc.Ticks);
                totalLength += file.Length;
                count++;
            }
            return $"{newestTicks}|{count}|{totalLength}";
        }

        static void SaveState()
        {
            string directory = Path.GetDirectoryName(statePath);
            Directory.CreateDirectory(directory);
            string temporaryPath = $"{statePath}.{Process.GetCurrentProcess().Id}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(state), new UTF8Encoding(false));
                if (File.Exists(statePath))
                    File.Replace(temporaryPath, statePath, null);
                else
                    File.Move(temporaryPath, statePath);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
