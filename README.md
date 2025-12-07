# 🚀 DiffPilot

**MCP Server for AI-Powered PR Code Review**

<!-- mcp-name: io.github.bkalafat/diffpilot -->

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Protocol-00ADD8?style=for-the-badge&logo=json&logoColor=white)](https://modelcontextprotocol.io/)
[![VS Code](https://img.shields.io/badge/VS%20Code-Extension-007ACC?style=for-the-badge&logo=visualstudiocode&logoColor=white)](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

*Works with GitHub Copilot, Claude, Azure DevOps, TFS • 100% Local • Zero Dependencies*

[Features](#-features) • [Installation](#-installation) • [Tools](#-tools) • [Configuration](#-configuration)

---

## 🎯 What is DiffPilot?

DiffPilot is an MCP (Model Context Protocol) server that provides AI-powered PR code review and developer productivity tools. It runs locally and works with any MCP-compatible AI client.

### Key Benefits
- 🔍 **Auto Branch Detection** - Automatically detects your base branch
- 📝 **Smart PR Generation** - Conventional commit titles & comprehensive descriptions
- 🔐 **Secret Scanning** - Detects API keys, passwords, tokens before commit
- 🧪 **Test Suggestions** - Analyzes code patterns and recommends test cases
- ⚡ **Zero Dependencies** - Only uses .NET BCL, no external packages

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔄 **PR Diff** | Get diff between branches |
| 📊 **Code Review** | AI-structured diff output for review |
| 🏷️ **PR Title** | Conventional commit format titles |
| 📋 **PR Description** | Full description with checklist |
| 💬 **Commit Message** | Generate from staged/unstaged changes |
| 🔐 **Secret Scan** | Detect API keys, passwords, tokens |
| 📈 **Diff Stats** | Lines added/removed, file breakdown |
| 🧪 **Test Suggestions** | Pattern-based test case recommendations |
| 📝 **Changelog** | Keep a Changelog format from commits |

---

## ⚡ Installation

### VS Code Extension (Recommended)

1. Install from [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)
2. The extension automatically registers as an MCP server
3. Use with GitHub Copilot Agent Mode or any MCP client

### Manual Setup

```bash
# Prerequisites: .NET 9 SDK, Git

# Clone and build
git clone https://github.com/bkalafat/DiffPilot.git
cd DiffPilot
dotnet build

# Run tests
dotnet test

# Run server
dotnet run
```

### MCP Client Configuration

```json
{
  "mcpServers": {
    "diffpilot": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/DiffPilot"],
      "cwd": "/your/git/repository"
    }
  }
}
```

---

## 🛠️ Tools

### PR Review Tools

| Tool | Purpose |
|------|---------|
| `get_pr_diff` | Raw diff between branches |
| `review_pr_changes` | Diff with AI review instructions |
| `generate_pr_title` | Conventional PR title (feat/fix/refactor) |
| `generate_pr_description` | Full PR description with checklist |

### Developer Tools

| Tool | Purpose |
|------|---------|
| `generate_commit_message` | Commit message from staged/unstaged changes |
| `scan_secrets` | Detect API keys, passwords, tokens |
| `diff_stats` | Change statistics (lines, files, types) |
| `suggest_tests` | Test case recommendations |
| `generate_changelog` | Changelog from commits (Keep a Changelog) |

---

## ⚙️ Configuration

### VS Code Settings

```json
{
  "diffpilot.dotnetPath": "dotnet",
  "diffpilot.serverPath": "",
  "diffpilot.defaultBaseBranch": "main",
  "diffpilot.prTitleStyle": "conventional",
  "diffpilot.commitMessageStyle": "conventional"
}
```

---

## 📖 Usage Examples

```
# Get PR diff
"Show me the changes compared to main branch"

# Code review
"Review this PR for security and performance issues"

# Generate PR title
"Suggest a PR title for these changes"

# Scan for secrets
"Check if there are any secrets in my changes"

# Generate commit message
"Create a commit message for my staged changes"
```

---

## 🏗️ Architecture

```
DiffPilot/
├── src/
│   ├── Program.cs           # Entry point (JSON-RPC loop)
│   ├── Git/GitService.cs    # Git command execution
│   ├── Protocol/            # JSON-RPC & MCP handlers
│   └── Tools/               # Tool implementations
├── tests/                   # Unit tests
└── vscode-extension/        # VS Code extension wrapper
```

**Tech Stack:** C# 13 / .NET 9 | JSON-RPC 2.0 | MCP stdio | xUnit

---

## 📄 License

MIT License - see [LICENSE](LICENSE) file.

---

---

**[GitHub](https://github.com/bkalafat/DiffPilot)** • **[VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)** • **[@bkalafat](https://github.com/bkalafat)**

⭐ Star this repo if you find it useful!
