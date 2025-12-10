# DiffPilot TypeScript

**Local AI Code Review for GitHub Copilot, Claude, and AI Assistants**

> 🔌 MCP Server - TypeScript/Node.js Implementation  
> ⚡ No .NET required - Just Node.js 18+

---

## 💡 What Does DiffPilot Do?

1. **Self-Review Before PR** - After your last commit, run AI code review locally before pushing
2. **Reviewer Workflow** - Checkout any branch and get AI-assisted code review
3. **Auto Branch Detection** - No need to specify base branch - DiffPilot finds it

---

## 📦 Installation

### VS Code Extension (Recommended)

Install from [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)

### npm (Global)

```bash
npm install -g diffpilot
```

### npx (Direct Usage)

```bash
npx diffpilot
```

### Manual MCP Configuration

Add to your MCP settings (Claude Desktop, etc.):

```json
{
  "mcpServers": {
    "diffpilot": {
      "command": "npx",
      "args": ["diffpilot"]
    }
  }
}
```

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

| Tool | Description |
|------|-------------|
| `get_pr_diff` | Get raw diff between branches |
| `review_pr_changes` | Diff with AI review instructions |
| `generate_pr_title` | Conventional PR title |
| `generate_pr_description` | Full PR description with checklist |
| `generate_commit_message` | Commit message from changes |
| `scan_secrets` | Detect API keys, passwords, tokens |
| `diff_stats` | Change statistics by file |
| `suggest_tests` | Recommend test cases |
| `generate_changelog` | Changelog from commits |

---

## ✨ Key Features

- 🔄 **Auto Branch Detection** - Finds `main`, `master`, `develop` automatically
- 🔐 **Secret Scanning** - Detects API keys, passwords, tokens, JWT, private keys
- 📊 **Diff Statistics** - Lines added/removed, file breakdown
- 🧪 **Test Suggestions** - Pattern-based recommendations
- 🏢 **Enterprise Ready** - Azure DevOps, TFS, air-gapped environments
- ⚡ **Zero External Dependencies** - Pure TypeScript/Node.js

---

## 📁 Project Structure

```
diffpilot-ts/
├── src/
│   ├── index.ts           # MCP Server entry point
│   ├── git/               # Git operations
│   │   └── git-service.ts
│   ├── security/          # Security helpers
│   │   └── security-helpers.ts
│   ├── tools/             # MCP tool implementations
│   │   ├── pr-review.ts   # PR review tools
│   │   ├── developer.ts   # Developer tools
│   │   └── types.ts       # Shared types
│   └── utils/             # Utilities
├── tests/                 # Vitest tests
├── vscode-extension/      # VS Code Extension
│   ├── src/
│   │   └── extension.ts
│   ├── images/
│   └── package.json
├── package.json
├── tsconfig.json
└── vitest.config.ts
```

---

## 🔧 Development

### Prerequisites

- Node.js 18+
- Git

### Setup

```bash
# Install dependencies
npm install

# Build
npm run build

# Run tests
npm test

# Run in development
npm run dev
```

### Build Extension

```bash
cd vscode-extension
npm install
npm run package
```

This creates `diffpilot-2.0.0.vsix` in the vscode-extension folder.

---

## 🆚 C# vs TypeScript Version

| Feature | C# (.NET 9) | TypeScript (Node.js) |
|---------|-------------|---------------------|
| Runtime | .NET 9 SDK | Node.js 18+ |
| Startup | Slower (JIT) | Faster |
| Binary Size | ~50MB | ~7MB |
| Dependencies | None | @modelcontextprotocol/sdk |
| Performance | Excellent | Very Good |

Choose TypeScript if:
- You don't have .NET SDK installed
- You want faster startup time
- You prefer smaller extension size

Choose C# if:
- You already have .NET 9 SDK
- You want maximum performance
- You're in a .NET-heavy environment

---

## 📄 License

MIT - [Burak Kalafat](https://github.com/bkalafat)

---

## 🔗 Links

- **VS Code Marketplace**: [DiffPilot Extension](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)
- **C# Version**: [DiffPilot (C#)](https://github.com/bkalafat/DiffPilot)
- **Issues**: [GitHub Issues](https://github.com/bkalafat/DiffPilot-TS/issues)
