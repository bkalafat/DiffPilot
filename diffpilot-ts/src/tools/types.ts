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
export function success(text: string): ToolResult {
  return {
    content: [{ type: 'text', text }],
    isError: false,
  };
}

/**
 * Creates an error tool result.
 */
export function error(message: string): ToolResult {
  return {
    content: [{ type: 'text', text: `Error: ${message}` }],
    isError: true,
  };
}

/**
 * Creates a git error tool result with additional context.
 */
export function gitError(operation: string, details: string): ToolResult {
  return {
    content: [
      {
        type: 'text',
        text: `Git Error: ${operation}\n\nDetails:\n${details}`,
      },
    ],
    isError: true,
  };
}

/** Maximum diff content size (in characters) to include in the response */
export const MAX_DIFF_CONTENT_LENGTH = 500_000;

/**
 * Truncates content if it exceeds the maximum length.
 */
export function truncateContent(content: string, maxLength: number = MAX_DIFF_CONTENT_LENGTH): string {
  if (content.length <= maxLength) {
    return content;
  }
  
  return (
    content.slice(0, maxLength) +
    `\n\n[... Truncated at ${maxLength.toLocaleString()} characters. Total size: ${content.length.toLocaleString()} characters]`
  );
}
