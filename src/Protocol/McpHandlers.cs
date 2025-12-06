// ============================================================================
// McpHandlers.cs
// ============================================================================
// MCP protocol handlers for the server.
// Handles initialize, tools/list, and tools/call methods per the MCP spec.
// All responses must include "jsonrpc": "2.0" and the request's id.
// ============================================================================

using System.Text.Json;
using AzDoErrorMcpServer.Tools;

namespace AzDoErrorMcpServer.Protocol;

/// <summary>
/// Handles MCP protocol methods.
/// </summary>
internal static class McpHandlers
{
    /// <summary>
    /// Server name used in serverInfo.
    /// </summary>
    private const string ServerName = "AzDoErrorMcpServer";

    /// <summary>
    /// Server version used in serverInfo.
    /// </summary>
    private const string ServerVersion = "0.2.0";

    /// <summary>
    /// Protocol version we support.
    /// Using 2025-03-26 which is the current stable version per MCP spec.
    /// </summary>
    private const string ProtocolVersion = "2025-03-26";

    /// <summary>
    /// Handles the "initialize" method.
    /// Returns server capabilities, version info, and optional instructions.
    /// </summary>
    public static object HandleInitialize() =>
        new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new
                {
                    // We don't emit tools/listChanged notifications
                    listChanged = false,
                },
            },
            serverInfo = new { name = ServerName, version = ServerVersion },
            instructions = "This MCP server provides git patch generation tools for code review workflows. "
                + "Use generate_pr_patch to create a diff between specified branches, or "
                + "generate_pr_patch_auto to auto-detect branches and generate a patch.",
        };

    /// <summary>
    /// Handles the "tools/list" method.
    /// Returns the list of available tools with their schemas.
    /// </summary>
    public static object HandleListTools()
    {
        var tools = new object[]
        {
            new
            {
                name = "generate_pr_patch",
                description = "Fetches latest from git and generates a patch file diffing baseBranch...featureBranch.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main').",
                        },
                        featureBranch = new
                        {
                            type = "string",
                            description = "The feature branch name (e.g., 'feature/acqverse/tmbb').",
                        },
                        patchFileName = new
                        {
                            type = "string",
                            description = "Optional patch file name (default: 'pr.patch').",
                        },
                        remote = new
                        {
                            type = "string",
                            description = "Optional git remote (default: 'origin').",
                        },
                    },
                    required = new[] { "baseBranch", "featureBranch" },
                },
            },
            new
            {
                name = "generate_pr_patch_auto",
                description = "Detects the base branch and current branch from git and generates a patch diffing baseBranch...featureBranch.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        patchFileName = new
                        {
                            type = "string",
                            description = "Optional patch file name (default: 'pr-auto.patch').",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
        };

        return new { tools };
    }

    /// <summary>
    /// Handles the "tools/call" method.
    /// Dispatches to the appropriate tool handler based on the tool name.
    /// </summary>
    /// <returns>
    /// A tuple of (result, errorCode, errorMessage).
    /// If errorCode is non-null, it indicates a JSON-RPC protocol error.
    /// </returns>
    public static async Task<(
        object? Result,
        int? ErrorCode,
        string? ErrorMessage
    )> HandleCallToolAsync(JsonElement? parameters)
    {
        if (parameters == null)
        {
            return (null, -32602, "Missing parameters for tools/call.");
        }

        ToolCallParams? toolCall;
        try
        {
            toolCall = JsonSerializer.Deserialize<ToolCallParams>(
                parameters.Value.GetRawText(),
                JsonSerializerHelper.Options
            );
        }
        catch (JsonException)
        {
            return (null, -32602, "Invalid tool call parameters format.");
        }

        if (toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name))
        {
            return (null, -32602, "Tool name is required.");
        }

        // Dispatch to the appropriate tool
        ToolResult toolResult = toolCall.Name switch
        {
            "generate_pr_patch" => await GitPatchTools.GeneratePrPatchAsync(toolCall.Arguments),
            "generate_pr_patch_auto" => await GitPatchTools.GeneratePrPatchAutoAsync(
                toolCall.Arguments
            ),
            _ => null!,
        };

        // Handle unknown tool
        if (toolResult == null)
        {
            return (null, -32601, $"Tool not found: {toolCall.Name}");
        }

        // Return tool result in MCP format
        var result = new
        {
            content = toolResult
                .Content.Select(c => new { type = c.Type, text = c.Text })
                .ToArray(),
            isError = toolResult.IsError,
        };

        return (result, null, null);
    }
}

/// <summary>
/// Helper for JSON serialization options.
/// </summary>
internal static class JsonSerializerHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
