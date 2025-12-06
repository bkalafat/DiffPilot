// ============================================================================
// GitService.cs
// ============================================================================
// Centralized git operations for the MCP server.
// All git commands are executed via this service to ensure consistent handling.
// Validation helpers prevent shell injection by restricting allowed characters.
// 
// Base branch detection strategy (in order of reliability):
// 1. Reflog - "Created from X" entry (most reliable)
// 2. Git config - upstream tracking branch
// 3. Merge-base analysis - unique common ancestor with local/remote branches
// 4. If uncertain, return null and ask the user
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
    /// Default timeout for git commands in seconds.
    /// </summary>
    private const int DefaultTimeoutSeconds = 60;

    /// <summary>
    /// Runs a git command asynchronously and returns the exit code and combined output.
    /// Both stdout and stderr are captured to provide complete feedback.
    /// Includes a 60-second timeout to prevent hanging on slow operations.
    /// </summary>
    /// <param name="arguments">The git command arguments (e.g., "fetch origin").</param>
    /// <param name="workingDirectory">The repository directory to run the command in.</param>
    /// <param name="timeoutSeconds">Optional timeout in seconds (default: 60).</param>
    /// <returns>A tuple of (ExitCode, Output) where Output contains both stdout and stderr.</returns>
    public static async Task<(int ExitCode, string Output)> RunGitCommandAsync(
        string arguments,
        string workingDirectory,
        int timeoutSeconds = DefaultTimeoutSeconds
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
        var outputLock = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outputLock)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (outputLock)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore kill errors */ }
            return (-1, $"Git command timed out after {timeoutSeconds} seconds: git {arguments}");
        }

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
    /// Finds the base branch that the current branch was created from.
    /// Uses multiple detection strategies in order of reliability:
    /// 1. Reflog - "Created from X" entry
    /// 2. Git config - upstream tracking branch  
    /// 3. Merge-base analysis - unique common ancestor
    /// 
    /// Does NOT guess or use hardcoded branch names. Returns null if uncertain.
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
        if (string.IsNullOrWhiteSpace(workingDirectory) || string.IsNullOrWhiteSpace(currentBranch))
            return null;

        // 1️⃣ REFLOG - Branch creation record (most reliable)
        var reflogBase = await FindFromReflogAsync(workingDirectory, currentBranch);
        if (reflogBase != null)
        {
            var branchRemote = await FindRemoteForBranchAsync(workingDirectory, reflogBase) ?? remote;
            return (branchRemote, reflogBase);
        }

        // 2️⃣ GIT CONFIG - Upstream tracking configuration
        var trackingBase = await FindFromTrackingConfigAsync(workingDirectory, currentBranch);
        if (trackingBase != null && trackingBase != currentBranch)
        {
            var branchRemote = await GetBranchRemoteAsync(workingDirectory, currentBranch) ?? remote;
            return (branchRemote, trackingBase);
        }

        // 3️⃣ MERGE-BASE - Unique common ancestor with local/remote branches
        var mergeBaseResult = await FindFromUniqueMergeBaseAsync(workingDirectory, currentBranch, remote);
        if (mergeBaseResult != null)
        {
            return mergeBaseResult;
        }

        // No definitive evidence found - return null (caller should ask user)
        return null;
    }

    /// <summary>
    /// Searches reflog for "Created from X" record.
    /// This is the most reliable method as it shows the actual branch creation source.
    /// </summary>
    private static async Task<string?> FindFromReflogAsync(string workingDirectory, string currentBranch)
    {
        var result = await RunGitCommandAsync($"reflog show {currentBranch} --format=%gs", workingDirectory);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            return null;

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Start from oldest entry (branch creation moment)
        foreach (var line in lines.Reverse())
        {
            // Pattern: "branch: Created from main" or "branch: Created from origin/develop"
            var match = CreatedFromRegex().Match(line);
            if (match.Success)
            {
                var source = match.Groups[1].Value;

                // Skip if HEAD or commit hash (not definitive)
                if (source.Equals("HEAD", StringComparison.OrdinalIgnoreCase) || IsCommitHash(source))
                    continue;

                // "origin/main" -> "main"
                return source.Contains('/') ? source.Split('/').Last() : source;
            }

            // Pattern: "checkout: moving from develop to feature/x" (first checkout)
            match = CheckoutMovingRegex().Match(line);
            if (match.Success)
            {
                var fromBranch = match.Groups[1].Value;
                var toBranch = match.Groups[2].Value;

                // Find where we came from when first switching to this branch
                if (toBranch == currentBranch && !IsCommitHash(fromBranch))
                {
                    return fromBranch.Contains('/') ? fromBranch.Split('/').Last() : fromBranch;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads upstream tracking branch from git config.
    /// </summary>
    private static async Task<string?> FindFromTrackingConfigAsync(string workingDirectory, string branch)
    {
        var result = await RunGitCommandAsync($"config --get branch.{branch}.merge", workingDirectory);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            return null;

        var trackingRef = result.Output.Trim();

        // "refs/heads/main" -> "main"
        if (trackingRef.StartsWith("refs/heads/"))
            return trackingRef["refs/heads/".Length..];

        return trackingRef;
    }

    /// <summary>
    /// Merge-base analysis: Returns result only if there's a UNIQUE branch with common ancestor.
    /// If multiple candidates exist with ambiguity, returns null (no guessing).
    /// </summary>
    private static async Task<(string Remote, string BaseBranch)?> FindFromUniqueMergeBaseAsync(
        string workingDirectory,
        string currentBranch,
        string remote)
    {
        // Get all local and remote branches
        var localBranches = await GetLocalBranchesAsync(workingDirectory, currentBranch);
        var remoteBranches = await GetRemoteBranchesAsync(workingDirectory, remote);

        // Get current branch HEAD
        var currentHeadResult = await RunGitCommandAsync($"rev-parse {currentBranch}", workingDirectory);
        if (currentHeadResult.ExitCode != 0)
            return null;

        var currentHead = currentHeadResult.Output.Trim();

        string? uniqueBase = null;
        string? uniqueRemote = null;
        int candidateCount = 0;

        // Check local branches
        foreach (var branch in localBranches)
        {
            var mergeBase = await GetMergeBaseAsync(workingDirectory, currentBranch, branch);
            if (mergeBase == null)
                continue;

            // Merge-base should not be same as current HEAD (means no commits yet, ambiguous)
            if (mergeBase == currentHead)
                continue;

            // Is current branch ahead of this branch? (derived from it)
            var isAhead = await IsBranchAheadAsync(workingDirectory, currentBranch, branch);
            if (!isAhead)
                continue;

            candidateCount++;

            if (candidateCount == 1)
            {
                uniqueBase = branch;
                uniqueRemote = await FindRemoteForBranchAsync(workingDirectory, branch) ?? remote;
            }
            else
            {
                // Multiple candidates - check if one is parent of the other
                var isNewCandidateChildOfPrevious = await IsBranchAheadAsync(workingDirectory, branch, uniqueBase!);
                if (isNewCandidateChildOfPrevious)
                {
                    // New candidate is child of previous -> use new (more specific)
                    uniqueBase = branch;
                    uniqueRemote = await FindRemoteForBranchAsync(workingDirectory, branch) ?? remote;
                    candidateCount = 1;
                }
                else
                {
                    var isPreviousChildOfNewCandidate = await IsBranchAheadAsync(workingDirectory, uniqueBase!, branch);
                    if (isPreviousChildOfNewCandidate)
                    {
                        // Previous is more specific, keep it
                        candidateCount = 1;
                    }
                    // Else: Both are independent, ambiguity remains
                }
            }
        }

        // Check remote branches (if not already checked as local)
        foreach (var branch in remoteBranches)
        {
            if (localBranches.Contains(branch))
                continue;

            var remoteRef = $"{remote}/{branch}";
            var mergeBase = await GetMergeBaseAsync(workingDirectory, currentBranch, remoteRef);
            if (mergeBase == null || mergeBase == currentHead)
                continue;

            var isAhead = await IsBranchAheadAsync(workingDirectory, currentBranch, remoteRef);
            if (!isAhead)
                continue;

            candidateCount++;

            if (candidateCount == 1)
            {
                uniqueBase = branch;
                uniqueRemote = remote;
            }
            else
            {
                // Multiple candidates with remote - apply same parent/child logic
                var remoteUniqueRef = $"{uniqueRemote}/{uniqueBase}";
                var isNewCandidateChildOfPrevious = await IsBranchAheadAsync(workingDirectory, remoteRef, remoteUniqueRef);
                if (isNewCandidateChildOfPrevious)
                {
                    uniqueBase = branch;
                    uniqueRemote = remote;
                    candidateCount = 1;
                }
                else
                {
                    var isPreviousChildOfNewCandidate = await IsBranchAheadAsync(workingDirectory, remoteUniqueRef, remoteRef);
                    if (isPreviousChildOfNewCandidate)
                    {
                        candidateCount = 1;
                    }
                }
            }
        }

        // Only return if there's exactly one definitive candidate
        if (candidateCount == 1 && uniqueBase != null)
        {
            return (uniqueRemote ?? remote, uniqueBase);
        }

        return null;
    }

    /// <summary>
    /// Gets the merge-base (common ancestor) between two branches.
    /// </summary>
    private static async Task<string?> GetMergeBaseAsync(string workingDirectory, string branch1, string branch2)
    {
        var result = await RunGitCommandAsync($"merge-base {branch1} {branch2}", workingDirectory);
        return result.ExitCode == 0 ? result.Output.Trim() : null;
    }

    /// <summary>
    /// Checks if branch is ahead of baseBranch (has commits that baseBranch doesn't have).
    /// </summary>
    private static async Task<bool> IsBranchAheadAsync(string workingDirectory, string branch, string baseBranch)
    {
        var result = await RunGitCommandAsync($"rev-list --count {baseBranch}..{branch}", workingDirectory);
        return result.ExitCode == 0 &&
               int.TryParse(result.Output.Trim(), out int ahead) &&
               ahead > 0;
    }

    /// <summary>
    /// Gets all local branches except the specified one.
    /// </summary>
    private static async Task<string[]> GetLocalBranchesAsync(string workingDirectory, string excludeBranch)
    {
        var result = await RunGitCommandAsync("branch --list --format=%(refname:short)", workingDirectory);
        if (result.ExitCode != 0)
            return [];

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => b != excludeBranch && !string.IsNullOrWhiteSpace(b))
            .ToArray();
    }

    /// <summary>
    /// Gets all remote branches for the specified remote.
    /// </summary>
    private static async Task<string[]> GetRemoteBranchesAsync(string workingDirectory, string remote)
    {
        var result = await RunGitCommandAsync($"branch -r --list \"{remote}/*\" --format=%(refname:short)", workingDirectory);
        if (result.ExitCode != 0)
            return [];

        var prefix = $"{remote}/";
        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !b.Contains("->")) // Skip HEAD pointer
            .Select(b => b.StartsWith(prefix) ? b[prefix.Length..] : b)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToArray();
    }

    /// <summary>
    /// Gets the configured remote for a branch from git config.
    /// </summary>
    private static async Task<string?> GetBranchRemoteAsync(string workingDirectory, string branch)
    {
        var result = await RunGitCommandAsync($"config --get branch.{branch}.remote", workingDirectory);
        return result.ExitCode == 0 ? result.Output.Trim() : null;
    }

    /// <summary>
    /// Finds which remote contains the specified branch.
    /// </summary>
    private static async Task<string?> FindRemoteForBranchAsync(string workingDirectory, string branch)
    {
        // First check config
        var configRemote = await GetBranchRemoteAsync(workingDirectory, branch);
        if (!string.IsNullOrWhiteSpace(configRemote))
            return configRemote;

        // Check which remotes have this branch
        var remotesResult = await RunGitCommandAsync("remote", workingDirectory);
        if (remotesResult.ExitCode != 0)
            return null;

        foreach (var remoteLine in remotesResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var remoteName = remoteLine.Trim();
            var checkResult = await RunGitCommandAsync(
                $"show-ref --verify --quiet refs/remotes/{remoteName}/{branch}",
                workingDirectory);

            if (checkResult.ExitCode == 0)
                return remoteName;
        }

        return null;
    }

    /// <summary>
    /// Checks if a string looks like a git commit hash.
    /// </summary>
    private static bool IsCommitHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 7 || value.Length > 40)
            return false;
        return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
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

    [GeneratedRegex(@"branch:\s*Created from\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CreatedFromRegex();

    [GeneratedRegex(@"checkout:\s*moving from\s+(\S+)\s+to\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex CheckoutMovingRegex();
}
