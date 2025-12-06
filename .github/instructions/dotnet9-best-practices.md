# .NET 9 Best Practices for DiffPilot

This document outlines .NET 9 best practices and coding standards for the DiffPilot project.

## Target Framework

- **Target**: `net9.0`
- **Language Version**: C# 13
- **Nullable Reference Types**: Enabled

## C# 13 Features to Leverage

### Primary Constructors (Prefer for Simple Classes)
```csharp
// ✅ Good - Primary constructor
public class GitResult(bool success, string output, string error)
{
    public bool Success { get; } = success;
    public string Output { get; } = output;
    public string Error { get; } = error;
}

// ❌ Avoid - Verbose constructor
public class GitResult
{
    public bool Success { get; }
    public GitResult(bool success) => Success = success;
}
```

### Required Properties
```csharp
// ✅ Good - Required modifier for mandatory properties
public class ToolInput
{
    public required string BaseBranch { get; init; }
    public string? FeatureBranch { get; init; }  // Optional
}
```

### Collection Expressions
```csharp
// ✅ Good - Collection expressions
string[] tools = ["get_pr_diff", "review_pr_changes", "scan_secrets"];
List<string> files = [.. existingFiles, newFile];

// ❌ Avoid - Verbose initialization
string[] tools = new string[] { "get_pr_diff", "review_pr_changes" };
```

### Raw String Literals for JSON/Multi-line
```csharp
// ✅ Good - Raw string literals for JSON
string json = """
    {
        "jsonrpc": "2.0",
        "method": "tools/call",
        "params": { "name": "get_pr_diff" }
    }
    """;
```

## Async/Await Best Practices

### Always Use ConfigureAwait(false) in Libraries
```csharp
// ✅ Good - Avoid deadlocks in library code
var result = await process.StandardOutput.ReadToEndAsync()
    .ConfigureAwait(false);

// ✅ Good - Async all the way
public async Task<string> GetDiffAsync()
{
    return await RunGitCommandAsync("diff").ConfigureAwait(false);
}
```

### Prefer ValueTask for Hot Paths
```csharp
// ✅ Good - ValueTask for frequently sync-completing methods
public ValueTask<string?> TryGetCachedAsync(string key)
{
    if (_cache.TryGetValue(key, out var value))
        return new ValueTask<string?>(value);
    
    return new ValueTask<string?>(FetchFromGitAsync(key));
}
```

### Cancellation Token Support
```csharp
// ✅ Good - Accept and forward cancellation tokens
public async Task<ToolResult> ExecuteAsync(
    string tool, 
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

## Performance Optimizations

### Use Span<T> and Memory<T>
```csharp
// ✅ Good - Span for parsing without allocations
public static bool TryParseDiffLine(ReadOnlySpan<char> line, out int added)
{
    var tabIndex = line.IndexOf('\t');
    return int.TryParse(line[..tabIndex], out added);
}
```

### SearchValues for Multiple Pattern Matching (.NET 9)
```csharp
// ✅ Good - Optimized SIMD search for multiple patterns
private static readonly SearchValues<string> SecretPatterns = 
    SearchValues.Create(["api_key", "secret", "password", "token"]);

public static int FindSecretPattern(ReadOnlySpan<char> text)
{
    return text.IndexOfAny(SecretPatterns);
}
```

### StringBuilder for String Concatenation in Loops
```csharp
// ✅ Good - StringBuilder for building output
var sb = new StringBuilder();
foreach (var line in lines)
{
    sb.AppendLine(line);
}
return sb.ToString();

// ❌ Avoid - String concatenation in loops
string result = "";
foreach (var line in lines)
{
    result += line + "\n";  // Creates new string each iteration
}
```

### Compiled Regex (Source Generated)
```csharp
// ✅ Good - Source-generated regex (compile-time)
[GeneratedRegex(@"^[a-f0-9]{40}$", RegexOptions.Compiled)]
private static partial Regex CommitHashRegex();

// Usage
bool isValid = CommitHashRegex().IsMatch(hash);
```

## Exception Handling

### Use Exception Helpers
```csharp
// ✅ Good - ThrowIfNull helper
public void Process(string input)
{
    ArgumentNullException.ThrowIfNull(input);
    ArgumentException.ThrowIfNullOrEmpty(input);
    // ...
}
```

### Don't Catch Exception Without Re-throwing
```csharp
// ✅ Good - Log and re-throw or handle specifically
catch (GitException ex)
{
    await Console.Error.WriteLineAsync($"Git error: {ex.Message}");
    return ToolResult.Error(ex.Message);
}

// ❌ Avoid - Swallowing exceptions
catch (Exception) { }
```

## Nullable Reference Types

### Enable and Honor Nullable Annotations
```csharp
// ✅ Good - Proper nullable handling
public string? GetBranchName()
{
    var result = RunGitCommand("branch --show-current");
    return result.Success ? result.Output.Trim() : null;
}

// ✅ Good - Null checks before use
if (branch is not null)
{
    await FetchBranchAsync(branch);
}
```

### Use Null-Coalescing Operators
```csharp
// ✅ Good - Null coalescing
string branch = inputBranch ?? await DetectBaseBranchAsync();
string message = error ?? "Unknown error";

// ✅ Good - Null-conditional
int? length = output?.Length;
```

## Unit Testing (xUnit)

### Naming Convention
```csharp
// ✅ Good - MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void ScanSecrets_WithApiKey_DetectsSecret()

[Fact]
public void ParseDiffStats_EmptyInput_ReturnsZeroCounts()
```

### Use Theory for Parameterized Tests
```csharp
// ✅ Good - Theory with InlineData
[Theory]
[InlineData("feat: add feature", "feat")]
[InlineData("fix: bug fix", "fix")]
[InlineData("chore: update deps", "chore")]
public void DetectCommitType_ConventionalCommit_ExtractsType(
    string message, string expectedType)
{
    var result = CommitAnalyzer.DetectType(message);
    Assert.Equal(expectedType, result);
}
```

### Arrange-Act-Assert Pattern
```csharp
[Fact]
public void GeneratePrTitle_WithChanges_ReturnsConventionalFormat()
{
    // Arrange
    var diff = "diff --git a/src/Program.cs b/src/Program.cs...";
    
    // Act
    var title = PrTitleGenerator.Generate(diff);
    
    // Assert
    Assert.StartsWith("feat:", title);
}
```

## MCP-Specific Best Practices

### Tool Results Always Return Valid JSON
```csharp
// ✅ Good - Consistent tool result format
return ToolResult.Success(new
{
    title = generatedTitle,
    confidence = 0.95
});
```

### Stderr for Diagnostics, Stdout for JSON-RPC Only
```csharp
// ✅ Good - Diagnostics to stderr
await Console.Error.WriteLineAsync($"Processing: {toolName}");

// Stdout is reserved for JSON-RPC responses
await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
```

### Validate Git Repository Before Operations
```csharp
// ✅ Good - Validate before operations
if (!await GitService.IsGitRepositoryAsync())
{
    return ToolResult.Error("Not a git repository");
}
```

## Memory and Resource Management

### Use `using` for Disposables
```csharp
// ✅ Good - using declaration
using var process = new Process();
using var reader = new StreamReader(path);

// ✅ Good - Async using
await using var stream = File.OpenRead(path);
```

### Avoid Large Object Heap for Temp Data
```csharp
// ✅ Good - Rent arrays for large buffers
byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## Code Organization

### Keep Methods Small and Focused
- Each method should do one thing
- Extract helper methods for complex logic
- Aim for < 30 lines per method

### File-Scoped Namespaces
```csharp
// ✅ Good - File-scoped namespace
namespace DiffPilot.Tools;

public class PrReviewTools
{
    // ...
}
```

### Const for Compile-Time Constants
```csharp
// ✅ Good - Const for known values
private const string DefaultRemote = "origin";
private const int MaxRetries = 3;
```

## Build Configuration

### Project File Best Practices
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>
</Project>
```

## References

- [What's new in .NET 9](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview)
- [C# 13 Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)
- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/performance/)
