# DiffPilot - Local AI Code Review

**Review your code before creating a PR. 100% local.**

> 🔌 MCP Server for GitHub Copilot, Claude, and AI assistants  
> ⚡ TypeScript/Node.js implementation - No external dependencies required!

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
- ⚡ **Zero External Dependencies** - Pure TypeScript/Node.js, no .NET or Python required

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
| `diffpilot.nodePath` | `node` | Path to Node.js executable |

---

## 📦 Requirements

- VS Code 1.101+
- Node.js 18+
- Git

> **Note:** This is the TypeScript implementation. No .NET SDK required!

---

## 🆚 C# vs TypeScript Version

| Feature | C# (.NET 9) | TypeScript (Node.js) |
|---------|-------------|---------------------|
| Runtime | .NET 9 SDK | Node.js 18+ |
| Startup | Slower (JIT) | Faster |
| Binary Size | Larger | Smaller |
| Cross-platform | ✅ | ✅ |
| Dependencies | None | @modelcontextprotocol/sdk |

---

## 📜 Version History

### 2.0.0 (2025-12-10)
- 🎉 Complete TypeScript/Node.js rewrite
- ⚡ No .NET dependency required
- 🔄 Same 9 MCP tools, better performance
- 📦 Smaller extension size

---

## 📄 License

MIT - [Burak Kalafat](https://github.com/bkalafat)

**[GitHub](https://github.com/bkalafat/DiffPilot-TS)** • **[Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)**
