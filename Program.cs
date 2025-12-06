using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (line == null)
                break;

            line = line.Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            JsonRcpRequest? request = null;

            try
            {
                request = JsonSerializer.Deserialize<JsonRcpRequest>(line, _jsonOptions);
            }
            catch (JsonException)
            {
                WriteError(null, -32700, "Parse error");
                continue;
            }

            if (request == null || request.Method == null)
            {
                WriteError(null, -32600, "Request was null after deserialization");
                continue;
            }

            // dispatch methods
            try
            {
                await HandleRequestAsync(request);
            }
            catch (Exception ex)
            {
                WriteError(request.Id, -32603, "Internal error: " + ex.ToString());
            }
        }
    }

    private static async Task HandleRequestAsync(JsonRcpRequest request)
    {
        // Notifications have no id → do not respond (e.g. notifications/initialized)
        if (!request.Id.HasValue)
        {
            // You could log here if you want, but don't write a JSON-RPC response.
            return;
        }

        switch (request.Method)
        {
            case "initialize":
                await HandleInitializeAsync(request);
                break;

            case "tools/list":
                await HandleListTools(request);
                break;

            case "tools/call":
                await HandleCallToolAsync(request);
                break;

            default:
                WriteError(request.Id, -32601, "Method not found: " + request.Method);
                break;
        }
    }

    private static Task HandleInitializeAsync(JsonRcpRequest request)
    {
        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new
                {
                    // we don't send tools/listChanged notifications
                    listChanged = false,
                },
            },
            serverInfo = new { name = "AzDoErrorMcpServer", version = "0.1.0" },
            instructions = "Simple manually implemented MCP echo server.",
        };

        WriteResult(request.Id, result);
        return Task.CompletedTask;
    }

    #region Tool: list tools

    private static async Task HandleListTools(JsonRcpRequest request)
    {
        var tools = new[]
        {
            new
            {
                name = "echo",
                description = "Echoes the input text.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "The text to echo." },
                    },
                    required = new[] { "text" },
                },
            },
        };

        var result = new { tools };

        WriteResult(request.Id, result);
    }

    #endregion

    #region Tool: call tools

    private static async Task HandleCallToolAsync(JsonRcpRequest request)
    {
        if (request.Params == null)
        {
            WriteError(request.Id, -32602, "Missing parameters for tool call.");
            return;
        }

        var toolCall = JsonSerializer.Deserialize<ToolCallParams>(
            request.Params.Value.GetRawText(),
            _jsonOptions
        );
        if (toolCall == null)
        {
            WriteError(request.Id, -32602, "Invalid tool call parameters.");
            return;
        }

        switch (toolCall.Name)
        {
            case "echo":
                await HandleEchoToolAsync(request.Id, toolCall.Arguments);
                break;

            default:
                WriteError(request.Id, -32601, "Tool not found: " + toolCall.Name);
                break;
        }
    }

    private static Task HandleEchoToolAsync(JsonElement? id, JsonElement? arguments)
    {
        if (arguments == null)
        {
            WriteError(id, -32602, "Invalid params", "Missing arguments for echo tool.");
            return Task.CompletedTask;
        }

        if (
            !arguments.Value.TryGetProperty("text", out var textElement)
            || textElement.ValueKind != JsonValueKind.String
        )
        {
            WriteError(id, -32602, "Invalid params", "Expected 'text' string in arguments.");
            return Task.CompletedTask;
        }

        var text = textElement.GetString() ?? string.Empty;

        var result = new { content = new[] { new { type = "text", text } }, isError = false };

        WriteResult(id, result);
        return Task.CompletedTask;
    }

    #endregion

    #region Helpers

    private static void WriteResult(JsonElement? id, object result)
    {
        var response = new JsonRcpResponse<object>
        {
            Id = id,
            Result = JsonSerializer.SerializeToElement(result, _jsonOptions),
        };

        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    private static void WriteError(JsonElement? id, int code, string message, string? data = null)
    {
        var error = new JsonRcpError
        {
            Code = code,
            Message = message,
            Data = data,
        };

        var response = new JsonRcpResponse<object> { Id = id, Error = error };

        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    #endregion

    #region JSON-RPC Models


    private class JsonRcpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string? Jsonrpc { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }

        // id can be string or number, so use JsonElement?
        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }
    }

    private class JsonRcpResponse<TResult>
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("result")]
        public TResult? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRcpError? Error { get; set; }
    }

    private class JsonRcpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    private class ToolCallParams
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public JsonElement? Arguments { get; set; }
    }

    #endregion
}
