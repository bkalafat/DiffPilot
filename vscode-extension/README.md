# DiffPilot for Visual Studio Code

AI-powered PR code review and developer productivity tools.

![DiffPilot](images/banner.png)

## Features

🔍 **PR Review Tools**
- **Get PR Diff** - Fetch diff between branches
- **Review PR Changes** - AI-powered code review
- **Generate PR Title** - Conventional commit format titles
- **Generate PR Description** - Complete PR descriptions with checklist

🚀 **Developer Productivity**
- **Generate Commit Message** - Smart commit messages from changes
- **Scan for Secrets** - Detect API keys, passwords, tokens
- **Diff Statistics** - Detailed change metrics
- **Suggest Tests** - AI-recommended test cases
- **Generate Changelog** - Automatic changelog from commits

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Git installed and accessible

## Installation

1. Install the extension from VS Code Marketplace
2. Open a Git repository
3. Use Command Palette (`Ctrl+Shift+P`) and type "DiffPilot"

## Usage

### Command Palette
Press `Ctrl+Shift+P` and type any DiffPilot command:
- `DiffPilot: Get PR Diff`
- `DiffPilot: Review PR Changes`
- `DiffPilot: Generate PR Title`
- `DiffPilot: Generate PR Description`
- `DiffPilot: Generate Commit Message`
- `DiffPilot: Scan for Secrets`
- `DiffPilot: Get Diff Statistics`
- `DiffPilot: Suggest Tests`
- `DiffPilot: Generate Changelog`

### SCM Integration
Right-click in the Source Control panel to access:
- Generate Commit Message
- Scan for Secrets

## Extension Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `diffpilot.dotnetPath` | Path to dotnet executable | `dotnet` |
| `diffpilot.serverPath` | Path to DiffPilot server | (bundled) |
| `diffpilot.defaultBaseBranch` | Default base branch | `main` |
| `diffpilot.prTitleStyle` | PR title style | `conventional` |
| `diffpilot.commitMessageStyle` | Commit message style | `conventional` |
| `diffpilot.includeChecklist` | Include PR checklist | `true` |
| `diffpilot.scanOnSave` | Auto-scan for secrets | `false` |

## Security Features

DiffPilot scans for:
- 🔑 API Keys (AWS, GitHub, Slack)
- 🔐 Private Keys (RSA, DSA, EC, OpenSSH)
- 🔒 Passwords in URLs and variables
- 🎫 Tokens (Bearer, JWT, Azure)

## Known Issues

- Requires .NET 9 SDK installed
- Some features require an active Git repository

## Release Notes

### 1.0.0
- Initial release
- 9 PR and developer tools
- Secret scanning
- Conventional commit support

## Contributing

Found a bug or have a feature request? [Open an issue](https://github.com/bkalafat/DiffPilot/issues) on GitHub.

## License

MIT License - see [LICENSE](LICENSE) for details.

---

**Made with ❤️ by [Burak Kalafat](https://github.com/bkalafat)**
