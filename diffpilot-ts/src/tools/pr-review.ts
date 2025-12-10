/**
 * PR Review Tools - Implementation of PR review tools for the MCP server.
 * 
 * These tools help developers with code review workflows:
 * - get_pr_diff: Raw diff for any purpose
 * - review_pr_changes: Diff with AI review instructions
 * - generate_pr_title: Conventional PR title generation
 * - generate_pr_description: Complete PR description with summary and checklist
 * 
 * Ported from: src/Tools/PrReviewTools.cs
 */

import {
  runGitCommand,
  getCurrentBranch,
  findBaseBranch,
  isValidBranchName,
  getWorkingDirectory,
  type BranchInfo,
} from '../git/git-service.js';
import { success, error, gitError, truncateContent, type ToolResult } from './types.js';

/** Parameters for branch-based operations */
interface BranchParams {
  baseBranch?: string;
  featureBranch?: string;
  remote?: string;
}

/** Extracted branch parameters with validation */
interface ExtractedBranches {
  baseBranch: string;
  featureBranch: string;
  remote: string;
  error?: ToolResult;
}

/**
 * Extracts and validates branch parameters from arguments.
 * Auto-detects branches if not provided.
 */
async function extractBranchParameters(
  args: BranchParams | undefined,
  repoDir: string
): Promise<ExtractedBranches> {
  let remote = args?.remote ?? 'origin';
  let baseBranch = args?.baseBranch;
  let featureBranch = args?.featureBranch;

  // Auto-detect feature branch if not provided
  if (!featureBranch) {
    featureBranch = (await getCurrentBranch(repoDir)) ?? undefined;
    if (!featureBranch) {
      return {
        baseBranch: '',
        featureBranch: '',
        remote,
        error: gitError(
          'Failed to detect current branch',
          'Could not determine current branch. You may be in a detached HEAD state.'
        ),
      };
    }
  }

  // Auto-detect base branch if not provided
  if (!baseBranch) {
    const baseInfo = await findBaseBranch(repoDir, featureBranch, remote);
    if (baseInfo) {
      remote = baseInfo.remote;
      baseBranch = baseInfo.baseBranch;
    } else {
      return {
        baseBranch: '',
        featureBranch: '',
        remote,
        error: error(
          `Could not automatically determine the base branch for '${featureBranch}'. ` +
            "Please specify the 'baseBranch' parameter (e.g., 'main' or 'develop')."
        ),
      };
    }
  }

  // Validate branch names
  if (!isValidBranchName(remote)) {
    return {
      baseBranch: '',
      featureBranch: '',
      remote,
      error: error('Remote name contains invalid characters.'),
    };
  }

  if (!isValidBranchName(baseBranch)) {
    return {
      baseBranch: '',
      featureBranch: '',
      remote,
      error: error('Base branch name contains invalid characters.'),
    };
  }

  if (!isValidBranchName(featureBranch)) {
    return {
      baseBranch: '',
      featureBranch: '',
      remote,
      error: error('Feature branch name contains invalid characters.'),
    };
  }

  return { baseBranch, featureBranch, remote };
}

/**
 * Gets the diff between base and feature branches.
 */
async function getDiff(
  repoDir: string,
  remote: string,
  baseBranch: string,
  featureBranch: string
): Promise<{ diff?: string; error?: ToolResult }> {
  const diffArgs = `diff ${remote}/${baseBranch}...${featureBranch}`;
  const diffResult = await runGitCommand(diffArgs, repoDir);

  if (diffResult.exitCode !== 0) {
    return { error: gitError('git diff failed', diffResult.output) };
  }

  if (!diffResult.output.trim()) {
    return { diff: 'No changes found between branches.' };
  }

  return { diff: diffResult.output };
}

// ============================================================================
// Tool: get_pr_diff
// ============================================================================

/** Parameters for get_pr_diff tool */
export interface GetPrDiffParams extends BranchParams {}

/**
 * Gets the raw diff between base branch and current/feature branch.
 * Auto-detects branches if not specified.
 */
export async function getPrDiff(args?: GetPrDiffParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();

  // Extract and validate parameters
  const branches = await extractBranchParameters(args, repoDir);
  if (branches.error) {
    return branches.error;
  }

  const { baseBranch, featureBranch, remote } = branches;

  // Fetch latest
  const fetchResult = await runGitCommand(`fetch ${remote}`, repoDir);
  if (fetchResult.exitCode !== 0) {
    return gitError('git fetch failed', fetchResult.output);
  }

  // Generate diff
  const diffResult = await getDiff(repoDir, remote, baseBranch, featureBranch);
  if (diffResult.error) {
    return diffResult.error;
  }

  // Return diff content
  const header =
    `## Diff: ${remote}/${baseBranch} → ${featureBranch}\n\n` +
    `Comparing \`${remote}/${baseBranch}...${featureBranch}\`\n\n`;

  const diffContent = truncateContent(diffResult.diff!);

  return success(header + '```diff\n' + diffContent + '\n```');
}

// ============================================================================
// Tool: review_pr_changes
// ============================================================================

/** Parameters for review_pr_changes tool */
export interface ReviewPrChangesParams extends BranchParams {
  focusAreas?: string;
}

/**
 * Gets the PR diff with instructions for AI code review.
 * Provides structured context to help AI perform a thorough review.
 */
export async function reviewPrChanges(args?: ReviewPrChangesParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();

  // Extract and validate parameters
  const branches = await extractBranchParameters(args, repoDir);
  if (branches.error) {
    return branches.error;
  }

  const { baseBranch, featureBranch, remote } = branches;
  const focusAreas = args?.focusAreas;

  // Fetch latest
  const fetchResult = await runGitCommand(`fetch ${remote}`, repoDir);
  if (fetchResult.exitCode !== 0) {
    return gitError('git fetch failed', fetchResult.output);
  }

  // Generate diff
  const diffResult = await getDiff(repoDir, remote, baseBranch, featureBranch);
  if (diffResult.error) {
    return diffResult.error;
  }

  // Get file stats for context
  const statsResult = await runGitCommand(
    `diff --stat ${remote}/${baseBranch}...${featureBranch}`,
    repoDir
  );

  // Build review prompt
  let output = '# Code Review Request\n\n';
  output += `**Branch:** \`${featureBranch}\` → \`${baseBranch}\`\n\n`;

  if (statsResult.exitCode === 0 && statsResult.output.trim()) {
    output += '## Change Summary\n```\n' + statsResult.output.trim() + '\n```\n\n';
  }

  output += '## Review Instructions\n\n';
  output += '⚠️ **CRITICAL REVIEW MODE** - Focus on finding problems, not praise.\n\n';
  output += 'Analyze the code changes below and identify ALL issues. Be thorough and critical.\n';
  output += 'Limit positive feedback to 1-2 items maximum. Prioritize finding defects.\n\n';

  output += '### 🔴 Security Vulnerabilities (HIGHEST PRIORITY)\n';
  output += '- **Injection Flaws** - SQL injection, command injection, LDAP injection, XPath injection\n';
  output += '- **XSS (Cross-Site Scripting)** - Reflected, stored, DOM-based XSS vulnerabilities\n';
  output += '- **Authentication/Authorization** - Broken auth, missing access controls, privilege escalation\n';
  output += '- **Sensitive Data Exposure** - Hardcoded secrets, PII leakage, insecure data transmission\n';
  output += '- **Insecure Deserialization** - Untrusted data deserialization without validation\n';
  output += '- **SSRF/CSRF** - Server-side request forgery, cross-site request forgery\n';
  output += '- **Path Traversal** - Directory traversal, file inclusion vulnerabilities\n';
  output += '- **Cryptographic Failures** - Weak algorithms, improper key management, missing encryption\n\n';

  output += '### 🟠 Correctness & Logic Errors\n';
  output += '- **Logic Flaws** - Incorrect conditions, off-by-one errors, race conditions\n';
  output += '- **Null/Undefined Handling** - Missing null checks, uninitialized variables\n';
  output += '- **Edge Cases** - Boundary conditions, empty inputs, overflow scenarios\n';
  output += '- **Resource Leaks** - Unclosed connections, memory leaks, file handle leaks\n';
  output += '- **Concurrency Issues** - Thread safety, deadlocks, data races\n\n';

  output += '### 🟡 Error Handling & Resilience\n';
  output += '- **Missing Exception Handling** - Unhandled exceptions, swallowed errors\n';
  output += '- **Information Disclosure** - Stack traces exposed, verbose error messages\n';
  output += '- **Fail-Open Behavior** - Security controls that fail permissively\n';
  output += '- **Missing Input Validation** - Unvalidated user input, missing sanitization\n\n';

  output += '### 🟢 Performance & Efficiency\n';
  output += '- **N+1 Query Problems** - Database queries in loops\n';
  output += '- **Memory Inefficiency** - Unnecessary allocations, large object retention\n';
  output += '- **Algorithm Complexity** - O(n²) or worse where O(n) is possible\n';
  output += '- **Resource Exhaustion** - Unbounded loops, missing pagination, DoS vectors\n\n';

  output += '### 🔵 Code Quality & Maintainability\n';
  output += '- **Code Duplication** - Copy-paste code, DRY violations\n';
  output += '- **SOLID Violations** - Single responsibility, open/closed principle issues\n';
  output += '- **Naming/Readability** - Unclear names, magic numbers, missing comments\n';
  output += '- **Test Coverage Gaps** - Untested paths, missing edge case tests\n\n';

  if (focusAreas) {
    output += `**Additional Focus Areas:** ${focusAreas}\n\n`;
  }

  output += '## Diff\n\n```diff\n' + truncateContent(diffResult.diff!) + '\n```\n\n';
  output += '---\n\n';
  output += '## Output Format\n\n';
  output += 'Structure your review as:\n';
  output += '1. **Critical Issues** (must fix before merge)\n';
  output += '2. **Major Issues** (should fix, high impact)\n';
  output += '3. **Minor Issues** (nice to fix, low impact)\n';
  output += '4. **Suggestions** (optional improvements)\n\n';
  output += 'For each issue, provide:\n';
  output += '- File path and line number\n';
  output += '- Severity level (Critical/Major/Minor)\n';
  output += '- Clear description of the problem\n';
  output += '- Recommended fix or mitigation\n';

  return success(output);
}

// ============================================================================
// Tool: generate_pr_title
// ============================================================================

/** Parameters for generate_pr_title tool */
export interface GeneratePrTitleParams extends BranchParams {
  style?: 'conventional' | 'ticket' | 'descriptive';
}

/**
 * Generates a conventional PR title based on the changes.
 * Analyzes the diff to determine the type and scope of changes.
 */
export async function generatePrTitle(args?: GeneratePrTitleParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();

  // Extract and validate parameters
  const branches = await extractBranchParameters(args, repoDir);
  if (branches.error) {
    return branches.error;
  }

  const { baseBranch, featureBranch, remote } = branches;
  const style = args?.style ?? 'conventional';

  // Fetch latest
  const fetchResult = await runGitCommand(`fetch ${remote}`, repoDir);
  if (fetchResult.exitCode !== 0) {
    return gitError('git fetch failed', fetchResult.output);
  }

  // Get diff stats and commit messages for context
  const statsResult = await runGitCommand(
    `diff --stat ${remote}/${baseBranch}...${featureBranch}`,
    repoDir
  );

  const logResult = await runGitCommand(
    `log --oneline ${remote}/${baseBranch}..${featureBranch}`,
    repoDir
  );

  // Extract ticket number from branch name if present
  const ticketMatch = featureBranch.match(/[A-Za-z]+-\d+/i);
  const ticketNumber = ticketMatch ? ticketMatch[0].toUpperCase() : null;

  // Build the response
  let output = '# PR Title Generator\n\n';
  output += `**Branch:** \`${featureBranch}\`\n`;
  output += `**Style:** ${style}\n`;
  if (ticketNumber) {
    output += `**Ticket:** ${ticketNumber}\n`;
  }
  output += '\n';

  output += '## Commits in this PR\n```\n' + logResult.output.trim() + '\n```\n\n';
  output += '## Files Changed\n```\n' + statsResult.output.trim() + '\n```\n\n';

  output += '## Instructions\n\n';
  output += 'Based on the commits and changes above, generate a PR title following these guidelines:\n\n';

  switch (style.toLowerCase()) {
    case 'conventional':
      output += '**Format:** `type(scope): description`\n\n';
      output += 'Types: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `perf`, `style`\n\n';
      output += 'Examples:\n';
      output += '- `feat(auth): add OAuth2 login support`\n';
      output += '- `fix(api): handle null response from external service`\n';
      output += '- `refactor(git): reorganize service into modular structure`\n';
      break;

    case 'ticket':
      output += `**Format:** \`[${ticketNumber ?? 'TICKET-XXX'}] Description\`\n\n`;
      output += 'Examples:\n';
      output += '- `[PROJ-123] Add user authentication flow`\n';
      output += '- `[BUG-456] Fix null reference in order processing`\n';
      break;

    default: // descriptive
      output += '**Format:** Clear, concise description starting with a verb\n\n';
      output += 'Examples:\n';
      output += '- `Add OAuth2 login support for enterprise users`\n';
      output += '- `Fix null reference exception in order processing`\n';
      output += '- `Reorganize git service into modular architecture`\n';
      break;
  }

  output += '\n---\nPlease provide 2-3 title suggestions based on the actual changes.\n';

  return success(output);
}

// ============================================================================
// Tool: generate_pr_description
// ============================================================================

/** Parameters for generate_pr_description tool */
export interface GeneratePrDescriptionParams extends BranchParams {
  includeChecklist?: boolean;
  ticketUrl?: string;
}

/**
 * Generates a complete PR description including summary, changes, and checklist.
 * Ready to paste directly into the PR description field.
 */
export async function generatePrDescription(args?: GeneratePrDescriptionParams): Promise<ToolResult> {
  const repoDir = getWorkingDirectory();

  // Extract and validate parameters
  const branches = await extractBranchParameters(args, repoDir);
  if (branches.error) {
    return branches.error;
  }

  const { baseBranch, featureBranch, remote } = branches;
  const includeChecklist = args?.includeChecklist ?? true;
  const ticketUrl = args?.ticketUrl;

  // Fetch latest
  const fetchResult = await runGitCommand(`fetch ${remote}`, repoDir);
  if (fetchResult.exitCode !== 0) {
    return gitError('git fetch failed', fetchResult.output);
  }

  // Get various context
  const statsResult = await runGitCommand(
    `diff --stat ${remote}/${baseBranch}...${featureBranch}`,
    repoDir
  );

  const logResult = await runGitCommand(
    `log --oneline ${remote}/${baseBranch}..${featureBranch}`,
    repoDir
  );

  const diffResult = await getDiff(repoDir, remote, baseBranch, featureBranch);
  if (diffResult.error) {
    return diffResult.error;
  }

  // Extract ticket from branch if no URL provided
  const ticketMatch = featureBranch.match(/[A-Za-z]+-\d+/i);
  const ticketNumber = ticketMatch ? ticketMatch[0].toUpperCase() : null;

  // Build the response
  let output = '# PR Description Generator\n\n';
  output += `**Branch:** \`${featureBranch}\` → \`${baseBranch}\`\n`;
  if (ticketNumber) {
    output += `**Ticket:** ${ticketNumber}\n`;
  }
  output += '\n';

  output += '## Commits\n```\n' + logResult.output.trim() + '\n```\n\n';
  output += '## Files Changed\n```\n' + statsResult.output.trim() + '\n```\n\n';
  output += '## Diff\n```diff\n' + truncateContent(diffResult.diff!) + '\n```\n\n';

  output += '---\n\n';
  output += '## Instructions\n\n';
  output += 'Based on the commits, files changed, and diff above, generate a PR description using this template:\n\n';
  output += '```markdown\n';
  output += '## Summary\n[Brief description of what this PR does and why]\n\n';

  if (ticketUrl) {
    output += '## Related Issue\n';
    output += `[${ticketNumber ?? 'Ticket'}](${ticketUrl})\n\n`;
  } else if (ticketNumber) {
    output += '## Related Issue\n' + ticketNumber + '\n\n';
  }

  output += '## Changes\n';
  output += '- [List key changes, one per line]\n';
  output += '- [Focus on WHAT changed and WHY]\n';
  output += '- [Group related changes together]\n\n';
  output += '## Testing\n';
  output += '- [How was this tested?]\n';
  output += '- [Any manual testing steps needed?]\n';
  output += '- [Were unit tests added/updated?]\n';

  if (includeChecklist) {
    output += '\n## Checklist\n';
    output += '- [ ] Code follows project style guidelines\n';
    output += '- [ ] Self-review completed\n';
    output += '- [ ] Tests added/updated for changes\n';
    output += '- [ ] Documentation updated if needed\n';
    output += '- [ ] No breaking changes (or documented)\n';
  }

  output += '```\n\n';
  output += 'Please fill in the template based on the actual changes shown above.\n';

  return success(output);
}
