// ============================================================================
// ToolResult.cs
// ============================================================================
// Represents the result of a tool execution in MCP format.
// Contains content items and an error flag.
// ============================================================================

namespace DiffPilot.Tools;

/// <summary>
/// Represents the result of a tool execution.
/// Contains content items and an error flag.
/// </summary>
internal sealed class ToolResult
{
    public required List<ContentItem> Content { get; init; }
    public required bool IsError { get; init; }

    /// <summary>
    /// Creates a successful result with text content.
    /// </summary>
    public static ToolResult Success(string text)
    {
        return new ToolResult
        {
            Content = [new ContentItem { Type = "text", Text = text }],
            IsError = false,
        };
    }

    /// <summary>
    /// Creates an error result for invalid parameters or general errors.
    /// </summary>
    public static ToolResult Error(string message)
    {
        return new ToolResult
        {
            Content = [new ContentItem { Type = "text", Text = message }],
            IsError = true,
        };
    }

    /// <summary>
    /// Creates an error result for git operation failures.
    /// </summary>
    public static ToolResult GitError(string message, string details)
    {
        var text = string.IsNullOrWhiteSpace(details) ? message : $"{message}: {details}";

        return new ToolResult
        {
            Content = [new ContentItem { Type = "text", Text = text }],
            IsError = true,
        };
    }
}

/// <summary>
/// Represents a content item in a tool result.
/// </summary>
internal sealed class ContentItem
{
    public required string Type { get; init; }
    public required string Text { get; init; }
}
