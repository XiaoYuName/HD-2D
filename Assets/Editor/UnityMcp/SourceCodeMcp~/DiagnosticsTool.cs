using System.Diagnostics;
using System.Text.Json;

namespace SourceCodeMcp;

sealed class DiagnosticsRequest
{
    public string Severity { get; set; } = DiagnosticSeveritySet.Error;
    public string[]? Paths { get; set; }
    public int MaxResults { get; set; } = 50;
    public int MaxChars { get; set; } = 12000;
}

sealed class DiagnosticsTool(ToolContext context)
{
    static readonly JsonSerializerOptions StateJson = new(Protocol.Json)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    public object Run(DiagnosticsRequest request)
    {
        if (request.Severity is not (DiagnosticSeveritySet.Error or DiagnosticSeveritySet.Warning or DiagnosticSeveritySet.All))
            throw new ToolException(ErrorCodeSet.InvalidArgument, "severity must be error, warning, or all.");
        int maxResults = Math.Clamp(request.MaxResults, 1, 200);
        int maxChars = Math.Clamp(request.MaxChars, 1000, 100000);
        string[] pathFilters = NormalizeFilters(request.Paths);
        StateFile? selected = SelectStateFile();
        if (selected is null)
        {
            return new
            {
                available = false,
                editorRunning = false,
                stale = true,
                message = "UnityCompileMcp has not written a compile state yet. Use force_unity_compile for a fresh result.",
            };
        }

        CompileState state;
        try
        {
            using FileStream stream = File.Open(selected.Path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            state = JsonSerializer.Deserialize<CompileState>(stream, StateJson)
                ?? throw new JsonException("Empty state.");
        }
        catch (Exception exception)
        {
            throw new ToolException("DIAGNOSTICS_STATE_INVALID", $"Could not read {context.Paths.Relative(selected.Path)}: {exception.Message}");
        }

        string sourceStamp = GetSourceStamp();
        bool stale = !string.Equals(sourceStamp, state.SourceStamp, StringComparison.Ordinal);
        IEnumerable<CompileMessage> filtered = (state.Messages ?? [])
            .Where(message => request.Severity == DiagnosticSeveritySet.All || message.Type == request.Severity)
            .Where(message => MatchesPath(message.File, pathFilters))
            .Distinct(CompileMessageComparer.Instance)
            .Take(maxResults);

        var files = filtered
            .GroupBy(message => NormalizePath(message.File), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DiagnosticFile
            {
                Path = group.Key,
                Items = group.Select(message => new DiagnosticItem
                {
                    Type = message.Type,
                    Line = message.Line,
                    Column = message.Column,
                    Message = CompactMessage(message),
                }).ToList(),
            })
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        bool resultLimitReached = files.Sum(file => file.Items.Count) < (state.Messages ?? [])
            .Count(message => (request.Severity == DiagnosticSeveritySet.All || message.Type == request.Severity) &&
                              MatchesPath(message.File, pathFilters));

        var response = BuildResponse(state, selected.EditorRunning, stale, sourceStamp, files, resultLimitReached, false);
        bool charLimitReached = false;
        while (files.Count > 0 && Protocol.Serialize(response).Length > maxChars)
        {
            charLimitReached = true;
            DiagnosticFile last = files[^1];
            if (last.Items.Count > 1)
                last.Items.RemoveAt(last.Items.Count - 1);
            else
                files.RemoveAt(files.Count - 1);
            response = BuildResponse(state, selected.EditorRunning, stale, sourceStamp, files, resultLimitReached, true);
        }
        return BuildResponse(state, selected.EditorRunning, stale, sourceStamp, files, resultLimitReached, charLimitReached);
    }

    object BuildResponse(
        CompileState state,
        bool editorRunning,
        bool stale,
        string currentSourceStamp,
        List<DiagnosticFile> files,
        bool resultLimit,
        bool charLimit)
    {
        string? pathBase = PathGuard.GetCommonDirectory(files.Select(file => file.Path));
        return new
        {
            available = true,
            editorRunning,
            isCompiling = state.IsCompiling,
            hasResult = state.HasResult,
            succeeded = state.Succeeded,
            stale,
            generation = state.Generation,
            status = state.Status,
            errorCount = state.ErrorCount,
            warningCount = state.WarningCount,
            startedAtUtc = state.StartedAtUtc,
            finishedAtUtc = state.FinishedAtUtc,
            sourceStamp = stale ? state.SourceStamp : null,
            currentSourceStamp = stale ? currentSourceStamp : null,
            pathBase,
            files = files.Select(file => new object[]
            {
                PathGuard.Compact(file.Path, pathBase),
                file.Items.Select(item => new object[] { item.Line, item.Column, item.Type, item.Message }).ToArray(),
            }).ToArray(),
            returnedResults = files.Sum(file => file.Items.Count),
            truncated = resultLimit || charLimit,
            limitReason = resultLimit && charLimit
                ? LimitReasonSet.ResultsAndChars
                : resultLimit ? LimitReasonSet.Results : charLimit ? LimitReasonSet.Chars : null,
        };
    }

    StateFile? SelectStateFile()
    {
        string directory = Path.Combine(context.ProjectRoot, UnityPathSet.Library, "UnityCompileMcp");
        if (!Directory.Exists(directory))
            return null;
        IEnumerable<string> paths = Directory.EnumerateFiles(directory, "compile-state-*.json");
        string legacy = Path.Combine(directory, "compile-state.json");
        if (File.Exists(legacy))
            paths = paths.Append(legacy);
        return paths
            .Select(path => new StateFile(path, IsLiveState(path), File.GetLastWriteTimeUtc(path)))
            .OrderByDescending(state => state.EditorRunning)
            .ThenByDescending(state => state.LastWriteUtc)
            .FirstOrDefault();
    }

    static bool IsLiveState(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int separator = name.LastIndexOf('-');
        if (separator < 0 || !int.TryParse(name[(separator + 1)..], out int pid))
            return false;
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    string[] NormalizeFilters(string[]? paths)
    {
        if (paths is not { Length: > 0 })
            return [];
        return paths.Select(path => context.Paths.Relative(context.Paths.Resolve(path))).ToArray();
    }

    static bool MatchesPath(string? path, string[] filters)
    {
        if (filters.Length == 0)
            return true;
        string normalized = NormalizePath(path);
        return filters.Any(filter =>
            string.Equals(normalized, filter, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(filter.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    static string NormalizePath(string? path) => (path ?? "").Replace('\\', '/');

    static string CompactMessage(CompileMessage message)
    {
        string text = message.Message.Replace('\\', '/');
        string file = NormalizePath(message.File);
        if (!text.StartsWith(file + "(", StringComparison.OrdinalIgnoreCase))
            return message.Message;
        int prefixEnd = text.IndexOf("): ", file.Length, StringComparison.Ordinal);
        if (prefixEnd < 0)
            return message.Message;
        string severity = message.Type + " ";
        int contentStart = prefixEnd + 3;
        return text.AsSpan(contentStart).StartsWith(severity, StringComparison.OrdinalIgnoreCase)
            ? text[(contentStart + severity.Length)..]
            : message.Message;
    }

    string GetSourceStamp()
    {
        string assets = Path.Combine(context.ProjectRoot, UnityPathSet.Assets);
        if (!Directory.Exists(assets))
            return $"{DateTime.MinValue.Ticks}|0|0";
        long newestTicks = DateTime.MinValue.Ticks;
        long totalLength = 0;
        int count = 0;
        foreach (string path in Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories))
        {
            FileInfo file = new(path);
            newestTicks = Math.Max(newestTicks, file.LastWriteTimeUtc.Ticks);
            totalLength += file.Length;
            count++;
        }
        return $"{newestTicks}|{count}|{totalLength}";
    }

    sealed record StateFile(string Path, bool EditorRunning, DateTime LastWriteUtc);

    sealed class CompileState
    {
        public long Generation { get; set; }
        public bool IsCompiling { get; set; }
        public bool HasResult { get; set; }
        public bool Succeeded { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string? StartedAtUtc { get; set; }
        public string? FinishedAtUtc { get; set; }
        public string? Status { get; set; }
        public string? SourceStamp { get; set; }
        public CompileMessage[]? Messages { get; set; }
    }

    sealed class CompileMessage
    {
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public string? File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    sealed class CompileMessageComparer : IEqualityComparer<CompileMessage>
    {
        public static readonly CompileMessageComparer Instance = new();

        public bool Equals(CompileMessage? x, CompileMessage? y) =>
            x is not null && y is not null &&
            x.Line == y.Line &&
            x.Column == y.Column &&
            string.Equals(x.Type, y.Type, StringComparison.Ordinal) &&
            string.Equals(x.File, y.File, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Message, y.Message, StringComparison.Ordinal);

        public int GetHashCode(CompileMessage value) =>
            HashCode.Combine(
                value.Type,
                value.File?.ToUpperInvariant(),
                value.Line,
                value.Column,
                value.Message);
    }
}

sealed class DiagnosticFile
{
    public string Path { get; set; } = "";
    public List<DiagnosticItem> Items { get; set; } = [];
}

sealed class DiagnosticItem
{
    public string Type { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";
}
