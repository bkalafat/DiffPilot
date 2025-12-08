# MCP Tools Reference

DiffPilot provides 9 MCP tools. **Auto branch detection** - no need to specify base branch.

---

## PR Review Tools

### #get_pr_diff
Get raw diff between branches.

```
@workspace #get_pr_diff
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch (main/master/develop) |
| `featureBranch` | current branch | Feature branch to compare |

---

### #review_pr_changes
**Most used tool.** AI-powered code review on your local changes.

```
@workspace #review_pr_changes
@workspace #review_pr_changes focus on security and performance
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch |
| `focusAreas` | - | Focus areas: security, performance, error handling |

---

### #generate_pr_title
Generate conventional PR title from changes.

```
@workspace #generate_pr_title
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch |
| `style` | conventional | Style: conventional, descriptive, ticket |

---

### #generate_pr_description
Generate complete PR description with checklist.

```
@workspace #generate_pr_description
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch |
| `ticketUrl` | - | Optional ticket/issue URL |
| `includeChecklist` | true | Include PR checklist |

---

## Developer Productivity Tools

### #generate_commit_message
Generate commit message from staged/unstaged changes.

```
@workspace #generate_commit_message
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `style` | conventional | Style: conventional, simple |
| `scope` | - | Scope for conventional commits (api, ui) |
| `includeBody` | true | Include body section |

---

### #scan_secrets
**Run before every commit!** Detect secrets, API keys, passwords.

```
@workspace #scan_secrets
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `scanStaged` | true | Scan staged changes |
| `scanUnstaged` | true | Scan unstaged changes |

**Detects:** AWS keys, GitHub tokens, passwords, JWT, Bearer tokens, Azure connection strings

---

### #diff_stats
Get change statistics and breakdown.

```
@workspace #diff_stats
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch |
| `featureBranch` | current branch | Feature branch |
| `includeWorkingDir` | true | Include working directory stats |

---

### #suggest_tests
Suggest tests for changed code.

```
@workspace #suggest_tests
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | auto-detected | Base branch |

---

### #generate_changelog
Generate changelog from commits (Keep a Changelog format).

```
@workspace #generate_changelog
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseBranch` | main | Base branch |
| `featureBranch` | current branch | Feature branch |
| `format` | keepachangelog | Format: keepachangelog, simple |
