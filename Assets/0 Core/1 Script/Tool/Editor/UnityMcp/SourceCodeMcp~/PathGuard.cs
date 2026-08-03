namespace SourceCodeMcp;

sealed class PathGuard
{
    const string PathOutsideProjectCode = "PATH_OUTSIDE_PROJECT";

    static readonly HashSet<string> DeniedWriteRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "Library", "Temp", "Obj", "Logs", "Build", "Builds", "UserSettings",
    };

    readonly string rootWithSeparator;
    readonly StringComparison pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public PathGuard(string root)
    {
        Root = root;
        rootWithSeparator = root + Path.DirectorySeparatorChar;
    }

    public string Root { get; }

    public string Resolve(string relativePath, bool forWrite = false, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ToolException(ErrorCodeSet.InvalidPath, "Path is required.");
        if (Path.IsPathRooted(relativePath))
            throw new ToolException(PathOutsideProjectCode, "Only project-relative paths are allowed.");

        string fullPath = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!IsInside(fullPath))
            throw new ToolException(PathOutsideProjectCode, $"Path escapes the project root: {relativePath}");

        string normalized = Relative(fullPath);
        string first = normalized.Split('/', 2)[0];
        if (forWrite && DeniedWriteRoots.Contains(first))
            throw new ToolException("WRITE_PATH_DENIED", $"Writes under {first}/ are not allowed.");

        EnsureNoEscapingReparsePoint(fullPath);
        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new ToolException("PATH_NOT_FOUND", $"Path does not exist: {normalized}");
        return fullPath;
    }

    public string Relative(string fullPath) =>
        Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    public bool IsInside(string fullPath) =>
        string.Equals(fullPath, Root, pathComparison) ||
        fullPath.StartsWith(rootWithSeparator, pathComparison);

    public static string? GetCommonDirectory(IEnumerable<string> paths)
    {
        string[][] parts = paths.Select(path => path.Split('/')).ToArray();
        if (parts.Length == 0)
            return null;
        int count = 0;
        int maximum = parts.Min(value => value.Length) - 1;
        while (count < maximum && parts.All(value =>
                   string.Equals(value[count], parts[0][count], StringComparison.OrdinalIgnoreCase)))
            count++;
        return count == 0 ? null : string.Join('/', parts[0][..count]) + "/";
    }

    public static string Compact(string path, string? commonDirectory) =>
        commonDirectory is null ? path : path[commonDirectory.Length..];

    void EnsureNoEscapingReparsePoint(string fullPath)
    {
        string relative = Path.GetRelativePath(Root, fullPath);
        string cursor = Root;
        foreach (string part in relative.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            cursor = Path.Combine(cursor, part);
            FileSystemInfo info = Directory.Exists(cursor)
                ? new DirectoryInfo(cursor)
                : new FileInfo(cursor);
            if (!info.Exists || !info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;
            FileSystemInfo? target = info.ResolveLinkTarget(true);
            if (target is null || !IsInside(Path.GetFullPath(target.FullName)))
                throw new ToolException(
                    PathOutsideProjectCode,
                    $"Path crosses a link outside the project: {Relative(cursor)}");
        }
    }
}
