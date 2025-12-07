using System.Text.RegularExpressions;
using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for git-related validation logic.
/// </summary>
public class GitValidationTests
{
    #region Branch Name Validation Tests

    [Theory]
    [InlineData("main", true)]
    [InlineData("develop", true)]
    [InlineData("feature/add-login", true)]
    [InlineData("feature/JIRA-123-description", true)]
    [InlineData("bugfix/fix-null-pointer", true)]
    [InlineData("release/v1.0.0", true)]
    [InlineData("hotfix/security-patch", true)]
    [InlineData("user/john/experiment", true)]
    public void IsValidBranchName_AcceptsValidNames(string name, bool expected)
    {
        Assert.Equal(expected, IsValidBranchName(name));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("branch with spaces", false)]
    [InlineData("branch..double-dot", false)]
    [InlineData("branch~tilde", false)]
    [InlineData("branch^caret", false)]
    [InlineData("branch:colon", false)]
    [InlineData(".hidden", false)]
    [InlineData("branch/", false)]
    [InlineData("/branch", false)]
    [InlineData("branch.lock", false)]
    public void IsValidBranchName_RejectsInvalidNames(string name, bool expected)
    {
        Assert.Equal(expected, IsValidBranchName(name));
    }

    #endregion

    #region Commit Hash Validation Tests

    [Theory]
    [InlineData("abc1234", true)] // Short hash (7 chars)
    [InlineData("abc12345678", true)] // Medium hash
    [InlineData("abc1234567890abcdef1234567890abcdef1234", true)] // Full hash (40 chars)
    [InlineData("ABC1234", true)] // Uppercase is valid
    public void IsCommitHash_AcceptsValidHashes(string hash, bool expected)
    {
        Assert.Equal(expected, IsCommitHash(hash));
    }

    [Theory]
    [InlineData("abc123", false)] // Too short (6 chars)
    [InlineData("ghijkl", false)] // Non-hex characters
    [InlineData("abc1234567890abcdef1234567890abcdef12345678", false)] // Too long (42 chars)
    [InlineData("", false)]
    [InlineData("not-a-hash", false)]
    public void IsCommitHash_RejectsInvalidHashes(string hash, bool expected)
    {
        Assert.Equal(expected, IsCommitHash(hash));
    }

    #endregion

    #region File Path Validation Tests

    [Theory]
    [InlineData("src/main.cs", true)]
    [InlineData("path/to/file.txt", true)]
    [InlineData("file.cs", true)]
    [InlineData(".gitignore", true)]
    [InlineData("path/with spaces/file.cs", true)] // Spaces are valid
    public void IsValidFilePath_AcceptsValidPaths(string path, bool expected)
    {
        Assert.Equal(expected, IsValidFilePath(path));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("path\0with\0null", false)] // Null bytes
    [InlineData("path//double/slash", false)]
    public void IsValidFilePath_RejectsInvalidPaths(string path, bool expected)
    {
        Assert.Equal(expected, IsValidFilePath(path));
    }

    #endregion

    #region Remote URL Validation Tests

    [Theory]
    [InlineData("https://github.com/user/repo.git", true)]
    [InlineData("git@github.com:user/repo.git", true)]
    [InlineData("https://gitlab.com/user/repo", true)]
    [InlineData("git@bitbucket.org:user/repo.git", true)]
    public void IsValidRemoteUrl_AcceptsValidUrls(string url, bool expected)
    {
        Assert.Equal(expected, IsValidRemoteUrl(url));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("not-a-url", false)]
    [InlineData("ftp://invalid.com/repo", false)]
    [InlineData("http://insecure.com/repo", false)] // We require https
    public void IsValidRemoteUrl_RejectsInvalidUrls(string url, bool expected)
    {
        Assert.Equal(expected, IsValidRemoteUrl(url));
    }

    #endregion

    #region Auto-Detection Tests

    [Theory]
    [InlineData("origin/main", "main")]
    [InlineData("origin/feature/test", "feature/test")]
    [InlineData("upstream/develop", "develop")]
    public void StripRemotePrefix_RemovesPrefix(string input, string expected)
    {
        Assert.Equal(expected, StripRemotePrefix(input));
    }

    [Theory]
    [InlineData("main", "main")]
    [InlineData("feature/test", "feature/test")]
    public void StripRemotePrefix_PreservesLocalBranches(string input, string expected)
    {
        Assert.Equal(expected, StripRemotePrefix(input));
    }

    [Theory]
    [InlineData("main", "master", "main")]
    [InlineData("master", "develop", "master")]
    [InlineData("develop", "main", "main")]
    public void DetectBaseBranch_PrefersMainOverMaster(
        string branch1,
        string branch2,
        string expected
    )
    {
        var branches = new[] { branch1, branch2 };
        var result = DetectBaseBranch(branches);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Helper Methods (simulating GitService logic)

    private static bool IsValidBranchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Git branch name restrictions
        if (name.StartsWith('.') || name.EndsWith('/') || name.StartsWith('/'))
            return false;

        if (name.EndsWith(".lock", StringComparison.Ordinal))
            return false;

        // Invalid characters
        var invalidChars = new[] { ' ', '~', '^', ':', '\\', '?', '*', '[' };
        if (invalidChars.Any(c => name.Contains(c)))
            return false;

        // No consecutive dots
        if (name.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsCommitHash(string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length < 7 || hash.Length > 40)
            return false;

        return Regex.IsMatch(hash, "^[0-9a-fA-F]+$");
    }

    private static bool IsValidFilePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.Contains('\0'))
            return false;

        if (path.Contains("//", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsValidRemoteUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // SSH format
        if (url.StartsWith("git@", StringComparison.Ordinal) && url.Contains(':'))
            return true;

        // HTTPS format (require https, not http)
        if (url.StartsWith("https://", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string StripRemotePrefix(string branch)
    {
        var prefixes = new[] { "origin/", "upstream/", "remote/" };
        foreach (var prefix in prefixes)
        {
            if (branch.StartsWith(prefix, StringComparison.Ordinal))
                return branch[prefix.Length..];
        }
        return branch;
    }

    private static string DetectBaseBranch(string[] branches)
    {
        // Priority: main > master > develop
        if (branches.Contains("main"))
            return "main";
        if (branches.Contains("master"))
            return "master";
        if (branches.Contains("develop"))
            return "develop";

        return branches.FirstOrDefault() ?? "main";
    }

    #endregion
}
