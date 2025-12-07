// ============================================================================
// DeveloperTools.cs
// ============================================================================
// Additional developer productivity tools for the MCP server:
// - generate_commit_message: Generate commit message from staged/unstaged changes
// - scan_secrets: Detect accidentally committed secrets in diff
// - diff_stats: Get statistics about changes
// - suggest_tests: Recommend test cases for changed code
// - generate_changelog: Generate changelog entries from commits
//
// NO FILES ARE CREATED - all output is returned directly.
// ============================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiffPilot.Git;

namespace DiffPilot.Tools;

/// <summary>
/// Additional developer productivity tools.
/// </summary>
internal static partial class DeveloperTools
{
    #region Tool: generate_commit_message

    /// <summary>
    /// Generates a commit message based on staged changes (or unstaged if nothing is staged).
    /// </summary>
    public static async Task<ToolResult> GenerateCommitMessageAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract optional parameters
        string? style = "conventional";
        string? scope = null;
        bool includeBody = true;

        if (arguments.HasValue)
        {
            if (
                arguments.Value.TryGetProperty("style", out var styleElement)
                && styleElement.ValueKind == JsonValueKind.String
            )
            {
                style = styleElement.GetString() ?? "conventional";
            }

            if (
                arguments.Value.TryGetProperty("scope", out var scopeElement)
                && scopeElement.ValueKind == JsonValueKind.String
            )
            {
                scope = scopeElement.GetString();
            }

            if (
                arguments.Value.TryGetProperty("includeBody", out var bodyElement)
                    && bodyElement.ValueKind == JsonValueKind.True
                || bodyElement.ValueKind == JsonValueKind.False
            )
            {
                includeBody = bodyElement.GetBoolean();
            }
        }

        // First, check for staged changes
        var stagedResult = await GitService.RunGitCommandAsync("diff --cached --stat", repoDir);
        bool hasStaged =
            stagedResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(stagedResult.Output);

        // Get the diff (staged first, fallback to unstaged)
        string diffOutput;
        string changeType;

        if (hasStaged)
        {
            var stagedDiff = await GitService.RunGitCommandAsync("diff --cached", repoDir);
            if (stagedDiff.ExitCode != 0)
            {
                return ToolResult.GitError("Failed to get staged diff", stagedDiff.Output);
            }
            diffOutput = stagedDiff.Output;
            changeType = "staged";
        }
        else
        {
            // Check for unstaged changes
            var unstagedResult = await GitService.RunGitCommandAsync("diff --stat", repoDir);
            if (unstagedResult.ExitCode != 0 || string.IsNullOrWhiteSpace(unstagedResult.Output))
            {
                return ToolResult.Error(
                    "No changes found. Please stage your changes with `git add` or make some modifications first."
                );
            }

            var unstagedDiff = await GitService.RunGitCommandAsync("diff", repoDir);
            if (unstagedDiff.ExitCode != 0)
            {
                return ToolResult.GitError("Failed to get unstaged diff", unstagedDiff.Output);
            }
            diffOutput = unstagedDiff.Output;
            changeType = "unstaged";
        }

        if (string.IsNullOrWhiteSpace(diffOutput))
        {
            return ToolResult.Error("No changes detected in the repository.");
        }

        // Get file stats
        var statsCommand = hasStaged ? "diff --cached --stat" : "diff --stat";
        var statsResult = await GitService.RunGitCommandAsync(statsCommand, repoDir);

        // Analyze the changes
        var analysis = AnalyzeChanges(diffOutput);

        // Build the response
        var sb = new StringBuilder();
        sb.AppendLine("# Commit Message Generator");
        sb.AppendLine();
        sb.Append("**Analyzing:** ").Append(changeType).AppendLine(" changes");
        sb.AppendLine();

        if (statsResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(statsResult.Output))
        {
            sb.AppendLine("## Changed Files");
            sb.AppendLine("```");
            sb.AppendLine(statsResult.Output.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## Change Analysis");
        sb.Append("- **Files modified:** ")
            .AppendLine(analysis.FilesChanged.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Lines added:** ")
            .AppendLine(analysis.LinesAdded.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Lines removed:** ")
            .AppendLine(analysis.LinesRemoved.ToString(CultureInfo.InvariantCulture));
        sb.Append("- **Primary change type:** ").AppendLine(analysis.ChangeType);
        sb.AppendLine();

        sb.AppendLine("## Suggested Commit Message");
        sb.AppendLine();

        // Generate conventional commit suggestion
        var commitType = DetermineCommitType(analysis, diffOutput);
        var scopePart = !string.IsNullOrWhiteSpace(scope) ? $"({scope})" : "";

        sb.AppendLine("```");
        if (string.Equals(style, "conventional", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(commitType).Append(scopePart).AppendLine(": <brief description>");
            if (includeBody)
            {
                sb.AppendLine();
                sb.AppendLine("<optional body explaining what and why>");
            }
        }
        else
        {
            sb.AppendLine("<Brief description of changes>");
            if (includeBody)
            {
                sb.AppendLine();
                sb.AppendLine("<Optional detailed explanation>");
            }
        }
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Diff Preview");
        sb.AppendLine("```diff");
        sb.AppendLine(TruncateContent(diffOutput, 50000));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine(
            "Please analyze the diff and generate an appropriate commit message based on the actual changes."
        );

        return ToolResult.Success(sb.ToString());
    }

    private static string DetermineCommitType(ChangeAnalysis analysis, string diff)
    {
        var lowerDiff = diff.ToLowerInvariant();

        // Check for test files
        if (lowerDiff.Contains("test") || lowerDiff.Contains("spec"))
            return "test";

        // Check for documentation
        if (lowerDiff.Contains(".md") || lowerDiff.Contains("readme") || lowerDiff.Contains("doc"))
            return "docs";

        // Check for configuration
        if (
            lowerDiff.Contains("config")
            || lowerDiff.Contains(".json")
            || lowerDiff.Contains(".yaml")
            || lowerDiff.Contains(".yml")
        )
            return "chore";

        // Check for bug fixes
        if (
            lowerDiff.Contains("fix")
            || lowerDiff.Contains("bug")
            || lowerDiff.Contains("error")
            || lowerDiff.Contains("issue")
        )
            return "fix";

        // Check for refactoring (more deletions or similar add/remove)
        if (analysis.LinesRemoved > analysis.LinesAdded * 0.5 && analysis.LinesAdded > 0)
            return "refactor";

        // Default to feat for new functionality
        return "feat";
    }

    #endregion

    #region Tool: scan_secrets

    /// <summary>
    /// Scans the diff for accidentally committed secrets, API keys, passwords, etc.
    /// </summary>
    public static async Task<ToolResult> ScanSecretsAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Determine what to scan
        bool scanStaged = true;
        bool scanUnstaged = true;

        if (arguments.HasValue)
        {
            if (arguments.Value.TryGetProperty("scanStaged", out var stagedElement))
            {
                scanStaged = stagedElement.ValueKind != JsonValueKind.False;
            }
            if (arguments.Value.TryGetProperty("scanUnstaged", out var unstagedElement))
            {
                scanUnstaged = unstagedElement.ValueKind != JsonValueKind.False;
            }
        }

        var findings = new List<SecretFinding>();
        var scannedDiffs = new StringBuilder();

        // Scan staged changes
        if (scanStaged)
        {
            var stagedDiff = await GitService.RunGitCommandAsync("diff --cached", repoDir);
            if (stagedDiff.ExitCode == 0 && !string.IsNullOrWhiteSpace(stagedDiff.Output))
            {
                scannedDiffs.AppendLine("=== STAGED CHANGES ===");
                scannedDiffs.AppendLine(stagedDiff.Output);
                findings.AddRange(ScanForSecrets(stagedDiff.Output, "staged"));
            }
        }

        // Scan unstaged changes
        if (scanUnstaged)
        {
            var unstagedDiff = await GitService.RunGitCommandAsync("diff", repoDir);
            if (unstagedDiff.ExitCode == 0 && !string.IsNullOrWhiteSpace(unstagedDiff.Output))
            {
                scannedDiffs.AppendLine("=== UNSTAGED CHANGES ===");
                scannedDiffs.AppendLine(unstagedDiff.Output);
                findings.AddRange(ScanForSecrets(unstagedDiff.Output, "unstaged"));
            }
        }

        if (scannedDiffs.Length == 0)
        {
            return ToolResult.Success("✅ No changes to scan. Working directory is clean.");
        }

        // Build response
        var sb = new StringBuilder();
        sb.AppendLine("# 🔐 Secret Scan Results");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("## ✅ No Secrets Detected");
            sb.AppendLine();
            sb.AppendLine(
                "No obvious secrets, API keys, or sensitive data patterns were found in the changes."
            );
            sb.AppendLine();
            sb.AppendLine(
                "> **Note:** This is a pattern-based scan and may not catch all secrets."
            );
            sb.AppendLine(
                "> Always review your changes manually before committing sensitive code."
            );
        }
        else
        {
            sb.Append("## ⚠️ ").Append(findings.Count).AppendLine(" Potential Secret(s) Found");
            sb.AppendLine();
            sb.AppendLine("The following patterns may indicate sensitive data:");
            sb.AppendLine();

            foreach (var finding in findings)
            {
                sb.Append("### 🚨 ").AppendLine(finding.Type);
                sb.Append("- **Location:** ")
                    .Append(finding.Location)
                    .Append(" (")
                    .Append(finding.Source)
                    .AppendLine(")");
                sb.Append("- **Pattern:** `").Append(finding.Pattern).AppendLine("`");
                sb.Append("- **Match:** `").Append(MaskSecret(finding.Match)).AppendLine("`");
                sb.AppendLine();
            }

            sb.AppendLine("## Recommendations");
            sb.AppendLine();
            sb.AppendLine("1. **Remove secrets** from your code before committing");
            sb.AppendLine("2. **Use environment variables** or a secrets manager");
            sb.AppendLine("3. **Add to .gitignore** any files containing secrets");
            sb.AppendLine("4. **Consider using** `.env` files (gitignored) for local development");
        }

        return findings.Count > 0
            ? new ToolResult
            {
                Content = [new ContentItem { Type = "text", Text = sb.ToString() }],
                IsError = true,
            }
            : ToolResult.Success(sb.ToString());
    }

    private static List<SecretFinding> ScanForSecrets(string diff, string source)
    {
        var findings = new List<SecretFinding>();
        var lines = diff.Split('\n');
        var currentFile = "unknown";

        foreach (var line in lines)
        {
            // Track current file
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                currentFile = line[6..];
                continue;
            }

            // Only scan added lines
            if (!line.StartsWith('+') || line.StartsWith("+++", StringComparison.Ordinal))
                continue;

            var content = line[1..]; // Remove the + prefix

            // Check each pattern
            foreach (var pattern in SecretPatterns)
            {
                var matches = pattern.Regex.Matches(content);
                foreach (Match match in matches)
                {
                    findings.Add(
                        new SecretFinding
                        {
                            Type = pattern.Name,
                            Pattern = pattern.Description,
                            Match = match.Value,
                            Location = currentFile,
                            Source = source,
                        }
                    );
                }
            }
        }

        return findings;
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return new string('*', secret.Length);
        return secret[..4] + new string('*', secret.Length - 8) + secret[^4..];
    }

    private static readonly SecretPattern[] SecretPatterns =
    [
        new("API Key", "Generic API key pattern", ApiKeyPattern()),
        new("AWS Access Key", "AWS access key ID", AwsKeyPattern()),
        new("AWS Secret Key", "AWS secret access key", AwsSecretPattern()),
        new("GitHub Token", "GitHub personal access token", GithubTokenPattern()),
        new("Private Key", "Private key block", PrivateKeyPattern()),
        new("Password in URL", "Password in connection string", PasswordUrlPattern()),
        new("Password Assignment", "Password variable assignment", PasswordAssignPattern()),
        new("Bearer Token", "Bearer authentication token", BearerTokenPattern()),
        new(
            "Azure Connection String",
            "Azure storage/service connection string",
            AzureConnectionPattern()
        ),
        new("JWT Token", "JSON Web Token", JwtPattern()),
        new("Slack Token", "Slack bot/webhook token", SlackTokenPattern()),
        new("Generic Secret", "Generic secret/token pattern", GenericSecretPattern()),
    ];

    [GeneratedRegex(
        @"['""]?[a-zA-Z0-9_-]*[aA][pP][iI][_-]?[kK][eE][yY]['""]?\s*[:=]\s*['""]?[a-zA-Z0-9_\-]{20,}['""]?",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}")]
    private static partial Regex AwsKeyPattern();

    [GeneratedRegex(@"['""]?[a-zA-Z0-9/+=]{40}['""]?")]
    private static partial Regex AwsSecretPattern();

    [GeneratedRegex(@"ghp_[a-zA-Z0-9]{36}|github_pat_[a-zA-Z0-9]{22}_[a-zA-Z0-9]{59}")]
    private static partial Regex GithubTokenPattern();

    [GeneratedRegex(@"-----BEGIN\s+(RSA|DSA|EC|OPENSSH)?\s*PRIVATE KEY-----")]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"://[^:]+:([^@]+)@", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordUrlPattern();

    [GeneratedRegex(@"['""]?[pP]assword['""]?\s*[:=]\s*['""][^'""]{8,}['""]")]
    private static partial Regex PasswordAssignPattern();

    [GeneratedRegex(@"[bB]earer\s+[a-zA-Z0-9_\-\.]+")]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"DefaultEndpointsProtocol=https?;AccountName=[^;]+;AccountKey=[^;]+")]
    private static partial Regex AzureConnectionPattern();

    [GeneratedRegex(@"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"xox[baprs]-[0-9]{10,13}-[a-zA-Z0-9-]+")]
    private static partial Regex SlackTokenPattern();

    [GeneratedRegex(
        @"['""]?(?:secret|token|key|auth)['""]?\s*[:=]\s*['""][a-zA-Z0-9_\-]{16,}['""]",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex GenericSecretPattern();

    private record SecretPattern(string Name, string Description, Regex Regex);

    private record SecretFinding
    {
        public required string Type { get; init; }
        public required string Pattern { get; init; }
        public required string Match { get; init; }
        public required string Location { get; init; }
        public required string Source { get; init; }
    }

    #endregion

    #region Tool: diff_stats

    /// <summary>
    /// Gets detailed statistics about changes between branches or in working directory.
    /// </summary>
    public static async Task<ToolResult> GetDiffStatsAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract optional parameters
        string? baseBranch = null;
        string? featureBranch = null;
        bool includeWorkingDir = true;

        if (arguments.HasValue)
        {
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

            if (arguments.Value.TryGetProperty("includeWorkingDir", out var wdElement))
            {
                includeWorkingDir = wdElement.ValueKind != JsonValueKind.False;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("# 📊 Diff Statistics");
        sb.AppendLine();

        // Working directory stats
        if (includeWorkingDir)
        {
            sb.AppendLine("## Working Directory");
            sb.AppendLine();

            // Staged changes
            var stagedStat = await GitService.RunGitCommandAsync(
                "diff --cached --numstat",
                repoDir
            );
            var stagedStats = ParseNumstat(stagedStat.Output);

            // Unstaged changes
            var unstagedStat = await GitService.RunGitCommandAsync("diff --numstat", repoDir);
            var unstagedStats = ParseNumstat(unstagedStat.Output);

            sb.AppendLine("### Staged Changes");
            if (stagedStats.Files > 0)
            {
                sb.AppendLine("| Metric | Value |");
                sb.AppendLine("|--------|-------|");
                sb.Append("| Files | ").Append(stagedStats.Files).AppendLine(" |");
                sb.Append("| Lines Added | +").Append(stagedStats.Added).AppendLine(" |");
                sb.Append("| Lines Removed | -").Append(stagedStats.Removed).AppendLine(" |");
                sb.Append("| Net Change | ")
                    .Append(
                        (stagedStats.Added - stagedStats.Removed).ToString(
                            "+#;-#;0",
                            CultureInfo.InvariantCulture
                        )
                    )
                    .AppendLine(" |");
            }
            else
            {
                sb.AppendLine("*No staged changes*");
            }
            sb.AppendLine();

            sb.AppendLine("### Unstaged Changes");
            if (unstagedStats.Files > 0)
            {
                sb.AppendLine("| Metric | Value |");
                sb.AppendLine("|--------|-------|");
                sb.Append("| Files | ").Append(unstagedStats.Files).AppendLine(" |");
                sb.Append("| Lines Added | +").Append(unstagedStats.Added).AppendLine(" |");
                sb.Append("| Lines Removed | -").Append(unstagedStats.Removed).AppendLine(" |");
                sb.Append("| Net Change | ")
                    .Append(
                        (unstagedStats.Added - unstagedStats.Removed).ToString(
                            "+#;-#;0",
                            CultureInfo.InvariantCulture
                        )
                    )
                    .AppendLine(" |");
            }
            else
            {
                sb.AppendLine("*No unstaged changes*");
            }
            sb.AppendLine();
        }

        // Branch comparison stats
        if (!string.IsNullOrWhiteSpace(baseBranch) || !string.IsNullOrWhiteSpace(featureBranch))
        {
            // Auto-detect if needed
            if (string.IsNullOrWhiteSpace(featureBranch))
            {
                featureBranch = await GitService.GetCurrentBranchAsync(repoDir) ?? "HEAD";
            }

            if (string.IsNullOrWhiteSpace(baseBranch))
            {
                var baseInfo = await GitService.FindBaseBranchAsync(
                    repoDir,
                    featureBranch,
                    "origin"
                );
                baseBranch = baseInfo?.BaseBranch ?? "main";
            }

            sb.Append("## Branch Comparison: `")
                .Append(baseBranch)
                .Append("` → `")
                .Append(featureBranch)
                .AppendLine("`");
            sb.AppendLine();

            // Fetch latest
            await GitService.RunGitCommandAsync("fetch origin", repoDir);

            // Get numstat for branch comparison
            var branchStat = await GitService.RunGitCommandAsync(
                $"diff --numstat origin/{baseBranch}...{featureBranch}",
                repoDir
            );
            var branchStats = ParseNumstat(branchStat.Output);

            // Get shortstat for summary
            var shortstat = await GitService.RunGitCommandAsync(
                $"diff --shortstat origin/{baseBranch}...{featureBranch}",
                repoDir
            );

            // Get commit count
            var commitCount = await GitService.RunGitCommandAsync(
                $"rev-list --count origin/{baseBranch}..{featureBranch}",
                repoDir
            );

            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.Append("| Commits | ").Append(commitCount.Output.Trim()).AppendLine(" |");
            sb.Append("| Files Changed | ").Append(branchStats.Files).AppendLine(" |");
            sb.Append("| Lines Added | +").Append(branchStats.Added).AppendLine(" |");
            sb.Append("| Lines Removed | -").Append(branchStats.Removed).AppendLine(" |");
            sb.Append("| Net Change | ")
                .Append(
                    (branchStats.Added - branchStats.Removed).ToString(
                        "+#;-#;0",
                        CultureInfo.InvariantCulture
                    )
                )
                .AppendLine(" |");
            sb.AppendLine();

            // File breakdown by type
            if (branchStats.FileDetails.Count > 0)
            {
                sb.AppendLine("### Files Changed");
                sb.AppendLine();
                sb.AppendLine("| File | Added | Removed |");
                sb.AppendLine("|------|-------|---------|");
                foreach (var file in branchStats.FileDetails.Take(20))
                {
                    sb.Append("| `")
                        .Append(file.Name)
                        .Append("` | +")
                        .Append(file.Added)
                        .Append(" | -")
                        .Append(file.Removed)
                        .AppendLine(" |");
                }
                if (branchStats.FileDetails.Count > 20)
                {
                    sb.Append("| *...and ")
                        .Append(branchStats.FileDetails.Count - 20)
                        .AppendLine(" more files* | | |");
                }
                sb.AppendLine();

                // Group by extension
                var byExtension = branchStats
                    .FileDetails.GroupBy(f => Path.GetExtension(f.Name).ToLowerInvariant())
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .OrderByDescending(g => g.Sum(f => f.Added + f.Removed))
                    .Take(10);

                sb.AppendLine("### Changes by File Type");
                sb.AppendLine();
                sb.AppendLine("| Extension | Files | Added | Removed |");
                sb.AppendLine("|-----------|-------|-------|---------|");
                foreach (var group in byExtension)
                {
                    sb.Append("| `")
                        .Append(group.Key)
                        .Append("` | ")
                        .Append(group.Count())
                        .Append(" | +")
                        .Append(group.Sum(f => f.Added))
                        .Append(" | -")
                        .Append(group.Sum(f => f.Removed))
                        .AppendLine(" |");
                }
            }
        }

        return ToolResult.Success(sb.ToString());
    }

    private static DiffStats ParseNumstat(string numstatOutput)
    {
        var stats = new DiffStats();
        if (string.IsNullOrWhiteSpace(numstatOutput))
            return stats;

        foreach (var line in numstatOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 3)
            {
                var added =
                    parts[0] == "-" ? 0
                    : int.TryParse(parts[0], out var a) ? a
                    : 0;
                var removed =
                    parts[1] == "-" ? 0
                    : int.TryParse(parts[1], out var r) ? r
                    : 0;
                var fileName = parts[2];

                stats.Added += added;
                stats.Removed += removed;
                stats.Files++;
                stats.FileDetails.Add(new FileStats(fileName, added, removed));
            }
        }

        return stats;
    }

    private record DiffStats
    {
        public int Added { get; set; }
        public int Removed { get; set; }
        public int Files { get; set; }
        public List<FileStats> FileDetails { get; } = [];
    }

    private record FileStats(string Name, int Added, int Removed);

    #endregion

    #region Tool: suggest_tests

    /// <summary>
    /// Analyzes changed code and suggests appropriate test cases.
    /// </summary>
    public static async Task<ToolResult> SuggestTestsAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Get diff - prefer branch comparison, fallback to working directory
        string diffOutput;
        string context;

        if (
            arguments.HasValue
            && arguments.Value.TryGetProperty("baseBranch", out var baseElement)
            && baseElement.ValueKind == JsonValueKind.String
        )
        {
            var baseBranch = baseElement.GetString()!;
            var featureBranch = await GitService.GetCurrentBranchAsync(repoDir) ?? "HEAD";

            await GitService.RunGitCommandAsync("fetch origin", repoDir);
            var branchDiff = await GitService.RunGitCommandAsync(
                $"diff origin/{baseBranch}...{featureBranch}",
                repoDir
            );

            if (branchDiff.ExitCode != 0 || string.IsNullOrWhiteSpace(branchDiff.Output))
            {
                return ToolResult.Error(
                    $"Failed to get diff between {baseBranch} and {featureBranch}"
                );
            }
            diffOutput = branchDiff.Output;
            context = $"branch comparison ({baseBranch} → {featureBranch})";
        }
        else
        {
            // Try staged, then unstaged
            var staged = await GitService.RunGitCommandAsync("diff --cached", repoDir);
            if (staged.ExitCode == 0 && !string.IsNullOrWhiteSpace(staged.Output))
            {
                diffOutput = staged.Output;
                context = "staged changes";
            }
            else
            {
                var unstaged = await GitService.RunGitCommandAsync("diff", repoDir);
                if (unstaged.ExitCode != 0 || string.IsNullOrWhiteSpace(unstaged.Output))
                {
                    return ToolResult.Error("No changes found to analyze for test suggestions.");
                }
                diffOutput = unstaged.Output;
                context = "unstaged changes";
            }
        }

        // Analyze the diff
        var analysis = AnalyzeCodeForTests(diffOutput);

        var sb = new StringBuilder();
        sb.AppendLine("# 🧪 Test Suggestions");
        sb.AppendLine();
        sb.Append("**Analyzing:** ").AppendLine(context);
        sb.AppendLine();

        sb.AppendLine("## Changed Files");
        sb.AppendLine();
        foreach (var file in analysis.ChangedFiles.Take(10))
        {
            var icon = file.IsTestFile ? "✅" : "📝";
            sb.Append("- ").Append(icon).Append(" `").Append(file.Name).AppendLine("`");
        }
        if (analysis.ChangedFiles.Count > 10)
        {
            sb.Append("- *...and ")
                .Append(analysis.ChangedFiles.Count - 10)
                .AppendLine(" more files*");
        }
        sb.AppendLine();

        // Identify files needing tests
        var filesNeedingTests = analysis
            .ChangedFiles.Where(f => !f.IsTestFile && !f.IsConfig && !f.IsDocumentation)
            .ToList();

        if (filesNeedingTests.Count == 0)
        {
            sb.AppendLine("## ✅ All Changes Covered");
            sb.AppendLine();
            sb.AppendLine(
                "The changes appear to be in test files, configuration, or documentation."
            );
        }
        else
        {
            sb.AppendLine("## 📋 Suggested Tests");
            sb.AppendLine();

            foreach (var file in filesNeedingTests)
            {
                sb.Append("### `").Append(file.Name).AppendLine("`");
                sb.AppendLine();

                if (file.AddedMethods.Count > 0)
                {
                    sb.AppendLine("**New/Modified Methods:**");
                    foreach (var method in file.AddedMethods)
                    {
                        sb.Append("- `").Append(method).AppendLine("`");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("**Suggested Test Cases:**");
                sb.AppendLine();

                // Generate suggestions based on analysis
                if (file.HasAsyncCode)
                {
                    sb.AppendLine("- [ ] Test async operation completes successfully");
                    sb.AppendLine("- [ ] Test async operation handles cancellation");
                    sb.AppendLine("- [ ] Test async operation timeout behavior");
                }

                if (file.HasExceptionHandling)
                {
                    sb.AppendLine("- [ ] Test exception is thrown for invalid input");
                    sb.AppendLine("- [ ] Test exception handling doesn't swallow errors");
                }

                if (file.HasNullChecks)
                {
                    sb.AppendLine("- [ ] Test null input handling");
                    sb.AppendLine("- [ ] Test empty collection handling");
                }

                if (file.HasConditionalLogic)
                {
                    sb.AppendLine("- [ ] Test all conditional branches");
                    sb.AppendLine("- [ ] Test boundary conditions");
                }

                if (file.HasLoops)
                {
                    sb.AppendLine("- [ ] Test empty collection iteration");
                    sb.AppendLine("- [ ] Test single item collection");
                    sb.AppendLine("- [ ] Test large collection performance");
                }

                if (file.HasDatabaseCalls)
                {
                    sb.AppendLine("- [ ] Test database operation success");
                    sb.AppendLine("- [ ] Test database connection failure");
                    sb.AppendLine("- [ ] Test transaction rollback on error");
                }

                if (file.HasHttpCalls)
                {
                    sb.AppendLine("- [ ] Test successful HTTP response");
                    sb.AppendLine("- [ ] Test HTTP error responses (4xx, 5xx)");
                    sb.AppendLine("- [ ] Test network timeout handling");
                }

                // Default suggestions if nothing specific detected
                if (
                    !file.HasAsyncCode
                    && !file.HasExceptionHandling
                    && !file.HasNullChecks
                    && !file.HasConditionalLogic
                    && !file.HasLoops
                    && !file.HasDatabaseCalls
                    && !file.HasHttpCalls
                )
                {
                    sb.AppendLine("- [ ] Test happy path scenario");
                    sb.AppendLine("- [ ] Test edge cases");
                    sb.AppendLine("- [ ] Test error conditions");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine("## Test Coverage Tips");
        sb.AppendLine();
        sb.AppendLine("1. **Arrange-Act-Assert** pattern for clear test structure");
        sb.AppendLine("2. **One assertion per test** for easier debugging");
        sb.AppendLine("3. **Mock external dependencies** for unit tests");
        sb.AppendLine("4. **Use meaningful test names** that describe the scenario");

        return ToolResult.Success(sb.ToString());
    }

    private static TestAnalysis AnalyzeCodeForTests(string diff)
    {
        var analysis = new TestAnalysis();
        var currentFile = new FileAnalysis { Name = "unknown" };

        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                if (currentFile.Name != "unknown")
                {
                    analysis.ChangedFiles.Add(currentFile);
                }
                var fileName = line[6..];
                currentFile = new FileAnalysis
                {
                    Name = fileName,
                    IsTestFile = IsTestFile(fileName),
                    IsConfig = IsConfigFile(fileName),
                    IsDocumentation = IsDocFile(fileName),
                };
                continue;
            }

            if (!line.StartsWith('+') || line.StartsWith("+++", StringComparison.Ordinal))
                continue;

            var content = line[1..]; // Remove the + prefix

            // Detect patterns
            if (content.Contains("async ") || content.Contains("await "))
                currentFile.HasAsyncCode = true;

            if (content.Contains("catch") || content.Contains("throw "))
                currentFile.HasExceptionHandling = true;

            if (
                content.Contains("== null")
                || content.Contains("!= null")
                || content.Contains("?? ")
                || content.Contains("?.")
            )
                currentFile.HasNullChecks = true;

            if (content.Contains("if ") || content.Contains("switch ") || content.Contains("? "))
                currentFile.HasConditionalLogic = true;

            if (
                content.Contains("for ")
                || content.Contains("foreach ")
                || content.Contains("while ")
            )
                currentFile.HasLoops = true;

            if (
                content.Contains("DbContext")
                || content.Contains("SqlConnection")
                || content.Contains("Repository")
            )
                currentFile.HasDatabaseCalls = true;

            if (
                content.Contains("HttpClient")
                || content.Contains("HttpRequest")
                || content.Contains("WebClient")
            )
                currentFile.HasHttpCalls = true;

            // Extract method names
            var methodMatch = MethodSignaturePattern().Match(content);
            if (methodMatch.Success)
            {
                currentFile.AddedMethods.Add(methodMatch.Groups[1].Value);
            }
        }

        if (currentFile.Name != "unknown")
        {
            analysis.ChangedFiles.Add(currentFile);
        }

        return analysis;
    }

    private static bool IsTestFile(string fileName) =>
        fileName.Contains("Test", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains("Spec", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfigFile(string fileName) =>
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsDocFile(string fileName) =>
        fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".rst", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(
        @"(?:public|private|protected|internal)?\s*(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\("
    )]
    private static partial Regex MethodSignaturePattern();

    private record TestAnalysis
    {
        public List<FileAnalysis> ChangedFiles { get; } = [];
    }

    private record FileAnalysis
    {
        public required string Name { get; init; }
        public bool IsTestFile { get; init; }
        public bool IsConfig { get; init; }
        public bool IsDocumentation { get; init; }
        public bool HasAsyncCode { get; set; }
        public bool HasExceptionHandling { get; set; }
        public bool HasNullChecks { get; set; }
        public bool HasConditionalLogic { get; set; }
        public bool HasLoops { get; set; }
        public bool HasDatabaseCalls { get; set; }
        public bool HasHttpCalls { get; set; }
        public List<string> AddedMethods { get; } = [];
    }

    #endregion

    #region Tool: generate_changelog

    /// <summary>
    /// Generates changelog entries from commits between branches.
    /// </summary>
    public static async Task<ToolResult> GenerateChangelogAsync(JsonElement? arguments)
    {
        var repoDir = Directory.GetCurrentDirectory();

        // Extract parameters
        string? baseBranch = "main";
        string? featureBranch = null;
        string format = "keepachangelog"; // keepachangelog, conventional, simple

        if (arguments.HasValue)
        {
            if (
                arguments.Value.TryGetProperty("baseBranch", out var baseElement)
                && baseElement.ValueKind == JsonValueKind.String
            )
            {
                baseBranch = baseElement.GetString() ?? "main";
            }

            if (
                arguments.Value.TryGetProperty("featureBranch", out var featureElement)
                && featureElement.ValueKind == JsonValueKind.String
            )
            {
                featureBranch = featureElement.GetString();
            }

            if (
                arguments.Value.TryGetProperty("format", out var formatElement)
                && formatElement.ValueKind == JsonValueKind.String
            )
            {
                format = formatElement.GetString() ?? "keepachangelog";
            }
        }

        // Auto-detect feature branch
        if (string.IsNullOrWhiteSpace(featureBranch))
        {
            featureBranch = await GitService.GetCurrentBranchAsync(repoDir) ?? "HEAD";
        }

        // Fetch latest
        await GitService.RunGitCommandAsync("fetch origin", repoDir);

        // Get commit log
        var logResult = await GitService.RunGitCommandAsync(
            $"log origin/{baseBranch}..{featureBranch} --pretty=format:\"%h|%s|%an|%ad\" --date=short",
            repoDir
        );

        if (logResult.ExitCode != 0)
        {
            return ToolResult.GitError("Failed to get commit log", logResult.Output);
        }

        if (string.IsNullOrWhiteSpace(logResult.Output))
        {
            return ToolResult.Success(
                $"No commits found between `{baseBranch}` and `{featureBranch}`."
            );
        }

        // Parse commits
        var commits = logResult
            .Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var parts = line.Split('|');
                return new CommitInfo
                {
                    Hash = parts.Length > 0 ? parts[0] : "",
                    Message = parts.Length > 1 ? parts[1] : "",
                    Author = parts.Length > 2 ? parts[2] : "",
                    Date = parts.Length > 3 ? parts[3] : "",
                };
            })
            .ToList();

        // Categorize commits
        var categorized = CategorizeCommits(commits);

        // Build changelog
        var sb = new StringBuilder();
        sb.AppendLine("# 📝 Changelog");
        sb.AppendLine();
        sb.Append("**Branch:** `")
            .Append(featureBranch)
            .Append("` (compared to `")
            .Append(baseBranch)
            .AppendLine("`)");
        sb.Append("**Commits:** ").AppendLine(commits.Count.ToString(CultureInfo.InvariantCulture));
        sb.Append("**Date Range:** ")
            .Append(commits.LastOrDefault()?.Date ?? "N/A")
            .Append(" to ")
            .AppendLine(commits.FirstOrDefault()?.Date ?? "N/A");
        sb.AppendLine();

        if (format == "keepachangelog")
        {
            // Keep a Changelog format (https://keepachangelog.com)
            sb.AppendLine("## [Unreleased]");
            sb.AppendLine();

            if (categorized.Added.Count > 0)
            {
                sb.AppendLine("### Added");
                foreach (var commit in categorized.Added)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Changed.Count > 0)
            {
                sb.AppendLine("### Changed");
                foreach (var commit in categorized.Changed)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Fixed.Count > 0)
            {
                sb.AppendLine("### Fixed");
                foreach (var commit in categorized.Fixed)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Deprecated.Count > 0)
            {
                sb.AppendLine("### Deprecated");
                foreach (var commit in categorized.Deprecated)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Removed.Count > 0)
            {
                sb.AppendLine("### Removed");
                foreach (var commit in categorized.Removed)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Security.Count > 0)
            {
                sb.AppendLine("### Security");
                foreach (var commit in categorized.Security)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }

            if (categorized.Other.Count > 0)
            {
                sb.AppendLine("### Other");
                foreach (var commit in categorized.Other)
                {
                    sb.Append("- ")
                        .Append(CleanCommitMessage(commit.Message))
                        .Append(" (")
                        .Append(commit.Hash)
                        .AppendLine(")");
                }
                sb.AppendLine();
            }
        }
        else
        {
            // Simple format
            sb.AppendLine("## Changes");
            sb.AppendLine();
            foreach (var commit in commits)
            {
                sb.Append("- ")
                    .Append(commit.Message)
                    .Append(" (")
                    .Append(commit.Hash)
                    .Append(") - ")
                    .AppendLine(commit.Author);
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by DiffPilot*");

        return ToolResult.Success(sb.ToString());
    }

    private static CategorizedCommits CategorizeCommits(List<CommitInfo> commits)
    {
        var result = new CategorizedCommits();

        foreach (var commit in commits)
        {
            var msg = commit.Message.ToLowerInvariant();

            if (
                msg.StartsWith("feat", StringComparison.Ordinal)
                || msg.Contains("add ", StringComparison.Ordinal)
                || msg.Contains("new ", StringComparison.Ordinal)
            )
            {
                result.Added.Add(commit);
            }
            else if (
                msg.StartsWith("fix", StringComparison.Ordinal)
                || msg.Contains("bug", StringComparison.Ordinal)
                || msg.Contains("issue", StringComparison.Ordinal)
            )
            {
                result.Fixed.Add(commit);
            }
            else if (
                msg.StartsWith("refactor", StringComparison.Ordinal)
                || msg.Contains("change", StringComparison.Ordinal)
                || msg.Contains("update", StringComparison.Ordinal)
                || msg.Contains("improve", StringComparison.Ordinal)
            )
            {
                result.Changed.Add(commit);
            }
            else if (msg.Contains("deprecat", StringComparison.Ordinal))
            {
                result.Deprecated.Add(commit);
            }
            else if (
                msg.Contains("remove", StringComparison.Ordinal)
                || msg.Contains("delete", StringComparison.Ordinal)
            )
            {
                result.Removed.Add(commit);
            }
            else if (
                msg.Contains("security", StringComparison.Ordinal)
                || msg.Contains("vulnerab", StringComparison.Ordinal)
                || msg.Contains("cve", StringComparison.Ordinal)
            )
            {
                result.Security.Add(commit);
            }
            else
            {
                result.Other.Add(commit);
            }
        }

        return result;
    }

    private static string CleanCommitMessage(string message)
    {
        // Remove conventional commit prefix
        var cleaned = ConventionalPrefixPattern().Replace(message, "");
        // Capitalize first letter
        if (cleaned.Length > 0)
        {
            cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }
        return cleaned.Trim();
    }

    [GeneratedRegex(
        @"^(feat|fix|docs|style|refactor|test|chore|perf|ci|build|revert)(\([^)]+\))?:\s*",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ConventionalPrefixPattern();

    private record CommitInfo
    {
        public required string Hash { get; init; }
        public required string Message { get; init; }
        public required string Author { get; init; }
        public required string Date { get; init; }
    }

    private record CategorizedCommits
    {
        public List<CommitInfo> Added { get; } = [];
        public List<CommitInfo> Changed { get; } = [];
        public List<CommitInfo> Fixed { get; } = [];
        public List<CommitInfo> Deprecated { get; } = [];
        public List<CommitInfo> Removed { get; } = [];
        public List<CommitInfo> Security { get; } = [];
        public List<CommitInfo> Other { get; } = [];
    }

    #endregion

    #region Helper Methods

    private static ChangeAnalysis AnalyzeChanges(string diff)
    {
        var analysis = new ChangeAnalysis();
        var currentFile = "";

        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                currentFile = line[6..];
                analysis.FilesChanged++;
            }
            else if (line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            {
                analysis.LinesAdded++;
            }
            else if (line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal))
            {
                analysis.LinesRemoved++;
            }
        }

        // Determine change type based on patterns
        var lowerDiff = diff.ToLowerInvariant();
        if (
            lowerDiff.Contains("fix", StringComparison.Ordinal)
            || lowerDiff.Contains("bug", StringComparison.Ordinal)
        )
            analysis.ChangeType = "bug fix";
        else if (analysis.LinesRemoved > analysis.LinesAdded)
            analysis.ChangeType = "refactoring/cleanup";
        else if (lowerDiff.Contains("test", StringComparison.Ordinal))
            analysis.ChangeType = "testing";
        else
            analysis.ChangeType = "feature/enhancement";

        return analysis;
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;

        return content[..maxLength] + $"\n\n[... Truncated at {maxLength:N0} characters]";
    }

    private record ChangeAnalysis
    {
        public int FilesChanged { get; set; }
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public string ChangeType { get; set; } = "unknown";
    }

    #endregion
}
