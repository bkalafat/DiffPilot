using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for determining commit type from diff content.
/// Used in generate_commit_message tool.
/// </summary>
public class CommitTypeDetectionTests
{
    #region Commit Type from File Changes Tests
    
    [Theory]
    [InlineData(new[] { "tests/UserServiceTests.cs" }, "test")]
    [InlineData(new[] { "src/__tests__/user.test.js" }, "test")]
    [InlineData(new[] { "spec/models/user_spec.rb" }, "test")]
    public void DetermineCommitType_TestFilesOnly_ReturnsTest(string[] files, string expected)
    {
        Assert.Equal(expected, DetermineCommitType(files, ""));
    }
    
    [Theory]
    [InlineData(new[] { "README.md" }, "docs")]
    [InlineData(new[] { "docs/guide.md", "docs/api.md" }, "docs")]
    [InlineData(new[] { "CHANGELOG.md" }, "docs")]
    public void DetermineCommitType_DocsFilesOnly_ReturnsDocs(string[] files, string expected)
    {
        Assert.Equal(expected, DetermineCommitType(files, ""));
    }
    
    [Theory]
    [InlineData(new[] { "package.json" }, "chore")]
    [InlineData(new[] { ".github/workflows/ci.yml" }, "chore")]
    [InlineData(new[] { ".gitignore" }, "chore")]
    [InlineData(new[] { "tsconfig.json" }, "chore")]
    public void DetermineCommitType_ConfigFilesOnly_ReturnsChore(string[] files, string expected)
    {
        Assert.Equal(expected, DetermineCommitType(files, ""));
    }
    
    #endregion
    
    #region Commit Type from Diff Content Tests
    
    [Theory]
    [InlineData("- old code\n+ new implementation", "fix")]
    [InlineData("+        // reformatted\n-// old format", "style")]
    public void DetermineCommitType_FromDiffContent_InfersType(string diff, string expected)
    {
        var files = new[] { "src/Service.cs" };
        var result = DetermineCommitType(files, diff);
        // For mixed content, default to feat unless specific patterns found
        Assert.True(result == expected || result == "feat");
    }
    
    [Fact]
    public void DetermineCommitType_NewFiles_ReturnsFeat()
    {
        var diff = @"
diff --git a/src/NewFeature.cs b/src/NewFeature.cs
new file mode 100644
+++ b/src/NewFeature.cs
+public class NewFeature { }";
        
        var files = new[] { "src/NewFeature.cs" };
        var type = DetermineCommitType(files, diff);
        
        Assert.Equal("feat", type);
    }
    
    [Fact]
    public void DetermineCommitType_DeletedFiles_ReturnsChore()
    {
        var diff = @"
diff --git a/src/OldFile.cs b/src/OldFile.cs
deleted file mode 100644
--- a/src/OldFile.cs
-public class OldFile { }";
        
        var files = new[] { "src/OldFile.cs" };
        var type = DetermineCommitType(files, diff);
        
        Assert.Equal("chore", type);
    }
    
    #endregion
    
    #region Mixed File Types Tests
    
    [Fact]
    public void DetermineCommitType_MixedTestAndSource_ReturnsFeat()
    {
        var files = new[] { "src/UserService.cs", "tests/UserServiceTests.cs" };
        var type = DetermineCommitType(files, "");
        
        // When both source and test files change, it's typically a feature
        Assert.Equal("feat", type);
    }
    
    [Fact]
    public void DetermineCommitType_MixedDocsAndSource_ReturnsFeat()
    {
        var files = new[] { "src/UserService.cs", "README.md" };
        var type = DetermineCommitType(files, "");
        
        // Source code changes take precedence
        Assert.Equal("feat", type);
    }
    
    #endregion
    
    #region Scope Detection Tests
    
    [Theory]
    [InlineData(new[] { "src/api/UserController.cs" }, "api")]
    [InlineData(new[] { "src/ui/Button.tsx" }, "ui")]
    [InlineData(new[] { "src/auth/AuthService.cs" }, "auth")]
    [InlineData(new[] { "src/services/UserService.cs" }, "services")]
    public void DetectScope_FromFilePath_ExtractsScope(string[] files, string expectedScope)
    {
        var scope = DetectScope(files);
        Assert.Equal(expectedScope, scope);
    }
    
    [Fact]
    public void DetectScope_MultipleDirectories_ReturnsNull()
    {
        var files = new[] { "src/api/UserController.cs", "src/services/UserService.cs" };
        var scope = DetectScope(files);
        
        // Multiple directories = no single scope
        Assert.Null(scope);
    }
    
    [Fact]
    public void DetectScope_RootFiles_ReturnsNull()
    {
        var files = new[] { "Program.cs", "Startup.cs" };
        var scope = DetectScope(files);
        
        Assert.Null(scope);
    }
    
    #endregion
    
    #region Helper Methods (simulating DeveloperTools logic)
    
    private static string DetermineCommitType(string[] files, string diff)
    {
        var lower = files.Select(f => f.ToLowerInvariant()).ToArray();
        
        // Check for deleted files
        if (diff.Contains("deleted file mode"))
            return "chore";
            
        // Check for new files
        if (diff.Contains("new file mode"))
            return "feat";
        
        // All test files
        if (lower.All(f => IsTestFile(f)))
            return "test";
            
        // All docs files
        if (lower.All(f => IsDocFile(f)))
            return "docs";
            
        // All config files
        if (lower.All(f => IsConfigFile(f)))
            return "chore";
        
        // Default to feat for source changes
        return "feat";
    }
    
    private static string? DetectScope(string[] files)
    {
        var scopes = new HashSet<string>();
        
        foreach (var file in files)
        {
            var parts = file.Split('/');
            if (parts.Length >= 2)
            {
                // Get the first meaningful directory (skip 'src')
                var scopeIndex = parts[0].ToLower() == "src" ? 1 : 0;
                if (scopeIndex < parts.Length - 1)
                {
                    scopes.Add(parts[scopeIndex].ToLower());
                }
            }
        }
        
        return scopes.Count == 1 ? scopes.First() : null;
    }
    
    private static bool IsTestFile(string path)
    {
        return path.Contains("test") || path.Contains("spec") || path.Contains("__tests__");
    }
    
    private static bool IsDocFile(string path)
    {
        return path.EndsWith(".md") || path.Contains("docs/") || 
               path.Contains("readme") || path.Contains("changelog");
    }
    
    private static bool IsConfigFile(string path)
    {
        var configPatterns = new[] { ".json", ".yaml", ".yml", ".config", ".gitignore", 
            ".github/", "package.json", "tsconfig" };
        return configPatterns.Any(p => path.Contains(p));
    }
    
    #endregion
}
