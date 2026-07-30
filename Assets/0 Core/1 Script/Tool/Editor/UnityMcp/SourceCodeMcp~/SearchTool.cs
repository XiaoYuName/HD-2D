using System.Diagnostics;
using System.Text.Json;

namespace SourceCodeMcp;

sealed class SearchRequest
{
    public string Pattern { get; set; } = "";
    public string Mode { get; set; } = SearchModeSet.Literal;
    public string[]? Paths { get; set; }
    public string[]? IncludeGlobs { get; set; }
    public string[]? ExcludeGlobs { get; set; }
    public bool? CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public int ContextLines { get; set; }
    public int MaxResults { get; set; } = 50;
    public int MaxChars { get; set; } = 12000;
}

sealed class SearchTool(ToolContext context)
{
    static readonly string[] DefaultExcludes =
    {
        "!.git/**", "!Library/**", "!Temp/**", "!Obj/**", "!Logs/**",
        "!Build/**", "!Builds/**", "!UserSettings/**",
    };

    public async Task<object> RunAsync(SearchRequest request)
    {
        if (string.IsNullOrEmpty(request.Pattern))
            throw new ToolException("INVALID_PATTERN", "pattern is required.");
        if (request.Mode is not (SearchModeSet.Literal or SearchModeSet.Regex))
            throw new ToolException(ErrorCodeSet.InvalidArgument, "mode must be literal or regex.");

        int maxResults = Math.Clamp(request.MaxResults, 1, 500);
        int maxChars = Math.Clamp(request.MaxChars, 1000, 100000);
        int contextLines = Math.Clamp(request.ContextLines, 0, 10);
        string[] paths = ResolvePaths(request.Paths);

        using Process process = new() { StartInfo = BuildStartInfo(request, paths) };
        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            throw new ToolException("RIPGREP_UNAVAILABLE", $"Could not start ripgrep: {exception.Message}");
        }

        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        List<SearchMatch> matches = [];
        bool resultLimitReached = false;
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (!ReadMatch(line, out SearchMatch? match))
                continue;
            if (matches.Count >= maxResults)
            {
                resultLimitReached = true;
                process.Kill(true);
                break;
            }
            matches.Add(match!);
        }
        await process.WaitForExitAsync();
        string stderr = await errorTask;
        if (process.ExitCode > 1 && !resultLimitReached)
            throw new ToolException("SEARCH_FAILED", string.IsNullOrWhiteSpace(stderr) ? "ripgrep failed." : stderr.Trim());

        return BuildResponse(matches, contextLines, resultLimitReached, maxChars);
    }

    ProcessStartInfo BuildStartInfo(SearchRequest request, string[] paths)
    {
        ProcessStartInfo start = new()
        {
            FileName = "rg",
            WorkingDirectory = context.ProjectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--json");
        start.ArgumentList.Add("--no-messages");
        if (request.Mode == SearchModeSet.Literal)
            start.ArgumentList.Add("--fixed-strings");
        if (request.WholeWord)
            start.ArgumentList.Add("--word-regexp");
        start.ArgumentList.Add(request.CaseSensitive switch
        {
            true => "--case-sensitive",
            false => "--ignore-case",
            null => "--smart-case",
        });
        foreach (string glob in request.IncludeGlobs ?? Array.Empty<string>())
        {
            start.ArgumentList.Add("--glob");
            start.ArgumentList.Add(glob);
        }
        foreach (string glob in request.ExcludeGlobs ?? Array.Empty<string>())
        {
            start.ArgumentList.Add("--glob");
            start.ArgumentList.Add(glob.StartsWith('!') ? glob : "!" + glob);
        }
        foreach (string glob in DefaultExcludes)
        {
            start.ArgumentList.Add("--glob");
            start.ArgumentList.Add(glob);
        }
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(request.Pattern);
        foreach (string path in paths)
            start.ArgumentList.Add(path);
        return start;
    }

    string[] ResolvePaths(string[]? requested)
    {
        if (requested is not { Length: > 0 })
            return ["."];
        return requested.Select(path =>
        {
            string fullPath = context.Paths.Resolve(path);
            return context.Paths.Relative(fullPath);
        }).ToArray();
    }

    object BuildResponse(
        List<SearchMatch> matches,
        int contextLines,
        bool resultLimitReached,
        int maxChars)
    {
        List<SearchFile> files = [];
        foreach (IGrouping<string, SearchMatch> group in matches
                     .GroupBy(match => match.Path, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            string fullPath = context.Paths.Resolve(group.Key);
            TextDocument document;
            try
            {
                document = TextDocument.Read(fullPath);
            }
            catch (ToolException)
            {
                continue;
            }
            int[] hitLines = group.Select(match => match.Line).Distinct().Order().ToArray();
            var intervals = MergeIntervals(hitLines, contextLines, document.Lines.Count);
            var blocks = intervals.Select(interval => new SearchBlock
            {
                StartLine = interval.Start,
                Content = string.Join('\n', document.Lines
                    .Skip(interval.Start - 1)
                    .Take(interval.End - interval.Start + 1)
                    .Select(line => contextLines == 0 ? line.TrimStart() : line)),
                HitLines = hitLines.Where(line => line >= interval.Start && line <= interval.End).ToArray(),
            }).ToList();
            files.Add(new SearchFile { Path = group.Key, Blocks = blocks });
        }

        bool charLimitReached = false;
        while (files.Count > 0 && ResponseLength(files, contextLines, resultLimitReached, charLimitReached) > maxChars)
        {
            charLimitReached = true;
            SearchFile last = files[^1];
            if (last.Blocks.Count > 1)
                last.Blocks.RemoveAt(last.Blocks.Count - 1);
            else
                files.RemoveAt(files.Count - 1);
        }

        int returnedMatches = files.Sum(file => file.Blocks.Sum(block => block.HitLines.Length));
        bool truncated = resultLimitReached || charLimitReached;
        return CreateResponse(files, contextLines, returnedMatches, truncated,
            resultLimitReached && charLimitReached
                ? LimitReasonSet.ResultsAndChars
                : resultLimitReached ? LimitReasonSet.Results : charLimitReached ? LimitReasonSet.Chars : null);
    }

    static int ResponseLength(List<SearchFile> files, int contextLines, bool results, bool chars) =>
        Protocol.Serialize(CreateResponse(
            files,
            contextLines,
            files.Sum(file => file.Blocks.Sum(block => block.HitLines.Length)),
            results || chars,
            null)).Length;

    static object CreateResponse(
        List<SearchFile> files,
        int contextLines,
        int returnedMatches,
        bool truncated,
        string? limitReason)
    {
        string? pathBase = PathGuard.GetCommonDirectory(files.Select(file => file.Path));
        return new
        {
            pathBase,
            files = files.Select(file => new object[]
            {
                PathGuard.Compact(file.Path, pathBase),
                file.Blocks.Select(block => contextLines == 0
                    ? new object[] { block.StartLine, block.Content }
                    : [block.StartLine, block.Content, block.HitLines.Select(line => line - block.StartLine).ToArray()])
                    .ToArray(),
            }).ToArray(),
            returnedMatches,
            truncated,
            limitReason,
        };
    }

    static List<(int Start, int End)> MergeIntervals(int[] lines, int contextLines, int totalLines)
    {
        List<(int Start, int End)> intervals = [];
        foreach (int line in lines)
        {
            int start = Math.Max(1, line - contextLines);
            int end = Math.Min(totalLines, line + contextLines);
            if (intervals.Count == 0 || start > intervals[^1].End + 1)
                intervals.Add((start, end));
            else
                intervals[^1] = (intervals[^1].Start, Math.Max(intervals[^1].End, end));
        }
        return intervals;
    }

    static bool ReadMatch(string json, out SearchMatch? match)
    {
        match = null;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("type").GetString() != "match")
            return false;
        JsonElement data = root.GetProperty("data");
        if (!data.GetProperty("path").TryGetProperty("text", out JsonElement pathValue) ||
            pathValue.GetString() is not { } path)
            return false;
        match = new(path.Replace('\\', '/'), data.GetProperty("line_number").GetInt32());
        return true;
    }

    sealed record SearchMatch(string Path, int Line);
}

sealed class SearchFile
{
    public string Path { get; set; } = "";
    public List<SearchBlock> Blocks { get; set; } = [];
}

sealed class SearchBlock
{
    public int StartLine { get; set; }
    public string Content { get; set; } = "";
    public int[] HitLines { get; set; } = [];
}
