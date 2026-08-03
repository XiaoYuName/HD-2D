using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SourceCodeMcp;

sealed class SymbolEditRequest
{
    public string SymbolId { get; set; } = "";
    public string ExpectedSha256 { get; set; } = "";
    public string Mode { get; set; } = SymbolEditModeSet.Declaration;
    public string NewText { get; set; } = "";
    public bool DryRun { get; set; }
}

sealed class SymbolEditTool(ToolContext context, SymbolTool symbols)
{
    public async Task<object> RunAsync(SymbolEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SymbolId))
            throw new ToolException(ErrorCodeSet.InvalidArgument, "symbolId is required.");
        SymbolEditTarget target = await symbols.ResolveEditTargetAsync(request.SymbolId, request.Mode);
        string fullPath = context.Paths.Resolve(target.Path);
        TextDocument document = TextDocument.Read(fullPath);
        context.Sources.RegisterDocument(target.Path, document);
        if (!string.Equals(request.ExpectedSha256, document.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ToolException(
                "HASH_MISMATCH",
                $"{target.Path} changed since it was read.",
                new
                {
                    path = target.Path,
                    expectedSha256 = request.ExpectedSha256,
                    currentSha256 = document.Sha256,
                    target.Line,
                });

        string source = string.Join(document.Eol, document.Lines);
        if (document.HasFinalNewline && document.Lines.Count > 0)
            source += document.Eol;
        if (target.Start < 0 || target.Start + target.Length > source.Length)
            throw new ToolException(
                "SYMBOL_STALE",
                "The symbol span no longer fits the current source; query find_symbol again.");
        string updated = string.Concat(
            source.AsSpan(0, target.Start),
            request.NewText,
            source.AsSpan(target.Start + target.Length));
        ValidateSyntax(target.Path, updated);
        byte[] bytes = Encode(document, updated);
        if (!request.DryRun)
            Commit(fullPath, bytes);

        return new
        {
            dryRun = request.DryRun ? (bool?)true : null,
            syntaxValid = true,
            path = target.Path,
            target = new { target.Kind, target.Display, target.Line, request.Mode },
            beforeSha256 = document.Sha256,
            afterSha256 = TextDocument.Hash(bytes),
            preview = request.DryRun
                ? $"@@ {target.Line} {target.Display} @@\n-{source.Substring(target.Start, Math.Min(target.Length, 1200))}\n+{request.NewText[..Math.Min(request.NewText.Length, 1200)]}"
                : null,
        };
    }

    static byte[] Encode(TextDocument document, string text)
    {
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        bool finalNewline = normalized.EndsWith('\n');
        List<string> lines = normalized.Length == 0 ? [] : normalized.Split('\n').ToList();
        if (finalNewline)
            lines.RemoveAt(lines.Count - 1);
        return document.Encode(lines, finalNewline);
    }

    static void ValidateSyntax(string path, string source)
    {
        Diagnostic[] errors = CSharpSyntaxTree.ParseText(
                source,
                new CSharpParseOptions(LanguageVersion.Preview))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(20)
            .ToArray();
        if (errors.Length == 0)
            return;
        throw new ToolException(
            "SYNTAX_INVALID",
            $"{path} would contain C# syntax errors; no file was written.",
            new
            {
                path,
                diagnostics = errors.Select(error => new
                {
                    id = error.Id,
                    message = error.GetMessage(),
                    line = error.Location.GetLineSpan().StartLinePosition.Line + 1,
                    column = error.Location.GetLineSpan().StartLinePosition.Character + 1,
                }).ToArray(),
            });
    }

    static void Commit(string path, byte[] bytes)
    {
        string temporary = Path.Combine(
            Path.GetDirectoryName(path)!,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.source-mcp.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
