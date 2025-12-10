#!/usr/bin/env node
/**
 * DiffPilot MCP Server - TypeScript Implementation
 * 
 * A Model Context Protocol server that provides PR code review and
 * developer productivity tools.
 * 
 * Run with: npx diffpilot
 * 
 * Tools provided:
 * - get_pr_diff: Get raw diff between branches
 * - review_pr_changes: Get diff with AI review instructions
 * - generate_pr_title: Generate conventional PR title
 * - generate_pr_description: Generate full PR description with checklist
 * - generate_commit_message: Generate commit message from changes
 * - scan_secrets: Detect secrets, API keys, passwords
 * - diff_stats: Get change statistics
 * - suggest_tests: Recommend test cases
 * - generate_changelog: Generate changelog from commits
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  type Tool,
} from '@modelcontextprotocol/sdk/types.js';

// Import security helpers
import {
  checkRateLimit,
  sanitizeOutput,
  sanitizeErrorMessage,
  logSecurityEvent,
} from './security/security-helpers.js';

// Import PR review tools
import {
  getPrDiff,
  reviewPrChanges,
  generatePrTitle,
  generatePrDescription,
  type GetPrDiffParams,
  type ReviewPrChangesParams,
  type GeneratePrTitleParams,
  type GeneratePrDescriptionParams,
} from './tools/pr-review.js';

// Import developer tools
import {
  generateCommitMessage,
  scanSecrets,
  diffStats,
  suggestTests,
  generateChangelog,
  type GenerateCommitMessageParams,
  type ScanSecretsParams,
  type DiffStatsParams,
  type SuggestTestsParams,
  type GenerateChangelogParams,
} from './tools/developer.js';

// ============================================================================
// Tool Definitions
// ============================================================================

const TOOLS: Tool[] = [
  // PR Review Tools
  {
    name: 'get_pr_diff',
    description: 'Get the raw diff between branches for any purpose. Useful for analyzing changes, reviewing code, or generating reports.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: "The base branch name (e.g., 'main'). Auto-detected if not provided.",
        },
        featureBranch: {
          type: 'string',
          description: 'The feature branch name. Defaults to current branch if not provided.',
        },
        remote: {
          type: 'string',
          description: "Git remote name (default: 'origin').",
        },
      },
      required: [],
    },
  },
  {
    name: 'review_pr_changes',
    description: 'Get the diff between branches with detailed AI review instructions. Perfect for comprehensive code reviews.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: "The base branch name (e.g., 'main'). Auto-detected if not provided.",
        },
        focusAreas: {
          type: 'string',
          description: "Optional focus areas for the review (e.g., 'security, performance, error handling').",
        },
      },
      required: [],
    },
  },
  {
    name: 'generate_pr_title',
    description: 'Generate a conventional commit-style PR title based on the changes.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: "The base branch name (e.g., 'main'). Auto-detected if not provided.",
        },
        style: {
          type: 'string',
          enum: ['conventional', 'descriptive', 'ticket'],
          description: "Title style: 'conventional' (feat/fix/chore), 'descriptive', or 'ticket' (includes branch ticket number). Default: 'conventional'.",
        },
      },
      required: [],
    },
  },
  {
    name: 'generate_pr_description',
    description: 'Generate a complete PR description with summary, changes, testing notes, and checklist.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: "The base branch name (e.g., 'main'). Auto-detected if not provided.",
        },
        includeChecklist: {
          type: 'boolean',
          description: 'Include a PR checklist (default: true).',
        },
        ticketUrl: {
          type: 'string',
          description: 'Optional ticket/issue URL to include in the description.',
        },
      },
      required: [],
    },
  },

  // Developer Tools
  {
    name: 'generate_commit_message',
    description: 'Generate a commit message based on staged or unstaged changes.',
    inputSchema: {
      type: 'object',
      properties: {
        style: {
          type: 'string',
          enum: ['conventional', 'simple'],
          description: "Message style: 'conventional' (feat/fix/chore) or 'simple'. Default: 'conventional'.",
        },
        scope: {
          type: 'string',
          description: "Optional scope for conventional commits (e.g., 'api', 'ui', 'auth').",
        },
        includeBody: {
          type: 'boolean',
          description: 'Include body section in suggestion (default: true).',
        },
      },
      required: [],
    },
  },
  {
    name: 'scan_secrets',
    description: 'Scan staged and unstaged changes for accidentally committed secrets, API keys, passwords, and tokens.',
    inputSchema: {
      type: 'object',
      properties: {
        scanStaged: {
          type: 'boolean',
          description: 'Scan staged changes (default: true).',
        },
        scanUnstaged: {
          type: 'boolean',
          description: 'Scan unstaged changes (default: true).',
        },
      },
      required: [],
    },
  },
  {
    name: 'diff_stats',
    description: 'Get detailed statistics about changes by file, including lines added/removed and file type breakdown.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: 'Base branch for comparison. If provided, compares branches instead of working directory.',
        },
        featureBranch: {
          type: 'string',
          description: 'Feature branch for comparison. Defaults to current branch.',
        },
        includeWorkingDir: {
          type: 'boolean',
          description: 'Include working directory stats (default: true).',
        },
      },
      required: [],
    },
  },
  {
    name: 'suggest_tests',
    description: 'Analyze changed code and suggest appropriate test cases based on the types of changes detected.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: 'Base branch for comparison. If not provided, analyzes working directory changes.',
        },
      },
      required: [],
    },
  },
  {
    name: 'generate_changelog',
    description: 'Generate changelog entries from commits between branches, categorized by type of change.',
    inputSchema: {
      type: 'object',
      properties: {
        baseBranch: {
          type: 'string',
          description: "Base branch to compare against (default: 'main').",
        },
        featureBranch: {
          type: 'string',
          description: 'Feature branch with commits. Defaults to current branch.',
        },
        format: {
          type: 'string',
          enum: ['keepachangelog', 'simple'],
          description: "Output format: 'keepachangelog' (categorized) or 'simple' (flat list). Default: 'keepachangelog'.",
        },
      },
      required: [],
    },
  },
];

// ============================================================================
// Server Setup
// ============================================================================

/**
 * Creates and configures the MCP server.
 */
function createServer(): Server {
  const server = new Server(
    {
      name: 'diffpilot',
      version: '1.0.0',
    },
    {
      capabilities: {
        tools: {},
      },
    }
  );

  // Register tool listing handler
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return { tools: TOOLS };
  });

  // Register tool execution handler
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    // Security: Validate tool name format (CWE-20: Input Validation)
    if (!name || name.length > 50 || !/^[a-zA-Z_][a-zA-Z0-9_-]*$/.test(name)) {
      logSecurityEvent('INVALID_TOOL_NAME', `Rejected tool name: ${name?.slice(0, 50)}`);
      return {
        content: [{ type: 'text', text: 'Invalid tool name format.' }],
        isError: true,
      };
    }

    // Security: Rate limiting per tool (CWE-400: DoS Prevention)
    if (!checkRateLimit(name)) {
      logSecurityEvent('RATE_LIMIT_EXCEEDED', `Tool: ${name}`);
      return {
        content: [{ type: 'text', text: 'Rate limit exceeded. Please wait before retrying.' }],
        isError: true,
      };
    }

    try {
      let result;
      switch (name) {
        // PR Review Tools
        case 'get_pr_diff':
          result = await getPrDiff(args as GetPrDiffParams);
          break;

        case 'review_pr_changes':
          result = await reviewPrChanges(args as ReviewPrChangesParams);
          break;

        case 'generate_pr_title':
          result = await generatePrTitle(args as GeneratePrTitleParams);
          break;

        case 'generate_pr_description':
          result = await generatePrDescription(args as GeneratePrDescriptionParams);
          break;

        // Developer Tools
        case 'generate_commit_message':
          result = await generateCommitMessage(args as GenerateCommitMessageParams);
          break;

        case 'scan_secrets':
          result = await scanSecrets(args as ScanSecretsParams);
          break;

        case 'diff_stats':
          result = await diffStats(args as DiffStatsParams);
          break;

        case 'suggest_tests':
          result = await suggestTests(args as SuggestTestsParams);
          break;

        case 'generate_changelog':
          result = await generateChangelog(args as GenerateChangelogParams);
          break;

        default:
          return {
            content: [{ type: 'text', text: `Unknown tool: ${name}` }],
            isError: true,
          };
      }

      // Security: Sanitize output to prevent sensitive data exposure (CWE-200)
      if (result && result.content) {
        result.content = result.content.map((item) => {
          if (item.type === 'text' && typeof item.text === 'string') {
            return { ...item, text: sanitizeOutput(item.text) };
          }
          return item;
        });
      }

      return result;
    } catch (err) {
      // Security: Sanitize error messages (CWE-200: Information Disclosure)
      const rawMessage = err instanceof Error ? err.message : String(err);
      const safeMessage = sanitizeErrorMessage(rawMessage);
      logSecurityEvent('TOOL_ERROR', `Tool '${name}' failed`);
      return {
        content: [{ type: 'text', text: `Error: ${safeMessage}` }],
        isError: true,
      };
    }
  });

  return server;
}

// ============================================================================
// Main Entry Point
// ============================================================================

async function main(): Promise<void> {
  const server = createServer();
  const transport = new StdioServerTransport();

  // Log startup to stderr (not stdout - that's for JSON-RPC)
  console.error('DiffPilot MCP Server starting...');
  console.error(`Tools available: ${TOOLS.length}`);

  await server.connect(transport);

  console.error('DiffPilot MCP Server running on stdio');
}

// Run the server
main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});
