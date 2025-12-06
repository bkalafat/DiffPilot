# DiffPilot - Copilot Instructions

## Project Overview
DiffPilot is an MCP (Model Context Protocol) server that provides PR code review and developer productivity tools via JSON-RPC 2.0 over stdio.

## Tech Stack
- **Language**: C# 13 (.NET 9)
- **Protocol**: MCP stdio transport (JSON-RPC 2.0)
- **Testing**: xUnit 2.9.2
- **No external runtime dependencies** - uses only .NET BCL

## Project Structure
```
DiffPilot/
├── .editorconfig               # Code style configuration
├── .gitignore
├── Directory.Build.props       # Shared build properties (all projects)
├── DiffPilot.sln               # Solution file with folder organization
├── DiffPilot.csproj            # Main project file
├── README.md
│
├── .github/
│   ├── copilot-instructions.md # This file
│   └── instructions/
│       └── dotnet9-best-practices.md  # .NET 9 coding standards
│
├── src/                        # Source code
│   ├── Program.cs              # Entry point - stdin/stdout JSON-RPC loop
│   ├── Git/
│   │   └── GitService.cs       # Git command execution, branch detection
│   ├── Protocol/
│   │   ├── JsonRpcModels.cs    # JSON-RPC 2.0 request/response models
│   │   └── McpHandlers.cs      # MCP method handlers
│   └── Tools/
│       ├── ToolResult.cs       # Tool response wrapper
│       ├── PrReviewTools.cs    # PR review tools (get_pr_diff, etc.)
│       └── DeveloperTools.cs   # Developer tools (scan_secrets, etc.)
│
└── tests/                      # Unit tests
    ├── DiffPilot.Tests.csproj
    ├── SecretScanningTests.cs
    ├── ChangelogGenerationTests.cs
    ├── DiffStatsParsingTests.cs
    ├── TestSuggestionAnalysisTests.cs
    ├── CommitTypeDetectionTests.cs
    ├── PrGenerationTests.cs
    └── GitValidationTests.cs
```

## Available MCP Tools

### PR Review Tools
1. `get_pr_diff` - Raw diff between branches
2. `review_pr_changes` - Diff with AI review instructions
3. `generate_pr_title` - Conventional PR title from changes
4. `generate_pr_description` - Complete PR description with checklist

### Developer Productivity Tools
5. `generate_commit_message` - Generate commit message from staged/unstaged changes
6. `scan_secrets` - Detect API keys, passwords, tokens in changes
7. `diff_stats` - Get detailed change statistics
8. `suggest_tests` - Recommend test cases for changed code
9. `generate_changelog` - Generate changelog from commits

## Key Conventions
- All output to stdout must be valid JSON-RPC responses
- Diagnostics/logging go to stderr only
- No files are created - all output returned directly
- Git commands run via `GitService.RunGitCommandAsync()`
- Tools return `ToolResult.Success()` or `ToolResult.Error()`

## Build & Test
```bash
# Build
dotnet build

# Run tests (213 unit tests)
dotnet test

# Run the MCP server
dotnet run
```

## Best Practices
See `.github/instructions/dotnet9-best-practices.md` for:
- C# 13 features (primary constructors, collection expressions)
- Async/await patterns
- Performance optimizations
- Exception handling
- Unit testing conventions

## VS Code Extension

The project includes a VS Code extension that wraps the MCP server for Marketplace distribution.

### Extension Structure
```
vscode-extension/
├── package.json              # Extension manifest
├── src/
│   ├── extension.ts          # Entry point
│   └── client.ts             # MCP JSON-RPC client
├── server/                   # Bundled DiffPilot source
└── images/                   # Icons and banner
```

### Extension Commands
```bash
# Build VSIX package
cd vscode-extension
vsce package

# Publish to Marketplace
vsce publish
```

### Marketplace
- **Publisher**: BurakKalafat
- **Extension ID**: BurakKalafat.diffpilot
- **URL**: https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot
