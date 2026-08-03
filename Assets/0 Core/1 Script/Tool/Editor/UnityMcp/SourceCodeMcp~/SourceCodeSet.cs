namespace SourceCodeMcp;

static class ToolNameSet
{
    public const string SearchCode = "search_code";
    public const string ReadCode = "read_code";
    public const string FindSymbol = "find_symbol";
    public const string ReplaceSymbol = "replace_symbol";
    public const string ApplyPatch = "apply_patch";
    public const string GetDiagnostics = "get_diagnostics";
    public const string InspectUnityCode = "inspect_unity_code";
}

static class ErrorCodeSet
{
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string InvalidPath = "INVALID_PATH";
    public const string InvalidRange = "INVALID_RANGE";
    public const string NotAFile = "NOT_A_FILE";
}

static class LimitReasonSet
{
    public const string Results = "results";
    public const string Chars = "chars";
    public const string ResultsAndChars = "results,chars";
}

static class UnityPathSet
{
    public const string Assets = "Assets";
    public const string Packages = "Packages";
    public const string Library = "Library";
}

static class SearchModeSet
{
    public const string Literal = "literal";
    public const string Regex = "regex";
}

static class DiagnosticSeveritySet
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string All = "all";
}
