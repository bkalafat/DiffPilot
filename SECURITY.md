# DiffPilot Security Documentation

## Overview

DiffPilot implements comprehensive security features designed to meet enterprise and bank-grade security requirements. This document describes the security controls implemented in the MCP server.

## Security Features

### 1. Input Validation (CWE-20)

All input parameters are validated before processing:

- **Branch Names**: Validated against strict regex pattern (`^[a-zA-Z0-9/_.\-]+$`), max 256 characters
- **Remote Names**: Alphanumeric, underscore, and hyphen only (`^[a-zA-Z0-9_\-]+$`), max 100 characters
- **Tool Names**: Validated format, max 50 characters
- **General Parameters**: Configurable max length (default 1,000 characters)

### 2. Injection Prevention

#### Command Injection (CWE-78)
- Branch names starting with `-` are rejected (prevents git option injection)
- Shell metacharacters (`;`, `|`, `&`, `` ` ``, `$()`) are blocked
- All git commands use arguments, never string interpolation

#### Path Traversal (CWE-22)
- `..` sequences in branch names are rejected
- File paths are normalized and validated against workspace root
- Null byte injection (CWE-158) is prevented by stripping `\0` characters

### 3. Output Sanitization (CWE-200)

All tool output is automatically sanitized to prevent sensitive data leakage:

#### Redacted Patterns:
- **API Keys**: `api_key`, `apiKey`, `API_KEY` patterns
- **Passwords**: `password`, `passwd`, `pwd` assignments
- **AWS Credentials**: `AKIA*` access keys
- **GitHub Tokens**: `ghp_*`, `gho_*`, `ghu_*`, `ghs_*`, `ghr_*` patterns
- **JWT Tokens**: Standard JWT format (`eyJ...`)
- **Slack Tokens**: `xoxb-*`, `xoxa-*`, etc.
- **Azure Credentials**: Connection strings and storage keys
- **Private Keys**: `-----BEGIN * PRIVATE KEY-----` blocks
- **Bearer Tokens**: `Bearer` authentication headers
- **Generic Secrets**: `secret`, `token`, `key`, `auth` patterns

### 4. Rate Limiting (CWE-400)

- Maximum 120 requests per minute per tool
- Prevents denial-of-service attacks
- Sliding window implementation

### 5. Input Size Limits

- Maximum request size: 10MB
- Maximum output size: 500KB (auto-truncated)
- Maximum diff content: 500KB
- Prevents memory exhaustion attacks

### 6. Secure Error Handling

- Internal error details are never exposed to clients
- Stack traces are removed from error messages
- Absolute paths in errors are redacted
- Generic error messages for internal failures

### 7. Security Logging

All security events are logged to stderr with:
- Timestamp (ISO 8601 format)
- Event type
- Sanitized details (no sensitive data)
- Log injection prevention (newlines escaped)

Example:
```
[SECURITY] [2024-01-15T10:30:45.123Z] RATE_LIMIT_EXCEEDED: Tool: scan_secrets
[SECURITY] [2024-01-15T10:30:45.456Z] PATH_TRAVERSAL_ATTEMPT: Branch 'branch' contains '..'
```

### 8. Git Security

The `GitService` class implements additional protections:
- Command timeout: 60 seconds (prevents hanging)
- Branch name validation before all git operations
- File name validation with restricted character set
- No shell execution (prevents injection)

## Security Event Types

| Event Type | Description |
|------------|-------------|
| `INPUT_VALIDATION_FAILED` | Parameter failed validation check |
| `BRANCH_VALIDATION_FAILED` | Invalid branch name format |
| `REMOTE_VALIDATION_FAILED` | Invalid remote name format |
| `PATH_TRAVERSAL_ATTEMPT` | Detected `..` in path/branch |
| `OPTION_INJECTION_ATTEMPT` | Branch starting with `-` |
| `NULL_BYTE_DETECTED` | Null byte found in input |
| `PATTERN_VALIDATION_FAILED` | Input failed regex pattern |
| `RATE_LIMIT_EXCEEDED` | Too many requests |
| `OUTPUT_TRUNCATED` | Output exceeded size limit |
| `SECURITY_EXCEPTION` | Security validation threw |
| `UNEXPECTED_ERROR` | Unhandled exception occurred |
| `WORKSPACE_VALIDATION_FAILED` | Invalid workspace directory |
| `INVALID_TOOL_NAME` | Tool name format invalid |
| `INVALID_METHOD` | JSON-RPC method invalid |
| `INPUT_TOO_LARGE` | Request exceeded size limit |

## Compliance

The security implementation addresses:

### OWASP Top 10
- **A01:2021 – Broken Access Control**: Workspace boundary enforcement
- **A03:2021 – Injection**: Command/path injection prevention
- **A04:2021 – Insecure Design**: Defense in depth approach
- **A05:2021 – Security Misconfiguration**: Secure defaults
- **A09:2021 – Security Logging**: Comprehensive audit logging

### CWE Categories
- CWE-20: Improper Input Validation
- CWE-22: Path Traversal
- CWE-78: OS Command Injection
- CWE-117: Log Injection
- CWE-158: Null Byte Injection
- CWE-200: Information Exposure
- CWE-400: Resource Consumption
- CWE-532: Sensitive Information in Log Files

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DIFFPILOT_WORKSPACE` | Working directory for git operations | Current directory |

### Security Limits

| Limit | Value | Purpose |
|-------|-------|---------|
| Max input size | 10MB | Prevent DoS |
| Max output size | 500KB | Prevent memory exhaustion |
| Max parameter length | 1,000 chars | Prevent buffer issues |
| Max branch name | 256 chars | Reasonable limit |
| Max remote name | 100 chars | Reasonable limit |
| Rate limit | 120/min | Prevent abuse |
| Git timeout | 60 seconds | Prevent hanging |

## Testing

Security features are covered by comprehensive unit tests in `SecurityHelpersTests.cs`:

- Branch name validation (valid, invalid, injection attempts)
- Remote name validation
- Parameter validation
- Output sanitization (all sensitive patterns)
- Error message sanitization
- Rate limiting
- File path validation
- Workspace validation

Run tests:
```bash
dotnet test --filter "SecurityHelpers"
```

## Best Practices for Users

1. **Set `DIFFPILOT_WORKSPACE`** to restrict git operations to a specific directory
2. **Monitor stderr** for security event logs
3. **Review outputs** before sharing externally (additional secrets may exist)
4. **Keep updated** to receive security patches

## Reporting Security Issues

If you discover a security vulnerability, please report it responsibly by emailing [security contact] rather than opening a public issue.
