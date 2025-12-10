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
import { type ToolResult } from './types.js';
/** Parameters for generate_commit_message tool */
export interface GenerateCommitMessageParams {
    style?: 'conventional' | 'simple';
    scope?: string;
    includeBody?: boolean;
}
/**
 * Generates a commit message based on staged changes (or unstaged if nothing is staged).
 */
export declare function generateCommitMessage(args?: GenerateCommitMessageParams): Promise<ToolResult>;
/** Parameters for scan_secrets tool */
export interface ScanSecretsParams {
    scanStaged?: boolean;
    scanUnstaged?: boolean;
}
/**
 * Scans the diff for accidentally committed secrets, API keys, passwords, etc.
 */
export declare function scanSecrets(args?: ScanSecretsParams): Promise<ToolResult>;
/** Parameters for diff_stats tool */
export interface DiffStatsParams {
    baseBranch?: string;
    featureBranch?: string;
    includeWorkingDir?: boolean;
}
/**
 * Gets detailed statistics about changes between branches or in working directory.
 */
export declare function diffStats(args?: DiffStatsParams): Promise<ToolResult>;
/** Parameters for suggest_tests tool */
export interface SuggestTestsParams {
    baseBranch?: string;
}
/**
 * Analyzes changed code and suggests appropriate test cases.
 */
export declare function suggestTests(args?: SuggestTestsParams): Promise<ToolResult>;
/** Parameters for generate_changelog tool */
export interface GenerateChangelogParams {
    baseBranch?: string;
    featureBranch?: string;
    format?: 'keepachangelog' | 'simple';
}
/**
 * Generates changelog entries from commits between branches.
 */
export declare function generateChangelog(args?: GenerateChangelogParams): Promise<ToolResult>;
//# sourceMappingURL=developer.d.ts.map