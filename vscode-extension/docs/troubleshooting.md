# Troubleshooting

Common issues and solutions for DiffPilot.

## Extension Not Showing as MCP Server

### Symptoms
- Extension installed but not visible in MCP servers list
- `@mcp` search doesn't show DiffPilot

### Solutions

1. **Check VS Code version**
   - DiffPilot requires VS Code 1.101.0 or later
   - Run `code --version` to check
   - Update VS Code if needed

2. **Restart VS Code**
   - Close all VS Code windows
   - Reopen VS Code

3. **Check Output panel**
   - View → Output
   - Select "DiffPilot" from dropdown
   - Look for error messages

4. **Verify .NET SDK**
   ```bash
   dotnet --version
   ```
   Should show 9.0.x or later

## Server Fails to Start

### Symptoms
- Error message when using DiffPilot commands
- MCP server shows as stopped

### Solutions

1. **Check .NET installation**
   ```bash
   dotnet --list-sdks
   ```
   Ensure .NET 9 SDK is installed

2. **Set custom dotnet path**
   ```json
   {
     "diffpilot.dotnetPath": "/path/to/dotnet"
   }
   ```

3. **Check server path**
   - Open Output panel → DiffPilot
   - Look for path-related errors
   - Verify bundled server exists

## Git Repository Not Detected

### Symptoms
- "Not a git repository" errors
- Commands fail with git errors

### Solutions

1. **Open a folder with .git**
   - File → Open Folder
   - Select a folder containing `.git` directory

2. **Check git installation**
   ```bash
   git --version
   ```

3. **Initialize git if needed**
   ```bash
   git init
   ```

## Diff Returns Empty

### Symptoms
- PR diff or review returns no changes
- Stats show 0 files changed

### Solutions

1. **Check branch names**
   - Verify base branch exists: `git branch -a`
   - Ensure there are actual changes between branches

2. **Fetch remote branches**
   ```bash
   git fetch origin
   ```

3. **Check working directory**
   - Some tools check working directory changes
   - Stage or commit changes first

## Secret Scanner False Positives

### Symptoms
- Scanner flags non-secret content
- Test data triggers warnings

### Solutions

1. **Review context**
   - Check if flagged content is actually sensitive
   - Test fixtures may contain fake credentials

2. **Use environment variables**
   - Move real secrets to `.env` file
   - Add `.env` to `.gitignore`

## Performance Issues

### Symptoms
- Commands take too long
- Extension becomes unresponsive

### Solutions

1. **Large repositories**
   - Limit diff scope with specific branches
   - Use working directory mode for local changes

2. **Many unstaged changes**
   - Stage specific files to reduce scope
   - Commit frequently

## Reporting Issues

If problems persist:

1. Open Output panel → DiffPilot
2. Copy the log content
3. [Open an issue](https://github.com/bkalafat/DiffPilot/issues) with:
   - VS Code version
   - .NET SDK version
   - Operating system
   - Error logs
   - Steps to reproduce
