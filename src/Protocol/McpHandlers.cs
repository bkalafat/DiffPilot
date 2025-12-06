// ============================================================================
// McpHandlers.cs
// ============================================================================
// MCP protocol handlers for the server.
// Handles initialize, tools/list, and tools/call methods per the MCP spec.
// All responses must include "jsonrpc": "2.0" and the request's id.
// ============================================================================

using System.Text.Json;
using DiffPilot.Tools;

namespace DiffPilot.Protocol;

/// <summary>
/// Handles MCP protocol methods.
/// </summary>
internal static class McpHandlers
{
    /// <summary>
    /// Server name used in serverInfo.
    /// </summary>
    private const string ServerName = "DiffPilot";

    /// <summary>
    /// Server version used in serverInfo.
    /// </summary>
    private const string ServerVersion = "1.0.0";

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
            instructions = "This MCP server provides PR code review tools. Available tools:\n"
                + "- get_pr_diff: Get the raw diff between branches for any purpose\n"
                + "- review_pr_changes: Get diff with instructions for AI code review\n"
                + "- generate_pr_title: Generate a conventional PR title from changes\n"
                + "- generate_pr_description: Generate a complete PR description with summary, changes, and testing notes",
        };

    /// <summary>
    /// Handles the "tools/list" method.
    /// Returns the list of available tools with their schemas.
    /// </summary>
    public static object HandleListTools()
    {
        var tools = new object[]
        {
            // Tool 1: get_pr_diff - Raw diff for any purpose
            new
            {
                name = "get_pr_diff",
                description = "Fetches latest from git and returns the diff between base branch and current/feature branch. "
                    + "Auto-detects branches if not specified. Returns raw diff output for any purpose.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main'). Auto-detected if not provided.",
                        },
                        featureBranch = new
                        {
                            type = "string",
                            description = "The feature branch name. Defaults to current branch if not provided.",
                        },
                        remote = new
                        {
                            type = "string",
                            description = "Git remote name (default: 'origin').",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 2: review_pr_changes - Diff with review instructions
            new
            {
                name = "review_pr_changes",
                description = "Gets the PR diff and provides it with instructions for AI code review. "
                    + "Use this when you want to perform a code review on the changes.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main'). Auto-detected if not provided.",
                        },
                        focusAreas = new
                        {
                            type = "string",
                            description = "Optional focus areas for the review (e.g., 'security, performance, error handling').",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 3: generate_pr_title - Generate PR title
            new
            {
                name = "generate_pr_title",
                description = "Analyzes the diff and generates a concise, conventional PR title. "
                    + "Returns a suggested title following conventional commit format.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main'). Auto-detected if not provided.",
                        },
                        style = new
                        {
                            type = "string",
                            description = "Title style: 'conventional' (feat/fix/chore), 'descriptive', or 'ticket' (includes branch ticket number). Default: 'conventional'.",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 4: generate_pr_description - Generate full PR description
            new
            {
                name = "generate_pr_description",
                description = "Analyzes the diff and generates a complete PR description including summary, "
                    + "list of changes, testing notes, and checklist. Ready to paste into PR.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main'). Auto-detected if not provided.",
                        },
                        includeChecklist = new
                        {
                            type = "boolean",
                            description = "Include a PR checklist (default: true).",
                        },
                        ticketUrl = new
                        {
                            type = "string",
                            description = "Optional ticket/issue URL to include in the description.",
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
            "get_pr_diff" => await PrReviewTools.GetPrDiffAsync(toolCall.Arguments),
            "review_pr_changes" => await PrReviewTools.ReviewPrChangesAsync(toolCall.Arguments),
            "generate_pr_title" => await PrReviewTools.GeneratePrTitleAsync(toolCall.Arguments),
            "generate_pr_description" => await PrReviewTools.GeneratePrDescriptionAsync(toolCall.Arguments),
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
