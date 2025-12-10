/**
 * Developer Tools Tests - Phase 1 (Feature Tests)
 * 
 * Tests for developer productivity tool functionality.
 * Security tests (scan_secrets) are in Phase 2.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock the git service
vi.mock('../src/git/git-service.js', () => ({
  runGitCommand: vi.fn(),
  getCurrentBranch: vi.fn(),
  findBaseBranch: vi.fn(),
  getWorkingDirectory: vi.fn(() => '/test/repo'),
}));

import {
  generateCommitMessage,
  scanSecrets,
  diffStats,
  suggestTests,
  generateChangelog,
} from '../src/tools/developer.js';
import { runGitCommand, getCurrentBranch, findBaseBranch } from '../src/git/git-service.js';

const mockRunGitCommand = vi.mocked(runGitCommand);
const mockGetCurrentBranch = vi.mocked(getCurrentBranch);
const mockFindBaseBranch = vi.mocked(findBaseBranch);

describe('Developer Tools', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetCurrentBranch.mockResolvedValue('feature-branch');
    mockFindBaseBranch.mockResolvedValue({ baseBranch: 'main', remote: 'origin' });
  });

  describe('generateCommitMessage', () => {
    it('should analyze staged changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }) // staged stat
        .mockResolvedValueOnce({ exitCode: 0, output: '+console.log("hello")' }) // staged diff
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }); // stat for display

      const result = await generateCommitMessage({});

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('staged changes');
    });

    it('should fall back to unstaged when no staged changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // no staged
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }) // unstaged stat
        .mockResolvedValueOnce({ exitCode: 0, output: '+new code' }) // unstaged diff
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' }); // stat

      const result = await generateCommitMessage({});

      expect(result.content[0].text).toContain('unstaged changes');
    });

    it('should detect fix commit type', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' })
        .mockResolvedValueOnce({ exitCode: 0, output: '+fix: bug in validation' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' });

      const result = await generateCommitMessage({ style: 'conventional' });

      expect(result.content[0].text).toContain('fix');
    });

    it('should return error when no changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // no staged
        .mockResolvedValueOnce({ exitCode: 0, output: '' }); // no unstaged

      const result = await generateCommitMessage({});

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('No changes');
    });

    it('should include scope when provided', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' })
        .mockResolvedValueOnce({ exitCode: 0, output: '+new feature' })
        .mockResolvedValueOnce({ exitCode: 0, output: 'file.ts | 5 +' });

      const result = await generateCommitMessage({ scope: 'api' });

      expect(result.content[0].text).toContain('(api)');
    });
  });

  describe('scanSecrets', () => {
    it('should detect API keys', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/config.js\n+apiKey = "sk_live_1234567890abcdefghij"' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await scanSecrets({});

      expect(result.content[0].text).toContain('Secret');
    });

    it('should detect AWS access keys', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/aws.ts\n+const key = "AKIAIOSFODNN7EXAMPLE"' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await scanSecrets({});

      expect(result.content[0].text).toContain('AWS');
    });

    it('should detect GitHub tokens', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/.env\n+GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await scanSecrets({});

      expect(result.content[0].text).toContain('GitHub');
    });

    it('should report clean when no secrets found', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/code.ts\n+console.log("hello")' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await scanSecrets({});

      expect(result.content[0].text).toContain('No Secrets Detected');
    });

    it('should handle no changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await scanSecrets({});

      expect(result.content[0].text).toContain('clean');
    });
  });

  describe('diffStats', () => {
    it('should show working directory stats', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '10\t5\tfile.ts' }) // staged numstat
        .mockResolvedValueOnce({ exitCode: 0, output: '3\t1\tother.ts' }); // unstaged numstat

      const result = await diffStats({});

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('Working Directory');
      expect(result.content[0].text).toContain('Staged');
      expect(result.content[0].text).toContain('Unstaged');
    });

    it('should show branch comparison stats', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // staged
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // unstaged
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: '20\t10\tfile.ts\n5\t2\tother.ts' }) // numstat
        .mockResolvedValueOnce({ exitCode: 0, output: '3' }); // commit count

      const result = await diffStats({ baseBranch: 'main' });

      expect(result.content[0].text).toContain('Branch Comparison');
      expect(result.content[0].text).toContain('Commits');
    });

    it('should group changes by file type', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '10\t5\tfile.ts\n20\t10\tother.ts\n5\t2\tstyle.css' })
        .mockResolvedValueOnce({ exitCode: 0, output: '5' });

      const result = await diffStats({ baseBranch: 'main' });

      expect(result.content[0].text).toContain('.ts');
      expect(result.content[0].text).toContain('.css');
    });
  });

  describe('suggestTests', () => {
    it('should suggest tests for async code', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/service.ts\n+async function fetchData() {\n+  await api.get();\n+}' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await suggestTests({});

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toContain('async');
    });

    it('should suggest tests for exception handling', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/handler.ts\n+try { doSomething(); } catch (e) { log(e); }' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await suggestTests({});

      expect(result.content[0].text).toContain('exception');
    });

    it('should skip test files in suggestions', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '+++ b/service.test.ts\n+it("should work", () => {})' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await suggestTests({});

      expect(result.content[0].text).toContain('test files');
    });

    it('should handle no changes', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' })
        .mockResolvedValueOnce({ exitCode: 0, output: '' });

      const result = await suggestTests({});

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('No changes');
    });
  });

  describe('generateChangelog', () => {
    beforeEach(() => {
      mockRunGitCommand.mockReset();
    });

    it('should handle no commits', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: '' }); // empty log

      const result = await generateChangelog({});

      expect(result.content[0].text).toContain('No commits');
    });

    it('should categorize feat commits as Added', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'abc1234|feat: add new feature|John|2024-01-01\ndef5678|fix: fix bug|Jane|2024-01-02' }); // log with commits

      const result = await generateChangelog({});

      expect(result.content[0].text).toContain('Added');
    });

    it('should categorize fix commits as Fixed', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'abc1234|fix: resolve bug|John|2024-01-01' }); // log with fix commit

      const result = await generateChangelog({});

      expect(result.content[0].text).toContain('Fixed');
    });

    it('should use simple format when specified', async () => {
      mockRunGitCommand
        .mockResolvedValueOnce({ exitCode: 0, output: '' }) // fetch
        .mockResolvedValueOnce({ exitCode: 0, output: 'abc1234|feat: new feature|John|2024-01-01' }); // log

      const result = await generateChangelog({ format: 'simple' });

      expect(result.content[0].text).toContain('Changes');
    });
  });
});
