using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for test suggestion logic - detecting patterns that need testing.
/// </summary>
public class TestSuggestionAnalysisTests
{
    #region File Type Detection Tests

    [Theory]
    [InlineData("UserServiceTests.cs", true)]
    [InlineData("UserService.test.ts", true)]
    [InlineData("user_service_test.py", true)]
    [InlineData("UserServiceSpec.cs", true)]
    [InlineData("tests/UserService.cs", true)]
    [InlineData("__tests__/user.js", true)]
    public void IsTestFile_DetectsTestFiles(string path, bool expected)
    {
        Assert.Equal(expected, IsTestFile(path));
    }

    [Theory]
    [InlineData("UserService.cs", false)]
    [InlineData("src/UserService.ts", false)]
    [InlineData("models/user.py", false)]
    public void IsTestFile_IgnoresNonTestFiles(string path, bool expected)
    {
        Assert.Equal(expected, IsTestFile(path));
    }

    [Theory]
    [InlineData("appsettings.json", true)]
    [InlineData("config.yaml", true)]
    [InlineData(".env", true)]
    [InlineData("tsconfig.json", true)]
    [InlineData("package.json", true)]
    [InlineData(".eslintrc", true)]
    public void IsConfigFile_DetectsConfigFiles(string path, bool expected)
    {
        Assert.Equal(expected, IsConfigFile(path));
    }

    [Theory]
    [InlineData("README.md", true)]
    [InlineData("CHANGELOG.md", true)]
    [InlineData("docs/guide.md", true)]
    [InlineData("LICENSE", true)]
    [InlineData("CONTRIBUTING.md", true)]
    public void IsDocFile_DetectsDocFiles(string path, bool expected)
    {
        Assert.Equal(expected, IsDocFile(path));
    }

    #endregion

    #region Code Pattern Detection Tests

    [Theory]
    [InlineData("public async Task FetchData()", true)]
    [InlineData("async function getData() {", true)]
    [InlineData("await httpClient.GetAsync(url)", true)]
    [InlineData("return Promise.resolve(data)", true)]
    public void HasAsyncCode_DetectsAsyncPatterns(string code, bool expected)
    {
        Assert.Equal(expected, HasAsyncPattern(code));
    }

    [Theory]
    [InlineData("throw new ArgumentException()", true)]
    [InlineData("catch (Exception ex)", true)]
    [InlineData("try { DoSomething(); }", true)]
    [InlineData("if (x == null) throw new Exception()", true)]
    public void HasExceptionHandling_DetectsExceptionPatterns(string code, bool expected)
    {
        Assert.Equal(expected, HasExceptionPattern(code));
    }

    [Theory]
    [InlineData("if (user == null)", true)]
    [InlineData("x ?? defaultValue", true)]
    [InlineData("user?.Name", true)]
    [InlineData("ArgumentNullException.ThrowIfNull(x)", true)]
    public void HasNullChecks_DetectsNullPatterns(string code, bool expected)
    {
        Assert.Equal(expected, HasNullCheckPattern(code));
    }

    [Theory]
    [InlineData("foreach (var item in items)", true)]
    [InlineData("for (int i = 0; i < 10; i++)", true)]
    [InlineData("while (condition)", true)]
    [InlineData("items.Select(x => x.Name)", true)]
    [InlineData("items.Where(x => x.Active)", true)]
    public void HasLoops_DetectsLoopPatterns(string code, bool expected)
    {
        Assert.Equal(expected, HasLoopPattern(code));
    }

    #endregion

    #region Test Suggestion Generation Tests

    [Fact]
    public void SuggestTests_ForAsyncCode_SuggestsAsyncTests()
    {
        var code =
            @"
public async Task<User> GetUserAsync(int id)
{
    return await _repository.FindAsync(id);
}";

        var suggestions = GenerateTestSuggestions(code, "UserService.cs");

        Assert.Contains(suggestions, s => s.Contains("async") || s.Contains("Task"));
    }

    [Fact]
    public void SuggestTests_ForNullableCode_SuggestsNullTests()
    {
        var code =
            @"
public string GetName(User? user)
{
    return user?.Name ?? ""Unknown"";
}";

        var suggestions = GenerateTestSuggestions(code, "UserService.cs");

        Assert.Contains(suggestions, s => s.Contains("null"));
    }

    [Fact]
    public void SuggestTests_ForLoopCode_SuggestsBoundaryTests()
    {
        var code =
            @"
public int Sum(IEnumerable<int> numbers)
{
    foreach (var n in numbers) { }
    return numbers.Where(x => x > 0).Count();
}";

        var suggestions = GenerateTestSuggestions(code, "Calculator.cs");

        Assert.Contains(suggestions, s => s.Contains("empty") || s.Contains("collection"));
    }

    [Fact]
    public void SuggestTests_ForExceptionCode_SuggestsErrorTests()
    {
        var code =
            @"
public void Validate(string input)
{
    if (string.IsNullOrEmpty(input))
        throw new ArgumentException(""Input required"");
}";

        var suggestions = GenerateTestSuggestions(code, "Validator.cs");

        Assert.Contains(
            suggestions,
            s => s.Contains("exception") || s.Contains("throw") || s.Contains("error")
        );
    }

    #endregion

    #region Helper Methods (simulating DeveloperTools logic)

    private static bool IsTestFile(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("test")
            || lower.Contains("spec")
            || lower.Contains("__tests__")
            || lower.StartsWith("tests/");
    }

    private static bool IsConfigFile(string path)
    {
        var lower = path.ToLowerInvariant();
        var configExtensions = new[] { ".json", ".yaml", ".yml", ".env", ".config" };
        var configNames = new[]
        {
            "config",
            "settings",
            "appsettings",
            "package.json",
            "tsconfig",
            ".eslintrc",
            ".prettierrc",
            ".env",
        };

        return configExtensions.Any(e => lower.EndsWith(e))
                && configNames.Any(n => lower.Contains(n))
            || lower.StartsWith(".") && !lower.Contains("/");
    }

    private static bool IsDocFile(string path)
    {
        var lower = path.ToLowerInvariant();
        var docPatterns = new[]
        {
            "readme",
            "changelog",
            "license",
            "contributing",
            "docs/",
            "documentation/",
            ".md",
        };
        return docPatterns.Any(p => lower.Contains(p));
    }

    private static bool HasAsyncPattern(string code)
    {
        var patterns = new[] { "async ", "await ", "Task<", "Task ", "Promise", ".then(" };
        return patterns.Any(p => code.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExceptionPattern(string code)
    {
        var patterns = new[] { "throw ", "catch ", "try ", "Exception" };
        return patterns.Any(p => code.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasNullCheckPattern(string code)
    {
        var patterns = new[] { "== null", "!= null", "is null", "??", "?.", "ThrowIfNull" };
        return patterns.Any(p => code.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasLoopPattern(string code)
    {
        var patterns = new[]
        {
            "foreach ",
            "for (",
            "while (",
            ".Select(",
            ".Where(",
            ".ForEach(",
            ".Any(",
            ".All(",
        };
        return patterns.Any(p => code.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GenerateTestSuggestions(string code, string fileName)
    {
        var suggestions = new List<string>();

        if (HasAsyncPattern(code))
            suggestions.Add("Test async/await behavior and Task completion");

        if (HasNullCheckPattern(code))
            suggestions.Add("Test with null inputs and verify null handling");

        if (HasLoopPattern(code))
            suggestions.Add("Test with empty collection and boundary cases");

        if (HasExceptionPattern(code))
            suggestions.Add("Test exception throwing conditions and error handling");

        if (suggestions.Count == 0)
            suggestions.Add($"Add unit tests for {Path.GetFileNameWithoutExtension(fileName)}");

        return suggestions;
    }

    #endregion
}
