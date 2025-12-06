# DiffPilot - Copilot Instructions

## Project Overview
DiffPilot is an MCP (Model Context Protocol) server that provides PR code review tools via JSON-RPC 2.0 over stdio.

## Tech Stack
- **Language**: C# (.NET 9)
- **Protocol**: MCP stdio transport (JSON-RPC 2.0)
- **No external dependencies** - uses only .NET BCL

## Project Structure
```
src/
├── Program.cs              # Entry point - stdin/stdout JSON-RPC loop
├── Protocol/
│   ├── JsonRpcModels.cs    # Request/Response/Error models
│   └── McpHandlers.cs      # MCP method handlers (initialize, tools/list, tools/call)
├── Git/
│   └── GitService.cs       # Git command execution, branch detection, validation
└── Tools/
    ├── ToolResult.cs       # Tool response wrapper
    └── PrReviewTools.cs    # Tool implementations
```

## Available MCP Tools
1. `get_pr_diff` - Raw diff between branches
2. `review_pr_changes` - Diff with AI review instructions
3. `generate_pr_title` - Conventional PR title from changes
4. `generate_pr_description` - Complete PR description with checklist

## Key Conventions
- All output to stdout must be valid JSON-RPC responses
- Diagnostics/logging go to stderr only
- No files are created - all output returned directly
- Git commands run via `GitService.RunGitCommandAsync()`
- Tools return `ToolResult.Success()` or `ToolResult.Error()`

## Build & Run
```bash
dotnet build
dotnet run
```
