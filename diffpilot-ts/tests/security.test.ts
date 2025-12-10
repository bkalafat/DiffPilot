/**
 * Security Helper Tests
 * Ported from: tests/SecurityHelpersTests.cs
 * 
 * Tests for input validation, output sanitization, rate limiting, and path security.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  validateParameter,
  validateBranchName,
  validateRemoteName,
  validateFilePath,
  validateEnumParameter,
  checkRateLimit,
  resetRateLimits,
  sanitizeOutput,
  sanitizeErrorMessage,
  validateWorkingDirectory,
  SecurityError,
  SECURITY_CONSTANTS,
} from '../src/security/security-helpers';
import * as fs from 'fs';
import * as path from 'path';

// ============================================================================
// validateBranchName Tests
// ============================================================================

describe('validateBranchName', () => {
  it('should accept valid branch name', () => {
    expect(validateBranchName('main')).toBe('main');
    expect(validateBranchName('feature/new-feature')).toBe('feature/new-feature');
    expect(validateBranchName('release-1.0.0')).toBe('release-1.0.0');
    expect(validateBranchName('hotfix_urgent')).toBe('hotfix_urgent');
  });

  it('should return null for empty values', () => {
    expect(validateBranchName(null)).toBeNull();
    expect(validateBranchName(undefined)).toBeNull();
    expect(validateBranchName('')).toBeNull();
    expect(validateBranchName('   ')).toBeNull();
  });

  it('should reject branch names with command injection characters', () => {
    expect(() => validateBranchName('main; rm -rf /')).toThrow(SecurityError);
    expect(() => validateBranchName('main && echo pwned')).toThrow(SecurityError);
    expect(() => validateBranchName('main | cat /etc/passwd')).toThrow(SecurityError);
    expect(() => validateBranchName('$(whoami)')).toThrow(SecurityError);
    expect(() => validateBranchName('`id`')).toThrow(SecurityError);
  });

  it('should reject branch names starting with hyphen', () => {
    expect(() => validateBranchName('-branch')).toThrow(SecurityError);
    expect(() => validateBranchName('--help')).toThrow(SecurityError);
  });

  it('should reject branch names with path traversal', () => {
    expect(() => validateBranchName('../etc/passwd')).toThrow(SecurityError);
    expect(() => validateBranchName('main/../../secret')).toThrow(SecurityError);
  });

  it('should reject branch names exceeding max length', () => {
    const longBranch = 'a'.repeat(300);
    expect(() => validateBranchName(longBranch)).toThrow(SecurityError);
  });

  it('should accept branch names at max length', () => {
    const maxBranch = 'a'.repeat(SECURITY_CONSTANTS.MAX_BRANCH_NAME_LENGTH);
    expect(validateBranchName(maxBranch)).toBe(maxBranch);
  });
});

// ============================================================================
// validateRemoteName Tests
// ============================================================================

describe('validateRemoteName', () => {
  it('should accept valid remote names', () => {
    expect(validateRemoteName('origin')).toBe('origin');
    expect(validateRemoteName('upstream')).toBe('upstream');
    expect(validateRemoteName('my-remote')).toBe('my-remote');
    expect(validateRemoteName('remote_123')).toBe('remote_123');
  });

  it('should return origin as default for empty values', () => {
    expect(validateRemoteName(null)).toBe('origin');
    expect(validateRemoteName(undefined)).toBe('origin');
    expect(validateRemoteName('')).toBe('origin');
    expect(validateRemoteName('   ')).toBe('origin');
  });

  it('should reject remote names with invalid characters', () => {
    expect(() => validateRemoteName('origin; rm -rf /')).toThrow(SecurityError);
    expect(() => validateRemoteName('remote/sub')).toThrow(SecurityError);
    expect(() => validateRemoteName('remote.name')).toThrow(SecurityError);
  });

  it('should reject remote names exceeding max length', () => {
    const longRemote = 'a'.repeat(101);
    expect(() => validateRemoteName(longRemote)).toThrow(SecurityError);
  });
});

// ============================================================================
// validateParameter Tests
// ============================================================================

describe('validateParameter', () => {
  it('should return null for empty values', () => {
    expect(validateParameter(null, 'param')).toBeNull();
    expect(validateParameter(undefined, 'param')).toBeNull();
    expect(validateParameter('', 'param')).toBeNull();
    expect(validateParameter('   ', 'param')).toBeNull();
  });

  it('should return value for valid input', () => {
    expect(validateParameter('valid value', 'param')).toBe('valid value');
    expect(validateParameter('hello world', 'message')).toBe('hello world');
  });

  it('should remove null bytes from input', () => {
    expect(validateParameter('before\0after', 'param')).toBe('beforeafter');
    expect(validateParameter('\0start', 'param')).toBe('start');
    expect(validateParameter('end\0', 'param')).toBe('end');
  });

  it('should reject values exceeding max length', () => {
    const longValue = 'x'.repeat(SECURITY_CONSTANTS.MAX_PARAMETER_LENGTH + 1);
    expect(() => validateParameter(longValue, 'param')).toThrow(SecurityError);
  });

  it('should respect custom max length', () => {
    expect(() => validateParameter('12345678901', 'param', { maxLength: 10 })).toThrow(SecurityError);
    expect(validateParameter('1234567890', 'param', { maxLength: 10 })).toBe('1234567890');
  });

  it('should validate against allowed pattern', () => {
    const alphaNumeric = /^[a-zA-Z0-9]+$/;
    expect(validateParameter('abc123', 'param', { allowedPattern: alphaNumeric })).toBe('abc123');
    expect(() => validateParameter('abc-123', 'param', { allowedPattern: alphaNumeric })).toThrow(SecurityError);
  });
});

// ============================================================================
// validateEnumParameter Tests
// ============================================================================

describe('validateEnumParameter', () => {
  const allowedValues = ['conventional', 'simple', 'descriptive'];

  it('should return valid enum value', () => {
    expect(validateEnumParameter('conventional', allowedValues, 'simple')).toBe('conventional');
    expect(validateEnumParameter('simple', allowedValues, 'conventional')).toBe('simple');
  });

  it('should be case-insensitive', () => {
    expect(validateEnumParameter('CONVENTIONAL', allowedValues, 'simple')).toBe('conventional');
    expect(validateEnumParameter('Simple', allowedValues, 'conventional')).toBe('simple');
  });

  it('should return default for empty values', () => {
    expect(validateEnumParameter(null, allowedValues, 'simple')).toBe('simple');
    expect(validateEnumParameter(undefined, allowedValues, 'simple')).toBe('simple');
    expect(validateEnumParameter('', allowedValues, 'simple')).toBe('simple');
  });

  it('should return default for invalid values', () => {
    expect(validateEnumParameter('invalid', allowedValues, 'simple')).toBe('simple');
    expect(validateEnumParameter('unknown', allowedValues, 'conventional')).toBe('conventional');
  });
});

// ============================================================================
// sanitizeOutput Tests
// ============================================================================

describe('sanitizeOutput', () => {
  it('should return empty string for null/undefined', () => {
    expect(sanitizeOutput(null as any)).toBeFalsy();
    expect(sanitizeOutput(undefined as any)).toBeFalsy();
    expect(sanitizeOutput('')).toBe('');
  });

  it('should redact API keys', () => {
    const input = 'api_key=test_key_abcdef123456789012345678';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('test_key_abcdef123456789012345678');
  });

  it('should redact passwords', () => {
    const input = 'password=mysupersecretpassword123';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('mysupersecretpassword123');
  });

  it('should redact bearer tokens', () => {
    const input = 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9');
  });

  it('should redact AWS access keys', () => {
    const input = 'AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('AKIAIOSFODNN7EXAMPLE');
  });

  it('should redact JWT tokens', () => {
    const input = 'token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ');
  });

  it('should redact GitHub tokens', () => {
    const input = 'GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx');
  });

  it('should redact Slack tokens', () => {
    const input = 'slack_token=xoxb-1234567890-abcdefghij';
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
  });

  it('should redact private keys', () => {
    const input = `-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7
-----END PRIVATE KEY-----`;
    const output = sanitizeOutput(input);
    expect(output).toContain('[REDACTED]');
    expect(output).not.toContain('MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7');
  });

  it('should truncate extremely long output', () => {
    const longOutput = 'a'.repeat(SECURITY_CONSTANTS.MAX_OUTPUT_SIZE + 1000);
    const result = sanitizeOutput(longOutput);
    expect(result.length).toBeLessThan(longOutput.length);
    expect(result).toContain('[Output truncated');
  });

  it('should preserve safe content', () => {
    const safeInput = 'Normal git diff output with some code changes';
    expect(sanitizeOutput(safeInput)).toBe(safeInput);
  });

  it('should redact Windows paths when enabled', () => {
    const input = 'Error at C:\\Users\\john\\secret\\config.json';
    const output = sanitizeOutput(input, true);
    expect(output).toContain('[PATH]');
    expect(output).not.toContain('C:\\Users\\john\\secret');
  });

  it('should redact Unix paths when enabled', () => {
    const input = 'Error at /home/john/secret/config.json';
    const output = sanitizeOutput(input, true);
    expect(output).toContain('[PATH]');
    expect(output).not.toContain('/home/john/secret');
  });
});

// ============================================================================
// sanitizeErrorMessage Tests
// ============================================================================

describe('sanitizeErrorMessage', () => {
  it('should return generic message for empty input', () => {
    expect(sanitizeErrorMessage('')).toBe('An error occurred.');
    expect(sanitizeErrorMessage(null as any)).toBe('An error occurred.');
  });

  it('should remove stack traces', () => {
    const errorWithStack = 'Error: Something failed\n   at SomeMethod() in /path/to/file.cs:line 42';
    const result = sanitizeErrorMessage(errorWithStack);
    expect(result).not.toContain('/path/to/file.cs');
    expect(result).not.toContain('line 42');
  });

  it('should remove Node.js stack traces', () => {
    const errorWithStack = 'Error: Something failed\n    at Object.<anonymous> (/path/to/file.js:10:5)';
    const result = sanitizeErrorMessage(errorWithStack);
    expect(result).not.toContain('/path/to/file.js');
    expect(result).not.toContain('at Object');
  });

  it('should redact paths in error messages', () => {
    const errorWithPath = 'Cannot find file C:\\Users\\john\\Documents\\secret.txt';
    const result = sanitizeErrorMessage(errorWithPath);
    expect(result).toContain('[path]');
    expect(result).not.toContain('C:\\Users\\john');
  });

  it('should preserve the actual error message', () => {
    const simpleError = 'Invalid branch name format';
    expect(sanitizeErrorMessage(simpleError)).toBe(simpleError);
  });
});

// ============================================================================
// Rate Limiting Tests
// ============================================================================

describe('checkRateLimit', () => {
  beforeEach(() => {
    resetRateLimits();
  });

  it('should allow first request', () => {
    expect(checkRateLimit('test_tool')).toBe(true);
  });

  it('should allow requests within limit', () => {
    for (let i = 0; i < 10; i++) {
      expect(checkRateLimit('test_tool')).toBe(true);
    }
  });

  it('should block requests exceeding limit', () => {
    // Use up all allowed requests
    for (let i = 0; i < SECURITY_CONSTANTS.MAX_REQUESTS_PER_MINUTE; i++) {
      checkRateLimit('flood_tool');
    }
    
    // Next request should be blocked
    expect(checkRateLimit('flood_tool')).toBe(false);
  });

  it('should track different tools separately', () => {
    for (let i = 0; i < SECURITY_CONSTANTS.MAX_REQUESTS_PER_MINUTE; i++) {
      checkRateLimit('tool_a');
    }
    
    // tool_a is exhausted, but tool_b should work
    expect(checkRateLimit('tool_a')).toBe(false);
    expect(checkRateLimit('tool_b')).toBe(true);
  });
});

// ============================================================================
// Working Directory Validation Tests
// ============================================================================

describe('validateWorkingDirectory', () => {
  it('should return false for empty values', () => {
    expect(validateWorkingDirectory(null)).toBe(false);
    expect(validateWorkingDirectory(undefined)).toBe(false);
    expect(validateWorkingDirectory('')).toBe(false);
    expect(validateWorkingDirectory('   ')).toBe(false);
  });

  it('should return false for non-existent directory', () => {
    expect(validateWorkingDirectory('/nonexistent/path/xyz123')).toBe(false);
  });

  it('should return true for valid git repository', () => {
    // Use current test repo as a valid git directory
    const gitRepo = path.resolve(__dirname, '..');
    // This should be a git repo (diffpilot-ts is in DiffPilot which has .git)
    // Check parent directory
    const parentRepo = path.resolve(__dirname, '..', '..');
    if (fs.existsSync(path.join(parentRepo, '.git'))) {
      expect(validateWorkingDirectory(parentRepo)).toBe(true);
    }
  });
});

// ============================================================================
// validateFilePath Tests
// ============================================================================

describe('validateFilePath', () => {
  it('should throw for empty path', () => {
    expect(() => validateFilePath(null)).toThrow(SecurityError);
    expect(() => validateFilePath('')).toThrow(SecurityError);
    expect(() => validateFilePath('   ')).toThrow(SecurityError);
  });

  it('should normalize and return valid path', () => {
    const testPath = '/some/valid/path';
    const result = validateFilePath(testPath);
    expect(result).toBeTruthy();
    expect(path.isAbsolute(result)).toBe(true);
  });

  it('should remove null bytes from path', () => {
    const pathWithNull = '/some/path\0injection';
    const result = validateFilePath(pathWithNull);
    expect(result).not.toContain('\0');
  });

  it('should reject paths outside workspace root', () => {
    const workspaceRoot = '/workspace/project';
    const outsidePath = '/etc/passwd';
    
    expect(() => validateFilePath(outsidePath, workspaceRoot)).toThrow(SecurityError);
  });

  it('should allow paths within workspace root', () => {
    // Use actual paths that exist
    const workspaceRoot = process.cwd();
    const insidePath = path.join(workspaceRoot, 'src', 'index.ts');
    
    const result = validateFilePath(insidePath, workspaceRoot);
    expect(result).toContain(workspaceRoot);
  });
});

// ============================================================================
// Security Constants Tests
// ============================================================================

describe('SECURITY_CONSTANTS', () => {
  it('should have reasonable limits', () => {
    expect(SECURITY_CONSTANTS.MAX_INPUT_LENGTH).toBe(100_000);
    expect(SECURITY_CONSTANTS.MAX_PARAMETER_LENGTH).toBe(1_000);
    expect(SECURITY_CONSTANTS.MAX_OUTPUT_SIZE).toBe(500_000);
    expect(SECURITY_CONSTANTS.MAX_REQUESTS_PER_MINUTE).toBe(120);
    expect(SECURITY_CONSTANTS.MAX_BRANCH_NAME_LENGTH).toBe(256);
    expect(SECURITY_CONSTANTS.MAX_REMOTE_NAME_LENGTH).toBe(100);
  });
});

// ============================================================================
// Secret Pattern Detection Tests (Ported from SecretScanningTests.cs)
// ============================================================================

describe('Secret Pattern Detection', () => {
  // Patterns matching those in SecretScanningTests.cs
  // Note: Using factory functions to avoid regex state issues with /g flag
  const createAwsKeyPattern = () => /AKIA[0-9A-Z]{16}/;
  const createGitHubTokenPattern = () => /gh[pousr]_[A-Za-z0-9_]{36,255}/;
  const createApiKeyPattern = () => /(?:api[_-]?key|apikey)\s*[:=]\s*["']?([A-Za-z0-9_\-]{20,})["']?/i;
  const createPasswordPattern = () => /(?:password|passwd|pwd)\s*[:=]\s*["']?([^\s"']{8,})["']?/i;
  const createJwtPattern = () => /eyJ[A-Za-z0-9_-]*\.eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*/;
  const createPrivateKeyPattern = () => /-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----/;

  describe('AWS Key Detection', () => {
    it.each([
      ['AKIAIOSFODNN7EXAMPLE'],
      ['AKIAI44QH8DHBEXAMPLE'],
      ['AKIAEXAMPLE123456789'],
    ])('detects valid AWS key "%s"', (key) => {
      expect(createAwsKeyPattern().test(key)).toBe(true);
    });

    it.each([
      ['AKIA123'], // Too short
      ['AKZA1234567890123456'], // Wrong prefix
      ['akiaiosfodnn7example'], // Lowercase (AWS keys are uppercase)
      ["const awsRegion = 'us-east-1'"], // Normal code
    ])('ignores invalid pattern "%s"', (text) => {
      expect(createAwsKeyPattern().test(text)).toBe(false);
    });
  });

  describe('GitHub Token Detection', () => {
    it.each([
      ['ghp_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234'], // Personal access token
      ['gho_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234'], // OAuth token
      ['ghu_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234'], // User-to-server token
      ['ghs_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234'], // Server-to-server token
      ['ghr_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefgh1234'], // Refresh token
    ])('detects valid GitHub token "%s"', (token) => {
      expect(createGitHubTokenPattern().test(token)).toBe(true);
    });

    it.each([
      ['ghx_invalid'], // Invalid prefix
      ['ghp_short'], // Too short
      ['github.com/user/repo'], // Normal GitHub URL
    ])('ignores invalid pattern "%s"', (text) => {
      expect(createGitHubTokenPattern().test(text)).toBe(false);
    });
  });

  describe('API Key Detection', () => {
    it.each([
      ['api_key=abc123def456ghi789jkl012'],
      ["API_KEY: 'abc123def456ghi789jkl012mno'"],
      ['apikey="abc123def456ghi789jkl012"'],
      ['api-key = abc123def456ghi789jkl012'],
    ])('detects API key in "%s"', (line) => {
      expect(createApiKeyPattern().test(line)).toBe(true);
    });

    it.each([
      ['// This function returns the API key'], // Comment
      ['const apiKeyLength = 32'], // Variable about API keys
      ['validateApiKey(key)'], // Function call
    ])('ignores normal code "%s"', (code) => {
      expect(createApiKeyPattern().test(code)).toBe(false);
    });
  });

  describe('Password Detection', () => {
    it.each([
      ['password=MySecr3tP@ss!'],
      ["PASSWORD: 'SuperSecr3t123'"],
      ['pwd="database_password_123"'],
      ['passwd: my-secret-password'],
    ])('detects password in "%s"', (line) => {
      expect(createPasswordPattern().test(line)).toBe(true);
    });

    it.each([
      ['password='], // No value
      ['password=short'], // Too short (less than 8 chars)
      ['// TODO: add password validation'], // Comment
    ])('ignores empty or short "%s"', (text) => {
      expect(createPasswordPattern().test(text)).toBe(false);
    });
  });

  describe('JWT Token Detection', () => {
    it('detects valid JWT', () => {
      const jwt = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c';
      expect(createJwtPattern().test(jwt)).toBe(true);
    });

    it.each([
      ['eyJ'], // Too short, missing parts
      ['notAJwt.atAll.really'], // Not starting with eyJ
      ['base64EncodedData'], // Random base64-ish text
    ])('ignores non-JWT "%s"', (text) => {
      expect(createJwtPattern().test(text)).toBe(false);
    });
  });

  describe('Private Key Detection', () => {
    it.each([
      ['-----BEGIN PRIVATE KEY-----'],
      ['-----BEGIN RSA PRIVATE KEY-----'],
      ['-----BEGIN EC PRIVATE KEY-----'],
      ['-----BEGIN DSA PRIVATE KEY-----'],
      ['-----BEGIN OPENSSH PRIVATE KEY-----'],
    ])('detects private key header "%s"', (header) => {
      expect(createPrivateKeyPattern().test(header)).toBe(true);
    });

    it.each([
      ['-----BEGIN PUBLIC KEY-----'], // Public key, not private
      ['-----BEGIN CERTIFICATE-----'], // Certificate
      ['private key'], // Just text
    ])('ignores non-private keys "%s"', (text) => {
      expect(createPrivateKeyPattern().test(text)).toBe(false);
    });
  });

  describe('Secret Masking', () => {
    const maskSecret = (secret: string): string => {
      if (secret.length <= 8) {
        return '*'.repeat(secret.length);
      }
      const visibleStart = Math.floor(secret.length / 5);
      const visibleEnd = Math.floor(secret.length / 5);
      return secret.slice(0, visibleStart) + 
             '*'.repeat(secret.length - visibleStart - visibleEnd) + 
             secret.slice(-visibleEnd);
    };

    it('hides middle of secret', () => {
      const secret = 'AKIAIOSFODNN7EXAMPLE';
      const masked = maskSecret(secret);

      expect(masked.startsWith('AKIA')).toBe(true);
      expect(masked.endsWith('MPLE')).toBe(true);
      expect(masked).toContain('*');
      expect(masked).not.toBe(secret);
    });

    it('handles short secrets', () => {
      const shortSecret = 'abc';
      const masked = maskSecret(shortSecret);

      expect(masked).toBe('***');
    });
  });
});

// ============================================================================
// Integration-style Tests
// ============================================================================

describe('Security Helpers Integration', () => {
  it('should handle multiple validation errors gracefully', () => {
    // Test that each validation type fails independently
    const errors: string[] = [];
    
    try { validateBranchName('$(inject)'); } 
    catch (e) { errors.push('branch'); }
    
    try { validateRemoteName('invalid/remote'); } 
    catch (e) { errors.push('remote'); }
    
    try { validateFilePath(''); } 
    catch (e) { errors.push('path'); }
    
    expect(errors).toContain('branch');
    expect(errors).toContain('remote');
    expect(errors).toContain('path');
  });

  it('should sanitize complex git diff output', () => {
    const diffOutput = `diff --git a/config.js b/config.js
--- a/config.js
+++ b/config.js
@@ -1,3 +1,4 @@
 module.exports = {
-  api_key: "fake_key_abcdef1234567890abcdef12"
+  api_key: process.env.API_KEY,
+  password: "mysecretpassword123"
 };`;

    const sanitized = sanitizeOutput(diffOutput);
    expect(sanitized).not.toContain('fake_key_abcdef1234567890abcdef12');
    expect(sanitized).not.toContain('mysecretpassword123');
    expect(sanitized).toContain('[REDACTED]');
    expect(sanitized).toContain('diff --git'); // Preserve structure
  });

  it('should validate tool names correctly', () => {
    // Valid tool names (pattern: ^[a-zA-Z_][a-zA-Z0-9_-]*$)
    const validNames = ['get_pr_diff', 'scan_secrets', 'generate_changelog', 'myTool123'];
    const invalidNames = ['', '123tool', '-tool', 'tool;rm -rf', 'a'.repeat(51)];
    
    const pattern = /^[a-zA-Z_][a-zA-Z0-9_-]*$/;
    
    for (const name of validNames) {
      expect(pattern.test(name), `Expected "${name}" to be valid`).toBe(true);
    }
    
    for (const name of invalidNames) {
      const isInvalid = !name || name.length > 50 || !pattern.test(name);
      expect(isInvalid, `Expected "${name}" to be invalid`).toBe(true);
    }
  });
});
