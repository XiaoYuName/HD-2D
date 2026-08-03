namespace SourceCodeMcp;

static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            string projectPath = GetArgument(args, "--project-path")
                ?? throw new ArgumentException("Missing required --project-path argument.");
            McpServer server = McpServer.Create(ToolContext.Create(projectPath));
            await server.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"[source-code-mcp] {exception}");
            return 1;
        }
    }

    static string? GetArgument(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
                return args[i + 1];
        }
        return null;
    }
}
