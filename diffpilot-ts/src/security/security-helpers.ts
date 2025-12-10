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

import * as path from 'path';
import * as fs from 'fs';

// ============================================================================
// Security Configuration Constants
// ============================================================================

/** Maximum allowed input length to prevent DoS attacks */
const MAX_INPUT_LENGTH = 100_000;

/** Maximum allowed parameter value length */
const MAX_PARAMETER_LENGTH = 1_000;

/** Maximum output size to prevent memory exhaustion */
const MAX_OUTPUT_SIZE = 500_000;

/** Rate limiting: max requests per minute per tool */
const MAX_REQUESTS_PER_MINUTE = 120;

/** Maximum branch name length */
const MAX_BRANCH_NAME_LENGTH = 256;

/** Maximum remote name length */
const MAX_REMOTE_NAME_LENGTH = 100;

// ============================================================================
// Security Exception
// ============================================================================

/** Exception thrown when a security violation is detected */
export class SecurityError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'SecurityError';
  }
}

// ============================================================================
// Rate Limiting State
// ============================================================================

/** Track request counts for rate limiting */
const rateLimits = new Map<string, { windowStart: Date; count: number }>();

// ============================================================================
// Sensitive Data Patterns for Redaction
// Based on CWE-200 (Exposure of Sensitive Information) prevention.
// ============================================================================

const SENSITIVE_PATTERNS: RegExp[] = [
  // API keys
  /(?:api[_\-]?key|apikey)\s*['"]?\s*[:=]\s*['"]?[\w\-]{16,}['"]?/gi,
  // Passwords
  /(?:password|passwd|pwd)\s*['"]?\s*[:=]\s*['"]?[^\s'"]{6,}['"]?/gi,
  // Tokens
  /(?:token|auth[_\-]?token|access[_\-]?token)\s*['"]?\s*[:=]\s*['"]?[\w\-\.]{20,}['"]?/gi,
  // Secrets
  /(?:secret|private[_\-]?key|client[_\-]?secret)\s*['"]?\s*[:=]\s*['"]?[\w\-\.]{16,}['"]?/gi,
  // Connection strings
  /(?:connection[_\-]?string|connstr|database[_\-]?url)\s*['"]?\s*[:=]\s*['"]?[^\n]{20,}['"]?/gi,
  // Private keys
  /-----BEGIN\s+(?:RSA|DSA|EC|OPENSSH|PRIVATE)?\s*PRIVATE\s+KEY-----[\s\S]*?-----END/gi,
  // Bearer tokens
  /bearer\s+[\w\-\.]{20,}/gi,
  // AWS keys
  /AKIA[0-9A-Z]{16}/g,
  // JWT tokens
  /eyJ[a-zA-Z0-9_\-]*\.eyJ[a-zA-Z0-9_\-]*\.[a-zA-Z0-9_\-]*/g,
  // Slack tokens
  /xox[baprs]\-[0-9]{10,}/g,
  // GitHub tokens
  /(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,}/g,
  // Azure keys
  /azure[_\-]?(?:storage|key|secret|connection)\s*[:=]\s*['"]?[\w\-\./+=]{20,}['"]?/gi,
  // Generic keys
  /(?:key|secret|credential)\s*[:=]\s*['"]?[\w\-]{32,}['"]?/gi,
];

// Path patterns for redaction
const WINDOWS_PATH_PATTERN = /[A-Za-z]:\\(?:Users|Windows|Program Files)[^\s\n"']*/gi;
const UNIX_PATH_PATTERN = /\/(?:home|Users|var|etc|tmp|root)\/[^\s\n"']*/gi;

// Validation patterns
const BRANCH_NAME_PATTERN = /^[a-zA-Z0-9/_.\-]+$/;
const REMOTE_NAME_PATTERN = /^[a-zA-Z0-9_\-]+$/;

// ============================================================================
// Input Validation Methods
// ============================================================================

/**
 * Validates and sanitizes a tool parameter to prevent injection attacks.
 * @param value The parameter value to validate
 * @param paramName The parameter name for error messages
 * @param options Optional configuration
 * @returns Sanitized value or null if input was null/empty
 * @throws SecurityError if validation fails
 */
export function validateParameter(
  value: string | null | undefined,
  paramName: string,
  options?: {
    allowedPattern?: RegExp;
    maxLength?: number;
  }
): string | null {
  if (!value || typeof value !== 'string' || !value.trim()) {
    return null;
  }

  // Length check to prevent DoS
  const maxLength = options?.maxLength ?? MAX_PARAMETER_LENGTH;
  if (value.length > maxLength) {
    logSecurityEvent('INPUT_VALIDATION_FAILED', `Parameter '${paramName}' exceeds max length (${value.length} > ${maxLength})`);
    throw new SecurityError(`Parameter '${paramName}' exceeds maximum length of ${maxLength} characters.`);
  }

  // Remove null bytes (CWE-158: Improper Neutralization of Null Byte)
  if (value.includes('\0')) {
    logSecurityEvent('NULL_BYTE_DETECTED', `Parameter '${paramName}' contains null bytes`);
    value = value.replace(/\0/g, '');
  }

  // Validate against allowed pattern if provided
  if (options?.allowedPattern && !options.allowedPattern.test(value)) {
    logSecurityEvent('PATTERN_VALIDATION_FAILED', `Parameter '${paramName}' failed pattern validation`);
    throw new SecurityError(`Parameter '${paramName}' contains invalid characters.`);
  }

  return value;
}

/**
 * Validates a branch name parameter (CWE-78: OS Command Injection prevention).
 * @param value The branch name to validate
 * @param paramName Parameter name for error messages
 * @returns Validated branch name or null if input was null/empty
 * @throws SecurityError if validation fails
 */
export function validateBranchName(value: string | null | undefined, paramName = 'branch'): string | null {
  if (!value || typeof value !== 'string' || !value.trim()) {
    return null;
  }

  // Length check
  if (value.length > MAX_BRANCH_NAME_LENGTH) {
    logSecurityEvent('BRANCH_VALIDATION_FAILED', `Branch '${paramName}' exceeds max length`);
    throw new SecurityError(`Branch name '${paramName}' exceeds maximum length of ${MAX_BRANCH_NAME_LENGTH} characters.`);
  }

  // Strict validation for branch names
  if (!BRANCH_NAME_PATTERN.test(value)) {
    logSecurityEvent('BRANCH_VALIDATION_FAILED', `Branch '${paramName}' contains invalid characters`);
    throw new SecurityError(`Branch name '${paramName}' contains invalid characters. Only alphanumeric, slash, underscore, hyphen, and dot are allowed.`);
  }

  // Prevent path traversal in branch names
  if (value.includes('..')) {
    logSecurityEvent('PATH_TRAVERSAL_ATTEMPT', `Branch '${paramName}' contains '..'`);
    throw new SecurityError(`Branch name '${paramName}' contains potentially dangerous patterns.`);
  }

  // Prevent options injection (branch names starting with -)
  if (value.startsWith('-')) {
    logSecurityEvent('OPTION_INJECTION_ATTEMPT', `Branch '${paramName}' starts with '-'`);
    throw new SecurityError(`Branch name '${paramName}' cannot start with a hyphen.`);
  }

  return value;
}

/**
 * Validates a remote name parameter.
 * @param value The remote name to validate
 * @returns Validated remote name or "origin" as safe default
 * @throws SecurityError if validation fails
 */
export function validateRemoteName(value: string | null | undefined): string {
  if (!value || typeof value !== 'string' || !value.trim()) {
    return 'origin'; // Safe default
  }

  if (value.length > MAX_REMOTE_NAME_LENGTH) {
    logSecurityEvent('REMOTE_VALIDATION_FAILED', 'Remote name exceeds max length');
    throw new SecurityError('Remote name exceeds maximum length.');
  }

  if (!REMOTE_NAME_PATTERN.test(value)) {
    logSecurityEvent('REMOTE_VALIDATION_FAILED', 'Remote name contains invalid characters');
    throw new SecurityError('Remote name contains invalid characters. Only alphanumeric, underscore, and hyphen are allowed.');
  }

  return value;
}

/**
 * Validates a file path to prevent path traversal attacks (CWE-22).
 * @param filePath The file path to validate
 * @param workspaceRoot Optional workspace root to ensure path is within bounds
 * @returns Validated and normalized file path
 * @throws SecurityError if validation fails
 */
export function validateFilePath(filePath: string | null | undefined, workspaceRoot?: string): string {
  if (!filePath || typeof filePath !== 'string' || !filePath.trim()) {
    throw new SecurityError('File path cannot be empty.');
  }

  // Remove null bytes
  filePath = filePath.replace(/\0/g, '');

  // Normalize the path
  let normalizedPath: string;
  try {
    normalizedPath = path.resolve(filePath);
  } catch (ex) {
    logSecurityEvent('PATH_VALIDATION_FAILED', `Invalid path format: ${ex}`);
    throw new SecurityError('Invalid file path format.');
  }

  // If workspace root is provided, ensure path is within it
  if (workspaceRoot && workspaceRoot.trim()) {
    const normalizedRoot = path.resolve(workspaceRoot);
    if (!normalizedPath.toLowerCase().startsWith(normalizedRoot.toLowerCase())) {
      logSecurityEvent('PATH_TRAVERSAL_ATTEMPT', 'Attempted access outside workspace root');
      throw new SecurityError('Access denied: Path is outside the allowed workspace.');
    }
  }

  return normalizedPath;
}

/**
 * Validates an enum-style parameter.
 * @param value The value to validate
 * @param allowedValues Array of allowed values
 * @param defaultValue Default value if input is null/empty
 * @returns Validated value or default value
 */
export function validateEnumParameter(
  value: string | null | undefined,
  allowedValues: string[],
  defaultValue: string
): string {
  if (!value || typeof value !== 'string' || !value.trim()) {
    return defaultValue;
  }

  const normalized = value.trim().toLowerCase();
  if (allowedValues.some(v => v.toLowerCase() === normalized)) {
    return normalized;
  }

  logSecurityEvent('ENUM_VALIDATION_FAILED', `Invalid value '${value}', using default '${defaultValue}'`);
  return defaultValue;
}

// ============================================================================
// Rate Limiting
// ============================================================================

/**
 * Checks rate limit for a tool. Returns true if within limits.
 * Implements protection against DoS (CWE-400: Uncontrolled Resource Consumption).
 * @param toolName The tool being called
 * @returns True if the request is allowed, false if rate limited
 */
export function checkRateLimit(toolName: string): boolean {
  const now = new Date();
  const existing = rateLimits.get(toolName);

  if (!existing) {
    rateLimits.set(toolName, { windowStart: now, count: 1 });
    return true;
  }

  // Reset window if more than a minute has passed
  const elapsed = now.getTime() - existing.windowStart.getTime();
  if (elapsed >= 60_000) {
    rateLimits.set(toolName, { windowStart: now, count: 1 });
    return true;
  }

  existing.count++;
  rateLimits.set(toolName, existing);

  if (existing.count > MAX_REQUESTS_PER_MINUTE) {
    logSecurityEvent('RATE_LIMIT_EXCEEDED', `Tool '${toolName}' rate limit exceeded (${existing.count} requests)`);
    return false;
  }

  return true;
}

/**
 * Resets rate limit state (for testing purposes).
 */
export function resetRateLimits(): void {
  rateLimits.clear();
}

// ============================================================================
// Output Sanitization
// ============================================================================

/**
 * Sanitizes output to remove or redact sensitive information.
 * Prevents CWE-200: Exposure of Sensitive Information to an Unauthorized Actor.
 * @param output The output string to sanitize
 * @param redactPaths Whether to redact absolute paths (default: false)
 * @returns Sanitized output string
 */
export function sanitizeOutput(output: string, redactPaths = false): string {
  if (!output) {
    return output;
  }

  // Truncate if too large to prevent memory issues
  if (output.length > MAX_OUTPUT_SIZE) {
    output = output.slice(0, MAX_OUTPUT_SIZE) +
      `\n\n[Output truncated at ${MAX_OUTPUT_SIZE.toLocaleString()} characters for security reasons]`;
    logSecurityEvent('OUTPUT_TRUNCATED', `Output exceeded ${MAX_OUTPUT_SIZE} characters`);
  }

  // Redact sensitive patterns from output
  for (const pattern of SENSITIVE_PATTERNS) {
    output = output.replace(pattern, (match) => {
      // Preserve some structure but redact the sensitive value
      if (match.length <= 8) {
        return '[REDACTED]';
      }

      // Show first few chars of key name and redact value
      const colonIndex = match.search(/[:=]/);
      if (colonIndex > 0 && colonIndex < match.length - 1) {
        return match.slice(0, colonIndex + 1) + '[REDACTED]';
      }

      return match.slice(0, 4) + '[REDACTED]';
    });
  }

  // Optionally remove any absolute paths that might leak system info
  if (redactPaths) {
    output = output.replace(WINDOWS_PATH_PATTERN, '[PATH]');
    output = output.replace(UNIX_PATH_PATTERN, '[PATH]');
  }

  return output;
}

/**
 * Sanitizes error messages to prevent information disclosure.
 * @param errorMessage The error message to sanitize
 * @returns Sanitized error message safe for external display
 */
export function sanitizeErrorMessage(errorMessage: string): string {
  if (!errorMessage) {
    return 'An error occurred.';
  }

  // Remove stack traces
  const stackTraceIndex = errorMessage.indexOf('   at ');
  if (stackTraceIndex > 0) {
    errorMessage = errorMessage.slice(0, stackTraceIndex).trim();
  }

  // Also check for Node.js style stack traces
  const nodeStackIndex = errorMessage.indexOf('\n    at ');
  if (nodeStackIndex > 0) {
    errorMessage = errorMessage.slice(0, nodeStackIndex).trim();
  }

  // Remove absolute paths from error messages
  errorMessage = errorMessage.replace(WINDOWS_PATH_PATTERN, '[path]');
  errorMessage = errorMessage.replace(UNIX_PATH_PATTERN, '[path]');

  // Redact any sensitive data
  return sanitizeOutput(errorMessage);
}

// ============================================================================
// Working Directory Validation
// ============================================================================

/**
 * Validates the working directory is a git repository and within allowed paths.
 * Prevents CWE-22: Path Traversal.
 * @param directory The directory to validate
 * @returns True if directory is valid git repository, false otherwise
 */
export function validateWorkingDirectory(directory: string | null | undefined): boolean {
  if (!directory || typeof directory !== 'string' || !directory.trim()) {
    return false;
  }

  try {
    // Resolve to full path to detect path traversal
    const fullPath = path.resolve(directory);

    // Check directory exists
    if (!fs.existsSync(fullPath) || !fs.statSync(fullPath).isDirectory()) {
      logSecurityEvent('WORKSPACE_VALIDATION_FAILED', 'Directory does not exist');
      return false;
    }

    // Check it's a git repository
    const gitDir = path.join(fullPath, '.git');
    const isGitRepo = fs.existsSync(gitDir);

    if (!isGitRepo) {
      logSecurityEvent('WORKSPACE_VALIDATION_FAILED', 'Directory is not a git repository');
    }

    return isGitRepo;
  } catch (ex) {
    logSecurityEvent('WORKSPACE_VALIDATION_FAILED', `Exception: ${ex}`);
    return false;
  }
}

// ============================================================================
// Security Logging
// ============================================================================

/**
 * Logs security events to stderr (never stdout per MCP spec).
 * Implements CWE-532 safe logging (no sensitive data in logs).
 * @param eventType The type of security event
 * @param details Details about the event (will be sanitized)
 */
export function logSecurityEvent(eventType: string, details: string): void {
  // Sanitize details to prevent log injection (CWE-117)
  details = details
    .replace(/\r/g, '\\r')
    .replace(/\n/g, '\\n')
    .replace(/\t/g, '\\t');

  // Keep log entry short to prevent log flooding
  if (details.length > 200) {
    details = details.slice(0, 200) + '...';
  }

  const timestamp = new Date().toISOString();
  console.error(`[SECURITY] [${timestamp}] ${eventType}: ${details}`);
}

// ============================================================================
// Export Constants for Testing
// ============================================================================

export const SECURITY_CONSTANTS = {
  MAX_INPUT_LENGTH,
  MAX_PARAMETER_LENGTH,
  MAX_OUTPUT_SIZE,
  MAX_REQUESTS_PER_MINUTE,
  MAX_BRANCH_NAME_LENGTH,
  MAX_REMOTE_NAME_LENGTH,
};
