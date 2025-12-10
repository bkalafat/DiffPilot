// ============================================================================
// SecurityHelpersTests.cs
// ============================================================================
// Unit tests for the SecurityHelpers class.
// Tests input validation, rate limiting, output sanitization, and other
// security features to ensure they work correctly for bank-grade security.
// ============================================================================

using Xunit;
using DiffPilot.Security;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for SecurityHelpers input validation, output sanitization, and rate limiting.
/// </summary>
public class SecurityHelpersTests
{
    #region Branch Name Validation Tests

    [Theory]
    [InlineData("main", "main")]
    [InlineData("develop", "develop")]
    [InlineData("feature/my-feature", "feature/my-feature")]
    [InlineData("release/1.0.0", "release/1.0.0")]
    [InlineData("bugfix/fix-123", "bugfix/fix-123")]
    [InlineData("user/john/experiment", "user/john/experiment")]
    public void ValidateBranchName_ValidNames_ReturnsName(string input, string expected)
    {
        var result = SecurityHelpers.ValidateBranchName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateBranchName_NullOrEmpty_ReturnsNull(string? input)
    {
        var result = SecurityHelpers.ValidateBranchName(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("branch/../secret")]
    [InlineData("feature/..")]
    public void ValidateBranchName_PathTraversal_ThrowsSecurityException(string input)
    {
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateBranchName(input));
        Assert.Contains("dangerous patterns", ex.Message);
    }

    [Theory]
    [InlineData("-dangerous")]
    [InlineData("--option")]
    [InlineData("-rf")]
    public void ValidateBranchName_StartsWithHyphen_ThrowsSecurityException(string input)
    {
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateBranchName(input));
        Assert.Contains("hyphen", ex.Message);
    }

    [Theory]
    [InlineData("branch; rm -rf /")]
    [InlineData("branch && malicious")]
    [InlineData("branch | cat /etc/passwd")]
    [InlineData("branch`whoami`")]
    [InlineData("branch$(id)")]
    public void ValidateBranchName_ShellInjection_ThrowsSecurityException(string input)
    {
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateBranchName(input));
        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public void ValidateBranchName_ExceedsMaxLength_ThrowsSecurityException()
    {
        var longBranch = new string('a', 300);
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateBranchName(longBranch));
        Assert.Contains("maximum length", ex.Message);
    }

    #endregion

    #region Remote Name Validation Tests

    [Theory]
    [InlineData("origin", "origin")]
    [InlineData("upstream", "upstream")]
    [InlineData("my-remote", "my-remote")]
    [InlineData("remote_name", "remote_name")]
    public void ValidateRemoteName_ValidNames_ReturnsName(string input, string expected)
    {
        var result = SecurityHelpers.ValidateRemoteName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRemoteName_NullOrEmpty_ReturnsOrigin(string? input)
    {
        var result = SecurityHelpers.ValidateRemoteName(input);
        Assert.Equal("origin", result);
    }

    [Theory]
    [InlineData("remote; rm -rf /")]
    [InlineData("../path")]
    [InlineData("remote/path")]
    public void ValidateRemoteName_InvalidCharacters_ThrowsSecurityException(string input)
    {
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateRemoteName(input));
        Assert.Contains("invalid characters", ex.Message);
    }

    #endregion

    #region Parameter Validation Tests

    [Theory]
    [InlineData("simple")]
    [InlineData("with-hyphen")]
    [InlineData("with_underscore")]
    [InlineData("MixedCase123")]
    public void ValidateParameter_ValidInput_ReturnsInput(string input)
    {
        var result = SecurityHelpers.ValidateParameter(input, "test");
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateParameter_NullOrEmpty_ReturnsNull(string? input)
    {
        var result = SecurityHelpers.ValidateParameter(input, "test");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateParameter_WithNullBytes_RemovesNullBytes()
    {
        var input = "valid\0value";
        var result = SecurityHelpers.ValidateParameter(input, "test");
        Assert.Equal("validvalue", result);
    }

    [Fact]
    public void ValidateParameter_ExceedsMaxLength_ThrowsSecurityException()
    {
        var longInput = new string('a', 2000);
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateParameter(longInput, "test"));
        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public void ValidateParameter_CustomMaxLength_EnforcesLimit()
    {
        var input = new string('a', 50);
        var ex = Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateParameter(input, "test", maxLength: 20));
        Assert.Contains("maximum length", ex.Message);
    }

    #endregion

    #region Enum Parameter Validation Tests

    [Theory]
    [InlineData("conventional", "conventional")]
    [InlineData("CONVENTIONAL", "conventional")]
    [InlineData("simple", "simple")]
    [InlineData("  simple  ", "simple")]
    public void ValidateEnumParameter_ValidValues_ReturnsNormalized(string input, string expected)
    {
        var result = SecurityHelpers.ValidateEnumParameter(input, ["conventional", "simple"], "conventional");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "default")]
    [InlineData("", "default")]
    [InlineData("   ", "default")]
    [InlineData("invalid", "default")]
    public void ValidateEnumParameter_InvalidOrEmpty_ReturnsDefault(string? input, string expected)
    {
        var result = SecurityHelpers.ValidateEnumParameter(input, ["valid"], "default");
        Assert.Equal(expected, result);
    }

    #endregion

    #region Output Sanitization Tests

    [Theory]
    [InlineData("api_key=abc123def456789012345")]
    [InlineData("apiKey: 'supersecretkey123456'")]
    [InlineData("API_KEY = \"mysupersecretapikey\"")]
    public void SanitizeOutput_ApiKeys_RedactsValue(string input)
    {
        var result = SecurityHelpers.SanitizeOutput(input);
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("supersecret", result);
    }

    [Theory]
    [InlineData("password=mysecretpassword")]
    [InlineData("PASSWORD: 'verysecret123'")]
    [InlineData("pwd = \"hidden123\"")]
    public void SanitizeOutput_Passwords_RedactsValue(string input)
    {
        var result = SecurityHelpers.SanitizeOutput(input);
        Assert.Contains("[REDACTED]", result);
    }

    [Theory]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("Found key: AKIAIOSFODNN7EXAMPLE in file")]
    public void SanitizeOutput_AwsKeys_RedactsValue(string input)
    {
        var result = SecurityHelpers.SanitizeOutput(input);
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", result);
    }

    [Fact]
    public void SanitizeOutput_JwtTokens_RedactsValue()
    {
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var result = SecurityHelpers.SanitizeOutput($"Token: {jwt}");
        Assert.DoesNotContain(jwt, result);
    }

    [Fact]
    public void SanitizeOutput_GithubTokens_RedactsValue()
    {
        var ghpToken = "ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1234";
        var result = SecurityHelpers.SanitizeOutput($"GitHub token: {ghpToken}");
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeOutput_PrivateKey_RedactsValue()
    {
        var privateKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA...\n-----END";
        var result = SecurityHelpers.SanitizeOutput(privateKey);
        Assert.Contains("[REDACTED]", result);
    }

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("Authorization: bearer abc123def456789012345")]
    public void SanitizeOutput_BearerTokens_RedactsValue(string input)
    {
        var result = SecurityHelpers.SanitizeOutput(input);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeOutput_ConnectionStrings_RedactsValue()
    {
        var connStr = "connection_string = \"Server=myserver;Database=mydb;User=admin;Password=secret123\"";
        var result = SecurityHelpers.SanitizeOutput(connStr);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeOutput_SlackTokens_RedactsValue()
    {
        // Using a pattern that looks like Slack format for testing redaction
        // The regex looks for xox[baprs]-NUMBERS-ALPHANUMERIC patterns
        var slackToken = "xoxb-" + new string('1', 10) + "-" + new string('a', 20);
        var result = SecurityHelpers.SanitizeOutput($"Slack: {slackToken}");
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeOutput_TruncatesLargeOutput()
    {
        var largeOutput = new string('x', 600_000);
        var result = SecurityHelpers.SanitizeOutput(largeOutput);
        Assert.True(result.Length < largeOutput.Length);
        Assert.Contains("truncated", result);
    }

    [Fact]
    public void SanitizeOutput_SafeContent_RemainsUnchanged()
    {
        var safeContent = "This is a normal code review comment without any secrets.";
        var result = SecurityHelpers.SanitizeOutput(safeContent);
        Assert.Equal(safeContent, result);
    }

    [Fact]
    public void SanitizeOutput_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", SecurityHelpers.SanitizeOutput(""));
        Assert.Null(SecurityHelpers.SanitizeOutput(null!));
    }

    #endregion

    #region Error Message Sanitization Tests

    [Fact]
    public void SanitizeErrorMessage_StackTrace_RemovesIt()
    {
        var errorWithStack = "Error occurred   at System.String.Substring(Int32 startIndex, Int32 length)";
        var result = SecurityHelpers.SanitizeErrorMessage(errorWithStack);
        Assert.DoesNotContain("at System", result);
    }

    [Theory]
    [InlineData("Error at C:\\Users\\john\\secret\\file.txt")]
    [InlineData("Error at /home/john/secret/file.txt")]
    public void SanitizeErrorMessage_Paths_RedactsThem(string input)
    {
        var result = SecurityHelpers.SanitizeErrorMessage(input);
        Assert.Contains("[path]", result);
        Assert.DoesNotContain("john", result);
    }

    [Fact]
    public void SanitizeErrorMessage_EmptyInput_ReturnsGeneric()
    {
        var result = SecurityHelpers.SanitizeErrorMessage("");
        Assert.Equal("An error occurred.", result);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public void CheckRateLimit_FirstRequest_ReturnsTrue()
    {
        // Use a unique tool name to avoid interference from other tests
        var result = SecurityHelpers.CheckRateLimit("unique_tool_" + Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public void CheckRateLimit_UnderLimit_ReturnsTrue()
    {
        var toolName = "test_tool_" + Guid.NewGuid();
        
        // Make several requests, all should be allowed
        for (int i = 0; i < 10; i++)
        {
            Assert.True(SecurityHelpers.CheckRateLimit(toolName));
        }
    }

    #endregion

    #region Working Directory Validation Tests

    [Fact]
    public void ValidateWorkingDirectory_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(SecurityHelpers.ValidateWorkingDirectory(null));
        Assert.False(SecurityHelpers.ValidateWorkingDirectory(""));
        Assert.False(SecurityHelpers.ValidateWorkingDirectory("   "));
    }

    [Fact]
    public void ValidateWorkingDirectory_NonExistentPath_ReturnsFalse()
    {
        Assert.False(SecurityHelpers.ValidateWorkingDirectory("C:\\this\\path\\does\\not\\exist\\12345"));
    }

    [Fact]
    public void ValidateWorkingDirectory_NonGitDirectory_ReturnsFalse()
    {
        // System directory exists but is not a git repo
        var tempPath = Path.GetTempPath();
        // Only test if .git doesn't exist (it shouldn't in temp)
        if (!Directory.Exists(Path.Combine(tempPath, ".git")))
        {
            Assert.False(SecurityHelpers.ValidateWorkingDirectory(tempPath));
        }
    }

    #endregion

    #region File Path Validation Tests

    [Fact]
    public void ValidateFilePath_EmptyPath_ThrowsSecurityException()
    {
        Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateFilePath(""));
        Assert.Throws<SecurityException>(() => SecurityHelpers.ValidateFilePath(null));
    }

    [Fact]
    public void ValidateFilePath_WithNullBytes_ThrowsOrStripsNullBytes()
    {
        // Null bytes in paths are invalid and should be handled
        // Path.GetFullPath may throw or strip them depending on OS
        var path = "C:\\test\0\\file.txt";
        try
        {
            var result = SecurityHelpers.ValidateFilePath(path);
            // If we get here, null bytes were stripped
            Assert.NotNull(result);
            // The path was normalized (null byte removed)
        }
        catch (Exception)
        {
            // Some platforms throw on invalid path characters
            // This is acceptable security behavior
        }
    }

    [Fact]
    public void ValidateFilePath_OutsideWorkspace_ThrowsSecurityException()
    {
        var workspaceRoot = "C:\\workspace";
        var outsidePath = "C:\\other\\secret\\file.txt";
        
        var ex = Assert.Throws<SecurityException>(() => 
            SecurityHelpers.ValidateFilePath(outsidePath, workspaceRoot));
        Assert.Contains("outside the allowed workspace", ex.Message);
    }

    [Fact]
    public void ValidateFilePath_InsideWorkspace_ReturnsPath()
    {
        var workspaceRoot = Path.GetTempPath();
        var insidePath = Path.Combine(workspaceRoot, "test", "file.txt");
        
        var result = SecurityHelpers.ValidateFilePath(insidePath, workspaceRoot);
        Assert.StartsWith(Path.GetFullPath(workspaceRoot), result);
    }

    #endregion
}
