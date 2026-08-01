namespace SourceCodeMcp;

sealed class ReadRequest
{
    public string? Path { get; set; }
    public string? MatchId { get; set; }
    public int StartLine { get; set; } = 1;
    public int? EndLine { get; set; }
    public int MaxChars { get; set; } = 20000;
    public bool IncludeLineNumbers { get; set; }
}

sealed class ReadTool(ToolContext context)
{
    public object Run(ReadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) == string.IsNullOrWhiteSpace(request.MatchId))
            throw new ToolException(ErrorCodeSet.InvalidArgument, "Supply exactly one of path or matchId.");
        MatchSnapshot? match = string.IsNullOrWhiteSpace(request.MatchId)
            ? null
            : context.Sources.GetMatch(request.MatchId);
        string requestedPath = match?.Path ?? request.Path!;
        if (match is not null)
        {
            request.StartLine = match.StartLine;
            request.EndLine = match.EndLine;
        }
        if (request.StartLine < 1)
            throw new ToolException(ErrorCodeSet.InvalidRange, "startLine must be at least 1.");
        if (request.EndLine.HasValue && request.EndLine.Value < request.StartLine)
            throw new ToolException(ErrorCodeSet.InvalidRange, "endLine must be greater than or equal to startLine.");
        int maxChars = Math.Clamp(request.MaxChars, 1000, 100000);
        string fullPath = context.Paths.Resolve(requestedPath);
        if (!File.Exists(fullPath))
            throw new ToolException(ErrorCodeSet.NotAFile, $"Not a file: {requestedPath}");

        TextDocument document = TextDocument.Read(fullPath);
        string relativePath = context.Paths.Relative(fullPath);
        context.Sources.RegisterDocument(relativePath, document);
        if (match is not null && !string.Equals(match.Sha256, document.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ToolException(
                "MATCH_STALE",
                $"{request.MatchId} points to an older version of {relativePath}.",
                new { matchSha256 = match.Sha256, currentSha256 = document.Sha256, path = relativePath });
        int totalLines = document.Lines.Count;
        if (totalLines == 0)
        {
            if (request.StartLine != 1)
                throw new ToolException(ErrorCodeSet.InvalidRange, "An empty file only accepts startLine=1.");
            return new
            {
                path = relativePath,
                sha256 = document.Sha256,
                encoding = document.EncodingName,
                eol = EolName(document.Eol),
                hasFinalNewline = document.HasFinalNewline,
                totalLines = 0,
                startLine = 1,
                endLine = 0,
                content = "",
                lines = request.IncludeLineNumbers ? Array.Empty<object[]>() : null,
                truncated = false,
            };
        }
        if (request.StartLine > totalLines)
            throw new ToolException(ErrorCodeSet.InvalidRange, $"startLine exceeds totalLines ({totalLines}).");

        int requestedEnd = Math.Min(request.EndLine ?? totalLines, totalLines);
        List<string> selected = [];
        int used = 0;
        int endLine = request.StartLine - 1;
        bool lineTruncated = false;
        for (int line = request.StartLine; line <= requestedEnd; line++)
        {
            string text = document.Lines[line - 1];
            int separatorCost = selected.Count == 0 ? 0 : 1;
            if (used + separatorCost + text.Length > maxChars)
            {
                if (selected.Count == 0)
                {
                    selected.Add(text[..Math.Min(text.Length, maxChars)]);
                    endLine = line;
                    lineTruncated = text.Length > maxChars;
                }
                break;
            }
            selected.Add(text);
            used += separatorCost + text.Length;
            endLine = line;
        }

        bool truncated = lineTruncated || endLine < requestedEnd;
        return new
        {
            path = relativePath,
            sha256 = document.Sha256,
            encoding = document.EncodingName,
            eol = EolName(document.Eol),
            hasFinalNewline = document.HasFinalNewline,
            totalLines,
            startLine = request.StartLine,
            endLine,
            content = string.Join('\n', selected),
            lines = request.IncludeLineNumbers
                ? selected.Select((text, index) => new object[] { request.StartLine + index, text }).ToArray()
                : null,
            truncated,
            lineTruncated = lineTruncated ? (bool?)true : null,
            nextLine = truncated && !lineTruncated ? (int?)(endLine + 1) : null,
        };
    }

    static string EolName(string eol) => eol == "\r\n" ? "crlf" : "lf";
}
