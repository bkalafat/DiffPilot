/**
 * GitService - Centralized git operations for the MCP server.
 * All git commands are executed via this service to ensure consistent handling.
 *
 * Ported from: src/Git/GitService.cs
 */
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
export declare function runGitCommand(args: string, workingDirectory: string, timeoutMs?: number): Promise<GitCommandResult>;
/**
 * Gets the current branch name using 'git rev-parse --abbrev-ref HEAD'.
 * Returns null if in detached HEAD state or on error.
 */
export declare function getCurrentBranch(workingDirectory: string): Promise<string | null>;
/**
 * Finds the base branch that the current branch was created from.
 * Uses multiple detection strategies in order of reliability:
 * 1. Reflog - "Created from X" entry
 * 2. Git config - upstream tracking branch
 * 3. Merge-base analysis - unique common ancestor
 *
 * Does NOT guess or use hardcoded branch names. Returns null if uncertain.
 */
export declare function findBaseBranch(workingDirectory: string, currentBranch: string, remote?: string): Promise<BranchInfo | null>;
/**
 * Validates a branch name to prevent shell injection.
 * Only allows alphanumeric characters, slashes, underscores, and hyphens.
 */
export declare function isValidBranchName(name: string): boolean;
/**
 * Validates a file name to prevent path traversal and shell injection.
 */
export declare function isValidFileName(name: string): boolean;
/**
 * Gets the working directory for git operations.
 * Uses DIFFPILOT_WORKSPACE environment variable if set, otherwise current directory.
 */
export declare function getWorkingDirectory(): string;
//# sourceMappingURL=git-service.d.ts.map