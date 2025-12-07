using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for changelog generation logic.
/// Verifies commit categorization and changelog formatting.
/// </summary>
public class ChangelogGenerationTests
{
    #region Commit Categorization Tests

    [Theory]
    [InlineData("feat: add new feature", "Added")]
    [InlineData("feat(ui): new button component", "Added")]
    [InlineData("fix: resolve null pointer", "Fixed")]
    [InlineData("fix(api): handle timeout", "Fixed")]
    [InlineData("docs: update README", "Documentation")]
    [InlineData("chore: update dependencies", "Changed")]
    [InlineData("refactor: clean up code", "Changed")]
    [InlineData("perf: optimize query", "Changed")]
    [InlineData("test: add unit tests", "Changed")]
    [InlineData("style: format code", "Changed")]
    public void CategorizeCommit_ReturnsCorrectCategory(
        string commitMessage,
        string expectedCategory
    )
    {
        var category = CategorizeCommit(commitMessage);
        Assert.Equal(expectedCategory, category);
    }

    [Theory]
    [InlineData("random commit message", "Changed")]
    [InlineData("Updated something", "Changed")]
    [InlineData("WIP: work in progress", "Changed")]
    public void CategorizeCommit_DefaultsToChanged(string commitMessage, string expectedCategory)
    {
        var category = CategorizeCommit(commitMessage);
        Assert.Equal(expectedCategory, category);
    }

    #endregion

    #region Commit Message Cleaning Tests

    [Theory]
    [InlineData("feat: add login", "Add login")]
    [InlineData("fix(auth): handle null", "Handle null")]
    [InlineData("docs(readme): update install", "Update install")]
    [InlineData("chore!: breaking change", "Breaking change")]
    public void CleanCommitMessage_RemovesPrefixAndCapitalizes(string input, string expected)
    {
        var cleaned = CleanCommitMessage(input);
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void CleanCommitMessage_PreservesRegularMessages()
    {
        var message = "Update configuration";
        var cleaned = CleanCommitMessage(message);
        Assert.Equal("Update configuration", cleaned);
    }

    #endregion

    #region Changelog Formatting Tests

    [Fact]
    public void FormatChangelogSection_GroupsEntriesCorrectly()
    {
        var entries = new Dictionary<string, List<string>>
        {
            ["Added"] = new() { "New feature A", "New feature B" },
            ["Fixed"] = new() { "Bug fix X" },
        };

        var changelog = FormatChangelog(entries);

        Assert.Contains("### Added", changelog);
        Assert.Contains("### Fixed", changelog);
        Assert.Contains("- New feature A", changelog);
        Assert.Contains("- New feature B", changelog);
        Assert.Contains("- Bug fix X", changelog);
    }

    [Fact]
    public void FormatChangelogSection_OmitsEmptyCategories()
    {
        var entries = new Dictionary<string, List<string>>
        {
            ["Added"] = new() { "New feature" },
            ["Fixed"] = new(), // Empty
            ["Changed"] = new(), // Empty
        };

        var changelog = FormatChangelog(entries);

        Assert.Contains("### Added", changelog);
        Assert.DoesNotContain("### Fixed", changelog);
        Assert.DoesNotContain("### Changed", changelog);
    }

    [Fact]
    public void FormatChangelog_HandlesNoEntries()
    {
        var entries = new Dictionary<string, List<string>>();
        var changelog = FormatChangelog(entries);

        Assert.Equal("No changes.", changelog);
    }

    #endregion

    #region Keep A Changelog Order Tests

    [Fact]
    public void FormatChangelog_FollowsKeepAChangelogOrder()
    {
        // Keep a Changelog order: Added, Changed, Deprecated, Removed, Fixed, Security
        var entries = new Dictionary<string, List<string>>
        {
            ["Fixed"] = new() { "Fix 1" },
            ["Added"] = new() { "Feature 1" },
            ["Security"] = new() { "Security fix" },
            ["Changed"] = new() { "Change 1" },
        };

        var changelog = FormatChangelog(entries);

        var addedPos = changelog.IndexOf("### Added", StringComparison.Ordinal);
        var changedPos = changelog.IndexOf("### Changed", StringComparison.Ordinal);
        var fixedPos = changelog.IndexOf("### Fixed", StringComparison.Ordinal);
        var securityPos = changelog.IndexOf("### Security", StringComparison.Ordinal);

        Assert.True(addedPos < changedPos, "Added should come before Changed");
        Assert.True(changedPos < fixedPos, "Changed should come before Fixed");
        Assert.True(fixedPos < securityPos, "Fixed should come before Security");
    }

    #endregion

    #region Helper Methods (simulating DeveloperTools logic)

    private static string CategorizeCommit(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.StartsWith("feat", StringComparison.Ordinal))
            return "Added";
        if (lowerMessage.StartsWith("fix", StringComparison.Ordinal))
            return "Fixed";
        if (lowerMessage.StartsWith("docs", StringComparison.Ordinal))
            return "Documentation";

        return "Changed";
    }

    private static string CleanCommitMessage(string message)
    {
        // Remove conventional commit prefix like "feat:", "fix(scope):", etc.
        var colonIndex = message.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0 && colonIndex < 20)
        {
            var afterColon = message[(colonIndex + 1)..].TrimStart();
            if (!string.IsNullOrEmpty(afterColon))
            {
                // Capitalize first letter
                return char.ToUpperInvariant(afterColon[0]) + afterColon[1..];
            }
        }
        return message;
    }

    private static string FormatChangelog(Dictionary<string, List<string>> entries)
    {
        if (entries.Count == 0 || entries.All(e => e.Value.Count == 0))
            return "No changes.";

        var order = new[]
        {
            "Added",
            "Changed",
            "Deprecated",
            "Removed",
            "Fixed",
            "Security",
            "Documentation",
        };
        var sb = new System.Text.StringBuilder();

        foreach (var category in order)
        {
            if (entries.TryGetValue(category, out var items) && items.Count > 0)
            {
                sb.Append("### ").AppendLine(category);
                foreach (var item in items)
                {
                    sb.Append("- ").AppendLine(item);
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    #endregion
}
