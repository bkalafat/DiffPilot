// ============================================================================
// GitService.cs
// ============================================================================
// Centralized git operations for the MCP server.
// All git commands are executed via this service to ensure consistent handling.
// Validation helpers prevent shell injection by restricting allowed characters.
// ============================================================================

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AzDoErrorMcpServer.Git;

/// <summary>
/// Provides git operations and validation utilities.
/// All git commands are run asynchronously with proper output capture.
/// </summary>
internal static partial class GitService
{
    /// <summary>
    /// Runs a git command asynchronously and returns the exit code and combined output.
    /// Both stdout and stderr are captured to provide complete feedback.
    /// </summary>
    /// <param name="arguments">The git command arguments (e.g., "fetch origin").</param>
    /// <param name="workingDirectory">The repository directory to run the command in.</param>
    /// <returns>A tuple of (ExitCode, Output) where Output contains both stdout and stderr.</returns>
    public static async Task<(int ExitCode, string Output)> RunGitCommandAsync(
        string arguments,
        string workingDirectory
    )
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, output.ToString());
    }

    /// <summary>
    /// Gets the current branch name using 'git rev-parse --abbrev-ref HEAD'.
    /// Returns null if in detached HEAD state or on error.
    /// </summary>
    public static async Task<string?> GetCurrentBranchAsync(string workingDirectory)
    {
        var result = await RunGitCommandAsync("rev-parse --abbrev-ref HEAD", workingDirectory);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            return null;

        var branch = result.Output.Trim();
        // In detached HEAD state, git returns "HEAD"
        return branch == "HEAD" ? null : branch;
    }

    /// <summary>
    /// Finds the base branch that the current branch was likely created from.
    /// This is done by finding which common base branch (main, master, develop) 
    /// has the most recent common ancestor with the current branch.
    /// </summary>
    /// <param name="workingDirectory">The repository directory.</param>
    /// <param name="currentBranch">The current branch name.</param>
    /// <param name="remote">The remote name (default: origin).</param>
    /// <returns>A tuple of (remote, baseBranch) or null if detection fails.</returns>
    public static async Task<(string Remote, string BaseBranch)?> FindBaseBranchAsync(
        string workingDirectory,
        string currentBranch,
        string remote = "origin"
    )
    {
        // Common base branch names to check, in order of preference
        var candidateBranches = new[] { "main", "master", "develop", "development", "dev" };

        // First, try to get the default branch from the remote
        var defaultBranch = await GetDefaultBranchAsync(workingDirectory, remote);
        
        // Put default branch first if it's not already in the list
        var branchesToCheck = new List<string>();
        if (!string.IsNullOrEmpty(defaultBranch) && !candidateBranches.Contains(defaultBranch))
        {
            branchesToCheck.Add(defaultBranch);
        }
        branchesToCheck.AddRange(candidateBranches);

        string? bestBaseBranch = null;
        int bestAheadCount = int.MaxValue;

        foreach (var candidate in branchesToCheck)
        {
            // Skip if candidate is the same as current branch
            if (candidate == currentBranch)
                continue;

            var remoteBranch = $"{remote}/{candidate}";

            // Check if this remote branch exists
            var checkResult = await RunGitCommandAsync(
                $"rev-parse --verify {remoteBranch}",
                workingDirectory
            );
            if (checkResult.ExitCode != 0)
                continue; // Branch doesn't exist, skip

            // Find merge-base between current branch and candidate
            var mergeBaseResult = await RunGitCommandAsync(
                $"merge-base {remoteBranch} HEAD",
                workingDirectory
            );
            if (mergeBaseResult.ExitCode != 0 || string.IsNullOrWhiteSpace(mergeBaseResult.Output))
                continue;

            // Count how many commits ahead the candidate is from merge-base
            // This helps identify which branch is the most likely parent
            var countResult = await RunGitCommandAsync(
                $"rev-list --count {mergeBaseResult.Output.Trim()}..{remoteBranch}",
                workingDirectory
            );
            if (countResult.ExitCode != 0)
                continue;

            if (int.TryParse(countResult.Output.Trim(), out int aheadCount))
            {
                // Prefer the branch with the smallest ahead count (closest common ancestor)
                if (aheadCount < bestAheadCount)
                {
                    bestAheadCount = aheadCount;
                    bestBaseBranch = candidate;
                }
            }
        }

        // If we found a good candidate, return it
        if (!string.IsNullOrEmpty(bestBaseBranch))
        {
            return (remote, bestBaseBranch);
        }

        // Fallback: use the default branch
        if (!string.IsNullOrEmpty(defaultBranch) && defaultBranch != currentBranch)
        {
            return (remote, defaultBranch);
        }

        // Last resort: use main
        return (remote, "main");
    }

    /// <summary>
    /// Gets the upstream branch reference (e.g., "origin/main") for the current branch.
    /// Returns null if no upstream is configured.
    /// Note: This returns the tracking branch, which may be the same branch on remote,
    /// not necessarily the base branch that this branch was created from.
    /// </summary>
    public static async Task<(string Remote, string Branch)?> GetUpstreamBranchAsync(
        string workingDirectory
    )
    {
        var result = await RunGitCommandAsync(
            "rev-parse --abbrev-ref --symbolic-full-name @{upstream}",
            workingDirectory
        );

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            return null;

        var upstreamRef = result.Output.Trim();
        // Expected format: "origin/main" or "upstream/develop"
        var slashIndex = upstreamRef.IndexOf('/');
        if (slashIndex <= 0)
            return null;

        var remote = upstreamRef[..slashIndex];
        var branch = upstreamRef[(slashIndex + 1)..];
        return (remote, branch);
    }

    /// <summary>
    /// Gets the default branch for the specified remote by querying symbolic-ref.
    /// Falls back to "main" if detection fails.
    /// </summary>
    public static async Task<string> GetDefaultBranchAsync(
        string workingDirectory,
        string remote = "origin"
    )
    {
        // Try to get the default branch from refs/remotes/<remote>/HEAD
        var result = await RunGitCommandAsync(
            $"symbolic-ref refs/remotes/{remote}/HEAD",
            workingDirectory
        );

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            // Expected format: "refs/remotes/origin/main"
            var parts = result.Output.Trim().Split('/');
            if (parts.Length > 0)
            {
                return parts[^1]; // Last element is the branch name
            }
        }

        // Fallback to "main" as the most common default
        return "main";
    }

    /// <summary>
    /// Validates a branch name to prevent shell injection.
    /// Only allows alphanumeric characters, slashes, underscores, and hyphens.
    /// </summary>
    public static bool IsValidBranchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return BranchNameRegex().IsMatch(name);
    }

    /// <summary>
    /// Validates a file name to prevent path traversal and shell injection.
    /// Only allows alphanumeric characters, slashes, underscores, hyphens, and dots.
    /// Disallows ".." to prevent directory traversal.
    /// </summary>
    public static bool IsValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (name.Contains(".."))
            return false;
        return FileNameRegex().IsMatch(name);
    }

    // Compiled regex patterns for performance
    [GeneratedRegex(@"^[a-zA-Z0-9/_-]+$")]
    private static partial Regex BranchNameRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9/_.-]+$")]
    private static partial Regex FileNameRegex();
}
