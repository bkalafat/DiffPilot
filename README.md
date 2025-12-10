# 🔍 DiffPilot

**Local AI Code Review Before You Push**

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![VS Code](https://img.shields.io/badge/VS%20Code-1.101+-007ACC?style=for-the-badge&logo=visualstudiocode&logoColor=white)](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)
[![MCP](https://img.shields.io/badge/MCP-Protocol-00ADD8?style=for-the-badge)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

---

## 💡 Why DiffPilot?

**Review your code locally before creating a PR.** DiffPilot is an MCP server that lets you:

1. **Self-Review Before PR** - Run AI code review on your local changes after your last commit, before pushing
2. **Reviewer Workflow** - As a code reviewer, checkout the source branch locally and get AI-assisted review
3. **Auto Branch Detection** - No need to specify `main` - DiffPilot finds your base branch automatically

> 🔒 **100% Local** - No cloud, no external APIs. Works with Azure DevOps, TFS, air-gapped environments.

---

## 🚀 Quick Start

### Install
```bash
# VS Code Marketplace
ext install BurakKalafat.diffpilot

# Or NuGet (.NET tool)
dotnet tool install -g DiffPilot
```

### Use with GitHub Copilot
```
# Review my changes (auto-detects base branch)
@workspace #review_pr_changes

# Review with focus areas
@workspace #review_pr_changes focus on security and error handling

# Generate commit message
@workspace #generate_commit_message

# Scan for secrets before committing
@workspace #scan_secrets
```

---

## 🛠️ 9 MCP Tools

### PR Review Tools
| Tool | Example Prompt |
|------|----------------|
| `#get_pr_diff` | "Show diff between my branch and main" |
| `#review_pr_changes` | "Review my PR for security issues" |
| `#generate_pr_title` | "Generate a conventional PR title" |
| `#generate_pr_description` | "Create PR description with checklist" |

### Developer Tools
| Tool | Example Prompt |
|------|----------------|
| `#generate_commit_message` | "Generate commit message for staged changes" |
| `#scan_secrets` | "Check for API keys in my changes" |
| `#diff_stats` | "Show change statistics" |
| `#suggest_tests` | "What tests should I write?" |
| `#generate_changelog` | "Generate changelog from commits" |

---

## ✨ Key Features

| Feature | Description |
|---------|-------------|
| 🔄 **Auto Branch Detection** | Automatically finds `main`, `master`, or `develop` |
| 🔐 **Secret Scanning** | Detects API keys, passwords, tokens, JWT |
| 📊 **Diff Statistics** | Lines added/removed, file breakdown by type |
| 🧪 **Test Suggestions** | Pattern-based test case recommendations |
| 📝 **Conventional Commits** | Generate `feat:`, `fix:`, `refactor:` messages |
| 🛡️ **Enterprise Security** | Bank-grade input validation, rate limiting, output sanitization |

---

## � Security

DiffPilot implements enterprise-grade security features:

| Security Feature | Description |
|-----------------|-------------|
| **Input Validation** | All parameters validated against strict patterns |
| **Injection Prevention** | Command injection, path traversal protection |
| **Output Sanitization** | Auto-redacts secrets from tool outputs |
| **Rate Limiting** | Prevents DoS attacks (120 req/min) |
| **Secure Errors** | No internal details exposed to clients |
| **Audit Logging** | Security events logged to stderr |

**Auto-Redacted Patterns:** API keys, AWS credentials, GitHub/Slack tokens, JWTs, passwords, private keys, connection strings.

See [SECURITY.md](SECURITY.md) for full documentation.

---

## �📋 Use Cases

### 1. Self-Review Before PR
```
# After finishing your work, before creating PR:
@workspace #review_pr_changes

# AI reviews your changes and provides feedback
# Fix issues locally, then push with confidence
```

### 2. Code Reviewer Workflow
```bash
# Checkout the feature branch locally
git checkout feature/user-auth

# Use DiffPilot to review
@workspace #review_pr_changes focus on security

# Get structured review with AI assistance
```

### 3. Pre-Commit Secret Check
```
@workspace #scan_secrets

# Catches API keys, passwords, tokens before they're committed
```

---

## ⚙️ Configuration

```json
{
  "diffpilot.defaultBaseBranch": "main",
  "diffpilot.prTitleStyle": "conventional",
  "diffpilot.commitMessageStyle": "conventional"
}
```

---

## 📦 Installation Options

| Method | Command |
|--------|---------|
| VS Code | `ext install BurakKalafat.diffpilot` |
| NuGet | `dotnet tool install -g DiffPilot` |
| Manual | `git clone` + `dotnet build` |

**Requirements:** .NET 9 SDK, VS Code 1.101+, Git

---

## 📜 Version History

### 1.2.0 (2025-12-09)
- **Security Hardening** - Bank-grade security features
  - Input validation (CWE-20)
  - Command injection prevention (CWE-78)
  - Path traversal protection (CWE-22)
  - Output sanitization - auto-redacts secrets (CWE-200)
  - Rate limiting (CWE-400)
  - Secure error handling
- Added SECURITY.md documentation
- 80 new security unit tests

### 1.1.5 (2025-12-08)
- Updated README with use cases and `#tool` prompts
- Highlighted auto branch detection

### 1.1.4 (2025-12-07)
- Icon refinements

### 1.1.3 (2025-12-07)
- New extension icon (lens with plus/minus)

### 1.1.2 (2025-12-07)
- Optimized package size

### 1.1.1 (2025-12-07)
- Updated extension icon

### 1.1.0 (2025-12-07)
- Improved tool documentation

### 1.0.9 (2025-12-07)
- Fixed: Server uses workspace folder for git operations

### 1.0.8 (2025-12-07)
- Shortened tool descriptions for cleaner UI

### 1.0.7 (2025-12-07)
- Fixed: Bundled server includes TargetFramework

### 1.0.6 (2025-12-07)
- Fixed: MCP auto-registration for VS Code 1.101+

### 1.0.5 (2025-12-07)
- Published to NuGet and MCP Registry

### 1.0.0 (2025-12-06)
- Initial release with 9 MCP tools

---

## 📄 License

MIT License - [Burak Kalafat](https://github.com/bkalafat)

---

**[GitHub](https://github.com/bkalafat/DiffPilot)** • **[VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)** • **[NuGet](https://www.nuget.org/packages/DiffPilot)**

⭐ Star if useful!
