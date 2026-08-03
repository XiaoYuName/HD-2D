using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMcp
{
    [Serializable]
    public sealed class CompileMessage
    {
        public string type;
        public string message;
        public string file;
        public int line;
        public int column;
    }

    /// <summary>快照走 JsonUtility 存进 SessionState，所以字段保持可序列化的公开字段。</summary>
    [Serializable]
    public sealed class CompileSnapshot
    {
        public bool isCompiling;
        public bool hasResult;
        public bool succeeded;
        public int errorCount;
        public int warningCount;
        public string finishedAtUtc;
        public string refreshRequestedAtUtc;
        /// <summary>true=最近一次编译结果早于本会话最后一次 refresh 请求，读到的很可能是旧结果。</summary>
        public bool resultStale;
        public CompileMessage[] messages;
    }

    /// <summary>
    /// 保存最近一次脚本编译结果，使 MCP 在程序集重载后仍能读到错误信息。
    /// </summary>
    [InitializeOnLoad]
    static class CompileTracker
    {
        const string SnapshotKey = "InspectorBridge.CompileSnapshot";
        const string DomainReloadCountKey = "InspectorBridge.DomainReloadCount";
        const string RefreshRequestedKey = "InspectorBridge.RefreshRequestedAtUtc";
        const int StoredMessageLimit = 200;

        static readonly List<CompileMessage> currentMessages = new List<CompileMessage>();
        static int errorCount;
        static int warningCount;

        public static int DomainReloadCount { get; }

        static CompileTracker()
        {
            DomainReloadCount = SessionState.GetInt(DomainReloadCountKey, 0) + 1;
            SessionState.SetInt(DomainReloadCountKey, DomainReloadCount);

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        public static void RequestRefresh(bool requestScriptCompilation)
        {
            // 记下请求时间：Unity 在后台时会把编译推迟到重新获得焦点，
            // 不记的话调用方只能拿到一份旧快照，还看不出它是旧的。
            SessionState.SetString(RefreshRequestedKey, DateTime.UtcNow.ToString("O"));
            EditorApplication.delayCall += () =>
            {
                // 用默认 Refresh：磁盘上改过的脚本/资源靠时间戳就能被识别。
                // ForceUpdate 会强制重导入，大项目上能把主线程堵住好几分钟，期间桥的请求全部超时。
                AssetDatabase.Refresh();
                if (requestScriptCompilation)
                    CompilationPipeline.RequestScriptCompilation();
            };
        }

        public static CompileSnapshot GetSnapshot(int maxMessages)
        {
            string json = SessionState.GetString(SnapshotKey, string.Empty);
            CompileSnapshot snapshot = string.IsNullOrEmpty(json)
                ? new CompileSnapshot()
                : JsonUtility.FromJson<CompileSnapshot>(json);
            snapshot ??= new CompileSnapshot();
            snapshot.isCompiling = EditorApplication.isCompiling;
            snapshot.refreshRequestedAtUtc = SessionState.GetString(RefreshRequestedKey, string.Empty);
            snapshot.resultStale = !snapshot.isCompiling && IsResultStale(snapshot);

            if (maxMessages <= 0 || snapshot.messages == null)
                snapshot.messages = null;
            else if (snapshot.messages.Length > maxMessages)
                snapshot.messages = snapshot.messages.Take(maxMessages).ToArray();
            return snapshot;
        }

        /// <summary>本会话请求过刷新，但还没有比它更新的编译结果。</summary>
        static bool IsResultStale(CompileSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(snapshot.refreshRequestedAtUtc))
                return false;
            if (!snapshot.hasResult || string.IsNullOrEmpty(snapshot.finishedAtUtc))
                return true;
            return DateTime.TryParse(snapshot.refreshRequestedAtUtc, null,
                       DateTimeStyles.RoundtripKind, out DateTime requestedAt) &&
                   DateTime.TryParse(snapshot.finishedAtUtc, null,
                       DateTimeStyles.RoundtripKind, out DateTime finishedAt) &&
                   finishedAt < requestedAt;
        }

        static void OnCompilationStarted(object context)
        {
            SessionState.EraseString(RefreshRequestedKey);
            currentMessages.Clear();
            errorCount = 0;
            warningCount = 0;
            SaveSnapshot(true, false, null);
        }

        static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages)
        {
            foreach (CompilerMessage compilerMessage in compilerMessages)
            {
                if (compilerMessage.type == CompilerMessageType.Error)
                    errorCount++;
                else if (compilerMessage.type == CompilerMessageType.Warning)
                    warningCount++;
                else
                    continue;

                if (currentMessages.Count >= StoredMessageLimit)
                    continue;
                currentMessages.Add(new CompileMessage
                {
                    type = compilerMessage.type == CompilerMessageType.Error ? "error" : "warning",
                    message = compilerMessage.message,
                    file = compilerMessage.file,
                    line = compilerMessage.line,
                    column = compilerMessage.column,
                });
            }
            SaveSnapshot(true, false, null);
        }

        static void OnCompilationFinished(object context)
        {
            SaveSnapshot(false, true, DateTime.UtcNow.ToString("O"));
        }

        static void SaveSnapshot(bool isCompiling, bool hasResult, string finishedAtUtc)
        {
            var snapshot = new CompileSnapshot
            {
                isCompiling = isCompiling,
                hasResult = hasResult,
                succeeded = hasResult && errorCount == 0,
                errorCount = errorCount,
                warningCount = warningCount,
                finishedAtUtc = finishedAtUtc,
                messages = currentMessages.ToArray(),
            };
            SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
        }
    }
}
