# Changelog

All notable changes to the DiffPilot extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2025-12-10

### Added
- Complete TypeScript/Node.js implementation
- Same 9 MCP tools as C# version
- Bundled MCP server (no external runtime required except Node.js)
- Full compatibility with VS Code 1.101+ MCP features

### Changed
- Migrated from C#/.NET 9 to TypeScript/Node.js
- Reduced extension size and faster startup
- Simplified installation (no .NET SDK required)

### Removed
- .NET 9 SDK dependency
- `dotnetPath` configuration (replaced with `nodePath`)

## [1.x.x] - Previous Versions

See the [C# version changelog](https://github.com/bkalafat/DiffPilot) for previous release notes.
