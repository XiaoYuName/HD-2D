using System.Text.Json;

namespace SourceCodeMcp;

sealed class McpServer
{
    readonly ToolDispatcher dispatcher;

    McpServer(ToolDispatcher dispatcher) => this.dispatcher = dispatcher;

    public static McpServer Create(ToolContext context) => new(ToolDispatcher.Create(context));

    public async Task RunAsync()
    {
        Console.InputEncoding = new System.Text.UTF8Encoding(false);
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        while (await Console.In.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            object? response = await HandleAsync(line.TrimStart('\uFEFF'));
            if (response is null)
                continue;
            await Console.Out.WriteLineAsync(Protocol.Serialize(response));
            await Console.Out.FlushAsync();
        }
    }

    async Task<object?> HandleAsync(string line)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return RpcError(null, -32700, "Parse error");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            object? id = ReadId(root);
            if (!root.TryGetProperty("method", out JsonElement methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
                return RpcError(id, -32600, "Invalid Request");

            string method = methodElement.GetString()!;
            try
            {
                return method switch
                {
                    "initialize" => RpcResult(id, new
                    {
                        protocolVersion = GetProtocolVersion(root),
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "unity-source-code-mcp", version = "1.0.0" },
                    }),
                    "notifications/initialized" => null,
                    "ping" => RpcResult(id, new { }),
                    "tools/list" => RpcResult(id, new { tools = ToolDefinitions.All }),
                    "tools/call" => RpcResult(id, await CallToolAsync(root)),
                    _ when id is null => null,
                    _ => RpcError(id, -32601, $"Method not found: {method}"),
                };
            }
            catch (Exception exception)
            {
                return RpcError(id, -32603, exception.Message);
            }
        }
    }

    async Task<object> CallToolAsync(JsonElement root)
    {
        if (!root.TryGetProperty("params", out JsonElement parameters) ||
            !parameters.TryGetProperty("name", out JsonElement nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
            throw new ToolException("INVALID_REQUEST", "tools/call requires params.name.");

        JsonElement arguments = parameters.TryGetProperty("arguments", out JsonElement value)
            ? value
            : default;
        try
        {
            object result = await dispatcher.CallAsync(nameElement.GetString()!, arguments);
            return new
            {
                content = new[] { new { type = "text", text = Protocol.Serialize(result) } },
                isError = false,
            };
        }
        catch (ToolException exception)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = Protocol.Serialize(new { error = new { code = exception.Code, message = exception.Message } }),
                    },
                },
                isError = true,
            };
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"[source-code-mcp] tool failed: {exception}");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = Protocol.Serialize(new
                        {
                            error = new { code = "INTERNAL_ERROR", message = exception.Message },
                        }),
                    },
                },
                isError = true,
            };
        }
    }

    static string GetProtocolVersion(JsonElement root)
    {
        if (root.TryGetProperty("params", out JsonElement parameters) &&
            parameters.TryGetProperty("protocolVersion", out JsonElement version) &&
            version.ValueKind == JsonValueKind.String)
            return version.GetString()!;
        return "2024-11-05";
    }

    static object? ReadId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out JsonElement id))
            return null;
        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number when id.TryGetInt64(out long number) => number,
            _ => null,
        };
    }

    static object RpcResult(object? id, object result) => new { jsonrpc = "2.0", id, result };

    static object RpcError(object? id, int code, string message) =>
        new { jsonrpc = "2.0", id, error = new { code, message } };
}

sealed class ToolDispatcher
{
    readonly SearchTool search;
    readonly ReadTool read;
    readonly PatchTool patch;
    readonly DiagnosticsTool diagnostics;
    readonly SymbolTool symbols;

    ToolDispatcher(ToolContext context)
    {
        search = new(context);
        read = new(context);
        patch = new(context);
        diagnostics = new(context);
        symbols = new(context);
    }

    public static ToolDispatcher Create(ToolContext context) => new(context);

    public Task<object> CallAsync(string name, JsonElement arguments) => name switch
    {
        ToolNameSet.SearchCode => search.RunAsync(Protocol.Deserialize<SearchRequest>(arguments)),
        ToolNameSet.ReadCode => Task.FromResult<object>(read.Run(Protocol.Deserialize<ReadRequest>(arguments))),
        ToolNameSet.FindSymbol => symbols.RunAsync(Protocol.Deserialize<SymbolRequest>(arguments)),
        ToolNameSet.ApplyPatch => Task.FromResult<object>(patch.Run(Protocol.Deserialize<PatchRequest>(arguments))),
        ToolNameSet.GetDiagnostics => Task.FromResult<object>(diagnostics.Run(Protocol.Deserialize<DiagnosticsRequest>(arguments))),
        _ => throw new ToolException("UNKNOWN_TOOL", $"Unknown tool: {name}"),
    };
}
