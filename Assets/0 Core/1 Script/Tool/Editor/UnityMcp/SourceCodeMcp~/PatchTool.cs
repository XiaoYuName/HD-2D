namespace SourceCodeMcp;

sealed class PatchRequest
{
    public PatchFileRequest[] Files { get; set; } = [];
}

sealed class PatchFileRequest
{
    public string Path { get; set; } = "";
    public string? ExpectedSha256 { get; set; }
    public bool Create { get; set; }
    public PatchEditRequest[] Edits { get; set; } = [];
}

sealed class PatchEditRequest
{
    public int StartLine { get; set; }
    public int DeleteLineCount { get; set; }
    public string NewText { get; set; } = "";
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
        if (request.Files.Sum(file => file.Edits.Length) > MaximumEdits)
            throw new ToolException(InvalidPatchCode, $"A patch may contain at most {MaximumEdits} edits.");
        if (request.Files.Sum(file => file.Edits.Sum(edit => edit.NewText.Length)) > MaximumNewTextChars)
            throw new ToolException("PATCH_TOO_LARGE", $"newText is limited to {MaximumNewTextChars} characters per request.");

        var prepared = request.Files.Select(Prepare).ToList();
        if (prepared.Select(file => file.FullPath).Distinct(PathComparer).Count() != prepared.Count)
            throw new ToolException("DUPLICATE_PATH", "Each path may appear only once per patch.");

        Commit(prepared);
        return new
        {
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
            }).ToArray(),
        };
    }

    PreparedPatch Prepare(PatchFileRequest request)
    {
        if (request.Edits.Length == 0)
            throw new ToolException(InvalidPatchCode, $"No edits supplied for {request.Path}.");
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
        if (exists)
        {
            if (string.IsNullOrWhiteSpace(request.ExpectedSha256))
                throw new ToolException("HASH_REQUIRED", $"expectedSha256 is required for {request.Path}.");
            if (!string.Equals(request.ExpectedSha256, document.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new ToolException(
                    "HASH_MISMATCH",
                    $"{request.Path} changed since it was read. Expected {request.ExpectedSha256}, current {document.Sha256}.");
            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.ReadOnly))
                throw new ToolException("READ_ONLY_FILE", $"File is read-only: {request.Path}");
        }
        else if (!string.IsNullOrEmpty(request.ExpectedSha256))
        {
            throw new ToolException(InvalidPatchCode, $"expectedSha256 must be omitted when creating {request.Path}.");
        }

        var edits = request.Edits.Select((edit, index) => ValidateEdit(edit, index, document.Lines.Count, request.Path))
            .OrderBy(edit => edit.StartIndex)
            .ThenBy(edit => edit.OriginalIndex)
            .ToList();
        for (int i = 1; i < edits.Count; i++)
        {
            ValidatedEdit previous = edits[i - 1];
            ValidatedEdit current = edits[i];
            if (current.StartIndex < previous.EndIndex || current.StartIndex == previous.StartIndex)
                throw new ToolException("OVERLAPPING_EDITS", $"Edits overlap in {request.Path}.");
        }

        List<string> lines = new(document.Lines);
        foreach (ValidatedEdit edit in edits.OrderByDescending(edit => edit.StartIndex))
        {
            lines.RemoveRange(edit.StartIndex, edit.DeleteLineCount);
            lines.InsertRange(edit.StartIndex, edit.NewLines);
        }

        bool finalNewline = exists
            ? document.HasFinalNewline
            : GetNewFileFinalNewline(edits, lines.Count);
        byte[] newBytes = document.Encode(lines, finalNewline);
        return new PreparedPatch
        {
            FullPath = fullPath,
            RelativePath = context.Paths.Relative(fullPath),
            IsNew = !exists,
            OriginalBytes = document.Bytes,
            BeforeSha256 = exists ? document.Sha256 : null,
            NewBytes = newBytes,
            AddedLines = edits.Sum(edit => edit.NewLines.Count),
            DeletedLines = edits.Sum(edit => edit.DeleteLineCount),
            EditCount = edits.Count,
        };
    }

    static ValidatedEdit ValidateEdit(PatchEditRequest edit, int index, int lineCount, string path)
    {
        if (edit.StartLine < 1 || edit.StartLine > lineCount + 1)
            throw new ToolException(ErrorCodeSet.InvalidRange, $"{path} edit {index}: startLine must be between 1 and {lineCount + 1}.");
        if (edit.DeleteLineCount < 0 || edit.StartLine - 1 + edit.DeleteLineCount > lineCount)
            throw new ToolException(ErrorCodeSet.InvalidRange, $"{path} edit {index}: deleteLineCount exceeds the file.");
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
    }
}
