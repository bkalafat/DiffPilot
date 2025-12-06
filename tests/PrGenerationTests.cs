using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for PR title and description generation logic.
/// </summary>
public class PrGenerationTests
{
    #region PR Title Generation Tests
    
    [Theory]
    [InlineData("feat", "add login feature", "feat: add login feature")]
    [InlineData("fix", "resolve null pointer", "fix: resolve null pointer")]
    [InlineData("docs", "update README", "docs: update README")]
    [InlineData("chore", "update dependencies", "chore: update dependencies")]
    public void GeneratePrTitle_ConventionalFormat(string type, string description, string expected)
    {
        var title = GenerateConventionalTitle(type, null, description);
        Assert.Equal(expected, title);
    }
    
    [Theory]
    [InlineData("feat", "api", "add endpoint", "feat(api): add endpoint")]
    [InlineData("fix", "auth", "token expiry", "fix(auth): token expiry")]
    public void GeneratePrTitle_WithScope(string type, string scope, string description, string expected)
    {
        var title = GenerateConventionalTitle(type, scope, description);
        Assert.Equal(expected, title);
    }
    
    [Fact]
    public void GeneratePrTitle_TruncatesLongDescription()
    {
        var longDescription = new string('a', 100);
        var title = GenerateConventionalTitle("feat", null, longDescription);
        
        // PR titles should be max ~72 characters
        Assert.True(title.Length <= 75);
        Assert.EndsWith("...", title);
    }
    
    #endregion
    
    #region Ticket Extraction Tests
    
    [Theory]
    [InlineData("feature/JIRA-123-add-login", "JIRA-123")]
    [InlineData("bugfix/PROJ-456-fix-crash", "PROJ-456")]
    [InlineData("ABC-789/some-feature", "ABC-789")]
    [InlineData("feature/123-no-prefix", null)]  // Numbers only don't count
    public void ExtractTicketNumber_FromBranchName(string branch, string? expected)
    {
        var ticket = ExtractTicketNumber(branch);
        Assert.Equal(expected, ticket);
    }
    
    [Fact]
    public void ExtractTicketNumber_ReturnsNullForNoTicket()
    {
        var branch = "feature/add-login-page";
        var ticket = ExtractTicketNumber(branch);
        Assert.Null(ticket);
    }
    
    #endregion
    
    #region PR Description Section Tests
    
    [Fact]
    public void GeneratePrDescription_IncludesAllSections()
    {
        var changes = new[] { "Added login page", "Fixed validation" };
        var description = GeneratePrDescription("Summary", changes, true);
        
        Assert.Contains("## Summary", description);
        Assert.Contains("## Changes", description);
        Assert.Contains("## Checklist", description);
        Assert.Contains("- Added login page", description);
        Assert.Contains("- Fixed validation", description);
    }
    
    [Fact]
    public void GeneratePrDescription_OmitsChecklistWhenDisabled()
    {
        var changes = new[] { "Change 1" };
        var description = GeneratePrDescription("Summary", changes, false);
        
        Assert.DoesNotContain("Checklist", description);
    }
    
    [Fact]
    public void GeneratePrDescription_IncludesTicketLink()
    {
        var changes = new[] { "Change 1" };
        var ticketUrl = "https://jira.example.com/PROJ-123";
        var description = GeneratePrDescriptionWithTicket("Summary", changes, ticketUrl);
        
        Assert.Contains("Related Issue", description);
        Assert.Contains(ticketUrl, description);
    }
    
    #endregion
    
    #region Change Summary Tests
    
    [Fact]
    public void SummarizeChanges_GroupsByType()
    {
        var files = new[]
        {
            ("src/Login.cs", 50, 10),
            ("src/Auth.cs", 30, 5),
            ("tests/LoginTests.cs", 100, 0),
            ("README.md", 20, 5)
        };
        
        var summary = SummarizeChanges(files);
        
        Assert.Contains("source files", summary);
        Assert.Contains("test files", summary);
        Assert.Contains("documentation", summary);
    }
    
    [Fact]
    public void SummarizeChanges_CalculatesTotals()
    {
        var files = new[]
        {
            ("file1.cs", 100, 50),
            ("file2.cs", 50, 25)
        };
        
        var summary = SummarizeChanges(files);
        
        Assert.Contains("150", summary);  // Total additions
        Assert.Contains("75", summary);   // Total deletions
    }
    
    #endregion
    
    #region Helper Methods (simulating PrReviewTools logic)
    
    private static string GenerateConventionalTitle(string type, string? scope, string description)
    {
        var prefix = scope != null ? $"{type}({scope})" : type;
        var fullTitle = $"{prefix}: {description}";
        
        if (fullTitle.Length > 72)
        {
            return fullTitle[..69] + "...";
        }
        
        return fullTitle;
    }
    
    private static string? ExtractTicketNumber(string branchName)
    {
        // Pattern: LETTERS-NUMBERS (e.g., JIRA-123, PROJ-456)
        var match = System.Text.RegularExpressions.Regex.Match(
            branchName, 
            @"([A-Z]+-\d+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
    
    private static string GeneratePrDescription(string summary, string[] changes, bool includeChecklist)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("## Summary");
        sb.AppendLine(summary);
        sb.AppendLine();
        
        sb.AppendLine("## Changes");
        foreach (var change in changes)
        {
            sb.AppendLine($"- {change}");
        }
        sb.AppendLine();
        
        if (includeChecklist)
        {
            sb.AppendLine("## Checklist");
            sb.AppendLine("- [ ] Tests pass");
            sb.AppendLine("- [ ] Code reviewed");
            sb.AppendLine("- [ ] Documentation updated");
        }
        
        return sb.ToString();
    }
    
    private static string GeneratePrDescriptionWithTicket(string summary, string[] changes, string ticketUrl)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("## Summary");
        sb.AppendLine(summary);
        sb.AppendLine();
        
        sb.AppendLine("## Related Issue");
        sb.AppendLine(ticketUrl);
        sb.AppendLine();
        
        sb.AppendLine("## Changes");
        foreach (var change in changes)
        {
            sb.AppendLine($"- {change}");
        }
        
        return sb.ToString();
    }
    
    private static string SummarizeChanges((string file, int additions, int deletions)[] files)
    {
        var sourceFiles = files.Count(f => !f.file.Contains("test", StringComparison.OrdinalIgnoreCase) && 
                                           !f.file.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        var testFiles = files.Count(f => f.file.Contains("test", StringComparison.OrdinalIgnoreCase));
        var docFiles = files.Count(f => f.file.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        
        var totalAdditions = files.Sum(f => f.additions);
        var totalDeletions = files.Sum(f => f.deletions);
        
        var parts = new List<string>();
        if (sourceFiles > 0) parts.Add($"{sourceFiles} source files");
        if (testFiles > 0) parts.Add($"{testFiles} test files");
        if (docFiles > 0) parts.Add($"{docFiles} documentation");
        
        return $"Changed {string.Join(", ", parts)} (+{totalAdditions}/-{totalDeletions})";
    }
    
    #endregion
}
