# Configuration

DiffPilot can be configured through VS Code settings.

## Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `diffpilot.dotnetPath` | string | `dotnet` | Path to the dotnet executable |
| `diffpilot.serverPath` | string | (bundled) | Path to DiffPilot server source |
| `diffpilot.defaultBaseBranch` | string | `main` | Default base branch for comparisons |
| `diffpilot.prTitleStyle` | string | `conventional` | PR title style |
| `diffpilot.commitMessageStyle` | string | `conventional` | Commit message style |
| `diffpilot.includeChecklist` | boolean | `true` | Include checklist in PR descriptions |
| `diffpilot.scanOnSave` | boolean | `false` | Auto-scan for secrets on save |

## Setting Details

### diffpilot.dotnetPath

Path to the .NET executable. Change this if:
- .NET is not in your system PATH
- You want to use a specific .NET version
- Running in a container or isolated environment

```json
{
  "diffpilot.dotnetPath": "C:\\Program Files\\dotnet\\dotnet.exe"
}
```

### diffpilot.defaultBaseBranch

The default branch to compare against. Common values:
- `main` (default)
- `master`
- `develop`

```json
{
  "diffpilot.defaultBaseBranch": "develop"
}
```

### diffpilot.prTitleStyle

Style for generated PR titles:

| Style | Example |
|-------|---------|
| `conventional` | `feat(auth): add OAuth2 login support` |
| `descriptive` | `Add OAuth2 login support to authentication module` |
| `ticket` | `AUTH-123: Add OAuth2 login support` |

### diffpilot.commitMessageStyle

Style for generated commit messages:

| Style | Example |
|-------|---------|
| `conventional` | `fix(api): handle null response from server` |
| `simple` | `Handle null response from server` |

### diffpilot.includeChecklist

When `true`, PR descriptions include a checklist:

```markdown
## Checklist
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] No breaking changes
```

### diffpilot.scanOnSave

When `true`, automatically scans files for secrets when saved. Useful for catching accidental secret commits early.

## Workspace Settings

You can configure DiffPilot per-workspace by adding settings to `.vscode/settings.json`:

```json
{
  "diffpilot.defaultBaseBranch": "develop",
  "diffpilot.prTitleStyle": "ticket",
  "diffpilot.scanOnSave": true
}
```
