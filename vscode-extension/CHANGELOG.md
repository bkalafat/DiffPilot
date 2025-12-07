# Change Log

All notable changes to the "DiffPilot" extension will be documented in this file.

## [1.0.6] - 2025-12-07

### Fixed
- MCP server auto-registration now works correctly with VS Code 1.101+
- Extension automatically appears in MCP Servers list after installation

### Changed
- Minimum VS Code version updated to 1.101.0 for MCP support

## [1.0.5] - 2025-12-07

### Added
- Published to NuGet as .NET tool
- Published to MCP Registry (registry.modelcontextprotocol.io)

## [1.0.0] - 2025-12-06

### Added
- Initial release with 9 MCP tools
- **PR Review**: `get_pr_diff`, `review_pr_changes`, `generate_pr_title`, `generate_pr_description`
- **Developer Tools**: `generate_commit_message`, `scan_secrets`, `diff_stats`, `suggest_tests`, `generate_changelog`
- SCM panel integration
- Status bar indicator
