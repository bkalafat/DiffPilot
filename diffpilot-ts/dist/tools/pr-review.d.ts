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
import { type ToolResult } from './types.js';
/** Parameters for branch-based operations */
interface BranchParams {
    baseBranch?: string;
    featureBranch?: string;
    remote?: string;
}
/** Parameters for get_pr_diff tool */
export interface GetPrDiffParams extends BranchParams {
}
/**
 * Gets the raw diff between base branch and current/feature branch.
 * Auto-detects branches if not specified.
 */
export declare function getPrDiff(args?: GetPrDiffParams): Promise<ToolResult>;
/** Parameters for review_pr_changes tool */
export interface ReviewPrChangesParams extends BranchParams {
    focusAreas?: string;
}
/**
 * Gets the PR diff with instructions for AI code review.
 * Provides structured context to help AI perform a thorough review.
 */
export declare function reviewPrChanges(args?: ReviewPrChangesParams): Promise<ToolResult>;
/** Parameters for generate_pr_title tool */
export interface GeneratePrTitleParams extends BranchParams {
    style?: 'conventional' | 'ticket' | 'descriptive';
}
/**
 * Generates a conventional PR title based on the changes.
 * Analyzes the diff to determine the type and scope of changes.
 */
export declare function generatePrTitle(args?: GeneratePrTitleParams): Promise<ToolResult>;
/** Parameters for generate_pr_description tool */
export interface GeneratePrDescriptionParams extends BranchParams {
    includeChecklist?: boolean;
    ticketUrl?: string;
}
/**
 * Generates a complete PR description including summary, changes, and checklist.
 * Ready to paste directly into the PR description field.
 */
export declare function generatePrDescription(args?: GeneratePrDescriptionParams): Promise<ToolResult>;
export {};
//# sourceMappingURL=pr-review.d.ts.map