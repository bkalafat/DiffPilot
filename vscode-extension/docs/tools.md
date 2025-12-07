# MCP Tools Reference

DiffPilot provides 9 MCP tools for PR review and developer productivity.

## PR Review Tools

### get_pr_diff

Fetches the raw diff between base branch and current/feature branch.

**Command**: `DiffPilot: Get PR Diff`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch (default: auto-detected or 'main') |
| `featureBranch` | string | No | Feature branch (default: current branch) |
| `remote` | string | No | Git remote (default: 'origin') |

**Example prompt**: "Get the diff between main and my current branch"

---

### review_pr_changes

Gets the PR diff with AI review instructions for code review.

**Command**: `DiffPilot: Review PR Changes`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch for comparison |
| `focusAreas` | string | No | Focus areas like 'security, performance, error handling' |

**Example prompt**: "Review my PR changes focusing on security"

---

### generate_pr_title

Generates a conventional PR title from changes.

**Command**: `DiffPilot: Generate PR Title`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch for comparison |
| `style` | string | No | Style: 'conventional', 'descriptive', or 'ticket' |

**Example prompt**: "Generate a PR title for my changes"

---

### generate_pr_description

Generates a complete PR description with summary, changes, and checklist.

**Command**: `DiffPilot: Generate PR Description`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch for comparison |
| `ticketUrl` | string | No | Optional ticket/issue URL to include |
| `includeChecklist` | boolean | No | Include PR checklist (default: true) |

**Example prompt**: "Generate a PR description for this feature"

---

## Developer Productivity Tools

### generate_commit_message

Generates commit message from staged or unstaged changes.

**Command**: `DiffPilot: Generate Commit Message`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `style` | string | No | Style: 'conventional' or 'simple' |
| `scope` | string | No | Scope for conventional commits (e.g., 'api', 'ui') |
| `includeBody` | boolean | No | Include body section (default: true) |

**Example prompt**: "Generate a commit message for my staged changes"

---

### scan_secrets

Scans changes for accidentally committed secrets, API keys, passwords, and tokens.

**Command**: `DiffPilot: Scan for Secrets`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `scanStaged` | boolean | No | Scan staged changes (default: true) |
| `scanUnstaged` | boolean | No | Scan unstaged changes (default: true) |

**Example prompt**: "Scan my changes for any secrets or API keys"

---

### diff_stats

Gets detailed statistics about changes: lines added/removed, files changed, breakdown by file type.

**Command**: `DiffPilot: Get Diff Statistics`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch for comparison |
| `featureBranch` | string | No | Feature branch for comparison |
| `includeWorkingDir` | boolean | No | Include working directory stats (default: true) |

**Example prompt**: "Show me statistics about my changes"

---

### suggest_tests

Analyzes changed code and suggests appropriate test cases.

**Command**: `DiffPilot: Suggest Tests`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch for comparison |

**Example prompt**: "What tests should I write for these changes?"

---

### generate_changelog

Generates changelog entries from commits between branches.

**Command**: `DiffPilot: Generate Changelog`

**Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `baseBranch` | string | No | Base branch (default: 'main') |
| `featureBranch` | string | No | Feature branch (default: current branch) |
| `format` | string | No | Format: 'keepachangelog' or 'simple' |

**Example prompt**: "Generate a changelog for the commits since main"
