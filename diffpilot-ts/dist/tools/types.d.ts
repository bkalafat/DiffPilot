/**
 * Tool Result Types - Common result types for MCP tools
 *
 * Ported from: src/Tools/ToolResult.cs
 */
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
/** Content item in a tool result */
export interface ContentItem {
    type: 'text' | 'image' | 'resource';
    text?: string;
    data?: string;
    mimeType?: string;
}
/** Result returned by MCP tools - compatible with CallToolResult */
export type ToolResult = CallToolResult;
/**
 * Creates a successful tool result with text content.
 */
export declare function success(text: string): ToolResult;
/**
 * Creates an error tool result.
 */
export declare function error(message: string): ToolResult;
/**
 * Creates a git error tool result with additional context.
 */
export declare function gitError(operation: string, details: string): ToolResult;
/** Maximum diff content size (in characters) to include in the response */
export declare const MAX_DIFF_CONTENT_LENGTH = 500000;
/**
 * Truncates content if it exceeds the maximum length.
 */
export declare function truncateContent(content: string, maxLength?: number): string;
//# sourceMappingURL=types.d.ts.map