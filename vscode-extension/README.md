# DiffPilot - MCP Server for Code Review

<!-- mcp-name: io.github.bkalafat/diffpilot -->

**Model Context Protocol (MCP) Server** for AI-powered PR code review and developer productivity.

> 🔌 **MCP Compatible** - Works with GitHub Copilot, Claude, and other MCP-enabled AI assistants

## 🏢 On-Premise & Enterprise Ready

DiffPilot runs **100% locally** - no cloud services, no external API calls, no data leaves your network.

✅ Air-Gapped Environments | ✅ Azure DevOps Server / TFS | ✅ Banking & Financial | ✅ GDPR Compliant

## MCP Tools

| Tool | Command | Description |
|------|---------|-------------|
| `get_pr_diff` | `DiffPilot: Get PR Diff` | Fetches raw diff between base and feature branches |
| `review_pr_changes` | `DiffPilot: Review PR Changes` | Gets diff with AI review instructions for code review |
| `generate_pr_title` | `DiffPilot: Generate PR Title` | Generates conventional PR title from changes |
| `generate_pr_description` | `DiffPilot: Generate PR Description` | Creates PR description with summary, changes, and checklist |
| `generate_commit_message` | `DiffPilot: Generate Commit Message` | Generates commit message from staged/unstaged changes |
| `scan_secrets` | `DiffPilot: Scan for Secrets` | Detects API keys, passwords, tokens in changes |
| `diff_stats` | `DiffPilot: Get Diff Statistics` | Returns lines added/removed, files changed by type |
| `suggest_tests` | `DiffPilot: Suggest Tests` | Analyzes changes and recommends test cases |
| `generate_changelog` | `DiffPilot: Generate Changelog` | Generates changelog entries from commits |

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git installed

## Installation

1. Install from VS Code Marketplace
2. Open a Git repository
3. Extension auto-registers as MCP server - ready to use with Copilot Agent mode

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `diffpilot.dotnetPath` | `dotnet` | Path to dotnet executable |
| `diffpilot.defaultBaseBranch` | `main` | Default base branch for comparisons |
| `diffpilot.prTitleStyle` | `conventional` | PR title style: conventional, descriptive, ticket |
| `diffpilot.commitMessageStyle` | `conventional` | Commit message style: conventional, simple |
| `diffpilot.includeChecklist` | `true` | Include checklist in PR descriptions |

## Security Scanning

Detects: 🔑 API Keys (AWS, GitHub, Slack) | 🔐 Private Keys | 🔒 Passwords | 🎫 Tokens (JWT, Bearer, Azure)

## Changelog

### 1.0.6
- **Fixed**: MCP server auto-registration for VS Code 1.101+
- **Updated**: Minimum VS Code version to 1.101.0

### 1.0.5
- Published to NuGet and MCP Registry

### 1.0.0
- Initial release with 9 MCP tools

## License

MIT - [Burak Kalafat](https://github.com/bkalafat)
