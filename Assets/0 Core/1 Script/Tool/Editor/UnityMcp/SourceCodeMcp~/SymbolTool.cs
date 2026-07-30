using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace SourceCodeMcp;

sealed class SymbolRequest
{
    public string? Query { get; set; }
    public string? SymbolId { get; set; }
    public string[]? Kinds { get; set; }
    public bool IncludeReferences { get; set; }
    public string[]? Paths { get; set; }
    public int MaxResults { get; set; } = 50;
    public int MaxChars { get; set; } = 12000;
}

sealed class SymbolTool
{
    const string SemanticModeName = "semantic";
    const string SyntaxModeName = "syntax";

    static readonly HashSet<string> SupportedKinds = new(StringComparer.Ordinal)
    {
        "class", "struct", "interface", "enum", "record", "delegate",
        "method", "constructor", "property", "field", "event",
    };

    readonly ToolContext context;
    readonly SemaphoreSlim workspaceLock = new(1, 1);
    WorkspaceCache? cache;

    public SymbolTool(ToolContext context)
    {
        this.context = context;
    }

    public async Task<object> RunAsync(SymbolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query) == string.IsNullOrWhiteSpace(request.SymbolId))
            throw new ToolException(ErrorCodeSet.InvalidArgument, "Supply exactly one of query or symbolId.");
        string[] kinds = request.Kinds ?? [];
        if (kinds.Any(kind => !SupportedKinds.Contains(kind)))
            throw new ToolException(ErrorCodeSet.InvalidArgument, $"Unsupported symbol kind: {kinds.First(kind => !SupportedKinds.Contains(kind))}");

        int maxResults = Math.Clamp(request.MaxResults, 1, 500);
        int maxChars = Math.Clamp(request.MaxChars, 1000, 100000);
        string[] pathFilters = NormalizeFilters(request.Paths);
        WorkspaceResult workspace = await GetWorkspaceAsync();
        if (workspace.Solution is null || workspace.HasNewUntrackedSources)
        {
            string reason = workspace.Solution is null
                ? workspace.Reason ?? "Unity solution is unavailable."
                : "New C# files are not represented in the generated Unity projects.";
            return RunSyntaxFallback(request, kinds, pathFilters, maxResults, maxChars, reason, workspace);
        }

        var symbols = request.SymbolId is not null
            ? await ResolveSymbolIdAsync(workspace.Solution, request.SymbolId)
            : await FindDeclarationsAsync(workspace.Solution, request.Query!, kinds);
        symbols = symbols
            .Where(symbol => kinds.Length == 0 || kinds.Contains(GetKind(symbol), StringComparer.Ordinal))
            .Where(symbol => GetDeclarationLocations(symbol).Any(location => MatchesPath(location.Path, pathFilters)))
            .GroupBy(GetSymbolId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(maxResults + 1)
            .ToList();

        bool resultLimit = symbols.Count > maxResults;
        if (resultLimit)
            symbols.RemoveAt(symbols.Count - 1);
        bool ambiguous = request.SymbolId is null && symbols.Count != 1;
        var responseSymbols = symbols.Select(CreateSymbolInfo).ToList();
        List<SymbolLocation> references = [];
        if (request.IncludeReferences && !ambiguous && symbols.Count == 1)
        {
            IEnumerable<ReferencedSymbol> found = await SymbolFinder.FindReferencesAsync(symbols[0], workspace.Solution);
            references = found.SelectMany(reference => reference.Locations)
                .Where(location => !location.IsImplicit)
                .Select(location => CreateLocation(location.Location))
                .Where(location => MatchesPath(location.Path, pathFilters))
                .Distinct(SymbolLocationComparer.Instance)
                .OrderBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(location => location.Line)
                .ThenBy(location => location.Column)
                .Take(maxResults + 1)
                .ToList();
            if (references.Count > maxResults)
            {
                references.RemoveAt(references.Count - 1);
                resultLimit = true;
            }
        }

        var response = CreateResponse(workspace, responseSymbols, references, ambiguous, resultLimit, false);
        bool charLimit = false;
        while (Protocol.Serialize(response).Length > maxChars && (references.Count > 0 || responseSymbols.Count > 0))
        {
            charLimit = true;
            if (references.Count > 0)
                references.RemoveAt(references.Count - 1);
            else
                responseSymbols.RemoveAt(responseSymbols.Count - 1);
            response = CreateResponse(workspace, responseSymbols, references, ambiguous, resultLimit, true);
        }
        return CreateResponse(workspace, responseSymbols, references, ambiguous, resultLimit, charLimit);
    }

    async Task<List<ISymbol>> FindDeclarationsAsync(Solution solution, string query, string[] kinds)
    {
        SymbolFilter filter = kinds.Length > 0 && kinds.All(IsTypeKind)
            ? SymbolFilter.Type
            : kinds.Length > 0 && kinds.All(kind => !IsTypeKind(kind))
                ? SymbolFilter.Member
                : SymbolFilter.TypeAndMember;
        List<ISymbol> result = [];
        foreach (Project project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
            result.AddRange(await SymbolFinder.FindDeclarationsAsync(project, query, false, filter));
        return result;
    }

    static async Task<List<ISymbol>> ResolveSymbolIdAsync(Solution solution, string symbolId)
    {
        int separator = symbolId.IndexOf('|');
        if (separator <= 0 || separator == symbolId.Length - 1)
            throw new ToolException("INVALID_SYMBOL_ID", "symbolId is malformed.");
        string assemblyName = symbolId[..separator];
        string documentationId = symbolId[(separator + 1)..];
        List<ISymbol> result = [];
        foreach (Project project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            Compilation? compilation = await project.GetCompilationAsync();
            ISymbol? symbol = compilation is null || compilation.AssemblyName != assemblyName
                ? null
                : DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationId, compilation);
            if (symbol is not null)
                result.Add(symbol);
        }
        if (result.Count == 0)
            throw new ToolException("SYMBOL_NOT_FOUND", "symbolId no longer resolves in the current workspace.");
        return result;
    }

    async Task<WorkspaceResult> GetWorkspaceAsync()
    {
        string fingerprint = GetWorkspaceFingerprint();
        await workspaceLock.WaitAsync();
        try
        {
            if (cache?.Fingerprint == fingerprint)
                return cache.Result;

            cache?.Workspace?.Dispose();
            string? solutionPath = Directory.EnumerateFiles(context.ProjectRoot, "*.slnx", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(context.ProjectRoot, "*.sln", SearchOption.TopDirectoryOnly))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (solutionPath is null)
            {
                WorkspaceResult missing = new(null, null, "No .slnx or .sln exists at the project root.", false, 0, false);
                cache = new(fingerprint, null, missing);
                return missing;
            }

            try
            {
                if (!MSBuildLocator.IsRegistered)
                    MSBuildLocator.RegisterDefaults();
                MSBuildWorkspace workspace = MSBuildWorkspace.Create();
                workspace.LoadMetadataForReferencedProjects = true;
                bool hasWarnings = false;
                workspace.RegisterWorkspaceFailedHandler(_ => hasWarnings = true);
                Solution solution = await workspace.OpenSolutionAsync(solutionPath);
                WorkspaceCompleteness completeness = GetCompleteness(solution);
                WorkspaceResult result = new(
                    solution,
                    Path.GetFileName(solutionPath),
                    null,
                    completeness.HasNewMissingSources,
                    completeness.MissingCount,
                    hasWarnings);
                cache = new(fingerprint, workspace, result);
                return result;
            }
            catch (Exception exception)
            {
                WorkspaceResult failed = new(null, Path.GetFileName(solutionPath), exception.Message, false, 0, false);
                cache = new(fingerprint, null, failed);
                return failed;
            }
        }
        finally
        {
            workspaceLock.Release();
        }
    }

    WorkspaceCompleteness GetCompleteness(Solution solution)
    {
        var included = solution.Projects
            .SelectMany(project => project.Documents)
            .Select(document => document.FilePath)
            .Where(path => path is not null && context.Paths.IsInside(Path.GetFullPath(path)))
            .Select(path => Path.GetFullPath(path!))
            .ToHashSet(PathComparer);
        string assets = Path.Combine(context.ProjectRoot, UnityPathSet.Assets);
        if (!Directory.Exists(assets))
            return new(false, 0);
        string[] projectFiles = Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories).ToArray();
        string[] missing = projectFiles.Where(path => !included.Contains(Path.GetFullPath(path))).ToArray();
        DateTime newestProject = Directory.EnumerateFiles(context.ProjectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        return new(
            missing.Any(path => File.GetLastWriteTimeUtc(path) > newestProject),
            missing.Length);
    }

    object RunSyntaxFallback(
        SymbolRequest request,
        string[] kinds,
        string[] pathFilters,
        int maxResults,
        int maxChars,
        string reason,
        WorkspaceResult workspace)
    {
        if (request.SymbolId is not null)
            throw new ToolException("SEMANTIC_WORKSPACE_UNAVAILABLE", $"{reason} A semantic symbolId cannot be resolved.");

        List<SymbolInfo> symbols = [];
        foreach (string path in EnumerateSourceFiles())
        {
            string relative = context.Paths.Relative(path);
            if (!MatchesPath(relative, pathFilters))
                continue;
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            }
            catch
            {
                continue;
            }
            SyntaxNode root = tree.GetRoot();
            foreach (SyntaxDeclaration declaration in GetSyntaxDeclarations(root))
            {
                if (!string.Equals(declaration.Name, request.Query, StringComparison.Ordinal) ||
                    (kinds.Length > 0 && !kinds.Contains(declaration.Kind, StringComparer.Ordinal)))
                    continue;
                FileLinePositionSpan span = declaration.Node.GetLocation().GetLineSpan();
                symbols.Add(new SymbolInfo
                {
                    SymbolId = $"syntax:{relative}:{declaration.Node.SpanStart}",
                    Kind = declaration.Kind,
                    Display = declaration.Display,
                    Declarations =
                    [
                        new LocationFile
                        {
                            Path = relative,
                            Positions =
                            [
                                [span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1],
                            ],
                        },
                    ],
                });
                if (symbols.Count > maxResults)
                    break;
            }
            if (symbols.Count > maxResults)
                break;
        }

        bool resultLimit = symbols.Count > maxResults;
        if (resultLimit)
            symbols.RemoveAt(symbols.Count - 1);
        string? pathBase = PathGuard.GetCommonDirectory(
            symbols.SelectMany(symbol => symbol.Declarations.Select(file => file.Path)));
        symbols = PackSymbols(symbols, pathBase);
        SymbolResponse response = new()
        {
            SemanticMode = SyntaxModeName,
            Reason = reason,
            Solution = workspace.SolutionName,
            PathBase = pathBase,
            WorkspaceComplete = false,
            MissingSourceCount = workspace.MissingSourceCount,
            WorkspaceWarnings = workspace.HasWarnings ? (bool?)true : null,
            Symbols = symbols,
            References = [],
            Ambiguous = symbols.Count != 1,
            Truncated = resultLimit,
            LimitReason = resultLimit ? LimitReasonSet.Results : null,
        };
        bool charLimit = false;
        while (Protocol.Serialize(response).Length > maxChars && symbols.Count > 0)
        {
            charLimit = true;
            symbols.RemoveAt(symbols.Count - 1);
            response.Symbols = symbols;
        }
        response.Truncated = resultLimit || charLimit;
        response.LimitReason = resultLimit && charLimit
            ? LimitReasonSet.ResultsAndChars
            : resultLimit ? LimitReasonSet.Results : charLimit ? LimitReasonSet.Chars : null;
        response.ReturnedSymbols = symbols.Count;
        return response;
    }

    IEnumerable<string> EnumerateSourceFiles()
    {
        foreach (string rootName in new[] { UnityPathSet.Assets, UnityPathSet.Packages })
        {
            string root = Path.Combine(context.ProjectRoot, rootName);
            if (!Directory.Exists(root))
                continue;
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                yield return path;
        }
    }

    static IEnumerable<SyntaxDeclaration> GetSyntaxDeclarations(SyntaxNode root)
    {
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            (string Name, string Kind, SyntaxNode Node)? declaration = node switch
            {
                ClassDeclarationSyntax value => (value.Identifier.Text, "class", value),
                InterfaceDeclarationSyntax value => (value.Identifier.Text, "interface", value),
                StructDeclarationSyntax value => (value.Identifier.Text, "struct", value),
                RecordDeclarationSyntax value => (value.Identifier.Text, "record", value),
                EnumDeclarationSyntax value => (value.Identifier.Text, "enum", value),
                DelegateDeclarationSyntax value => (value.Identifier.Text, "delegate", value),
                MethodDeclarationSyntax value => (value.Identifier.Text, "method", value),
                ConstructorDeclarationSyntax value => (value.Identifier.Text, "constructor", value),
                PropertyDeclarationSyntax value => (value.Identifier.Text, "property", value),
                EventDeclarationSyntax value => (value.Identifier.Text, "event", value),
                _ => null,
            };
            if (declaration.HasValue)
            {
                yield return new(
                    declaration.Value.Name,
                    declaration.Value.Kind,
                    GetSyntaxDisplay(declaration.Value.Node, declaration.Value.Name),
                    declaration.Value.Node);
            }
            if (node is FieldDeclarationSyntax field)
            {
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    yield return new(variable.Identifier.Text, "field", GetSyntaxDisplay(variable, variable.Identifier.Text), variable);
            }
            if (node is EventFieldDeclarationSyntax eventField)
            {
                foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
                    yield return new(variable.Identifier.Text, "event", GetSyntaxDisplay(variable, variable.Identifier.Text), variable);
            }
        }
    }

    static string GetSyntaxDisplay(SyntaxNode node, string name)
    {
        string? namespaceName = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        string[] containers = node.Ancestors().OfType<TypeDeclarationSyntax>()
            .Reverse()
            .Select(type => type.Identifier.Text)
            .ToArray();
        return string.Join(".", new[] { namespaceName }.Where(value => !string.IsNullOrEmpty(value))
            .Concat(containers)
            .Append(name));
    }

    SymbolInfo CreateSymbolInfo(ISymbol symbol) => new()
    {
        SymbolId = GetSymbolId(symbol),
        Kind = GetKind(symbol),
        Display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
        Declarations = GroupLocations(GetDeclarationLocations(symbol)),
    };

    SymbolLocation CreateLocation(Location location)
    {
        FileLinePositionSpan span = location.GetLineSpan();
        return new SymbolLocation
        {
            Path = context.Paths.Relative(span.Path),
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
        };
    }

    IEnumerable<SymbolLocation> GetDeclarationLocations(ISymbol symbol) =>
        symbol.Locations
            .Where(location => location.IsInSource && location.SourceTree?.FilePath is { Length: > 0 } path &&
                               context.Paths.IsInside(Path.GetFullPath(path)))
            .Select(CreateLocation)
            .Distinct(SymbolLocationComparer.Instance);

    static string GetSymbolId(ISymbol symbol)
    {
        string projectName = symbol.ContainingAssembly?.Name ?? "unknown";
        string documentationId = DocumentationCommentId.CreateDeclarationId(symbol)
            ?? throw new ToolException("SYMBOL_ID_UNAVAILABLE", $"Cannot create a stable id for {symbol.Name}.");
        return $"{projectName}|{documentationId}";
    }

    static string GetKind(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol named when named.IsRecord => "record",
        INamedTypeSymbol { TypeKind: TypeKind.Class } => "class",
        INamedTypeSymbol { TypeKind: TypeKind.Struct } => "struct",
        INamedTypeSymbol { TypeKind: TypeKind.Interface } => "interface",
        INamedTypeSymbol { TypeKind: TypeKind.Enum } => "enum",
        INamedTypeSymbol { TypeKind: TypeKind.Delegate } => "delegate",
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "constructor",
        IMethodSymbol => "method",
        IPropertySymbol => "property",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        _ => symbol.Kind.ToString().ToLowerInvariant(),
    };

    SymbolResponse CreateResponse(
        WorkspaceResult workspace,
        List<SymbolInfo> symbols,
        List<SymbolLocation> references,
        bool ambiguous,
        bool resultLimit,
        bool charLimit)
    {
        List<LocationFile> groupedReferences = GroupLocations(references);
        string? pathBase = PathGuard.GetCommonDirectory(
            symbols.SelectMany(symbol => symbol.Declarations.Select(file => file.Path))
                .Concat(groupedReferences.Select(file => file.Path)));
        return new()
        {
            SemanticMode = SemanticModeName,
            Solution = workspace.SolutionName,
            PathBase = pathBase,
            WorkspaceComplete = workspace.MissingSourceCount == 0,
            MissingSourceCount = workspace.MissingSourceCount > 0 ? workspace.MissingSourceCount : null,
            WorkspaceWarnings = workspace.HasWarnings ? (bool?)true : null,
            Symbols = PackSymbols(symbols, pathBase),
            References = CompactLocations(groupedReferences, pathBase),
            Ambiguous = ambiguous,
            ReturnedSymbols = symbols.Count,
            ReturnedReferences = references.Count,
            Truncated = resultLimit || charLimit,
            LimitReason = resultLimit && charLimit
                ? LimitReasonSet.ResultsAndChars
                : resultLimit ? LimitReasonSet.Results : charLimit ? LimitReasonSet.Chars : null,
        };
    }

    string[] NormalizeFilters(string[]? paths)
    {
        if (paths is not { Length: > 0 })
            return [];
        return paths.Select(path => context.Paths.Relative(context.Paths.Resolve(path))).ToArray();
    }

    static bool MatchesPath(string path, string[] filters)
    {
        if (filters.Length == 0)
            return true;
        string normalized = path.Replace('\\', '/');
        return filters.Any(filter =>
            string.Equals(normalized, filter, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(filter.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    string GetWorkspaceFingerprint()
    {
        var files = Directory.EnumerateFiles(context.ProjectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(context.ProjectRoot, "*.slnx", SearchOption.TopDirectoryOnly))
            .Concat(Directory.EnumerateFiles(context.ProjectRoot, "*.sln", SearchOption.TopDirectoryOnly))
            .Concat(Directory.Exists(Path.Combine(context.ProjectRoot, UnityPathSet.Assets))
                ? Directory.EnumerateFiles(Path.Combine(context.ProjectRoot, UnityPathSet.Assets), "*.cs", SearchOption.AllDirectories)
                : []);
        long newest = 0;
        long length = 0;
        int count = 0;
        foreach (string file in files)
        {
            FileInfo info = new(file);
            newest = Math.Max(newest, info.LastWriteTimeUtc.Ticks);
            length += info.Length;
            count++;
        }
        return $"{newest}|{count}|{length}";
    }

    static bool IsTypeKind(string kind) =>
        kind is "class" or "struct" or "interface" or "enum" or "record" or "delegate";

    static List<LocationFile> GroupLocations(IEnumerable<SymbolLocation> locations) =>
        locations
            .GroupBy(location => location.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LocationFile
            {
                Path = group.Key,
                Positions = group
                    .OrderBy(location => location.Line)
                    .ThenBy(location => location.Column)
                    .Select(location => new[] { location.Line, location.Column })
                    .ToList(),
            })
            .ToList();

    static List<SymbolInfo> PackSymbols(IEnumerable<SymbolInfo> symbols, string? pathBase) =>
        symbols.Select(symbol => new SymbolInfo
        {
            SymbolId = symbol.SymbolId,
            Kind = symbol.Kind,
            Display = symbol.Display,
            Declarations = CompactLocations(symbol.Declarations, pathBase),
        }).ToList();

    static List<LocationFile> CompactLocations(IEnumerable<LocationFile> files, string? pathBase) =>
        files.Select(file => new LocationFile
        {
            Path = PathGuard.Compact(file.Path, pathBase),
            Positions = file.Positions,
        }).ToList();

    static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    sealed record WorkspaceCache(string Fingerprint, MSBuildWorkspace? Workspace, WorkspaceResult Result);
    sealed record WorkspaceResult(
        Solution? Solution,
        string? SolutionName,
        string? Reason,
        bool HasNewUntrackedSources,
        int MissingSourceCount,
        bool HasWarnings);
    sealed record WorkspaceCompleteness(bool HasNewMissingSources, int MissingCount);
    sealed record SyntaxDeclaration(string Name, string Kind, string Display, SyntaxNode Node);
}

sealed class SymbolResponse
{
    public string SemanticMode { get; set; } = "";
    public string? Reason { get; set; }
    public string? Solution { get; set; }
    public string? PathBase { get; set; }
    public bool WorkspaceComplete { get; set; }
    public int? MissingSourceCount { get; set; }
    public bool? WorkspaceWarnings { get; set; }
    public List<SymbolInfo> Symbols { get; set; } = [];
    public List<LocationFile> References { get; set; } = [];
    public bool Ambiguous { get; set; }
    public int ReturnedSymbols { get; set; }
    public int ReturnedReferences { get; set; }
    public bool Truncated { get; set; }
    public string? LimitReason { get; set; }
}

sealed class SymbolInfo
{
    public string SymbolId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Display { get; set; } = "";
    public List<LocationFile> Declarations { get; set; } = [];
}

sealed class SymbolLocation
{
    public string Path { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
}

sealed class LocationFile
{
    public string Path { get; set; } = "";
    public List<int[]> Positions { get; set; } = [];
}

sealed class SymbolLocationComparer : IEqualityComparer<SymbolLocation>
{
    public static readonly SymbolLocationComparer Instance = new();

    public bool Equals(SymbolLocation? x, SymbolLocation? y) =>
        x is not null && y is not null &&
        x.Line == y.Line &&
        x.Column == y.Column &&
        string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(SymbolLocation value) =>
        HashCode.Combine(value.Path.ToUpperInvariant(), value.Line, value.Column);
}
