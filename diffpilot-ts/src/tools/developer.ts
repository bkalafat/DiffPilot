/**
 * Developer Tools - Additional developer productivity tools for the MCP server.
 * 
 * These tools help developers with various workflows:
 * - generate_commit_message: Generate commit message from staged/unstaged changes
 * - scan_secrets: Detect accidentally committed secrets in diff (Phase 2)
 * - diff_stats: Get statistics about changes
 * - suggest_tests: Recommend test cases for changed code
 * - generate_changelog: Generate changelog entries from commits
 * 
 * Ported from: src/Tools/DeveloperTools.cs
 */

import {
  runGitCommand,
  getCurrentBranch,
  findBaseBranch,
  getWorkingDirectory,
} from '../git/git-service.js';
import { success, error, gitError, truncateContent, type ToolResult } from './types.js';

// ============================================================================
// Tool: generate_commit_message
// ============================================================================

/** Parameters for generate_commit_message tool */
export interface GenerateCommitMessageParams {
  style?: 'conventional' | 'simple';
  scope?: string;
  includeBody?: boolean;
}

/** Change analysis result */
interface ChangeAnalysis {
  filesChanged: number;
  linesAdded: number;
  linesRemoved: number;
  changeType: string;
}

/**
 * Analyzes diff content to determine change statistics.
 */
function analyzeChanges(diff: string): ChangeAnalysis {
  const analysis: ChangeAnalysis = {
    filesChanged: 0,
    linesAdded: 0,
    linesRemoved: 0,
    changeType: 'unknown',
  };

  for (const line of diff.split('\n')) {
    if (line.startsWith('+++ b/')) {
      analysis.filesChanged++;
    } else if (line.startsWith('+') && !line.startsWith('+++')) {
      analysis.linesAdded++;
    } else if (line.startsWith('-') && !line.startsWith('---')) {
      analysis.linesRemoved++;
    }
  }

  // Determine change type based on patterns
  const lowerDiff = diff.toLowerCase();
  if (lowerDiff.includes('fix') || lowerDiff.includes('bug')) {
    analysis.changeType = 'bug fix';
  } else if (analysis.linesRemoved > analysis.linesAdded) {
    analysis.changeType = 'refactoring/cleanup';
  } else if (lowerDiff.includes('test')) {
    analysis.changeType = 'testing';
  } else {
    analysis.changeType = 'feature/enhancement';
  }

  return analysis;
}

/**
 * Determines the commit type based on analysis and diff content.
 */
function determineCommitType(analysis: ChangeAnalysis, diff: string): string {
  const lowerDiff = diff.toLowerCase();

  if (lowerDiff.includes('test') || lowerDiff.includes('spec')) return 'test';
  if (lowerDiff.includes('.md') || lowerDiff.includes('readme') || lowerDiff.includes('doc')) return 'docs';
  if (lowerDiff.includes('config') || lowerDiff.includes('.json') || lowerDiff.includes('.yaml')) return 'chore';
  if (lowerDiff.includes('fix') || lowerDiff.includes('bug') || lowerDiff.includes('error')) return 'fix';
  if (analysis.linesRemoved > analysis.linesAdded * 0.5 && analysis.linesAdded > 0) return 'refactor';

  return 'feat';
}

/**
 * Generates a commit message based on staged changes (or unstaged if nothing is staged).
 */
export async function generateCommitMessage(args?: GenerateCommitMessageParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();
  const style = args?.style ?? 'conventional';
  const scope = args?.scope;
  const includeBody = args?.includeBody ?? true;

  // First, check for staged changes
  const stagedResult = await runGitCommand('diff --cached --stat', repoDir);
  const hasStaged = stagedResult.exitCode === 0 && stagedResult.output.trim();

  // Get the diff (staged first, fallback to unstaged)
  let diffOutput: string;
  let changeType: string;

  if (hasStaged) {
    const stagedDiff = await runGitCommand('diff --cached', repoDir);
    if (stagedDiff.exitCode !== 0) {
      return gitError('Failed to get staged diff', stagedDiff.output);
    }
    diffOutput = stagedDiff.output;
    changeType = 'staged';
  } else {
    // Check for unstaged changes
    const unstagedResult = await runGitCommand('diff --stat', repoDir);
    if (unstagedResult.exitCode !== 0 || !unstagedResult.output.trim()) {
      return error(
        'No changes found. Please stage your changes with `git add` or make some modifications first.'
      );
    }

    const unstagedDiff = await runGitCommand('diff', repoDir);
    if (unstagedDiff.exitCode !== 0) {
      return gitError('Failed to get unstaged diff', unstagedDiff.output);
    }
    diffOutput = unstagedDiff.output;
    changeType = 'unstaged';
  }

  if (!diffOutput.trim()) {
    return error('No changes detected in the repository.');
  }

  // Get file stats
  const statsCommand = hasStaged ? 'diff --cached --stat' : 'diff --stat';
  const statsResult = await runGitCommand(statsCommand, repoDir);

  // Analyze the changes
  const analysis = analyzeChanges(diffOutput);
  const commitType = determineCommitType(analysis, diffOutput);

  // Build the response
  let output = '# Commit Message Generator\n\n';
  output += `**Analyzing:** ${changeType} changes\n\n`;

  if (statsResult.exitCode === 0 && statsResult.output.trim()) {
    output += '## Changed Files\n```\n' + statsResult.output.trim() + '\n```\n\n';
  }

  output += '## Change Analysis\n';
  output += `- **Files modified:** ${analysis.filesChanged}\n`;
  output += `- **Lines added:** ${analysis.linesAdded}\n`;
  output += `- **Lines removed:** ${analysis.linesRemoved}\n`;
  output += `- **Primary change type:** ${analysis.changeType}\n\n`;

  output += '## Suggested Commit Message\n\n';

  const scopePart = scope ? `(${scope})` : '';

  output += '```\n';
  if (style === 'conventional') {
    output += `${commitType}${scopePart}: <brief description>\n`;
    if (includeBody) {
      output += '\n<optional body explaining what and why>\n';
    }
  } else {
    output += '<Brief description of changes>\n';
    if (includeBody) {
      output += '\n<Optional detailed explanation>\n';
    }
  }
  output += '```\n\n';

  output += '## Diff Preview\n```diff\n' + truncateContent(diffOutput, 50000) + '\n```\n\n';
  output += 'Please analyze the diff and generate an appropriate commit message based on the actual changes.\n';

  return success(output);
}

// ============================================================================
// Tool: scan_secrets (Phase 2 - Stub)
// ============================================================================

/** Parameters for scan_secrets tool */
export interface ScanSecretsParams {
  scanStaged?: boolean;
  scanUnstaged?: boolean;
}

/** Secret pattern definition */
interface SecretPattern {
  name: string;
  description: string;
  pattern: RegExp;
}

/** Secret finding result */
interface SecretFinding {
  type: string;
  pattern: string;
  match: string;
  location: string;
  source: string;
}

/** Secret patterns to detect */
const SECRET_PATTERNS: SecretPattern[] = [
  { name: 'API Key', description: 'Generic API key pattern', pattern: /['"]?[a-zA-Z0-9_-]*[aA][pP][iI][_-]?[kK][eE][yY]['"]?\s*[:=]\s*['"]?[a-zA-Z0-9_\-]{20,}['"]?/gi },
  { name: 'AWS Access Key', description: 'AWS access key ID', pattern: /AKIA[0-9A-Z]{16}/g },
  { name: 'AWS Secret Key', description: 'AWS secret access key (40-char base64)', pattern: /[a-zA-Z0-9/+=]{40}/g },
  { name: 'GitHub Token', description: 'GitHub personal access token', pattern: /ghp_[a-zA-Z0-9]{36}|github_pat_[a-zA-Z0-9]{22}_[a-zA-Z0-9]{59}/g },
  { name: 'Private Key', description: 'Private key block', pattern: /-----BEGIN\s+(RSA|DSA|EC|OPENSSH)?\s*PRIVATE KEY-----/g },
  { name: 'Password in URL', description: 'Password in connection string', pattern: /:\/\/[^:]+:([^@]+)@/gi },
  { name: 'Password Assignment', description: 'Password variable assignment', pattern: /['"]?[pP]assword['"]?\s*[:=]\s*['"][^'"]{8,}['"]/g },
  { name: 'Bearer Token', description: 'Bearer authentication token', pattern: /[bB]earer\s+[a-zA-Z0-9_\-\.]+/g },
  { name: 'JWT Token', description: 'JSON Web Token', pattern: /eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*/g },
  { name: 'Slack Token', description: 'Slack bot/webhook token', pattern: /xox[baprs]-[0-9]{10,13}-[a-zA-Z0-9-]+/g },
  { name: 'Azure Connection', description: 'Azure storage/key/connection string', pattern: /azure[_-]?(?:storage|key|secret|connection)\s*[:=]\s*['"]?[\w\-./+=]{20,}['"]?/gi },
  { name: 'Generic Secret', description: 'Generic secret/token pattern', pattern: /['"]?(?:secret|token|key|auth)['"]?\s*[:=]\s*['"][a-zA-Z0-9_\-]{16,}['"]/gi },
];

/**
 * Masks a secret value for display.
 */
function maskSecret(secret: string): string {
  if (secret.length <= 8) {
    return '*'.repeat(secret.length);
  }
  return secret.slice(0, 4) + '*'.repeat(secret.length - 8) + secret.slice(-4);
}

/**
 * Scans diff content for secrets.
 */
function scanForSecrets(diff: string, source: string): SecretFinding[] {
  const findings: SecretFinding[] = [];
  const lines = diff.split('\n');
  let currentFile = 'unknown';

  for (const line of lines) {
    // Track current file
    if (line.startsWith('+++ b/')) {
      currentFile = line.slice(6);
      continue;
    }

    // Only scan added lines
    if (!line.startsWith('+') || line.startsWith('+++')) {
      continue;
    }

    const content = line.slice(1); // Remove the + prefix

    // Check each pattern
    for (const pattern of SECRET_PATTERNS) {
      const matches = content.matchAll(pattern.pattern);
      for (const match of matches) {
        findings.push({
          type: pattern.name,
          pattern: pattern.description,
          match: match[0],
          location: currentFile,
          source,
        });
      }
    }
  }

  return findings;
}

/**
 * Scans the diff for accidentally committed secrets, API keys, passwords, etc.
 */
export async function scanSecrets(args?: ScanSecretsParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();
  const scanStaged = args?.scanStaged ?? true;
  const scanUnstaged = args?.scanUnstaged ?? true;

  const findings: SecretFinding[] = [];
  let scannedDiffs = '';

  // Scan staged changes
  if (scanStaged) {
    const stagedDiff = await runGitCommand('diff --cached', repoDir);
    if (stagedDiff.exitCode === 0 && stagedDiff.output.trim()) {
      scannedDiffs += '=== STAGED CHANGES ===\n' + stagedDiff.output;
      findings.push(...scanForSecrets(stagedDiff.output, 'staged'));
    }
  }

  // Scan unstaged changes
  if (scanUnstaged) {
    const unstagedDiff = await runGitCommand('diff', repoDir);
    if (unstagedDiff.exitCode === 0 && unstagedDiff.output.trim()) {
      scannedDiffs += '=== UNSTAGED CHANGES ===\n' + unstagedDiff.output;
      findings.push(...scanForSecrets(unstagedDiff.output, 'unstaged'));
    }
  }

  if (!scannedDiffs) {
    return success('✅ No changes to scan. Working directory is clean.');
  }

  // Build response
  let output = '# 🔐 Secret Scan Results\n\n';

  if (findings.length === 0) {
    output += '## ✅ No Secrets Detected\n\n';
    output += 'No obvious secrets, API keys, or sensitive data patterns were found in the changes.\n\n';
    output += '> **Note:** This is a pattern-based scan and may not catch all secrets.\n';
    output += '> Always review your changes manually before committing sensitive code.\n';
  } else {
    output += `## ⚠️ ${findings.length} Potential Secret(s) Found\n\n`;
    output += 'The following patterns may indicate sensitive data:\n\n';

    for (const finding of findings) {
      output += `### 🚨 ${finding.type}\n`;
      output += `- **Location:** ${finding.location} (${finding.source})\n`;
      output += `- **Pattern:** \`${finding.pattern}\`\n`;
      output += `- **Match:** \`${maskSecret(finding.match)}\`\n\n`;
    }

    output += '## Recommendations\n\n';
    output += '1. **Remove secrets** from your code before committing\n';
    output += '2. **Use environment variables** or a secrets manager\n';
    output += '3. **Add to .gitignore** any files containing secrets\n';
    output += '4. **Consider using** `.env` files (gitignored) for local development\n';
  }

  return {
    content: [{ type: 'text', text: output }],
    isError: findings.length > 0,
  };
}

// ============================================================================
// Tool: diff_stats
// ============================================================================

/** Parameters for diff_stats tool */
export interface DiffStatsParams {
  baseBranch?: string;
  featureBranch?: string;
  includeWorkingDir?: boolean;
}

/** Diff statistics result */
interface DiffStats {
  added: number;
  removed: number;
  files: number;
  fileDetails: { name: string; added: number; removed: number }[];
}

/**
 * Parses numstat output into statistics.
 */
function parseNumstat(numstatOutput: string): DiffStats {
  const stats: DiffStats = { added: 0, removed: 0, files: 0, fileDetails: [] };

  if (!numstatOutput.trim()) {
    return stats;
  }

  for (const line of numstatOutput.split('\n').filter(Boolean)) {
    const parts = line.split('\t');
    if (parts.length >= 3) {
      const added = parts[0] === '-' ? 0 : parseInt(parts[0], 10) || 0;
      const removed = parts[1] === '-' ? 0 : parseInt(parts[1], 10) || 0;
      const fileName = parts[2];

      stats.added += added;
      stats.removed += removed;
      stats.files++;
      stats.fileDetails.push({ name: fileName, added, removed });
    }
  }

  return stats;
}

/**
 * Gets detailed statistics about changes between branches or in working directory.
 */
export async function diffStats(args?: DiffStatsParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();
  let { baseBranch, featureBranch } = args ?? {};
  const includeWorkingDir = args?.includeWorkingDir ?? true;

  let output = '# 📊 Diff Statistics\n\n';

  // Working directory stats
  if (includeWorkingDir) {
    output += '## Working Directory\n\n';

    // Staged changes
    const stagedStat = await runGitCommand('diff --cached --numstat', repoDir);
    const stagedStats = parseNumstat(stagedStat.output);

    // Unstaged changes
    const unstagedStat = await runGitCommand('diff --numstat', repoDir);
    const unstagedStats = parseNumstat(unstagedStat.output);

    output += '### Staged Changes\n';
    if (stagedStats.files > 0) {
      output += '| Metric | Value |\n|--------|-------|\n';
      output += `| Files | ${stagedStats.files} |\n`;
      output += `| Lines Added | +${stagedStats.added} |\n`;
      output += `| Lines Removed | -${stagedStats.removed} |\n`;
      output += `| Net Change | ${stagedStats.added - stagedStats.removed > 0 ? '+' : ''}${stagedStats.added - stagedStats.removed} |\n`;
    } else {
      output += '*No staged changes*\n';
    }
    output += '\n';

    output += '### Unstaged Changes\n';
    if (unstagedStats.files > 0) {
      output += '| Metric | Value |\n|--------|-------|\n';
      output += `| Files | ${unstagedStats.files} |\n`;
      output += `| Lines Added | +${unstagedStats.added} |\n`;
      output += `| Lines Removed | -${unstagedStats.removed} |\n`;
      output += `| Net Change | ${unstagedStats.added - unstagedStats.removed > 0 ? '+' : ''}${unstagedStats.added - unstagedStats.removed} |\n`;
    } else {
      output += '*No unstaged changes*\n';
    }
    output += '\n';
  }

  // Branch comparison stats
  if (baseBranch || featureBranch) {
    // Auto-detect if needed
    if (!featureBranch) {
      featureBranch = (await getCurrentBranch(repoDir)) ?? 'HEAD';
    }

    if (!baseBranch) {
      const baseInfo = await findBaseBranch(repoDir, featureBranch, 'origin');
      baseBranch = baseInfo?.baseBranch ?? 'main';
    }

    output += `## Branch Comparison: \`${baseBranch}\` → \`${featureBranch}\`\n\n`;

    // Fetch latest
    await runGitCommand('fetch origin', repoDir);

    // Get numstat for branch comparison
    const branchStat = await runGitCommand(
      `diff --numstat origin/${baseBranch}...${featureBranch}`,
      repoDir
    );
    const branchStats = parseNumstat(branchStat.output);

    // Get commit count
    const commitCount = await runGitCommand(
      `rev-list --count origin/${baseBranch}..${featureBranch}`,
      repoDir
    );

    output += '| Metric | Value |\n|--------|-------|\n';
    output += `| Commits | ${commitCount.output.trim()} |\n`;
    output += `| Files Changed | ${branchStats.files} |\n`;
    output += `| Lines Added | +${branchStats.added} |\n`;
    output += `| Lines Removed | -${branchStats.removed} |\n`;
    output += `| Net Change | ${branchStats.added - branchStats.removed > 0 ? '+' : ''}${branchStats.added - branchStats.removed} |\n\n`;

    // File breakdown
    if (branchStats.fileDetails.length > 0) {
      output += '### Files Changed\n\n';
      output += '| File | Added | Removed |\n|------|-------|--------|\n';
      for (const file of branchStats.fileDetails.slice(0, 20)) {
        output += `| \`${file.name}\` | +${file.added} | -${file.removed} |\n`;
      }
      if (branchStats.fileDetails.length > 20) {
        output += `| *...and ${branchStats.fileDetails.length - 20} more files* | | |\n`;
      }
      output += '\n';

      // Group by extension
      const byExtension = new Map<string, { count: number; added: number; removed: number }>();
      for (const file of branchStats.fileDetails) {
        const ext = file.name.includes('.') ? '.' + file.name.split('.').pop()!.toLowerCase() : '(no ext)';
        const existing = byExtension.get(ext) ?? { count: 0, added: 0, removed: 0 };
        existing.count++;
        existing.added += file.added;
        existing.removed += file.removed;
        byExtension.set(ext, existing);
      }

      output += '### Changes by File Type\n\n';
      output += '| Extension | Files | Added | Removed |\n|-----------|-------|-------|--------|\n';
      const sortedExtensions = [...byExtension.entries()]
        .sort((a, b) => (b[1].added + b[1].removed) - (a[1].added + a[1].removed))
        .slice(0, 10);
      for (const [ext, data] of sortedExtensions) {
        output += `| \`${ext}\` | ${data.count} | +${data.added} | -${data.removed} |\n`;
      }
    }
  }

  return success(output);
}

// ============================================================================
// Tool: suggest_tests
// ============================================================================

/** Parameters for suggest_tests tool */
export interface SuggestTestsParams {
  baseBranch?: string;
}

/** File analysis for test suggestions */
interface FileAnalysis {
  name: string;
  isTestFile: boolean;
  isConfig: boolean;
  isDocumentation: boolean;
  hasAsyncCode: boolean;
  hasExceptionHandling: boolean;
  hasNullChecks: boolean;
  hasConditionalLogic: boolean;
  hasLoops: boolean;
  hasDatabaseCalls: boolean;
  hasHttpCalls: boolean;
  addedMethods: string[];
}

/**
 * Checks if a file is a test file.
 */
function isTestFile(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return lower.includes('test') || lower.includes('spec') || lower.includes('.test.') || lower.includes('.spec.');
}

/**
 * Checks if a file is a config file.
 */
function isConfigFile(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return lower.endsWith('.json') || lower.endsWith('.yaml') || lower.endsWith('.yml') || lower.endsWith('.config') || lower.endsWith('.csproj');
}

/**
 * Checks if a file is documentation.
 */
function isDocFile(fileName: string): boolean {
  const lower = fileName.toLowerCase();
  return lower.endsWith('.md') || lower.endsWith('.txt') || lower.endsWith('.rst');
}

/**
 * Analyzes code for test suggestions.
 */
function analyzeCodeForTests(diff: string): { changedFiles: FileAnalysis[] } {
  const analysis: { changedFiles: FileAnalysis[] } = { changedFiles: [] };
  let currentFile: FileAnalysis = {
    name: 'unknown',
    isTestFile: false,
    isConfig: false,
    isDocumentation: false,
    hasAsyncCode: false,
    hasExceptionHandling: false,
    hasNullChecks: false,
    hasConditionalLogic: false,
    hasLoops: false,
    hasDatabaseCalls: false,
    hasHttpCalls: false,
    addedMethods: [],
  };

  for (const line of diff.split('\n')) {
    if (line.startsWith('+++ b/')) {
      if (currentFile.name !== 'unknown') {
        analysis.changedFiles.push(currentFile);
      }
      const fileName = line.slice(6);
      currentFile = {
        name: fileName,
        isTestFile: isTestFile(fileName),
        isConfig: isConfigFile(fileName),
        isDocumentation: isDocFile(fileName),
        hasAsyncCode: false,
        hasExceptionHandling: false,
        hasNullChecks: false,
        hasConditionalLogic: false,
        hasLoops: false,
        hasDatabaseCalls: false,
        hasHttpCalls: false,
        addedMethods: [],
      };
      continue;
    }

    if (!line.startsWith('+') || line.startsWith('+++')) continue;

    const content = line.slice(1);

    // Detect patterns
    if (content.includes('async ') || content.includes('await ')) currentFile.hasAsyncCode = true;
    if (content.includes('catch') || content.includes('throw ')) currentFile.hasExceptionHandling = true;
    if (content.includes('== null') || content.includes('!= null') || content.includes('?? ') || content.includes('?.')) currentFile.hasNullChecks = true;
    if (content.includes('if ') || content.includes('switch ') || content.includes('? ')) currentFile.hasConditionalLogic = true;
    if (content.includes('for ') || content.includes('foreach ') || content.includes('while ')) currentFile.hasLoops = true;
    if (content.includes('DbContext') || content.includes('SqlConnection') || content.includes('Repository')) currentFile.hasDatabaseCalls = true;
    if (content.includes('HttpClient') || content.includes('fetch(') || content.includes('axios')) currentFile.hasHttpCalls = true;

    // Extract method names
    const methodMatch = content.match(/(?:public|private|protected|internal|export|async)?\s*(?:static\s+)?(?:async\s+)?(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\(/);
    if (methodMatch) {
      currentFile.addedMethods.push(methodMatch[1]);
    }
  }

  if (currentFile.name !== 'unknown') {
    analysis.changedFiles.push(currentFile);
  }

  return analysis;
}

/**
 * Analyzes changed code and suggests appropriate test cases.
 */
export async function suggestTests(args?: SuggestTestsParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();
  let diffOutput: string;
  let context: string;

  if (args?.baseBranch) {
    const featureBranch = (await getCurrentBranch(repoDir)) ?? 'HEAD';
    await runGitCommand('fetch origin', repoDir);
    const branchDiff = await runGitCommand(`diff origin/${args.baseBranch}...${featureBranch}`, repoDir);

    if (branchDiff.exitCode !== 0 || !branchDiff.output.trim()) {
      return error(`Failed to get diff between ${args.baseBranch} and ${featureBranch}`);
    }
    diffOutput = branchDiff.output;
    context = `branch comparison (${args.baseBranch} → ${featureBranch})`;
  } else {
    // Try staged, then unstaged
    const staged = await runGitCommand('diff --cached', repoDir);
    if (staged.exitCode === 0 && staged.output.trim()) {
      diffOutput = staged.output;
      context = 'staged changes';
    } else {
      const unstaged = await runGitCommand('diff', repoDir);
      if (unstaged.exitCode !== 0 || !unstaged.output.trim()) {
        return error('No changes found to analyze for test suggestions.');
      }
      diffOutput = unstaged.output;
      context = 'unstaged changes';
    }
  }

  // Analyze the diff
  const analysis = analyzeCodeForTests(diffOutput);

  let output = '# 🧪 Test Suggestions\n\n';
  output += `**Analyzing:** ${context}\n\n`;

  output += '## Changed Files\n\n';
  for (const file of analysis.changedFiles.slice(0, 10)) {
    const icon = file.isTestFile ? '✅' : '📝';
    output += `- ${icon} \`${file.name}\`\n`;
  }
  if (analysis.changedFiles.length > 10) {
    output += `- *...and ${analysis.changedFiles.length - 10} more files*\n`;
  }
  output += '\n';

  // Identify files needing tests
  const filesNeedingTests = analysis.changedFiles.filter(f => !f.isTestFile && !f.isConfig && !f.isDocumentation);

  if (filesNeedingTests.length === 0) {
    output += '## ✅ All Changes Covered\n\n';
    output += 'The changes appear to be in test files, configuration, or documentation.\n';
  } else {
    output += '## 📋 Suggested Tests\n\n';

    for (const file of filesNeedingTests) {
      output += `### \`${file.name}\`\n\n`;

      if (file.addedMethods.length > 0) {
        output += '**New/Modified Methods:**\n';
        for (const method of file.addedMethods) {
          output += `- \`${method}\`\n`;
        }
        output += '\n';
      }

      output += '**Suggested Test Cases:**\n\n';

      if (file.hasAsyncCode) {
        output += '- [ ] Test async operation completes successfully\n';
        output += '- [ ] Test async operation handles cancellation\n';
        output += '- [ ] Test async operation timeout behavior\n';
      }

      if (file.hasExceptionHandling) {
        output += '- [ ] Test exception is thrown for invalid input\n';
        output += "- [ ] Test exception handling doesn't swallow errors\n";
      }

      if (file.hasNullChecks) {
        output += '- [ ] Test null input handling\n';
        output += '- [ ] Test empty collection handling\n';
      }

      if (file.hasConditionalLogic) {
        output += '- [ ] Test all conditional branches\n';
        output += '- [ ] Test boundary conditions\n';
      }

      if (file.hasLoops) {
        output += '- [ ] Test empty collection iteration\n';
        output += '- [ ] Test single item collection\n';
        output += '- [ ] Test large collection performance\n';
      }

      if (file.hasDatabaseCalls) {
        output += '- [ ] Test database operation success\n';
        output += '- [ ] Test database connection failure\n';
        output += '- [ ] Test transaction rollback on error\n';
      }

      if (file.hasHttpCalls) {
        output += '- [ ] Test successful HTTP response\n';
        output += '- [ ] Test HTTP error responses (4xx, 5xx)\n';
        output += '- [ ] Test network timeout handling\n';
      }

      // Default suggestions
      if (!file.hasAsyncCode && !file.hasExceptionHandling && !file.hasNullChecks && !file.hasConditionalLogic && !file.hasLoops && !file.hasDatabaseCalls && !file.hasHttpCalls) {
        output += '- [ ] Test happy path scenario\n';
        output += '- [ ] Test edge cases\n';
        output += '- [ ] Test error conditions\n';
      }

      output += '\n';
    }
  }

  output += '## Test Coverage Tips\n\n';
  output += '1. **Arrange-Act-Assert** pattern for clear test structure\n';
  output += '2. **One assertion per test** for easier debugging\n';
  output += '3. **Mock external dependencies** for unit tests\n';
  output += '4. **Use meaningful test names** that describe the scenario\n';

  return success(output);
}

// ============================================================================
// Tool: generate_changelog
// ============================================================================

/** Parameters for generate_changelog tool */
export interface GenerateChangelogParams {
  baseBranch?: string;
  featureBranch?: string;
  format?: 'keepachangelog' | 'simple';
}

/** Commit information */
interface CommitInfo {
  hash: string;
  message: string;
  author: string;
  date: string;
}

/** Categorized commits */
interface CategorizedCommits {
  added: CommitInfo[];
  changed: CommitInfo[];
  fixed: CommitInfo[];
  deprecated: CommitInfo[];
  removed: CommitInfo[];
  security: CommitInfo[];
  other: CommitInfo[];
}

/**
 * Categorizes commits by type.
 */
function categorizeCommits(commits: CommitInfo[]): CategorizedCommits {
  const result: CategorizedCommits = {
    added: [],
    changed: [],
    fixed: [],
    deprecated: [],
    removed: [],
    security: [],
    other: [],
  };

  for (const commit of commits) {
    const msg = commit.message.toLowerCase();

    if (msg.startsWith('feat') || msg.includes('add ') || msg.includes('new ')) {
      result.added.push(commit);
    } else if (msg.startsWith('fix') || msg.includes('bug') || msg.includes('issue')) {
      result.fixed.push(commit);
    } else if (msg.startsWith('refactor') || msg.includes('change') || msg.includes('update') || msg.includes('improve')) {
      result.changed.push(commit);
    } else if (msg.includes('deprecat')) {
      result.deprecated.push(commit);
    } else if (msg.includes('remove') || msg.includes('delete')) {
      result.removed.push(commit);
    } else if (msg.includes('security') || msg.includes('vulnerab') || msg.includes('cve')) {
      result.security.push(commit);
    } else {
      result.other.push(commit);
    }
  }

  return result;
}

/**
 * Cleans commit message by removing conventional prefix.
 */
function cleanCommitMessage(message: string): string {
  let cleaned = message.replace(/^(feat|fix|docs|style|refactor|test|chore|perf|ci|build|revert)(\([^)]+\))?:\s*/i, '');
  if (cleaned.length > 0) {
    cleaned = cleaned.charAt(0).toUpperCase() + cleaned.slice(1);
  }
  return cleaned.trim();
}

/**
 * Generates changelog entries from commits between branches.
 */
export async function generateChangelog(args?: GenerateChangelogParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();
  let baseBranch = args?.baseBranch ?? 'main';
  let featureBranch = args?.featureBranch;
  const format = args?.format ?? 'keepachangelog';

  // Auto-detect feature branch
  if (!featureBranch) {
    featureBranch = (await getCurrentBranch(repoDir)) ?? 'HEAD';
  }

  // Fetch latest
  await runGitCommand('fetch origin', repoDir);

  // Get commit log
  const logResult = await runGitCommand(
    `log origin/${baseBranch}..${featureBranch} --pretty=format:"%h|%s|%an|%ad" --date=short`,
    repoDir
  );

  if (logResult.exitCode !== 0) {
    return gitError('Failed to get commit log', logResult.output);
  }

  if (!logResult.output.trim()) {
    return success(`No commits found between \`${baseBranch}\` and \`${featureBranch}\`.`);
  }

  // Parse commits
  const commits: CommitInfo[] = logResult.output
    .split('\n')
    .filter(Boolean)
    .map(line => {
      const parts = line.split('|');
      return {
        hash: parts[0] ?? '',
        message: parts[1] ?? '',
        author: parts[2] ?? '',
        date: parts[3] ?? '',
      };
    });

  // Categorize commits
  const categorized = categorizeCommits(commits);

  // Build changelog
  let output = '# 📝 Changelog\n\n';
  output += `**Branch:** \`${featureBranch}\` (compared to \`${baseBranch}\`)\n`;
  output += `**Commits:** ${commits.length}\n`;
  output += `**Date Range:** ${commits[commits.length - 1]?.date ?? 'N/A'} to ${commits[0]?.date ?? 'N/A'}\n\n`;

  if (format === 'keepachangelog') {
    output += '## [Unreleased]\n\n';

    const sections = [
      { name: 'Added', commits: categorized.added },
      { name: 'Changed', commits: categorized.changed },
      { name: 'Fixed', commits: categorized.fixed },
      { name: 'Deprecated', commits: categorized.deprecated },
      { name: 'Removed', commits: categorized.removed },
      { name: 'Security', commits: categorized.security },
      { name: 'Other', commits: categorized.other },
    ];

    for (const section of sections) {
      if (section.commits.length > 0) {
        output += `### ${section.name}\n`;
        for (const commit of section.commits) {
          output += `- ${cleanCommitMessage(commit.message)} (${commit.hash})\n`;
        }
        output += '\n';
      }
    }
  } else {
    output += '## Changes\n\n';
    for (const commit of commits) {
      output += `- ${commit.message} (${commit.hash}) - ${commit.author}\n`;
    }
  }

  output += '---\n\n*Generated by DiffPilot*\n';

  return success(output);
}
