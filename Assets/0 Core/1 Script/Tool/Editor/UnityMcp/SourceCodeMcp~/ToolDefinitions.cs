using System.Text.Json;

namespace SourceCodeMcp;

static class ToolDefinitions
{
    public static readonly JsonElement All = Load();

    static JsonElement Load()
    {
        using Stream stream = typeof(ToolDefinitions).Assembly
            .GetManifestResourceStream("SourceCodeMcp.source-code-tools.json")
            ?? throw new InvalidOperationException("Embedded tool definitions are missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
