/**
 * PR Review Tools Tests - Phase 1 (Feature Tests)
 * 
 * Tests for PR review tool functionality.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock the git service
vi.mock('../src/git/git-service.js', () => ({
  runGitCommand: vi.fn(),
  getCurrentBranch: vi.fn(),
  findBaseBranch: vi.fn(),
  getWorkingDirectory: vi.fn(() => '/test/repo'),
  isValidBranchName: vi.fn((name: string) => {
    if (!name) return false;
    return /^[a-zA-Z0-9/_-]+$/.test(name) && !name.includes('..');
  }),
}));

import {
  getPrDiff,
  reviewPrChanges,
  generatePrTitle,
  generatePrDescription,
} from '../src/tools/pr-review.js';
import { runGitCommand, getCurrentBranch, findBaseBranch } from '../src/git/git-service.js';

const mockRunGitCommand = vi.mocked(runGitCommand);
const mockGetCurrentBranch = vi.mocked(getCurrentBranch);
const mockFindBaseBranch = vi.mocked(findBaseBranch);

describe('PR Review Tools', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetCurrentBranch.mockResolvedValue('feature-branch');
    mockFindBaseBranch.mockResolvedValue({ baseBranch: 'main', remote: 'origin' });
  });

  describe('getPrDiff', () => {
    it('should return diff between branches', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff --git a/file.ts...' }); // diff

      const result = await getPrDiff({ baseBranch: 'main' });

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('diff');
    });

    it('should auto-detect branches when not provided', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff content' }); // diff

      await getPrDiff({});

      expect(mockGetCurrentBranch).toHaveBeenCalled();
      expect(mockFindBaseBranch).toHaveBeenCalled();
    });

    it('should handle empty diff', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: '' }); // empty diff

      const result = await getPrDiff({ baseBranch: 'main' });

      expect(result.content[0].text).toContain('No changes');
    });

    it('should reject invalid branch names', async () => {
      const result = await getPrDiff({ baseBranch: 'main; rm -rf /' });

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('invalid');
    });
  });

  describe('reviewPrChanges', () => {
    it('should return diff with review instructions', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff --git a/file.ts\n+new code' }) // diff
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }); // stat

      const result = await reviewPrChanges({});

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('CRITICAL REVIEW MODE');
    });

    it('should include focus areas when provided', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff content' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' });

      const result = await reviewPrChanges({ focusAreas: 'security, performance' });

      expect(result.content[0].text).toContain('security');
      expect(result.content[0].text).toContain('performance');
    });
  });

  describe('generatePrTitle', () => {
    it('should generate conventional title', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'sha1 feat: add new feature\nsha2 fix: bug fix' }) // log
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }); // stat

      const result = await generatePrTitle({ style: 'conventional' });

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('feat');
    });

    it('should handle empty commits list', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await generatePrTitle({});

      // Even with no commits, it generates instructions
      expect(result.content[0].text).toContain('PR Title Generator');
    });
  });

  describe('generatePrDescription', () => {
    it('should generate description with changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff --git a/file.ts' }) // diff
        .mockResolvedValueOnce({ exitCode: 0, output: 'sha1 add feature' }) // log
        .mockResolvedValueOnce({ exitCode: 0, output: 'file1.ts | 10 +' }); // stat

      const result = await generatePrDescription({});

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('Summary');
    });

    it('should include checklist by default', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff content' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'sha1 commit' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' });

      const result = await generatePrDescription({});

      expect(result.content[0].text).toContain('Checklist');
    });

    it('should include ticket URL when provided', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'diff content' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'sha1 commit' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' });

      const result = await generatePrDescription({ ticketUrl: 'https://jira.example.com/PROJ-123' });

      expect(result.content[0].text).toContain('https://jira.example.com/PROJ-123');
    });
  });
});
