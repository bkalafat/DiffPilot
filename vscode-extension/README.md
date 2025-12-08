# DiffPilot - Local AI Code Review

**Review your code before creating a PR. 100% local.**

> 🔌 MCP Server for GitHub Copilot, Claude, and AI assistants

---

## 💡 What Does DiffPilot Do?

1. **Self-Review Before PR** - After your last commit, run AI code review locally before pushing
2. **Reviewer Workflow** - Checkout any branch and get AI-assisted code review
3. **Auto Branch Detection** - No need to specify base branch - DiffPilot finds it

---

## 🚀 Quick Start

```
# Review my changes (auto-detects main/master/develop)
@workspace #review_pr_changes

# Review with focus
@workspace #review_pr_changes focus on security

# Generate commit message
@workspace #generate_commit_message

# Scan for secrets
@workspace #scan_secrets
```

---

## 🛠️ 9 MCP Tools

| Tool | Example Prompt |
|------|----------------|
| `#get_pr_diff` | "Get diff between branches" |
| `#review_pr_changes` | "Review my PR for security" |
| `#generate_pr_title` | "Generate conventional PR title" |
| `#generate_pr_description` | "Create PR description" |
| `#generate_commit_message` | "Generate commit message" |
| `#scan_secrets` | "Check for API keys" |
| `#diff_stats` | "Show change statistics" |
| `#suggest_tests` | "What tests to write?" |
| `#generate_changelog` | "Generate changelog" |

---

## ✨ Key Features

- 🔄 **Auto Branch Detection** - Finds `main`, `master`, `develop` automatically
- 🔐 **Secret Scanning** - Detects API keys, passwords, tokens, JWT
- 📊 **Diff Statistics** - Lines added/removed, file breakdown
- 🧪 **Test Suggestions** - Pattern-based recommendations
- 🏢 **Enterprise Ready** - Azure DevOps, TFS, air-gapped environments

---

## 📋 Use Cases

### Self-Review Before PR
```
# After finishing work, before creating PR:
@workspace #review_pr_changes

# Fix issues locally, then push
```

### Code Reviewer Workflow
```bash
git checkout feature/user-auth
# Then in Copilot:
@workspace #review_pr_changes focus on security
```

### Pre-Commit Secret Check
```
@workspace #scan_secrets
# Catches secrets before commit
```

---

## ⚙️ Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `diffpilot.defaultBaseBranch` | `main` | Default base branch |
| `diffpilot.prTitleStyle` | `conventional` | PR title style |
| `diffpilot.commitMessageStyle` | `conventional` | Commit style |

---

## 📦 Requirements

- VS Code 1.101+
- .NET 9 SDK
- Git

---

## 📜 Version History

### 1.1.5 (2025-12-08)
- Updated docs with use cases and `#tool` prompts

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
- Shortened tool descriptions

### 1.0.7 (2025-12-07)
- Fixed: Bundled server TargetFramework

### 1.0.6 (2025-12-07)
- Fixed: MCP auto-registration for VS Code 1.101+

### 1.0.5 (2025-12-07)
- Published to NuGet and MCP Registry

### 1.0.0 (2025-12-06)
- Initial release with 9 MCP tools

---

## 📄 License

MIT - [Burak Kalafat](https://github.com/bkalafat)

**[GitHub](https://github.com/bkalafat/DiffPilot)** • **[Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)**
