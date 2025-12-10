// ============================================================================
// McpHandlers.cs
// ============================================================================
// MCP protocol handlers for the server.
// Handles initialize, tools/list, and tools/call methods per the MCP spec.
// All responses must include "jsonrpc": "2.0" and the request's id.
//
// Security features:
// - Input validation on all tool parameters
// - Rate limiting to prevent DoS attacks
// - Output sanitization to prevent data leakage
// - Secure error handling (no stack traces exposed)
// ============================================================================

using System.Text.Json;
using DiffPilot.Security;
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
            instructions = "This MCP server provides PR code review and developer productivity tools. Available tools:\n"
                + "- get_pr_diff: Get the raw diff between branches for any purpose\n"
                + "- review_pr_changes: Get diff with instructions for AI code review\n"
                + "- generate_pr_title: Generate a conventional PR title from changes\n"
                + "- generate_pr_description: Generate a complete PR description with summary, changes, and testing notes\n"
                + "- generate_commit_message: Generate commit message from staged/unstaged changes\n"
                + "- scan_secrets: Detect accidentally committed secrets, API keys, passwords\n"
                + "- diff_stats: Get detailed statistics about changes\n"
                + "- suggest_tests: Analyze changes and suggest test cases\n"
                + "- generate_changelog: Generate changelog entries from commits",
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
                description = "Get diff between branches.",
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
                description = "Review PR with AI instructions.",
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
                description = "Generate conventional PR title.",
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
                description = "Generate PR description with checklist.",
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
            // Tool 5: generate_commit_message - Generate commit message from changes
            new
            {
                name = "generate_commit_message",
                description = "Generate commit message from changes.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        style = new
                        {
                            type = "string",
                            description = "Message style: 'conventional' (feat/fix/chore) or 'simple'. Default: 'conventional'.",
                        },
                        scope = new
                        {
                            type = "string",
                            description = "Optional scope for conventional commits (e.g., 'api', 'ui', 'auth').",
                        },
                        includeBody = new
                        {
                            type = "boolean",
                            description = "Include body section in suggestion (default: true).",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 6: scan_secrets - Detect secrets in changes
            new
            {
                name = "scan_secrets",
                description = "Detect secrets, keys, passwords.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        scanStaged = new
                        {
                            type = "boolean",
                            description = "Scan staged changes (default: true).",
                        },
                        scanUnstaged = new
                        {
                            type = "boolean",
                            description = "Scan unstaged changes (default: true).",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 7: diff_stats - Get change statistics
            new
            {
                name = "diff_stats",
                description = "Get change statistics by file.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "Base branch for comparison. If provided, compares branches instead of working directory.",
                        },
                        featureBranch = new
                        {
                            type = "string",
                            description = "Feature branch for comparison. Defaults to current branch.",
                        },
                        includeWorkingDir = new
                        {
                            type = "boolean",
                            description = "Include working directory stats (default: true).",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 8: suggest_tests - Suggest test cases
            new
            {
                name = "suggest_tests",
                description = "Suggest tests for changed code.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "Base branch for comparison. If not provided, analyzes working directory changes.",
                        },
                    },
                    required = Array.Empty<string>(),
                },
            },
            // Tool 9: generate_changelog - Generate changelog entries
            new
            {
                name = "generate_changelog",
                description = "Generate changelog from commits.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "Base branch to compare against (default: 'main').",
                        },
                        featureBranch = new
                        {
                            type = "string",
                            description = "Feature branch with commits. Defaults to current branch.",
                        },
                        format = new
                        {
                            type = "string",
                            description = "Output format: 'keepachangelog' (categorized) or 'simple' (flat list). Default: 'keepachangelog'.",
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
    /// Includes security validation, rate limiting, and output sanitization.
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

        // Security: Validate tool name (alphanumeric, underscore, hyphen only)
        var toolName = toolCall.Name;
        if (toolName.Length > 50 || !System.Text.RegularExpressions.Regex.IsMatch(toolName, @"^[a-zA-Z_][a-zA-Z0-9_-]*$"))
        {
            SecurityHelpers.LogSecurityEvent("INVALID_TOOL_NAME", $"Rejected tool name: {toolName[..Math.Min(20, toolName.Length)]}");
            return (null, -32601, "Invalid tool name format.");
        }

        // Security: Rate limiting per tool
        if (!SecurityHelpers.CheckRateLimit(toolName))
        {
            SecurityHelpers.LogSecurityEvent("RATE_LIMIT_EXCEEDED", $"Tool: {toolName}");
            return (null, -32000, "Rate limit exceeded. Please wait before making more requests.");
        }

        // Dispatch to the appropriate tool
        ToolResult toolResult;
        try
        {
            toolResult = toolCall.Name switch
            {
                "get_pr_diff" => await PrReviewTools.GetPrDiffAsync(toolCall.Arguments),
                "review_pr_changes" => await PrReviewTools.ReviewPrChangesAsync(toolCall.Arguments),
                "generate_pr_title" => await PrReviewTools.GeneratePrTitleAsync(toolCall.Arguments),
                "generate_pr_description" => await PrReviewTools.GeneratePrDescriptionAsync(toolCall.Arguments),
                "generate_commit_message" => await DeveloperTools.GenerateCommitMessageAsync(toolCall.Arguments),
                "scan_secrets" => await DeveloperTools.ScanSecretsAsync(toolCall.Arguments),
                "diff_stats" => await DeveloperTools.GetDiffStatsAsync(toolCall.Arguments),
                "suggest_tests" => await DeveloperTools.SuggestTestsAsync(toolCall.Arguments),
                "generate_changelog" => await DeveloperTools.GenerateChangelogAsync(toolCall.Arguments),
                _ => null!,
            };
        }
        catch (SecurityException ex)
        {
            // Security violations are logged and returned as errors
            SecurityHelpers.LogSecurityEvent("SECURITY_EXCEPTION", ex.Message);
            return (null, -32602, ex.Message);
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions but don't expose details to client
            SecurityHelpers.LogSecurityEvent("UNEXPECTED_ERROR", $"Tool {toolName}: {ex.GetType().Name}");
            Console.Error.WriteLine($"[ERROR] Tool execution failed: {ex}");
            return (null, -32603, "An internal error occurred while processing the request.");
        }

        // Handle unknown tool
        if (toolResult == null)
        {
            return (null, -32601, $"Tool not found: {toolCall.Name}");
        }

        // Security: Sanitize output to prevent sensitive data leakage
        var sanitizedContent = toolResult.Content.Select(c => new
        {
            type = c.Type,
            text = SecurityHelpers.SanitizeOutput(c.Text)
        }).ToArray();

        // Return tool result in MCP format
        var result = new
        {
            content = sanitizedContent,
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
