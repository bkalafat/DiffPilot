// ============================================================================
// Program.cs - DiffPilot
// ============================================================================
// Entry point for the MCP stdio server.
// This server reads JSON-RPC 2.0 requests from stdin and writes responses to stdout.
//
// Key MCP stdio transport requirements:
// - Messages are newline-delimited JSON
// - Only valid JSON-RPC messages may be written to stdout
// - Logging/diagnostics must go to stderr only
// - Notifications (requests without id) must not receive a response
//
// Security features:
// - Input size validation to prevent DoS
// - Secure error messages (no internal details exposed)
// - All diagnostics go to stderr only
// - Rate limiting via McpHandlers
// ============================================================================

using System.Text;
using System.Text.Json;
using DiffPilot.Protocol;
using DiffPilot.Security;

// Security: Maximum input line length to prevent memory exhaustion
const int MaxInputLineLength = 10_000_000; // 10MB max per message

// Configure console for UTF-8
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Log startup (to stderr only)
await Console.Error.WriteLineAsync($"[INFO] DiffPilot MCP server started at {DateTime.UtcNow:O}");

// Main loop: read lines from stdin, process requests, write responses to stdout
while (true)
{
    var line = await Console.In.ReadLineAsync();

    // EOF - client closed connection, exit gracefully
    if (line == null)
    {
        await Console.Error.WriteLineAsync("[INFO] EOF received, shutting down");
        break;
    }

    // Security: Check input size to prevent DoS
    if (line.Length > MaxInputLineLength)
    {
        SecurityHelpers.LogSecurityEvent("INPUT_TOO_LARGE", $"Rejected input of {line.Length} bytes");
        WriteError(null, -32600, "Request too large");
        continue;
    }

    line = line.Trim();

    // Skip empty lines
    if (string.IsNullOrEmpty(line))
        continue;

    // Parse the JSON-RPC request
    JsonRpcRequest? request = null;
    try
    {
        request = JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonSerializerHelper.Options);
    }
    catch (JsonException)
    {
        // Parse error - still need to respond with an error
        WriteError(null, -32700, "Parse error");
        continue;
    }

    // Validate request structure
    if (request == null || request.Method == null)
    {
        WriteError(null, -32600, "Invalid Request: missing method");
        continue;
    }

    // Security: Validate method name format
    if (request.Method.Length > 100 || request.Method.Contains('\0'))
    {
        SecurityHelpers.LogSecurityEvent("INVALID_METHOD", "Method name validation failed");
        WriteError(request.Id, -32601, "Invalid method name");
        continue;
    }

    // Handle the request
    try
    {
        await HandleRequestAsync(request);
    }
    catch (Exception ex)
    {
        // Security: Don't expose internal error details to client
        WriteError(request.Id, -32603, "Internal server error");

        // Log full exception to stderr for debugging (not to stdout!)
        await Console.Error.WriteLineAsync($"[ERROR] {DateTime.UtcNow:O} Unhandled exception: {ex}");
    }
}

/// <summary>
/// Routes requests to the appropriate handler.
/// Notifications (no id) are processed but do not receive a response.
/// </summary>
static async Task HandleRequestAsync(JsonRpcRequest request)
{
    // Notifications have no id and must not receive a response
    // Common notifications: "notifications/initialized", "notifications/cancelled"
    if (!request.Id.HasValue)
    {
        // Could log notification handling here to stderr if needed
        // await Console.Error.WriteLineAsync($"[INFO] Received notification: {request.Method}");
        return;
    }

    // Dispatch to method handlers
    switch (request.Method)
    {
        case "initialize":
            var initResult = McpHandlers.HandleInitialize();
            WriteResult(request.Id, initResult);
            break;

        case "tools/list":
            var listResult = McpHandlers.HandleListTools();
            WriteResult(request.Id, listResult);
            break;

        case "tools/call":
            var (callResult, errorCode, errorMessage) = await McpHandlers.HandleCallToolAsync(
                request.Params
            );
            if (errorCode.HasValue)
            {
                WriteError(request.Id, errorCode.Value, errorMessage!);
            }
            else
            {
                WriteResult(request.Id, callResult!);
            }
            break;

        default:
            // Method not found
            WriteError(request.Id, -32601, $"Method not found: {request.Method}");
            break;
    }
}

/// <summary>
/// Writes a successful JSON-RPC response to stdout.
/// </summary>
static void WriteResult(JsonElement? id, object result)
{
    var response = new JsonRpcResponse
    {
        Id = id,
        Result = JsonSerializer.SerializeToElement(result, JsonSerializerHelper.Options),
    };

    var responseJson = JsonSerializer.Serialize(response, JsonSerializerHelper.Options);
    Console.Out.WriteLine(responseJson);
    Console.Out.Flush();
}

/// <summary>
/// Writes a JSON-RPC error response to stdout.
/// Standard error codes:
/// -32700: Parse error
/// -32600: Invalid Request
/// -32601: Method not found
/// -32602: Invalid params
/// -32603: Internal error
/// </summary>
static void WriteError(JsonElement? id, int code, string message, string? data = null)
{
    var error = new JsonRpcError
    {
        Code = code,
        Message = message,
        Data = data,
    };

    var response = new JsonRpcResponse { Id = id, Error = error };

    var responseJson = JsonSerializer.Serialize(response, JsonSerializerHelper.Options);
    Console.Out.WriteLine(responseJson);
    Console.Out.Flush();
}
