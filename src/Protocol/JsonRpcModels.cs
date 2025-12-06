// ============================================================================
// JsonRpcModels.cs
// ============================================================================
// JSON-RPC 2.0 request/response/error models for the MCP server.
// These classes define the wire format for communication between client and server.
// The 'id' field is kept as JsonElement? to allow string or numeric IDs per JSON-RPC spec.
// ============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzDoErrorMcpServer.Protocol;

/// <summary>
/// Represents an incoming JSON-RPC 2.0 request or notification.
/// Notifications have no 'id' field and do not expect a response.
/// </summary>
internal sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string? Jsonrpc { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    /// <summary>
    /// Request ID. Can be string or number per JSON-RPC spec, so we use JsonElement?.
    /// If null, this is a notification and must not receive a response.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }
}

/// <summary>
/// Represents an outgoing JSON-RPC 2.0 response.
/// Contains either a result or an error, never both.
/// </summary>
internal sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// Represents a JSON-RPC 2.0 error object.
/// Standard error codes:
/// -32700: Parse error
/// -32600: Invalid request
/// -32601: Method not found
/// -32602: Invalid params
/// -32603: Internal error
/// </summary>
internal sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

/// <summary>
/// Represents the parameters for a tools/call request.
/// </summary>
internal sealed class ToolCallParams
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}
