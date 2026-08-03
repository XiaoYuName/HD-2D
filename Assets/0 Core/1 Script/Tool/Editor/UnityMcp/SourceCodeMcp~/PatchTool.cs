using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SourceCodeMcp;

sealed class PatchRequest
{
    public PatchFileRequest[] Files { get; set; } = [];
    public bool DryRun { get; set; }
    public bool ValidateSyntax { get; set; } = true;
}

sealed class PatchFileRequest
{
    public string Path { get; set; } = "";
    public string? ExpectedSha256 { get; set; }
    public bool Create { get; set; }
    public PatchEditRequest[] Edits { get; set; } = [];
    public PatchReplacementRequest[] Replacements { get; set; } = [];
    public bool AllowRebase { get; set; }
}

sealed class PatchEditRequest
{
    public int StartLine { get; set; }
    public int DeleteLineCount { get; set; }
    public string NewText { get; set; } = "";
    public string? ExpectedOldText { get; set; }
}

sealed class PatchReplacementRequest
{
    public string OldText { get; set; } = "";
    public string NewText { get; set; } = "";
    public bool ReplaceAll { get; set; }
}

sealed class PatchTool(ToolContext context)
{
    const string InvalidPatchCode = "INVALID_PATCH";

    const int MaximumFiles = 50;
    const int MaximumEdits = 500;
    const int MaximumNewTextChars = 1024 * 1024;

    public object Run(PatchRequest request)
    {
        if (request.Files.Length is < 1 or > MaximumFiles)
            throw new ToolException(InvalidPatchCode, $"files must contain 1 to {MaximumFiles} items.");
        if (request.Files.Sum(file => file.Edits.Length + file.Replacements.Length) > MaximumEdits)
            throw new ToolException(InvalidPatchCode, $"A patch may contain at most {MaximumEdits} edits.");
        if (request.Files.Sum(file =>
                file.Edits.Sum(edit => edit.NewText.Length) +
                file.Replacements.Sum(edit => edit.NewText.Length)) > MaximumNewTextChars)
            throw new ToolException("PATCH_TOO_LARGE", $"newText is limited to {MaximumNewTextChars} characters per request.");

        var prepared = request.Files.Select(file => Prepare(file, request.ValidateSyntax)).ToList();
        if (prepared.Select(file => file.FullPath).Distinct(PathComparer).Count() != prepared.Count)
            throw new ToolException("DUPLICATE_PATH", "Each path may appear only once per patch.");

        if (!request.DryRun)
            Commit(prepared);
        return new
        {
            dryRun = request.DryRun ? (bool?)true : null,
            syntaxValid = true,
            fileCount = prepared.Count,
            editCount = prepared.Sum(file => file.EditCount),
            files = prepared.Select(file => new
            {
                path = file.RelativePath,
                beforeSha256 = file.BeforeSha256,
                afterSha256 = TextDocument.Hash(file.NewBytes),
                addedLines = file.AddedLines,
                deletedLines = file.DeletedLines,
                created = file.IsNew ? (bool?)true : null,
                rebased = file.Rebased ? (bool?)true : null,
                preview = request.DryRun ? file.Preview : null,
            }).ToArray(),
        };
    }

    PreparedPatch Prepare(PatchFileRequest request, bool validateSyntax)
    {
        if ((request.Edits.Length == 0) == (request.Replacements.Length == 0))
            throw new ToolException(
                InvalidPatchCode,
                $"Supply exactly one of edits or replacements for {request.Path}.");
        string fullPath = context.Paths.Resolve(request.Path, forWrite: true, mustExist: false);
        bool exists = File.Exists(fullPath);
        if (request.Create == exists)
        {
            string message = request.Create
                ? $"create=true requires a missing file: {request.Path}"
                : $"File does not exist; set create=true: {request.Path}";
            throw new ToolException("CREATE_CONFLICT", message);
        }
        if (Directory.Exists(fullPath))
            throw new ToolException(ErrorCodeSet.NotAFile, $"Path is a directory: {request.Path}");

        TextDocument document = exists ? TextDocument.Read(fullPath) : TextDocument.Empty();
        string relativePath = context.Paths.Relative(fullPath);
        bool rebased = false;
        if (exists)
        {
            if (string.IsNullOrWhiteSpace(request.ExpectedSha256))
                throw new ToolException("HASH_REQUIRED", $"expectedSha256 is required for {request.Path}.");
            if (!string.Equals(request.ExpectedSha256, document.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                if (request.AllowRebase && request.Replacements.Length > 0)
                    rebased = true;
                else
                    throw CreateHashMismatch(request, relativePath, document);
            }
            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.ReadOnly))
                throw new ToolException("READ_ONLY_FILE", $"File is read-only: {request.Path}");
        }
        else if (!string.IsNullOrEmpty(request.ExpectedSha256))
        {
            throw new ToolException(InvalidPatchCode, $"expectedSha256 must be omitted when creating {request.Path}.");
        }

        List<string> lines;
        bool finalNewline;
        int addedLines;
        int deletedLines;
        int editCount;
        string preview;
        if (request.Replacements.Length > 0)
        {
            string oldText = GetLogicalText(document);
            string newText = ApplyReplacements(oldText, request.Replacements, request.Path);
            (lines, finalNewline) = SplitLogicalText(newText);
            addedLines = Math.Max(0, lines.Count - document.Lines.Count);
            deletedLines = Math.Max(0, document.Lines.Count - lines.Count);
            editCount = request.Replacements.Length;
            preview = CreatePreview(oldText, newText);
        }
        else
        {
            var edits = request.Edits
                .Select((edit, index) => ValidateEdit(edit, index, document, request.Path))
                .OrderBy(edit => edit.StartIndex)
                .ThenBy(edit => edit.OriginalIndex)
                .ToList();
            for (int i = 1; i < edits.Count; i++)
            {
                ValidatedEdit previous = edits[i - 1];
                ValidatedEdit current = edits[i];
                if (current.StartIndex < previous.EndIndex || current.StartIndex == previous.StartIndex)
                    throw new ToolException(
                        "OVERLAPPING_EDITS",
                        $"Edits {previous.OriginalIndex} and {current.OriginalIndex} overlap in {request.Path}.",
                        new { path = request.Path, edits = new[] { previous.OriginalIndex, current.OriginalIndex } });
            }

            lines = new(document.Lines);
            foreach (ValidatedEdit edit in edits.OrderByDescending(edit => edit.StartIndex))
            {
                lines.RemoveRange(edit.StartIndex, edit.DeleteLineCount);
                lines.InsertRange(edit.StartIndex, edit.NewLines);
            }
            finalNewline = exists
                ? document.HasFinalNewline
                : GetNewFileFinalNewline(edits, lines.Count);
            addedLines = edits.Sum(edit => edit.NewLines.Count);
            deletedLines = edits.Sum(edit => edit.DeleteLineCount);
            editCount = edits.Count;
            preview = CreatePreview(GetLogicalText(document), GetLogicalText(lines, finalNewline));
        }

        byte[] newBytes = document.Encode(lines, finalNewline);
        if (validateSyntax && Path.GetExtension(fullPath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            ValidateCSharp(request.Path, GetLogicalText(lines, finalNewline));
        return new PreparedPatch
        {
            FullPath = fullPath,
            RelativePath = relativePath,
            IsNew = !exists,
            OriginalBytes = document.Bytes,
            BeforeSha256 = exists ? document.Sha256 : null,
            NewBytes = newBytes,
            AddedLines = addedLines,
            DeletedLines = deletedLines,
            EditCount = editCount,
            Rebased = rebased,
            Preview = preview,
        };
    }

    ToolException CreateHashMismatch(
        PatchFileRequest request,
        string relativePath,
        TextDocument current)
    {
        SourceSnapshot? previous = context.Sources.GetSnapshot(relativePath, request.ExpectedSha256!);
        object details = previous is null
            ? new
            {
                path = relativePath,
                expectedSha256 = request.ExpectedSha256,
                currentSha256 = current.Sha256,
            }
            : new
            {
                path = relativePath,
                expectedSha256 = request.ExpectedSha256,
                currentSha256 = current.Sha256,
                changedRanges = GetChangedRanges(previous.Lines, current.Lines),
                canRebase = request.Replacements.Length > 0,
            };
        return new(
            "HASH_MISMATCH",
            $"{request.Path} changed since it was read. Expected {request.ExpectedSha256}, current {current.Sha256}.",
            details);
    }

    static ValidatedEdit ValidateEdit(
        PatchEditRequest edit,
        int index,
        TextDocument document,
        string path)
    {
        int lineCount = document.Lines.Count;
        if (edit.StartLine < 1 || edit.StartLine > lineCount + 1)
            throw new ToolException(
                ErrorCodeSet.InvalidRange,
                $"{path} edit {index}: startLine must be between 1 and {lineCount + 1}.",
                new { path, editIndex = index, edit.StartLine, lineCount });
        if (edit.DeleteLineCount < 0 || edit.StartLine - 1 + edit.DeleteLineCount > lineCount)
            throw new ToolException(
                ErrorCodeSet.InvalidRange,
                $"{path} edit {index}: deleteLineCount exceeds the file.",
                new { path, editIndex = index, edit.StartLine, edit.DeleteLineCount, lineCount });
        if (edit.ExpectedOldText is not null)
        {
            string actual = string.Join(
                '\n',
                document.Lines.Skip(edit.StartLine - 1).Take(edit.DeleteLineCount));
            string expected = NormalizeEol(edit.ExpectedOldText).TrimEnd('\n');
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new ToolException(
                    "EDIT_ANCHOR_MISMATCH",
                    $"{path} edit {index}: expectedOldText does not match the requested line range.",
                    new { path, editIndex = index, expected, actual });
        }
        List<string> newLines = SplitLogicalLines(edit.NewText);
        return new(
            index,
            edit.StartLine - 1,
            edit.DeleteLineCount,
            newLines,
            EndsWithNewline(edit.NewText));
    }

    static List<string> SplitLogicalLines(string text)
    {
        if (text.Length == 0)
            return [];
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        if (EndsWithNewline(normalized))
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    static bool EndsWithNewline(string text) => text.EndsWith('\n') || text.EndsWith('\r');

    static string ApplyReplacements(
        string text,
        IReadOnlyList<PatchReplacementRequest> replacements,
        string path)
    {
        for (int i = 0; i < replacements.Count; i++)
        {
            PatchReplacementRequest replacement = replacements[i];
            string oldText = NormalizeEol(replacement.OldText);
            string newText = NormalizeEol(replacement.NewText);
            if (oldText.Length == 0)
                throw new ToolException(InvalidPatchCode, $"{path} replacement {i}: oldText cannot be empty.");
            int count = CountOccurrences(text, oldText);
            if (count == 0 || !replacement.ReplaceAll && count != 1)
                throw new ToolException(
                    "REPLACEMENT_ANCHOR_MISMATCH",
                    $"{path} replacement {i}: expected {(replacement.ReplaceAll ? "at least one" : "exactly one")} occurrence, found {count}.",
                    new { path, editIndex = i, occurrences = count, replacement.ReplaceAll });
            text = replacement.ReplaceAll
                ? text.Replace(oldText, newText, StringComparison.Ordinal)
                : ReplaceOnce(text, oldText, newText);
        }
        return text;
    }

    static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    static string ReplaceOnce(string text, string oldText, string newText)
    {
        int index = text.IndexOf(oldText, StringComparison.Ordinal);
        return string.Concat(text.AsSpan(0, index), newText, text.AsSpan(index + oldText.Length));
    }

    static (List<string> Lines, bool FinalNewline) SplitLogicalText(string text)
    {
        string normalized = NormalizeEol(text);
        bool finalNewline = normalized.EndsWith('\n');
        List<string> lines = normalized.Length == 0 ? [] : normalized.Split('\n').ToList();
        if (finalNewline && lines.Count > 0)
            lines.RemoveAt(lines.Count - 1);
        return (lines, finalNewline);
    }

    static string GetLogicalText(TextDocument document) =>
        GetLogicalText(document.Lines, document.HasFinalNewline);

    static string GetLogicalText(IReadOnlyList<string> lines, bool finalNewline)
    {
        string text = string.Join('\n', lines);
        return finalNewline && lines.Count > 0 ? text + '\n' : text;
    }

    static string NormalizeEol(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    static void ValidateCSharp(string path, string text)
    {
        Diagnostic[] errors = CSharpSyntaxTree.ParseText(
                text,
                new CSharpParseOptions(LanguageVersion.Preview))
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(20)
            .ToArray();
        if (errors.Length == 0)
            return;
        throw new ToolException(
            "SYNTAX_INVALID",
            $"{path} would contain {errors.Length} C# syntax error(s); no files were written.",
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

    static object[] GetChangedRanges(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        int prefix = 0;
        while (prefix < before.Count && prefix < after.Count &&
               string.Equals(before[prefix], after[prefix], StringComparison.Ordinal))
            prefix++;
        int suffix = 0;
        while (suffix < before.Count - prefix && suffix < after.Count - prefix &&
               string.Equals(
                   before[before.Count - 1 - suffix],
                   after[after.Count - 1 - suffix],
                   StringComparison.Ordinal))
            suffix++;
        if (prefix == before.Count && prefix == after.Count)
            return [];
        return
        [
            new
            {
                before = new[] { prefix + 1, before.Count - suffix },
                current = new[] { prefix + 1, after.Count - suffix },
            },
        ];
    }

    static string CreatePreview(string before, string after)
    {
        const int maximum = 3000;
        int prefix = 0;
        int limit = Math.Min(before.Length, after.Length);
        while (prefix < limit && before[prefix] == after[prefix])
            prefix++;
        string oldPart = before.Substring(prefix, Math.Min(maximum / 2, before.Length - prefix));
        string newPart = after.Substring(prefix, Math.Min(maximum / 2, after.Length - prefix));
        return $"@@ char {prefix} @@\n-{oldPart}\n+{newPart}";
    }

    static bool GetNewFileFinalNewline(List<ValidatedEdit> edits, int lineCount) =>
        lineCount > 0 && edits[^1].NewTextEndedWithNewline;

    static void Commit(List<PreparedPatch> files)
    {
        List<string> createdDirectories = [];
        Dictionary<PreparedPatch, string> stagedPaths = [];
        List<PreparedPatch> committed = [];
        try
        {
            foreach (PreparedPatch file in files)
            {
                string? directory = Path.GetDirectoryName(file.FullPath);
                if (directory is null)
                    throw new ToolException(
                        ErrorCodeSet.InvalidPath,
                        $"Cannot resolve parent directory for {file.RelativePath}.");
                CreateDirectories(directory, createdDirectories);
                string temporary = Path.Combine(directory, $".{Path.GetFileName(file.FullPath)}.{Guid.NewGuid():N}.source-mcp.tmp");
                File.WriteAllBytes(temporary, file.NewBytes);
                stagedPaths[file] = temporary;
            }

            foreach (PreparedPatch file in files)
            {
                string temporary = stagedPaths[file];
                File.Move(temporary, file.FullPath, overwrite: !file.IsNew);
                committed.Add(file);
            }
        }
        catch (Exception exception)
        {
            var rollbackErrors = Rollback(committed);
            CleanupDirectories(createdDirectories);
            if (rollbackErrors.Count > 0)
                throw new ToolException("PATCH_ROLLBACK_FAILED", $"{exception.Message}; rollback errors: {string.Join("; ", rollbackErrors)}");
            if (exception is ToolException toolException)
                throw toolException;
            throw new ToolException("PATCH_COMMIT_FAILED", exception.Message);
        }
        finally
        {
            Cleanup(stagedPaths.Values);
        }
    }

    static List<string> Rollback(List<PreparedPatch> committed)
    {
        List<string> errors = [];
        foreach (PreparedPatch file in committed.AsEnumerable().Reverse())
        {
            try
            {
                if (file.IsNew)
                {
                    if (File.Exists(file.FullPath))
                        File.Delete(file.FullPath);
                    continue;
                }
                string temporary = $"{file.FullPath}.{Guid.NewGuid():N}.rollback.tmp";
                File.WriteAllBytes(temporary, file.OriginalBytes);
                File.Move(temporary, file.FullPath, true);
            }
            catch (Exception exception)
            {
                errors.Add($"{file.RelativePath}: {exception.Message}");
            }
        }
        return errors;
    }

    static void CreateDirectories(string directory, List<string> created)
    {
        Stack<string> missing = new();
        string? cursor = directory;
        while (!string.IsNullOrEmpty(cursor) && !Directory.Exists(cursor))
        {
            missing.Push(cursor);
            cursor = Path.GetDirectoryName(cursor);
        }
        while (missing.Count > 0)
        {
            string path = missing.Pop();
            Directory.CreateDirectory(path);
            created.Add(path);
        }
    }

    static void Cleanup(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup. The transactional target files are already stable.
            }
        }
    }

    static void CleanupDirectories(IEnumerable<string> directories)
    {
        foreach (string directory in directories.Reverse())
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch
            {
                // Best-effort cleanup after a failed transaction.
            }
        }
    }

    static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    sealed record ValidatedEdit(
        int OriginalIndex,
        int StartIndex,
        int DeleteLineCount,
        List<string> NewLines,
        bool NewTextEndedWithNewline)
    {
        public int EndIndex => StartIndex + DeleteLineCount;
    }

    sealed class PreparedPatch
    {
        public string FullPath { get; init; } = "";
        public string RelativePath { get; init; } = "";
        public bool IsNew { get; init; }
        public byte[] OriginalBytes { get; init; } = [];
        public string? BeforeSha256 { get; init; }
        public byte[] NewBytes { get; init; } = [];
        public int AddedLines { get; init; }
        public int DeletedLines { get; init; }
        public int EditCount { get; init; }
        public bool Rebased { get; init; }
        public string Preview { get; init; } = "";
    }
}
