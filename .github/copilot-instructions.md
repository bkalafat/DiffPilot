# DiffPilot - Copilot Instructions

## Overview
MCP server for PR code review & developer productivity. JSON-RPC 2.0 over stdio.

**Stack**: C# 13 / .NET 9 | xUnit | No external deps

## Structure
- `src/Program.cs` - Entry point (stdin/stdout JSON-RPC loop)
- `src/Git/GitService.cs` - Git command execution
- `src/Protocol/` - JSON-RPC models & MCP handlers
- `src/Tools/` - Tool implementations (`PrReviewTools.cs`, `DeveloperTools.cs`)
- `src/Security/` - Security utilities (`SecurityHelpers.cs`)
- `tests/` - xUnit tests (293 total, 80 security tests)
- `vscode-extension/` - VS Code extension wrapper (TypeScript)

## MCP Tools (9 total)
| Tool | Purpose |
|------|---------|
| `get_pr_diff` | Raw diff between branches |
| `review_pr_changes` | Diff with AI review instructions |
| `generate_pr_title` | Conventional PR title |
| `generate_pr_description` | Full PR description with checklist |
| `generate_commit_message` | Commit message from staged/unstaged |
| `scan_secrets` | Detect API keys, passwords, tokens |
| `diff_stats` | Change statistics |
| `suggest_tests` | Recommend test cases |
| `generate_changelog` | Changelog from commits |

## Key Patterns
- **stdout**: JSON-RPC responses only | **stderr**: Diagnostics
- Git via `GitService.RunGitCommandAsync()`
- Tools return `ToolResult.Success()` or `ToolResult.Error()`
- Use C# 13 features (primary constructors, collection expressions)
- See `.github/instructions/dotnet9-best-practices.md` for coding standards

## Security
- **Input Validation**: Use `SecurityHelpers.ValidateBranchName()`, `ValidateRemoteName()`, `ValidateParameter()`
- **Output Sanitization**: All tool outputs pass through `SecurityHelpers.SanitizeOutput()`
- **Rate Limiting**: `SecurityHelpers.CheckRateLimit(toolName)` in McpHandlers
- **Secure Errors**: Never expose stack traces or internal paths
- **Logging**: Security events via `SecurityHelpers.LogSecurityEvent()` to stderr
- See `SECURITY.md` for full documentation

## Commands
```bash
dotnet build          # Build
dotnet test           # Run tests
dotnet run            # Run MCP server
cd vscode-extension && vsce package  # Build VSIX
```

## Extension
- **ID**: `BurakKalafat.diffpilot`
- **Source**: `vscode-extension/src/` (TypeScript)
- **Bundled Server**: `vscode-extension/server/` (C# source copy)
