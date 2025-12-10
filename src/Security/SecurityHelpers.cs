// ============================================================================
// SecurityHelpers.cs
// ============================================================================
// Security utilities for protecting the MCP server and user data.
// Implements OWASP security best practices and MCP security guidelines.
//
// Security features implemented:
// - CWE-20:  Input Validation
// - CWE-22:  Path Traversal Prevention
// - CWE-78:  OS Command Injection Prevention
// - CWE-158: Null Byte Injection Prevention
// - CWE-200: Sensitive Information Exposure Prevention
// - CWE-400: Resource Consumption (DoS) Prevention
// - CWE-532: Log Injection Prevention
// ============================================================================

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DiffPilot.Security;

/// <summary>
/// Exception thrown when a security violation is detected.
/// </summary>
internal sealed class SecurityException(string message) : Exception(message);

/// <summary>
/// Security utilities for protecting the MCP server and user data.
/// </summary>
internal static partial class SecurityHelpers
{
    // ========================================================================
    // Security Configuration Constants
    // ========================================================================

    /// <summary>
    /// Maximum allowed input length to prevent DoS attacks.
    /// </summary>
    private const int MaxInputLength = 100_000;

    /// <summary>
    /// Maximum allowed parameter value length.
    /// </summary>
    private const int MaxParameterLength = 1_000;

    /// <summary>
    /// Maximum output size to prevent memory exhaustion.
    /// </summary>
    private const int MaxOutputSize = 500_000;

    /// <summary>
    /// Rate limiting: max requests per minute per tool.
    /// </summary>
    private const int MaxRequestsPerMinute = 120;

    /// <summary>
    /// Maximum branch name length.
    /// </summary>
    private const int MaxBranchNameLength = 256;

    /// <summary>
    /// Maximum remote name length.
    /// </summary>
    private const int MaxRemoteNameLength = 100;

    // ========================================================================
    // Rate Limiting State
    // ========================================================================

    /// <summary>
    /// Track request counts for rate limiting.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> s_rateLimits = new();

    // ========================================================================
    // Sensitive Data Patterns for Redaction
    // Based on CWE-200 (Exposure of Sensitive Information) prevention.
    // ========================================================================

    private static readonly Regex[] s_sensitivePatterns =
    [
        SensitiveApiKeyPattern(),
        SensitivePasswordPattern(),
        SensitiveTokenPattern(),
        SensitiveSecretPattern(),
        SensitiveConnectionStringPattern(),
        SensitivePrivateKeyPattern(),
        SensitiveBearerPattern(),
        SensitiveAwsKeyPattern(),
        SensitiveJwtPattern(),
        SensitiveSlackTokenPattern(),
        SensitiveGitHubTokenPattern(),
        SensitiveAzureKeyPattern(),
        SensitiveGenericKeyPattern(),
    ];

    // ========================================================================
    // Input Validation Methods
    // ========================================================================

    /// <summary>
    /// Validates and sanitizes a tool parameter to prevent injection attacks.
    /// </summary>
    /// <param name="value">The parameter value to validate.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <param name="allowedPattern">Optional regex pattern for allowed values.</param>
    /// <param name="maxLength">Optional maximum length (defaults to MaxParameterLength).</param>
    /// <returns>Sanitized value or null if input was null/empty.</returns>
    /// <exception cref="SecurityException">Thrown if validation fails.</exception>
    public static string? ValidateParameter(
        string? value,
        string paramName,
        Regex? allowedPattern = null,
        int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Length check to prevent DoS
        var max = maxLength ?? MaxParameterLength;
        if (value.Length > max)
        {
            LogSecurityEvent("INPUT_VALIDATION_FAILED", $"Parameter '{paramName}' exceeds max length ({value.Length} > {max})");
            throw new SecurityException($"Parameter '{paramName}' exceeds maximum length of {max} characters.");
        }

        // Remove null bytes (CWE-158: Improper Neutralization of Null Byte)
        if (value.Contains('\0'))
        {
            LogSecurityEvent("NULL_BYTE_DETECTED", $"Parameter '{paramName}' contains null bytes");
            value = value.Replace("\0", "");
        }

        // Validate against allowed pattern if provided
        if (allowedPattern is not null && !allowedPattern.IsMatch(value))
        {
            LogSecurityEvent("PATTERN_VALIDATION_FAILED", $"Parameter '{paramName}' failed pattern validation");
            throw new SecurityException($"Parameter '{paramName}' contains invalid characters.");
        }

        return value;
    }

    /// <summary>
    /// Validates a branch name parameter (CWE-78: OS Command Injection prevention).
    /// </summary>
    /// <param name="value">The branch name to validate.</param>
    /// <param name="paramName">Parameter name for error messages.</param>
    /// <returns>Validated branch name or null if input was null/empty.</returns>
    /// <exception cref="SecurityException">Thrown if validation fails.</exception>
    public static string? ValidateBranchName(string? value, string paramName = "branch")
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Length check
        if (value.Length > MaxBranchNameLength)
        {
            LogSecurityEvent("BRANCH_VALIDATION_FAILED", $"Branch '{paramName}' exceeds max length");
            throw new SecurityException($"Branch name '{paramName}' exceeds maximum length of {MaxBranchNameLength} characters.");
        }

        // Strict validation for branch names - alphanumeric, slash, underscore, hyphen, dot
        if (!BranchNamePattern().IsMatch(value))
        {
            LogSecurityEvent("BRANCH_VALIDATION_FAILED", $"Branch '{paramName}' contains invalid characters");
            throw new SecurityException($"Branch name '{paramName}' contains invalid characters. Only alphanumeric, slash, underscore, hyphen, and dot are allowed.");
        }

        // Prevent path traversal in branch names
        if (value.Contains(".."))
        {
            LogSecurityEvent("PATH_TRAVERSAL_ATTEMPT", $"Branch '{paramName}' contains '..'");
            throw new SecurityException($"Branch name '{paramName}' contains potentially dangerous patterns.");
        }

        // Prevent options injection (branch names starting with -)
        if (value.StartsWith('-'))
        {
            LogSecurityEvent("OPTION_INJECTION_ATTEMPT", $"Branch '{paramName}' starts with '-'");
            throw new SecurityException($"Branch name '{paramName}' cannot start with a hyphen.");
        }

        return value;
    }

    /// <summary>
    /// Validates a remote name parameter.
    /// </summary>
    /// <param name="value">The remote name to validate.</param>
    /// <returns>Validated remote name or "origin" as safe default.</returns>
    /// <exception cref="SecurityException">Thrown if validation fails.</exception>
    public static string ValidateRemoteName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "origin"; // Safe default

        if (value.Length > MaxRemoteNameLength)
        {
            LogSecurityEvent("REMOTE_VALIDATION_FAILED", "Remote name exceeds max length");
            throw new SecurityException("Remote name exceeds maximum length.");
        }

        if (!RemoteNamePattern().IsMatch(value))
        {
            LogSecurityEvent("REMOTE_VALIDATION_FAILED", "Remote name contains invalid characters");
            throw new SecurityException("Remote name contains invalid characters. Only alphanumeric, underscore, and hyphen are allowed.");
        }

        return value;
    }

    /// <summary>
    /// Validates a file path to prevent path traversal attacks (CWE-22).
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="workspaceRoot">Optional workspace root to ensure path is within bounds.</param>
    /// <returns>Validated and normalized file path.</returns>
    /// <exception cref="SecurityException">Thrown if validation fails.</exception>
    public static string ValidateFilePath(string? filePath, string? workspaceRoot = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new SecurityException("File path cannot be empty.");

        // Remove null bytes
        filePath = filePath.Replace("\0", "");

        // Normalize the path
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            LogSecurityEvent("PATH_VALIDATION_FAILED", $"Invalid path format: {ex.Message}");
            throw new SecurityException("Invalid file path format.");
        }

        // If workspace root is provided, ensure path is within it
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            var normalizedRoot = Path.GetFullPath(workspaceRoot);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                LogSecurityEvent("PATH_TRAVERSAL_ATTEMPT", "Attempted access outside workspace root");
                throw new SecurityException("Access denied: Path is outside the allowed workspace.");
            }
        }

        return normalizedPath;
    }

    /// <summary>
    /// Validates a commit style parameter.
    /// </summary>
    /// <param name="style">The style to validate.</param>
    /// <param name="allowedValues">Array of allowed values.</param>
    /// <param name="defaultValue">Default value if input is null/empty.</param>
    /// <returns>Validated style or default value.</returns>
    public static string ValidateEnumParameter(string? style, string[] allowedValues, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(style))
            return defaultValue;

        var normalized = style.Trim().ToLowerInvariant();
        if (Array.Exists(allowedValues, v => v.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return normalized;

        LogSecurityEvent("ENUM_VALIDATION_FAILED", $"Invalid value '{style}', using default '{defaultValue}'");
        return defaultValue;
    }

    // ========================================================================
    // Rate Limiting
    // ========================================================================

    /// <summary>
    /// Checks rate limit for a tool. Returns true if within limits.
    /// Implements protection against DoS (CWE-400: Uncontrolled Resource Consumption).
    /// </summary>
    /// <param name="toolName">The tool being called.</param>
    /// <returns>True if the request is allowed, false if rate limited.</returns>
    public static bool CheckRateLimit(string toolName)
    {
        var now = DateTime.UtcNow;

        s_rateLimits.AddOrUpdate(
            toolName,
            _ => (now, 1),
            (_, existing) =>
            {
                // Reset window if more than a minute has passed
                if ((now - existing.WindowStart).TotalMinutes >= 1)
                    return (now, 1);

                return (existing.WindowStart, existing.Count + 1);
            });

        var (_, count) = s_rateLimits[toolName];

        if (count > MaxRequestsPerMinute)
        {
            LogSecurityEvent("RATE_LIMIT_EXCEEDED", $"Tool '{toolName}' rate limit exceeded ({count} requests)");
            return false;
        }

        return true;
    }

    // ========================================================================
    // Output Sanitization
    // ========================================================================

    /// <summary>
    /// Sanitizes output to remove or redact sensitive information.
    /// Prevents CWE-200: Exposure of Sensitive Information to an Unauthorized Actor.
    /// </summary>
    /// <param name="output">The output string to sanitize.</param>
    /// <param name="redactPaths">Whether to redact absolute paths (default: false).</param>
    /// <returns>Sanitized output string.</returns>
    public static string SanitizeOutput(string output, bool redactPaths = false)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        // Truncate if too large to prevent memory issues
        if (output.Length > MaxOutputSize)
        {
            output = output[..MaxOutputSize] +
                $"\n\n[Output truncated at {MaxOutputSize:N0} characters for security reasons]";
            LogSecurityEvent("OUTPUT_TRUNCATED", $"Output exceeded {MaxOutputSize} characters");
        }

        // Redact sensitive patterns from output
        foreach (var pattern in s_sensitivePatterns)
        {
            output = pattern.Replace(output, match =>
            {
                // Preserve some structure but redact the sensitive value
                var value = match.Value;
                if (value.Length <= 8)
                    return "[REDACTED]";

                // Show first few chars of key name and redact value
                var colonIndex = value.IndexOfAny([':', '=']);
                if (colonIndex > 0 && colonIndex < value.Length - 1)
                {
                    return value[..(colonIndex + 1)] + "[REDACTED]";
                }

                return value[..4] + "[REDACTED]";
            });
        }

        // Optionally remove any absolute paths that might leak system info
        if (redactPaths)
        {
            output = WindowsAbsolutePathPattern().Replace(output, "[PATH]");
            output = UnixAbsolutePathPattern().Replace(output, "[PATH]");
        }

        return output;
    }

    /// <summary>
    /// Sanitizes error messages to prevent information disclosure.
    /// </summary>
    /// <param name="errorMessage">The error message to sanitize.</param>
    /// <returns>Sanitized error message safe for external display.</returns>
    public static string SanitizeErrorMessage(string errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "An error occurred.";

        // Remove stack traces
        var stackTraceIndex = errorMessage.IndexOf("   at ", StringComparison.Ordinal);
        if (stackTraceIndex > 0)
            errorMessage = errorMessage[..stackTraceIndex].Trim();

        // Remove absolute paths from error messages
        errorMessage = WindowsAbsolutePathPattern().Replace(errorMessage, "[path]");
        errorMessage = UnixAbsolutePathPattern().Replace(errorMessage, "[path]");

        // Redact any sensitive data
        return SanitizeOutput(errorMessage);
    }

    // ========================================================================
    // Working Directory Validation
    // ========================================================================

    /// <summary>
    /// Validates the working directory is a git repository and within allowed paths.
    /// Prevents CWE-22: Path Traversal.
    /// </summary>
    /// <param name="directory">The directory to validate.</param>
    /// <returns>True if directory is valid git repository, false otherwise.</returns>
    public static bool ValidateWorkingDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        try
        {
            // Resolve to full path to detect path traversal
            var fullPath = Path.GetFullPath(directory);

            // Check directory exists
            if (!Directory.Exists(fullPath))
            {
                LogSecurityEvent("WORKSPACE_VALIDATION_FAILED", "Directory does not exist");
                return false;
            }

            // Check it's a git repository
            var gitDir = Path.Combine(fullPath, ".git");
            var isGitRepo = Directory.Exists(gitDir) || File.Exists(gitDir); // .git can be a file for worktrees

            if (!isGitRepo)
            {
                LogSecurityEvent("WORKSPACE_VALIDATION_FAILED", "Directory is not a git repository");
            }

            return isGitRepo;
        }
        catch (Exception ex)
        {
            LogSecurityEvent("WORKSPACE_VALIDATION_FAILED", $"Exception: {ex.Message}");
            return false;
        }
    }

    // ========================================================================
    // Security Logging
    // ========================================================================

    /// <summary>
    /// Logs security events to stderr (never stdout per MCP spec).
    /// Implements CWE-532 safe logging (no sensitive data in logs).
    /// </summary>
    /// <param name="eventType">The type of security event.</param>
    /// <param name="details">Details about the event (will be sanitized).</param>
    public static void LogSecurityEvent(string eventType, string details)
    {
        // Sanitize details to prevent log injection (CWE-117)
        details = details
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");

        // Keep log entry short to prevent log flooding
        if (details.Length > 200)
            details = details[..200] + "...";

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        Console.Error.WriteLine($"[SECURITY] [{timestamp}] {eventType}: {details}");
    }

    // ========================================================================
    // Compiled Regex Patterns (for performance)
    // ========================================================================

    // Input validation patterns
    [GeneratedRegex(@"^[a-zA-Z0-9/_.\-]+$", RegexOptions.Compiled)]
    private static partial Regex BranchNamePattern();

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled)]
    private static partial Regex RemoteNamePattern();

    // Sensitive data patterns for redaction
    [GeneratedRegex(@"(?i)(api[_\-]?key|apikey)\s*['""]?\s*[:=]\s*['""]?[\w\-]{16,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveApiKeyPattern();

    [GeneratedRegex(@"(?i)(password|passwd|pwd)\s*['""]?\s*[:=]\s*['""]?[^\s'""]{6,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitivePasswordPattern();

    [GeneratedRegex(@"(?i)(token|auth[_\-]?token|access[_\-]?token)\s*['""]?\s*[:=]\s*['""]?[\w\-\.]{20,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveTokenPattern();

    [GeneratedRegex(@"(?i)(secret|private[_\-]?key|client[_\-]?secret)\s*['""]?\s*[:=]\s*['""]?[\w\-\.]{16,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveSecretPattern();

    [GeneratedRegex(@"(?i)(connection[_\-]?string|connstr|database[_\-]?url)\s*['""]?\s*[:=]\s*['""]?[^\n]{20,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveConnectionStringPattern();

    [GeneratedRegex(@"-----BEGIN\s+(RSA|DSA|EC|OPENSSH|PRIVATE)?\s*PRIVATE\s+KEY-----[\s\S]*?-----END", RegexOptions.Compiled)]
    private static partial Regex SensitivePrivateKeyPattern();

    [GeneratedRegex(@"(?i)bearer\s+[\w\-\.]{20,}", RegexOptions.Compiled)]
    private static partial Regex SensitiveBearerPattern();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)]
    private static partial Regex SensitiveAwsKeyPattern();

    [GeneratedRegex(@"eyJ[a-zA-Z0-9_\-]*\.eyJ[a-zA-Z0-9_\-]*\.[a-zA-Z0-9_\-]*", RegexOptions.Compiled)]
    private static partial Regex SensitiveJwtPattern();

    [GeneratedRegex(@"xox[baprs]\-[0-9]{10,}", RegexOptions.Compiled)]
    private static partial Regex SensitiveSlackTokenPattern();

    [GeneratedRegex(@"(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,}", RegexOptions.Compiled)]
    private static partial Regex SensitiveGitHubTokenPattern();

    [GeneratedRegex(@"(?i)azure[_\-]?(?:storage|key|secret|connection)\s*[:=]\s*['""]?[\w\-\./+=]{20,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveAzureKeyPattern();

    [GeneratedRegex(@"(?i)(?:key|secret|credential)\s*[:=]\s*['""]?[\w\-]{32,}['""]?", RegexOptions.Compiled)]
    private static partial Regex SensitiveGenericKeyPattern();

    // Path patterns for redaction
    [GeneratedRegex(@"[A-Za-z]:\\(?:Users|Windows|Program Files)[^\s\n""']*", RegexOptions.Compiled)]
    private static partial Regex WindowsAbsolutePathPattern();

    [GeneratedRegex(@"/(?:home|Users|var|etc|tmp|root)/[^\s\n""']*", RegexOptions.Compiled)]
    private static partial Regex UnixAbsolutePathPattern();
}
