/**
 * SecurityHelpers - Security utilities for protecting the MCP server and user data.
 *
 * Security features implemented:
 * - CWE-20:  Input Validation
 * - CWE-22:  Path Traversal Prevention
 * - CWE-78:  OS Command Injection Prevention
 * - CWE-158: Null Byte Injection Prevention
 * - CWE-200: Sensitive Information Exposure Prevention
 * - CWE-400: Resource Consumption (DoS) Prevention
 * - CWE-532: Log Injection Prevention
 *
 * Ported from: src/Security/SecurityHelpers.cs
 */
/** Exception thrown when a security violation is detected */
export declare class SecurityError extends Error {
    constructor(message: string);
}
/**
 * Validates and sanitizes a tool parameter to prevent injection attacks.
 * @param value The parameter value to validate
 * @param paramName The parameter name for error messages
 * @param options Optional configuration
 * @returns Sanitized value or null if input was null/empty
 * @throws SecurityError if validation fails
 */
export declare function validateParameter(value: string | null | undefined, paramName: string, options?: {
    allowedPattern?: RegExp;
    maxLength?: number;
}): string | null;
/**
 * Validates a branch name parameter (CWE-78: OS Command Injection prevention).
 * @param value The branch name to validate
 * @param paramName Parameter name for error messages
 * @returns Validated branch name or null if input was null/empty
 * @throws SecurityError if validation fails
 */
export declare function validateBranchName(value: string | null | undefined, paramName?: string): string | null;
/**
 * Validates a remote name parameter.
 * @param value The remote name to validate
 * @returns Validated remote name or "origin" as safe default
 * @throws SecurityError if validation fails
 */
export declare function validateRemoteName(value: string | null | undefined): string;
/**
 * Validates a file path to prevent path traversal attacks (CWE-22).
 * @param filePath The file path to validate
 * @param workspaceRoot Optional workspace root to ensure path is within bounds
 * @returns Validated and normalized file path
 * @throws SecurityError if validation fails
 */
export declare function validateFilePath(filePath: string | null | undefined, workspaceRoot?: string): string;
/**
 * Validates an enum-style parameter.
 * @param value The value to validate
 * @param allowedValues Array of allowed values
 * @param defaultValue Default value if input is null/empty
 * @returns Validated value or default value
 */
export declare function validateEnumParameter(value: string | null | undefined, allowedValues: string[], defaultValue: string): string;
/**
 * Checks rate limit for a tool. Returns true if within limits.
 * Implements protection against DoS (CWE-400: Uncontrolled Resource Consumption).
 * @param toolName The tool being called
 * @returns True if the request is allowed, false if rate limited
 */
export declare function checkRateLimit(toolName: string): boolean;
/**
 * Resets rate limit state (for testing purposes).
 */
export declare function resetRateLimits(): void;
/**
 * Sanitizes output to remove or redact sensitive information.
 * Prevents CWE-200: Exposure of Sensitive Information to an Unauthorized Actor.
 * @param output The output string to sanitize
 * @param redactPaths Whether to redact absolute paths (default: false)
 * @returns Sanitized output string
 */
export declare function sanitizeOutput(output: string, redactPaths?: boolean): string;
/**
 * Sanitizes error messages to prevent information disclosure.
 * @param errorMessage The error message to sanitize
 * @returns Sanitized error message safe for external display
 */
export declare function sanitizeErrorMessage(errorMessage: string): string;
/**
 * Validates the working directory is a git repository and within allowed paths.
 * Prevents CWE-22: Path Traversal.
 * @param directory The directory to validate
 * @returns True if directory is valid git repository, false otherwise
 */
export declare function validateWorkingDirectory(directory: string | null | undefined): boolean;
/**
 * Logs security events to stderr (never stdout per MCP spec).
 * Implements CWE-532 safe logging (no sensitive data in logs).
 * @param eventType The type of security event
 * @param details Details about the event (will be sanitized)
 */
export declare function logSecurityEvent(eventType: string, details: string): void;
export declare const SECURITY_CONSTANTS: {
    MAX_INPUT_LENGTH: number;
    MAX_PARAMETER_LENGTH: number;
    MAX_OUTPUT_SIZE: number;
    MAX_REQUESTS_PER_MINUTE: number;
    MAX_BRANCH_NAME_LENGTH: number;
    MAX_REMOTE_NAME_LENGTH: number;
};
//# sourceMappingURL=security-helpers.d.ts.map