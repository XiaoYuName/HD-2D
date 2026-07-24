using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

[Serializable]
public sealed class InspectorBridgeCompileMessage
{
    public string type;
    public string message;
    public string file;
    public int line;
    public int column;
}

[Serializable]
public sealed class InspectorBridgeCompileSnapshot
{
    public bool isCompiling;
    public bool hasResult;
    public bool succeeded;
    public int errorCount;
    public int warningCount;
    public string finishedAtUtc;
    public InspectorBridgeCompileMessage[] messages;
}

/// <summary>
/// 保存最近一次脚本编译结果，使 MCP 在程序集重载后仍能读取错误信息。
/// </summary>
[InitializeOnLoad]
public static class InspectorBridgeCompileTracker
{
    const string SnapshotKey = "InspectorBridge.CompileSnapshot";
    const string DomainReloadCountKey = "InspectorBridge.DomainReloadCount";
    const int StoredMessageLimit = 200;

    static readonly List<InspectorBridgeCompileMessage> currentMessages = new();
    static int errorCount;
    static int warningCount;

    public static int DomainReloadCount { get; }

    static InspectorBridgeCompileTracker()
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
        EditorApplication.delayCall += () =>
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (requestScriptCompilation)
                CompilationPipeline.RequestScriptCompilation();
        };
    }

    public static InspectorBridgeCompileSnapshot GetSnapshot(int maxMessages)
    {
        string json = SessionState.GetString(SnapshotKey, string.Empty);
        InspectorBridgeCompileSnapshot snapshot = string.IsNullOrEmpty(json)
            ? new InspectorBridgeCompileSnapshot()
            : JsonUtility.FromJson<InspectorBridgeCompileSnapshot>(json);
        snapshot ??= new InspectorBridgeCompileSnapshot();
        snapshot.isCompiling = EditorApplication.isCompiling;

        if (maxMessages <= 0 || snapshot.messages == null)
            snapshot.messages = null;
        else if (snapshot.messages.Length > maxMessages)
            snapshot.messages = snapshot.messages.Take(maxMessages).ToArray();
        return snapshot;
    }

    static void OnCompilationStarted(object context)
    {
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
            currentMessages.Add(new InspectorBridgeCompileMessage
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
        var snapshot = new InspectorBridgeCompileSnapshot
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
