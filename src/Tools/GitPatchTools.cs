// ============================================================================
// GitPatchTools.cs
// ============================================================================
// Implementation of the git patch generation tools for the MCP server.
// These tools generate unified diff patches for code review workflows.
// Both tools write the patch to disk AND return the content for immediate review.
// ============================================================================

using System.Text.Json;
using AzDoErrorMcpServer.Git;
using AzDoErrorMcpServer.Protocol;

namespace AzDoErrorMcpServer.Tools;

/// <summary>
/// Contains the implementation of git patch generation tools.
/// </summary>
internal static class GitPatchTools
{
    /// <summary>
    /// Maximum patch content size (in characters) to include in the response.
    /// Larger patches are truncated with a note.
    /// </summary>
    private const int MaxPatchContentLength = 500_000;

    /// <summary>
    /// Handles the generate_pr_patch tool call.
    /// Fetches from remote and generates a diff between specified branches.
    /// </summary>
    public static async Task<ToolResult> GeneratePrPatchAsync(JsonElement? arguments)
    {
        if (arguments == null)
        {
            return ToolResult.Error("Missing arguments for generate_pr_patch tool.");
        }

        // Extract and validate required parameters
        if (
            !arguments.Value.TryGetProperty("baseBranch", out var baseBranchElement)
            || baseBranchElement.ValueKind != JsonValueKind.String
        )
        {
            return ToolResult.Error("Expected 'baseBranch' string in arguments.");
        }

        if (
            !arguments.Value.TryGetProperty("featureBranch", out var featureBranchElement)
            || featureBranchElement.ValueKind != JsonValueKind.String
        )
        {
            return ToolResult.Error("Expected 'featureBranch' string in arguments.");
        }

        var baseBranch = baseBranchElement.GetString() ?? string.Empty;
        var featureBranch = featureBranchElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseBranch) || string.IsNullOrWhiteSpace(featureBranch))
        {
            return ToolResult.Error("baseBranch and featureBranch must be non-empty strings.");
        }

        // Extract optional parameters with defaults
        var remote = "origin";
        if (
            arguments.Value.TryGetProperty("remote", out var remoteElement)
            && remoteElement.ValueKind == JsonValueKind.String
        )
        {
            remote = remoteElement.GetString() ?? "origin";
        }

        var patchFileName = "pr.patch";
        if (
            arguments.Value.TryGetProperty("patchFileName", out var patchFileElement)
            && patchFileElement.ValueKind == JsonValueKind.String
        )
        {
            var fileName = patchFileElement.GetString();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                patchFileName = fileName;
            }
        }

        // Validate all inputs to prevent shell injection
        if (
            !GitService.IsValidBranchName(baseBranch)
            || !GitService.IsValidBranchName(featureBranch)
            || !GitService.IsValidBranchName(remote)
            || !GitService.IsValidFileName(patchFileName)
        )
        {
            return ToolResult.Error("Branch names or patch file name contain invalid characters.");
        }

        var repoDir = Directory.GetCurrentDirectory();

        // Run git fetch to ensure we have latest refs
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Generate the diff using three-dot notation (merge-base comparison)
        var diffArgs = $"diff {remote}/{baseBranch}...{remote}/{featureBranch}";
        var diffResult = await GitService.RunGitCommandAsync(diffArgs, repoDir);
        if (diffResult.ExitCode != 0)
        {
            return ToolResult.GitError("git diff failed", diffResult.Output);
        }

        // Write patch file to disk
        var patchPath = Path.Combine(repoDir, patchFileName);
        try
        {
            await File.WriteAllTextAsync(patchPath, diffResult.Output);
        }
        catch (Exception ex)
        {
            return ToolResult.GitError("Failed to write patch file", ex.Message);
        }

        // Build response with status and patch content
        var statusText =
            $"Created patch file {patchFileName} comparing {remote}/{baseBranch}...{remote}/{featureBranch}. "
            + "Below is the patch content; please review it.";

        var patchContent = TruncatePatchIfNeeded(diffResult.Output, patchFileName);

        return ToolResult.Success(statusText, patchContent);
    }

    /// <summary>
    /// Handles the generate_pr_patch_auto tool call.
    /// Auto-detects current and base branches, then generates a diff.
    /// </summary>
    public static async Task<ToolResult> GeneratePrPatchAutoAsync(JsonElement? arguments)
    {
        // Extract optional patch file name
        var patchFileName = "pr-auto.patch";
        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("patchFileName", out var patchFileElement)
            && patchFileElement.ValueKind == JsonValueKind.String
        )
        {
            var fileName = patchFileElement.GetString();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                patchFileName = fileName;
            }
        }

        if (!GitService.IsValidFileName(patchFileName))
        {
            return ToolResult.Error("Patch file name contains invalid characters.");
        }

        var repoDir = Directory.GetCurrentDirectory();

        // Step 1: Detect current branch
        var currentBranch = await GitService.GetCurrentBranchAsync(repoDir);
        if (string.IsNullOrWhiteSpace(currentBranch))
        {
            return ToolResult.GitError(
                "Failed to detect current branch",
                "Could not determine current branch. You may be in a detached HEAD state."
            );
        }

        // Step 2: Detect upstream and base branch
        string remote = "origin";
        string baseBranch;

        var upstream = await GitService.GetUpstreamBranchAsync(repoDir);
        if (upstream.HasValue)
        {
            remote = upstream.Value.Remote;
            baseBranch = upstream.Value.Branch;
        }
        else
        {
            // Fallback: try to get the default branch for origin
            baseBranch = await GitService.GetDefaultBranchAsync(repoDir, remote);
        }

        // Validate auto-detected values
        if (
            !GitService.IsValidBranchName(currentBranch)
            || !GitService.IsValidBranchName(baseBranch)
            || !GitService.IsValidBranchName(remote)
        )
        {
            return ToolResult.Error(
                "Auto-detected branch or remote name contains invalid characters."
            );
        }

        // Step 3: Run git fetch
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Step 4: Generate diff (comparing remote base to current local branch)
        var diffArgs = $"diff {remote}/{baseBranch}...{currentBranch}";
        var diffResult = await GitService.RunGitCommandAsync(diffArgs, repoDir);
        if (diffResult.ExitCode != 0)
        {
            return ToolResult.GitError("git diff failed", diffResult.Output);
        }

        // Step 5: Write patch file to disk
        var patchPath = Path.Combine(repoDir, patchFileName);
        try
        {
            await File.WriteAllTextAsync(patchPath, diffResult.Output);
        }
        catch (Exception ex)
        {
            return ToolResult.GitError("Failed to write patch file", ex.Message);
        }

        // Step 6: Build response with status and patch content
        var statusText =
            $"Auto-detected base branch {remote}/{baseBranch} and current branch {currentBranch}. "
            + $"Created patch file {patchFileName} comparing {remote}/{baseBranch}...{currentBranch}. "
            + "Below is the patch content; please review it.";

        var patchContent = TruncatePatchIfNeeded(diffResult.Output, patchFileName);

        return ToolResult.Success(statusText, patchContent);
    }

    /// <summary>
    /// Truncates patch content if it exceeds the maximum length.
    /// The full patch is always saved to disk regardless.
    /// </summary>
    private static string TruncatePatchIfNeeded(string patchContent, string patchFileName)
    {
        if (patchContent.Length <= MaxPatchContentLength)
        {
            return patchContent;
        }

        return patchContent[..MaxPatchContentLength]
            + $"\n\n[... Patch truncated. Full content ({patchContent.Length:N0} characters) saved to {patchFileName}]";
    }
}

/// <summary>
/// Represents the result of a tool execution.
/// Contains content items and an error flag.
/// </summary>
internal sealed class ToolResult
{
    public required List<ContentItem> Content { get; init; }
    public required bool IsError { get; init; }

    /// <summary>
    /// Creates a successful result with one or two text content items.
    /// </summary>
    public static ToolResult Success(string statusMessage, string? additionalContent = null)
    {
        var content = new List<ContentItem>
        {
            new() { Type = "text", Text = statusMessage },
        };

        if (!string.IsNullOrEmpty(additionalContent))
        {
            content.Add(new ContentItem { Type = "text", Text = additionalContent });
        }

        return new ToolResult { Content = content, IsError = false };
    }

    /// <summary>
    /// Creates an error result for invalid parameters.
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
