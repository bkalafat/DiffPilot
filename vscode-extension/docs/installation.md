# Installation

## Requirements

- **VS Code 1.101.0** or later (required for MCP support)
- **.NET 9 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Git** - Installed and accessible from command line

## Install from VS Code Marketplace

1. Open VS Code
2. Go to Extensions (`Ctrl+Shift+X`)
3. Search for "DiffPilot"
4. Click **Install**

After installation, DiffPilot automatically registers as an MCP server.

## Verify Installation

1. Open a Git repository in VS Code
2. Open Command Palette (`Ctrl+Shift+P`)
3. Type "MCP: List Servers"
4. You should see "DiffPilot" in the list

## Manual VSIX Installation

If installing from a local VSIX file:

1. Download the `.vsix` file
2. Open VS Code
3. Go to Extensions (`Ctrl+Shift+X`)
4. Click the `...` menu → "Install from VSIX..."
5. Select the downloaded file

## Install via NuGet (CLI)

DiffPilot is also available as a .NET tool:

```bash
dotnet tool install -g DiffPilot
```

Run the server:

```bash
diffpilot
```

## Troubleshooting

### Extension not showing as MCP server

- Ensure VS Code version is 1.101.0 or later
- Restart VS Code after installation
- Check Output panel → "DiffPilot" for errors

### .NET SDK not found

- Install .NET 9 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0)
- Verify with `dotnet --version` in terminal
- Set custom path in settings: `diffpilot.dotnetPath`
