# DiffPilot Documentation

DiffPilot is an MCP (Model Context Protocol) server that provides AI-powered PR code review and developer productivity tools. It runs 100% locally and integrates with GitHub Copilot, Claude, and other MCP-enabled AI assistants.

## Table of Contents

- [Installation](installation.md)
- [MCP Tools Reference](tools.md)
- [Configuration](configuration.md)
- [Security Scanning](security.md)
- [Troubleshooting](troubleshooting.md)

## Quick Start

1. Install the extension from VS Code Marketplace
2. Ensure .NET 9 SDK is installed
3. Open a Git repository
4. The extension auto-registers as an MCP server
5. Use Copilot Agent mode or Command Palette commands

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    VS Code                              │
│  ┌──────────────┐    ┌──────────────────────────────┐  │
│  │   Copilot    │◄──►│     DiffPilot Extension      │  │
│  │  Agent Mode  │    │  (MCP Server Provider)       │  │
│  └──────────────┘    └──────────────────────────────┘  │
│                              │                          │
│                              ▼                          │
│                    ┌──────────────────┐                 │
│                    │  DiffPilot MCP   │                 │
│                    │     Server       │                 │
│                    │   (.NET 9)       │                 │
│                    └────────┬─────────┘                 │
│                             │                           │
│                             ▼                           │
│                    ┌──────────────────┐                 │
│                    │       Git        │                 │
│                    │   Repository     │                 │
│                    └──────────────────┘                 │
└─────────────────────────────────────────────────────────┘
```

## Support

- [GitHub Issues](https://github.com/bkalafat/DiffPilot/issues)
- [GitHub Repository](https://github.com/bkalafat/DiffPilot)
