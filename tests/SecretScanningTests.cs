using System.Text.RegularExpressions;
using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for secret scanning regex patterns used in scan_secrets tool.
/// These tests verify that secret patterns correctly detect real secrets
/// while avoiding false positives on normal code.
/// </summary>
public class SecretScanningTests
{
    // AWS Access Key pattern: AKIA followed by 16 alphanumeric characters
    private static readonly Regex AwsKeyPattern = new(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled);

    // GitHub Token patterns
    private static readonly Regex GitHubTokenPattern = new(
        @"gh[pousr]_[A-Za-z0-9_]{36,255}",
        RegexOptions.Compiled
    );

    // Generic API key pattern
    private static readonly Regex ApiKeyPattern = new(
        @"(?i)(api[_-]?key|apikey)\s*[:=]\s*[""']?([A-Za-z0-9_\-]{20,})[""']?",
        RegexOptions.Compiled
    );

    // Password in config pattern
    private static readonly Regex PasswordPattern = new(
        @"(?i)(password|passwd|pwd)\s*[:=]\s*[""']?([^\s""']{8,})[""']?",
        RegexOptions.Compiled
    );

    // JWT Token pattern
    private static readonly Regex JwtPattern = new(
        @"eyJ[A-Za-z0-9_-]*\.eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*",
        RegexOptions.Compiled
    );

    // Private Key header pattern
    private static readonly Regex PrivateKeyPattern = new(
        @"-----BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----",
        RegexOptions.Compiled
    );

    #region AWS Key Detection Tests

    [Theory]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("AKIAI44QH8DHBEXAMPLE")]
    [InlineData("AKIAEXAMPLE123456789")]
    public void AwsKeyPattern_DetectsValidAwsKeys(string key)
    {
        Assert.True(AwsKeyPattern.IsMatch(key), $"Should detect AWS key: {key}");
    }

    [Theory]
    [InlineData("AKIA123")] // Too short
    [InlineData("AKZA1234567890123456")] // Wrong prefix
    [InlineData("akiaiosfodnn7example")] // Lowercase (AWS keys are uppercase)
    [InlineData("const awsRegion = 'us-east-1'")] // Normal code
    public void AwsKeyPattern_IgnoresInvalidPatterns(string text)
    {
        Assert.False(AwsKeyPattern.IsMatch(text), $"Should not match: {text}");
    }

    #endregion

    #region GitHub Token Detection Tests

    [Theory]
    [InlineData("ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234")] // Personal access token
    [InlineData("gho_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234")] // OAuth token
    [InlineData("ghu_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234")] // User-to-server token
    [InlineData("ghs_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234")] // Server-to-server token
    [InlineData("ghr_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234")] // Refresh token
    public void GitHubTokenPattern_DetectsValidTokens(string token)
    {
        Assert.True(GitHubTokenPattern.IsMatch(token), $"Should detect GitHub token: {token}");
    }

    [Theory]
    [InlineData("ghx_invalid")] // Invalid prefix
    [InlineData("ghp_short")] // Too short
    [InlineData("github.com/user/repo")] // Normal GitHub URL
    public void GitHubTokenPattern_IgnoresInvalidPatterns(string text)
    {
        Assert.False(GitHubTokenPattern.IsMatch(text), $"Should not match: {text}");
    }

    #endregion

    #region API Key Detection Tests

    [Theory]
    [InlineData("api_key=abc123def456ghi789jkl012")]
    [InlineData("API_KEY: 'abc123def456ghi789jkl012mno'")]
    [InlineData("apikey=\"abc123def456ghi789jkl012\"")]
    [InlineData("api-key = abc123def456ghi789jkl012")]
    public void ApiKeyPattern_DetectsApiKeys(string line)
    {
        Assert.True(ApiKeyPattern.IsMatch(line), $"Should detect API key in: {line}");
    }

    [Theory]
    [InlineData("// This function returns the API key")] // Comment
    [InlineData("const apiKeyLength = 32")] // Variable about API keys
    [InlineData("validateApiKey(key)")] // Function call
    public void ApiKeyPattern_IgnoresNormalCode(string code)
    {
        Assert.False(ApiKeyPattern.IsMatch(code), $"Should not match normal code: {code}");
    }

    #endregion

    #region Password Detection Tests

    [Theory]
    [InlineData("password=MySecr3tP@ss!")]
    [InlineData("PASSWORD: 'SuperSecr3t123'")]
    [InlineData("pwd=\"database_password_123\"")]
    [InlineData("passwd: my-secret-password")]
    public void PasswordPattern_DetectsPasswords(string line)
    {
        Assert.True(PasswordPattern.IsMatch(line), $"Should detect password in: {line}");
    }

    [Theory]
    [InlineData("password=")] // No value
    [InlineData("password=short")] // Too short (less than 8 chars)
    [InlineData("// TODO: add password validation")] // Comment
    public void PasswordPattern_IgnoresEmptyOrShort(string text)
    {
        Assert.False(PasswordPattern.IsMatch(text), $"Should not match: {text}");
    }

    #endregion

    #region JWT Token Detection Tests

    [Fact]
    public void JwtPattern_DetectsValidJwt()
    {
        // A real JWT structure (header.payload.signature)
        var jwt =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        Assert.Matches(JwtPattern, jwt);
    }

    [Theory]
    [InlineData("eyJ")] // Too short, missing parts
    [InlineData("notAJwt.atAll.really")] // Not starting with eyJ
    [InlineData("base64EncodedData")] // Random base64-ish text
    public void JwtPattern_IgnoresNonJwt(string text)
    {
        Assert.False(JwtPattern.IsMatch(text), $"Should not match: {text}");
    }

    #endregion

    #region Private Key Detection Tests

    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN EC PRIVATE KEY-----")]
    [InlineData("-----BEGIN DSA PRIVATE KEY-----")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----")]
    public void PrivateKeyPattern_DetectsKeyHeaders(string header)
    {
        Assert.True(
            PrivateKeyPattern.IsMatch(header),
            $"Should detect private key header: {header}"
        );
    }

    [Theory]
    [InlineData("-----BEGIN PUBLIC KEY-----")] // Public key, not private
    [InlineData("-----BEGIN CERTIFICATE-----")] // Certificate
    [InlineData("private key")] // Just text
    public void PrivateKeyPattern_IgnoresNonPrivateKeys(string text)
    {
        Assert.False(PrivateKeyPattern.IsMatch(text), $"Should not match: {text}");
    }

    #endregion

    #region Secret Masking Tests

    [Fact]
    public void MaskSecret_HidesMiddleOfSecret()
    {
        var secret = "AKIAIOSFODNN7EXAMPLE";
        var masked = MaskSecret(secret);

        Assert.StartsWith("AKIA", masked);
        Assert.EndsWith("MPLE", masked);
        Assert.Contains("****", masked);
        Assert.NotEqual(secret, masked);
    }

    [Fact]
    public void MaskSecret_HandlesShortSecrets()
    {
        var shortSecret = "abc";
        var masked = MaskSecret(shortSecret);

        Assert.Equal("***", masked);
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return new string('*', secret.Length);

        var visibleStart = secret.Length / 5;
        var visibleEnd = secret.Length / 5;
        return secret[..visibleStart]
            + new string('*', secret.Length - visibleStart - visibleEnd)
            + secret[^visibleEnd..];
    }

    #endregion
}
