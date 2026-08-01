using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace SourceCodeMcp;

static class Protocol
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    public static T Deserialize<T>(JsonElement element)
    {
        try
        {
            return element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? Activator.CreateInstance<T>()
                : element.Deserialize<T>(Json) ?? throw new ToolException(ErrorCodeSet.InvalidArgument, "Arguments cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ToolException(ErrorCodeSet.InvalidArgument, exception.Message);
        }
    }
}

sealed class ToolException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public object? Details { get; } = details;
}

sealed class ToolContext
{
    ToolContext(string projectPath)
    {
        ProjectRoot = Path.GetFullPath(projectPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(ProjectRoot))
            throw new DirectoryNotFoundException($"Project path does not exist: {ProjectRoot}");
        Paths = new(ProjectRoot);
        Sources = new();
    }

    public static ToolContext Create(string projectPath) => new(projectPath);

    public string ProjectRoot { get; }
    public PathGuard Paths { get; }
    public SourceSnapshotRegistry Sources { get; }
}
