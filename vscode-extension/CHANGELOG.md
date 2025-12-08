# Change Log

All notable changes to DiffPilot will be documented here.

## [1.1.5] - 2025-12-08

### Changed
- Updated README with use cases: self-review before PR, reviewer workflow
- Added example prompts with `#tool` syntax
- Highlighted auto branch detection feature
- Improved package description

## [1.1.4] - 2025-12-07

### Changed
- New extension icon (lens with plus/minus)

## [1.1.2] - 2025-12-07

### Changed
- Optimized package size (removed unused assets)
- Using high-quality 128x128 icon

## [1.1.1] - 2025-12-07

### Changed
- Updated extension icon (removed background circle)

## [1.1.0] - 2025-12-07

### Changed
- Improved tool documentation with concise descriptions

## [1.0.9] - 2025-12-07

### Fixed
- Server now uses workspace folder for git operations
- Tools work correctly when invoked via MCP

## [1.0.8] - 2025-12-07

### Changed
- Shortened tool descriptions for cleaner UI display

## [1.0.7] - 2025-12-07

### Fixed
- Bundled server now includes TargetFramework for standalone builds
- Server starts correctly without Directory.Build.props dependency

## [1.0.6] - 2025-12-07

### Fixed
- MCP server auto-registration now works correctly with VS Code 1.101+
- Extension automatically appears in MCP Servers list after installation

### Changed
- Minimum VS Code version updated to 1.101.0 for MCP support

## [1.0.5] - 2025-12-07

### Added
- Published to NuGet as .NET tool (`dotnet tool install -g DiffPilot`)
- Published to MCP Registry (registry.modelcontextprotocol.io)

## [1.0.0] - 2025-12-06

### Added
- Initial release with 9 MCP tools
- **PR Review**: `get_pr_diff`, `review_pr_changes`, `generate_pr_title`, `generate_pr_description`
- **Developer Tools**: `generate_commit_message`, `scan_secrets`, `diff_stats`, `suggest_tests`, `generate_changelog`
- Auto branch detection (main/master/develop)
- Secret scanning patterns (API keys, passwords, tokens, JWT)
- SCM panel integration
- Status bar indicator
