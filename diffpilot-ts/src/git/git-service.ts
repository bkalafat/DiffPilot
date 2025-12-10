/**
 * GitService - Centralized git operations for the MCP server.
 * All git commands are executed via this service to ensure consistent handling.
 * 
 * Ported from: src/Git/GitService.cs
 */

import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/** Default timeout for git commands in milliseconds */
const DEFAULT_TIMEOUT_MS = 60_000;

/** Result of a git command execution */
export interface GitCommandResult {
  exitCode: number;
  output: string;
}

/** Branch detection result */
export interface BranchInfo {
  remote: string;
  baseBranch: string;
}

/**
 * Runs a git command asynchronously and returns the exit code and combined output.
 * Both stdout and stderr are captured to provide complete feedback.
 */
export async function runGitCommand(
  args: string,
  workingDirectory: string,
  timeoutMs: number = DEFAULT_TIMEOUT_MS
): Promise<GitCommandResult> {
  try {
    const { stdout, stderr } = await execAsync(`git ${args}`, {
      cwd: workingDirectory,
      timeout: timeoutMs,
      maxBuffer: 10 * 1024 * 1024, // 10MB buffer for large diffs
    });
    
    return {
      exitCode: 0,
      output: stdout + stderr,
    };
  } catch (error: unknown) {
    if (error && typeof error === 'object' && 'killed' in error && error.killed) {
      return {
        exitCode: -1,
        output: `Git command timed out after ${timeoutMs}ms: git ${args}`,
      };
    }
    
    const execError = error as { code?: number; stdout?: string; stderr?: string; message?: string };
    return {
      exitCode: execError.code ?? 1,
      output: (execError.stdout ?? '') + (execError.stderr ?? '') || execError.message || 'Unknown error',
    };
  }
}

/**
 * Gets the current branch name using 'git rev-parse --abbrev-ref HEAD'.
 * Returns null if in detached HEAD state or on error.
 */
export async function getCurrentBranch(workingDirectory: string): Promise<string | null> {
  const result = await runGitCommand('rev-parse --abbrev-ref HEAD', workingDirectory);
  
  if (result.exitCode !== 0 || !result.output.trim()) {
    return null;
  }
  
  const branch = result.output.trim();
  // In detached HEAD state, git returns "HEAD"
  return branch === 'HEAD' ? null : branch;
}

/**
 * Gets the upstream branch reference for a given branch.
 * Returns the full remote/branch reference (e.g., "origin/main").
 */
export async function getUpstreamBranch(
  workingDirectory: string,
  branch?: string
): Promise<string | null> {
  const targetBranch = branch ?? (await getCurrentBranch(workingDirectory));
  if (!targetBranch) {
    return null;
  }

  const result = await runGitCommand(
    `rev-parse --abbrev-ref ${targetBranch}@{upstream}`,
    workingDirectory
  );
  
  if (result.exitCode !== 0 || !result.output.trim()) {
    return null;
  }
  
  return result.output.trim();
}

/**
 * Gets the default branch for the repository using symbolic-ref.
 * Tries to get the default branch from the remote's HEAD.
 */
export async function getDefaultBranch(
  workingDirectory: string,
  remote: string = 'origin'
): Promise<string | null> {
  // Try to get default branch via symbolic-ref (most reliable)
  const result = await runGitCommand(
    `symbolic-ref refs/remotes/${remote}/HEAD`,
    workingDirectory
  );
  
  if (result.exitCode === 0 && result.output.trim()) {
    // Output is like "refs/remotes/origin/main"
    const ref = result.output.trim();
    const prefix = `refs/remotes/${remote}/`;
    if (ref.startsWith(prefix)) {
      return ref.slice(prefix.length);
    }
  }
  
  // Fallback: check for common default branches
  const commonDefaults = ['main', 'master', 'develop'];
  for (const branch of commonDefaults) {
    const checkResult = await runGitCommand(
      `show-ref --verify --quiet refs/remotes/${remote}/${branch}`,
      workingDirectory
    );
    if (checkResult.exitCode === 0) {
      return branch;
    }
  }
  
  return null;
}

/**
 * Finds the base branch that the current branch was created from.
 * Uses multiple detection strategies in order of reliability:
 * 1. Reflog - "Created from X" entry
 * 2. Git config - upstream tracking branch  
 * 3. Merge-base analysis - unique common ancestor
 * 
 * Does NOT guess or use hardcoded branch names. Returns null if uncertain.
 */
export async function findBaseBranch(
  workingDirectory: string,
  currentBranch: string,
  remote: string = 'origin'
): Promise<BranchInfo | null> {
  if (!workingDirectory || !currentBranch) {
    return null;
  }

  // 1️⃣ REFLOG - Branch creation record (most reliable)
  const reflogBase = await findFromReflog(workingDirectory, currentBranch);
  if (reflogBase) {
    const branchRemote = await findRemoteForBranch(workingDirectory, reflogBase) ?? remote;
    return { remote: branchRemote, baseBranch: reflogBase };
  }

  // 2️⃣ GIT CONFIG - Upstream tracking configuration
  const trackingBase = await findFromTrackingConfig(workingDirectory, currentBranch);
  if (trackingBase && trackingBase !== currentBranch) {
    const branchRemote = await getBranchRemote(workingDirectory, currentBranch) ?? remote;
    return { remote: branchRemote, baseBranch: trackingBase };
  }

  // 3️⃣ MERGE-BASE - Unique common ancestor with local/remote branches
  const mergeBaseResult = await findFromUniqueMergeBase(workingDirectory, currentBranch, remote);
  if (mergeBaseResult) {
    return mergeBaseResult;
  }

  // No definitive evidence found - return null (caller should ask user)
  return null;
}

/**
 * Searches reflog for "Created from X" record.
 */
async function findFromReflog(
  workingDirectory: string,
  currentBranch: string
): Promise<string | null> {
  const result = await runGitCommand(
    `reflog show ${currentBranch} --format=%gs`,
    workingDirectory
  );
  
  if (result.exitCode !== 0 || !result.output.trim()) {
    return null;
  }

  const lines = result.output.split('\n').filter(Boolean);
  
  // Start from oldest entry (branch creation moment)
  for (const line of lines.reverse()) {
    // Pattern: "branch: Created from main" or "branch: Created from origin/develop"
    const createdMatch = line.match(/branch:\s*Created from\s+(\S+)/i);
    if (createdMatch) {
      const source = createdMatch[1];
      
      // Skip if HEAD or commit hash
      if (source.toUpperCase() === 'HEAD' || isCommitHash(source)) {
        continue;
      }
      
      // "origin/main" -> "main"
      return source.includes('/') ? source.split('/').pop()! : source;
    }

    // Pattern: "checkout: moving from develop to feature/x"
    const checkoutMatch = line.match(/checkout:\s*moving from\s+(\S+)\s+to\s+(\S+)/i);
    if (checkoutMatch) {
      const [, fromBranch, toBranch] = checkoutMatch;
      
      if (toBranch === currentBranch && !isCommitHash(fromBranch)) {
        return fromBranch.includes('/') ? fromBranch.split('/').pop()! : fromBranch;
      }
    }
  }

  return null;
}

/**
 * Reads upstream tracking branch from git config.
 */
async function findFromTrackingConfig(
  workingDirectory: string,
  branch: string
): Promise<string | null> {
  const result = await runGitCommand(
    `config --get branch.${branch}.merge`,
    workingDirectory
  );
  
  if (result.exitCode !== 0 || !result.output.trim()) {
    return null;
  }

  const trackingRef = result.output.trim();
  
  // "refs/heads/main" -> "main"
  if (trackingRef.startsWith('refs/heads/')) {
    return trackingRef.slice('refs/heads/'.length);
  }

  return trackingRef;
}

/**
 * Gets the configured remote for a branch from git config.
 */
async function getBranchRemote(
  workingDirectory: string,
  branch: string
): Promise<string | null> {
  const result = await runGitCommand(
    `config --get branch.${branch}.remote`,
    workingDirectory
  );
  return result.exitCode === 0 ? result.output.trim() : null;
}

/**
 * Finds which remote contains the specified branch.
 */
async function findRemoteForBranch(
  workingDirectory: string,
  branch: string
): Promise<string | null> {
  // First check config
  const configRemote = await getBranchRemote(workingDirectory, branch);
  if (configRemote) {
    return configRemote;
  }

  // Check which remotes have this branch
  const remotesResult = await runGitCommand('remote', workingDirectory);
  if (remotesResult.exitCode !== 0) {
    return null;
  }

  for (const remoteName of remotesResult.output.split('\n').filter(Boolean)) {
    const checkResult = await runGitCommand(
      `show-ref --verify --quiet refs/remotes/${remoteName.trim()}/${branch}`,
      workingDirectory
    );
    
    if (checkResult.exitCode === 0) {
      return remoteName.trim();
    }
  }

  return null;
}

// ============================================================================
// Merge-Base Analysis (Strategy 3)
// ============================================================================

/**
 * Merge-base analysis: Returns result only if there's a UNIQUE branch with common ancestor.
 * If multiple candidates exist with ambiguity, returns null (no guessing).
 */
async function findFromUniqueMergeBase(
  workingDirectory: string,
  currentBranch: string,
  remote: string
): Promise<BranchInfo | null> {
  // Get all local and remote branches
  const localBranches = await getLocalBranches(workingDirectory, currentBranch);
  const remoteBranches = await getRemoteBranches(workingDirectory, remote);

  // Get current branch HEAD
  const currentHeadResult = await runGitCommand(
    `rev-parse ${currentBranch}`,
    workingDirectory
  );
  if (currentHeadResult.exitCode !== 0) {
    return null;
  }

  const currentHead = currentHeadResult.output.trim();

  let uniqueBase: string | null = null;
  let uniqueRemote: string | null = null;
  let candidateCount = 0;

  // Check local branches
  for (const branch of localBranches) {
    const mergeBase = await getMergeBase(workingDirectory, currentBranch, branch);
    if (!mergeBase) continue;

    // Merge-base should not be same as current HEAD (means no commits yet, ambiguous)
    if (mergeBase === currentHead) continue;

    // Is current branch ahead of this branch? (derived from it)
    const isAhead = await isBranchAhead(workingDirectory, currentBranch, branch);
    if (!isAhead) continue;

    candidateCount++;

    if (candidateCount === 1) {
      uniqueBase = branch;
      uniqueRemote = await findRemoteForBranch(workingDirectory, branch) ?? remote;
    } else {
      // Multiple candidates - check if one is parent of the other
      const isNewCandidateChildOfPrevious = await isBranchAhead(
        workingDirectory,
        branch,
        uniqueBase!
      );
      if (isNewCandidateChildOfPrevious) {
        // New candidate is child of previous -> use new (more specific)
        uniqueBase = branch;
        uniqueRemote = await findRemoteForBranch(workingDirectory, branch) ?? remote;
        candidateCount = 1;
      } else {
        const isPreviousChildOfNewCandidate = await isBranchAhead(
          workingDirectory,
          uniqueBase!,
          branch
        );
        if (isPreviousChildOfNewCandidate) {
          // Previous is more specific, keep it
          candidateCount = 1;
        }
        // Else: Both are independent, ambiguity remains
      }
    }
  }

  // Check remote branches (if not already checked as local)
  for (const branch of remoteBranches) {
    if (localBranches.includes(branch)) continue;

    const remoteRef = `${remote}/${branch}`;
    const mergeBase = await getMergeBase(workingDirectory, currentBranch, remoteRef);
    if (!mergeBase || mergeBase === currentHead) continue;

    const isAhead = await isBranchAhead(workingDirectory, currentBranch, remoteRef);
    if (!isAhead) continue;

    candidateCount++;

    if (candidateCount === 1) {
      uniqueBase = branch;
      uniqueRemote = remote;
    } else {
      // Multiple candidates with remote - apply same parent/child logic
      const remoteUniqueRef = `${uniqueRemote}/${uniqueBase}`;
      const isNewCandidateChildOfPrevious = await isBranchAhead(
        workingDirectory,
        remoteRef,
        remoteUniqueRef
      );
      if (isNewCandidateChildOfPrevious) {
        uniqueBase = branch;
        uniqueRemote = remote;
        candidateCount = 1;
      } else {
        const isPreviousChildOfNewCandidate = await isBranchAhead(
          workingDirectory,
          remoteUniqueRef,
          remoteRef
        );
        if (isPreviousChildOfNewCandidate) {
          candidateCount = 1;
        }
      }
    }
  }

  // Only return if there's exactly one definitive candidate
  if (candidateCount === 1 && uniqueBase) {
    return { remote: uniqueRemote ?? remote, baseBranch: uniqueBase };
  }

  return null;
}

/**
 * Gets the merge-base (common ancestor) between two branches.
 */
async function getMergeBase(
  workingDirectory: string,
  branch1: string,
  branch2: string
): Promise<string | null> {
  const result = await runGitCommand(`merge-base ${branch1} ${branch2}`, workingDirectory);
  return result.exitCode === 0 ? result.output.trim() : null;
}

/**
 * Checks if branch is ahead of baseBranch (has commits that baseBranch doesn't have).
 */
async function isBranchAhead(
  workingDirectory: string,
  branch: string,
  baseBranch: string
): Promise<boolean> {
  const result = await runGitCommand(
    `rev-list --count ${baseBranch}..${branch}`,
    workingDirectory
  );
  if (result.exitCode !== 0) return false;
  
  const ahead = parseInt(result.output.trim(), 10);
  return !isNaN(ahead) && ahead > 0;
}

/**
 * Gets all local branches except the specified one.
 */
async function getLocalBranches(
  workingDirectory: string,
  excludeBranch: string
): Promise<string[]> {
  const result = await runGitCommand(
    'branch --list --format=%(refname:short)',
    workingDirectory
  );
  if (result.exitCode !== 0) return [];

  return result.output
    .split('\n')
    .map(b => b.trim())
    .filter(b => b && b !== excludeBranch);
}

/**
 * Gets all remote branches for the specified remote.
 */
async function getRemoteBranches(
  workingDirectory: string,
  remote: string
): Promise<string[]> {
  const result = await runGitCommand(
    `branch -r --list "${remote}/*" --format=%(refname:short)`,
    workingDirectory
  );
  if (result.exitCode !== 0) return [];

  const prefix = `${remote}/`;
  return result.output
    .split('\n')
    .map(b => b.trim())
    .filter(b => b && !b.includes('->')) // Skip HEAD pointer
    .map(b => b.startsWith(prefix) ? b.slice(prefix.length) : b)
    .filter(Boolean);
}

/**
 * Checks if a string looks like a git commit hash.
 */
function isCommitHash(value: string): boolean {
  if (!value || value.length < 7 || value.length > 40) {
    return false;
  }
  return /^[0-9a-fA-F]+$/.test(value);
}

/**
 * Validates a branch name to prevent shell injection.
 * Only allows alphanumeric characters, slashes, underscores, and hyphens.
 */
export function isValidBranchName(name: string): boolean {
  if (!name || !name.trim()) {
    return false;
  }
  return /^[a-zA-Z0-9/_-]+$/.test(name);
}

/**
 * Validates a file name to prevent path traversal and shell injection.
 */
export function isValidFileName(name: string): boolean {
  if (!name || !name.trim()) {
    return false;
  }
  if (name.includes('..')) {
    return false;
  }
  return /^[a-zA-Z0-9/_.-]+$/.test(name);
}

/**
 * Gets the working directory for git operations.
 * Uses DIFFPILOT_WORKSPACE environment variable if set, otherwise current directory.
 */
export function getWorkingDirectory(): string {
  return process.env.DIFFPILOT_WORKSPACE || process.cwd();
}
