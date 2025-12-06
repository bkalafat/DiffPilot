// ============================================================================
// PrReviewTools.cs
// ============================================================================
// Implementation of PR review tools for the MCP server.
// These tools help developers with code review workflows:
// - get_pr_diff: Raw diff for any purpose
// - review_pr_changes: Diff with AI review instructions
// - generate_pr_title: Conventional PR title generation
// - generate_pr_description: Complete PR description with summary and checklist
//
// NO FILES ARE CREATED - all output is returned directly to avoid repo pollution.
// ============================================================================

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzDoErrorMcpServer.Git;

namespace AzDoErrorMcpServer.Tools;

/// <summary>
/// Contains the implementation of PR review tools.
/// All tools return content directly without writing files.
/// </summary>
internal static partial class PrReviewTools
{
    /// <summary>
    /// Maximum diff content size (in characters) to include in the response.
    /// Larger diffs are truncated with a note about the truncation.
    /// </summary>
    private const int MaxDiffContentLength = 500_000;

    #region Tool: get_pr_diff

    /// <summary>
    /// Gets the raw diff between base branch and current/feature branch.
    /// Auto-detects branches if not specified.
    /// </summary>
    public static async Task<ToolResult> GetPrDiffAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract optional parameters
        var (baseBranch, featureBranch, remote, error) = await ExtractBranchParametersAsync(
            arguments,
            repoDir
        );
        if (error != null)
            return error;

        // Fetch latest
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Generate diff
        var diffResult = await GetDiffAsync(repoDir, remote, baseBranch!, featureBranch!);
        if (diffResult.Error != null)
            return diffResult.Error;

        // Return diff content
        var header =
            $"## Diff: {remote}/{baseBranch} → {featureBranch}\n\n"
            + $"Comparing `{remote}/{baseBranch}...{featureBranch}`\n\n";

        var diffContent = TruncateDiffIfNeeded(diffResult.Diff!);

        return ToolResult.Success(header + "```diff\n" + diffContent + "\n```");
    }

    #endregion

    #region Tool: review_pr_changes

    /// <summary>
    /// Gets the PR diff with instructions for AI code review.
    /// Provides structured context to help AI perform a thorough review.
    /// </summary>
    public static async Task<ToolResult> ReviewPrChangesAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract parameters
        var (baseBranch, featureBranch, remote, error) = await ExtractBranchParametersAsync(
            arguments,
            repoDir
        );
        if (error != null)
            return error;

        // Extract optional focus areas
        string? focusAreas = null;
        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("focusAreas", out var focusElement)
            && focusElement.ValueKind == JsonValueKind.String
        )
        {
            focusAreas = focusElement.GetString();
        }

        // Fetch latest
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Generate diff
        var diffResult = await GetDiffAsync(repoDir, remote, baseBranch!, featureBranch!);
        if (diffResult.Error != null)
            return diffResult.Error;

        // Get file stats for context
        var statsResult = await GitService.RunGitCommandAsync(
            $"diff --stat {remote}/{baseBranch}...{featureBranch}",
            repoDir
        );

        // Build review prompt
        var sb = new StringBuilder();
        sb.AppendLine("# Code Review Request");
        sb.AppendLine();
        sb.AppendLine($"**Branch:** `{featureBranch}` → `{baseBranch}`");
        sb.AppendLine();

        if (statsResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(statsResult.Output))
        {
            sb.AppendLine("## Change Summary");
            sb.AppendLine("```");
            sb.AppendLine(statsResult.Output.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## Review Instructions");
        sb.AppendLine();
        sb.AppendLine("Please review the following changes for:");
        sb.AppendLine("1. **Correctness** - Logic errors, edge cases, potential bugs");
        sb.AppendLine("2. **Security** - SQL injection, XSS, sensitive data exposure, auth issues");
        sb.AppendLine("3. **Performance** - N+1 queries, unnecessary allocations, inefficient algorithms");
        sb.AppendLine("4. **Code Quality** - Naming, readability, SOLID principles, duplication");
        sb.AppendLine("5. **Error Handling** - Missing try/catch, unhandled edge cases");
        sb.AppendLine("6. **Testing** - Missing test coverage, testability concerns");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(focusAreas))
        {
            sb.AppendLine($"**Focus Areas:** {focusAreas}");
            sb.AppendLine();
        }

        sb.AppendLine("## Diff");
        sb.AppendLine();
        sb.AppendLine("```diff");
        sb.AppendLine(TruncateDiffIfNeeded(diffResult.Diff!));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Please provide specific, actionable feedback with file paths and line references where applicable.");

        return ToolResult.Success(sb.ToString());
    }

    #endregion

    #region Tool: generate_pr_title

    /// <summary>
    /// Generates a conventional PR title based on the changes.
    /// Analyzes the diff to determine the type and scope of changes.
    /// </summary>
    public static async Task<ToolResult> GeneratePrTitleAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract parameters
        var (baseBranch, featureBranch, remote, error) = await ExtractBranchParametersAsync(
            arguments,
            repoDir
        );
        if (error != null)
            return error;

        // Extract style preference
        var style = "conventional";
        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("style", out var styleElement)
            && styleElement.ValueKind == JsonValueKind.String
        )
        {
            style = styleElement.GetString() ?? "conventional";
        }

        // Fetch latest
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Get diff stats and commit messages for context
        var statsResult = await GitService.RunGitCommandAsync(
            $"diff --stat {remote}/{baseBranch}...{featureBranch}",
            repoDir
        );

        var logResult = await GitService.RunGitCommandAsync(
            $"log --oneline {remote}/{baseBranch}..{featureBranch}",
            repoDir
        );

        // Get the diff for analysis
        var diffResult = await GetDiffAsync(repoDir, remote, baseBranch!, featureBranch!);
        if (diffResult.Error != null)
            return diffResult.Error;

        // Extract ticket number from branch name if present
        var ticketMatch = TicketPattern().Match(featureBranch!);
        var ticketNumber = ticketMatch.Success ? ticketMatch.Value.ToUpperInvariant() : null;

        // Build the response
        var sb = new StringBuilder();
        sb.AppendLine("# PR Title Generator");
        sb.AppendLine();
        sb.AppendLine($"**Branch:** `{featureBranch}`");
        sb.AppendLine($"**Style:** {style}");
        if (ticketNumber != null)
        {
            sb.AppendLine($"**Ticket:** {ticketNumber}");
        }
        sb.AppendLine();

        sb.AppendLine("## Commits in this PR");
        sb.AppendLine("```");
        sb.AppendLine(logResult.Output.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Files Changed");
        sb.AppendLine("```");
        sb.AppendLine(statsResult.Output.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("Based on the commits and changes above, generate a PR title following these guidelines:");
        sb.AppendLine();

        switch (style.ToLowerInvariant())
        {
            case "conventional":
                sb.AppendLine("**Format:** `type(scope): description`");
                sb.AppendLine();
                sb.AppendLine("Types: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `perf`, `style`");
                sb.AppendLine();
                sb.AppendLine("Examples:");
                sb.AppendLine("- `feat(auth): add OAuth2 login support`");
                sb.AppendLine("- `fix(api): handle null response from external service`");
                sb.AppendLine("- `refactor(git): reorganize service into modular structure`");
                break;

            case "ticket":
                sb.AppendLine($"**Format:** `[{ticketNumber ?? "TICKET-XXX"}] Description`");
                sb.AppendLine();
                sb.AppendLine("Examples:");
                sb.AppendLine("- `[PROJ-123] Add user authentication flow`");
                sb.AppendLine("- `[BUG-456] Fix null reference in order processing`");
                break;

            default: // descriptive
                sb.AppendLine("**Format:** Clear, concise description starting with a verb");
                sb.AppendLine();
                sb.AppendLine("Examples:");
                sb.AppendLine("- `Add OAuth2 login support for enterprise users`");
                sb.AppendLine("- `Fix null reference exception in order processing`");
                sb.AppendLine("- `Reorganize git service into modular architecture`");
                break;
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Please provide 2-3 title suggestions based on the actual changes.");

        return ToolResult.Success(sb.ToString());
    }

    #endregion

    #region Tool: generate_pr_description

    /// <summary>
    /// Generates a complete PR description including summary, changes, and checklist.
    /// Ready to paste directly into the PR description field.
    /// </summary>
    public static async Task<ToolResult> GeneratePrDescriptionAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract parameters
        var (baseBranch, featureBranch, remote, error) = await ExtractBranchParametersAsync(
            arguments,
            repoDir
        );
        if (error != null)
            return error;

        // Extract options
        var includeChecklist = true;
        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("includeChecklist", out var checklistElement)
            && checklistElement.ValueKind == JsonValueKind.False
        )
        {
            includeChecklist = false;
        }

        string? ticketUrl = null;
        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("ticketUrl", out var ticketElement)
            && ticketElement.ValueKind == JsonValueKind.String
        )
        {
            ticketUrl = ticketElement.GetString();
        }

        // Fetch latest
        var fetchResult = await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            return ToolResult.GitError("git fetch failed", fetchResult.Output);
        }

        // Get various context
        var statsResult = await GitService.RunGitCommandAsync(
            $"diff --stat {remote}/{baseBranch}...{featureBranch}",
            repoDir
        );

        var logResult = await GitService.RunGitCommandAsync(
            $"log --oneline {remote}/{baseBranch}..{featureBranch}",
            repoDir
        );

        var diffResult = await GetDiffAsync(repoDir, remote, baseBranch!, featureBranch!);
        if (diffResult.Error != null)
            return diffResult.Error;

        // Extract ticket from branch if no URL provided
        var ticketMatch = TicketPattern().Match(featureBranch!);
        var ticketNumber = ticketMatch.Success ? ticketMatch.Value.ToUpperInvariant() : null;

        // Build the response
        var sb = new StringBuilder();
        sb.AppendLine("# PR Description Generator");
        sb.AppendLine();
        sb.AppendLine($"**Branch:** `{featureBranch}` → `{baseBranch}`");
        if (ticketNumber != null)
        {
            sb.AppendLine($"**Ticket:** {ticketNumber}");
        }
        sb.AppendLine();

        sb.AppendLine("## Commits");
        sb.AppendLine("```");
        sb.AppendLine(logResult.Output.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Files Changed");
        sb.AppendLine("```");
        sb.AppendLine(statsResult.Output.Trim());
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Diff");
        sb.AppendLine("```diff");
        sb.AppendLine(TruncateDiffIfNeeded(diffResult.Diff!));
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("Based on the commits, files changed, and diff above, generate a PR description using this template:");
        sb.AppendLine();
        sb.AppendLine("```markdown");
        sb.AppendLine("## Summary");
        sb.AppendLine("[Brief description of what this PR does and why]");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ticketUrl))
        {
            sb.AppendLine("## Related Issue");
            sb.AppendLine($"[{ticketNumber ?? "Ticket"}]({ticketUrl})");
            sb.AppendLine();
        }
        else if (ticketNumber != null)
        {
            sb.AppendLine("## Related Issue");
            sb.AppendLine($"{ticketNumber}");
            sb.AppendLine();
        }

        sb.AppendLine("## Changes");
        sb.AppendLine("- [List key changes, one per line]");
        sb.AppendLine("- [Focus on WHAT changed and WHY]");
        sb.AppendLine("- [Group related changes together]");
        sb.AppendLine();
        sb.AppendLine("## Testing");
        sb.AppendLine("- [How was this tested?]");
        sb.AppendLine("- [Any manual testing steps needed?]");
        sb.AppendLine("- [Were unit tests added/updated?]");

        if (includeChecklist)
        {
            sb.AppendLine();
            sb.AppendLine("## Checklist");
            sb.AppendLine("- [ ] Code follows project style guidelines");
            sb.AppendLine("- [ ] Self-review completed");
            sb.AppendLine("- [ ] Tests added/updated for changes");
            sb.AppendLine("- [ ] Documentation updated if needed");
            sb.AppendLine("- [ ] No breaking changes (or documented)");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Please fill in the template based on the actual changes shown above.");

        return ToolResult.Success(sb.ToString());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Extracts and validates branch parameters from arguments.
    /// Auto-detects branches if not provided.
    /// </summary>
    private static async Task<(
        string? BaseBranch,
        string? FeatureBranch,
        string Remote,
        ToolResult? Error
    )> ExtractBranchParametersAsync(JsonElement? arguments, string repoDir)
    {
        var remote = "origin";
        string? baseBranch = null;
        string? featureBranch = null;

        // Extract provided values
        if (arguments.HasValue)
        {
            if (
                arguments.Value.TryGetProperty("remote", out var remoteElement)
                && remoteElement.ValueKind == JsonValueKind.String
            )
            {
                remote = remoteElement.GetString() ?? "origin";
            }

            if (
                arguments.Value.TryGetProperty("baseBranch", out var baseElement)
                && baseElement.ValueKind == JsonValueKind.String
            )
            {
                baseBranch = baseElement.GetString();
            }

            if (
                arguments.Value.TryGetProperty("featureBranch", out var featureElement)
                && featureElement.ValueKind == JsonValueKind.String
            )
            {
                featureBranch = featureElement.GetString();
            }
        }

        // Auto-detect feature branch if not provided
        if (string.IsNullOrWhiteSpace(featureBranch))
        {
            featureBranch = await GitService.GetCurrentBranchAsync(repoDir);
            if (string.IsNullOrWhiteSpace(featureBranch))
            {
                return (
                    null,
                    null,
                    remote,
                    ToolResult.GitError(
                        "Failed to detect current branch",
                        "Could not determine current branch. You may be in a detached HEAD state."
                    )
                );
            }
        }

        // Auto-detect base branch if not provided
        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            // First fetch to ensure we have latest refs
            await GitService.RunGitCommandAsync($"fetch {remote}", repoDir);

            var baseInfo = await GitService.FindBaseBranchAsync(repoDir, featureBranch, remote);
            if (baseInfo.HasValue)
            {
                remote = baseInfo.Value.Remote;
                baseBranch = baseInfo.Value.BaseBranch;
            }
            else
            {
                return (
                    null,
                    null,
                    remote,
                    ToolResult.Error(
                        $"Could not automatically determine the base branch for '{featureBranch}'. "
                            + "Please specify the 'baseBranch' parameter (e.g., 'main' or 'develop')."
                    )
                );
            }
        }

        // Validate branch names
        if (!GitService.IsValidBranchName(remote))
        {
            return (null, null, remote, ToolResult.Error("Remote name contains invalid characters."));
        }

        if (!GitService.IsValidBranchName(baseBranch))
        {
            return (null, null, remote, ToolResult.Error("Base branch name contains invalid characters."));
        }

        if (!GitService.IsValidBranchName(featureBranch))
        {
            return (null, null, remote, ToolResult.Error("Feature branch name contains invalid characters."));
        }

        return (baseBranch, featureBranch, remote, null);
    }

    /// <summary>
    /// Gets the diff between base and feature branches.
    /// </summary>
    private static async Task<(string? Diff, ToolResult? Error)> GetDiffAsync(
        string repoDir,
        string remote,
        string baseBranch,
        string featureBranch
    )
    {
        var diffArgs = $"diff {remote}/{baseBranch}...{featureBranch}";
        var diffResult = await GitService.RunGitCommandAsync(diffArgs, repoDir);

        if (diffResult.ExitCode != 0)
        {
            return (null, ToolResult.GitError("git diff failed", diffResult.Output));
        }

        if (string.IsNullOrWhiteSpace(diffResult.Output))
        {
            return ("No changes found between branches.", null);
        }

        return (diffResult.Output, null);
    }

    /// <summary>
    /// Truncates diff content if it exceeds the maximum length.
    /// </summary>
    private static string TruncateDiffIfNeeded(string diffContent)
    {
        if (diffContent.Length <= MaxDiffContentLength)
        {
            return diffContent;
        }

        return diffContent[..MaxDiffContentLength]
            + $"\n\n[... Diff truncated at {MaxDiffContentLength:N0} characters. Total size: {diffContent.Length:N0} characters]";
    }

    /// <summary>
    /// Regex pattern to extract ticket numbers from branch names.
    /// Matches patterns like: PROJ-123, ABC-1, feature/TICKET-456, etc.
    /// </summary>
    [GeneratedRegex(@"[A-Za-z]+-\d+", RegexOptions.IgnoreCase)]
    private static partial Regex TicketPattern();

    #endregion
}
