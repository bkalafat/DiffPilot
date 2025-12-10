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

// ============================================================================
// PR Generation Helper Tests (Ported from PrGenerationTests.cs)
// ============================================================================

describe('PR Generation Helpers', () => {
  // Helper functions simulating PrReviewTools logic
  const generateConventionalTitle = (type: string, scope: string | null, description: string): string => {
    const prefix = scope ? `${type}(${scope})` : type;
    const fullTitle = `${prefix}: ${description}`;
    
    if (fullTitle.length > 72) {
      return fullTitle.slice(0, 69) + '...';
    }
    
    return fullTitle;
  };

  const extractTicketNumber = (branchName: string): string | null => {
    // Pattern: LETTERS-NUMBERS (e.g., JIRA-123, PROJ-456)
    const match = branchName.match(/([A-Z]+-\d+)/i);
    return match ? match[1].toUpperCase() : null;
  };

  const generatePrDescription = (summary: string, changes: string[], includeChecklist: boolean): string => {
    let result = '## Summary\n' + summary + '\n\n';
    result += '## Changes\n';
    for (const change of changes) {
      result += '- ' + change + '\n';
    }
    result += '\n';
    
    if (includeChecklist) {
      result += '## Checklist\n';
      result += '- [ ] Tests pass\n';
      result += '- [ ] Code reviewed\n';
      result += '- [ ] Documentation updated\n';
    }
    
    return result;
  };

  const generatePrDescriptionWithTicket = (summary: string, changes: string[], ticketUrl: string): string => {
    let result = '## Summary\n' + summary + '\n\n';
    result += '## Related Issue\n' + ticketUrl + '\n\n';
    result += '## Changes\n';
    for (const change of changes) {
      result += '- ' + change + '\n';
    }
    return result;
  };

  const summarizeChanges = (files: Array<{ file: string; additions: number; deletions: number }>): string => {
    const sourceFiles = files.filter(f => 
      !f.file.toLowerCase().includes('test') && 
      !f.file.toLowerCase().endsWith('.md')
    ).length;
    const testFiles = files.filter(f => f.file.toLowerCase().includes('test')).length;
    const docFiles = files.filter(f => f.file.toLowerCase().endsWith('.md')).length;
    
    const totalAdditions = files.reduce((sum, f) => sum + f.additions, 0);
    const totalDeletions = files.reduce((sum, f) => sum + f.deletions, 0);
    
    const parts: string[] = [];
    if (sourceFiles > 0) parts.push(`${sourceFiles} source files`);
    if (testFiles > 0) parts.push(`${testFiles} test files`);
    if (docFiles > 0) parts.push(`${docFiles} documentation`);
    
    return `Changed ${parts.join(', ')} (+${totalAdditions}/-${totalDeletions})`;
  };

  describe('PR Title Generation', () => {
    it.each([
      ['feat', 'add login feature', 'feat: add login feature'],
      ['fix', 'resolve null pointer', 'fix: resolve null pointer'],
      ['docs', 'update README', 'docs: update README'],
      ['chore', 'update dependencies', 'chore: update dependencies'],
    ])('generates conventional title "%s: %s"', (type, description, expected) => {
      expect(generateConventionalTitle(type, null, description)).toBe(expected);
    });

    it.each([
      ['feat', 'api', 'add endpoint', 'feat(api): add endpoint'],
      ['fix', 'auth', 'token expiry', 'fix(auth): token expiry'],
    ])('generates title with scope "%s(%s): %s"', (type, scope, description, expected) => {
      expect(generateConventionalTitle(type, scope, description)).toBe(expected);
    });

    it('truncates long description', () => {
      const longDescription = 'a'.repeat(100);
      const title = generateConventionalTitle('feat', null, longDescription);
      
      // PR titles should be max ~72 characters
      expect(title.length).toBeLessThanOrEqual(75);
      expect(title.endsWith('...')).toBe(true);
    });
  });

  describe('Ticket Extraction', () => {
    it.each([
      ['feature/JIRA-123-add-login', 'JIRA-123'],
      ['bugfix/PROJ-456-fix-crash', 'PROJ-456'],
      ['ABC-789/some-feature', 'ABC-789'],
    ])('extracts ticket from branch "%s" -> "%s"', (branch, expected) => {
      expect(extractTicketNumber(branch)).toBe(expected);
    });

    it('returns null for no ticket', () => {
      const branch = 'feature/add-login-page';
      expect(extractTicketNumber(branch)).toBeNull();
    });

    it('returns null for numbers only', () => {
      const branch = 'feature/123-no-prefix';
      expect(extractTicketNumber(branch)).toBeNull();
    });
  });

  describe('PR Description Sections', () => {
    it('includes all sections', () => {
      const changes = ['Added login page', 'Fixed validation'];
      const description = generatePrDescription('Summary', changes, true);
      
      expect(description).toContain('## Summary');
      expect(description).toContain('## Changes');
      expect(description).toContain('## Checklist');
      expect(description).toContain('- Added login page');
      expect(description).toContain('- Fixed validation');
    });

    it('omits checklist when disabled', () => {
      const changes = ['Change 1'];
      const description = generatePrDescription('Summary', changes, false);
      
      expect(description).not.toContain('Checklist');
    });

    it('includes ticket link', () => {
      const changes = ['Change 1'];
      const ticketUrl = 'https://jira.example.com/PROJ-123';
      const description = generatePrDescriptionWithTicket('Summary', changes, ticketUrl);
      
      expect(description).toContain('Related Issue');
      expect(description).toContain(ticketUrl);
    });
  });

  describe('Change Summary', () => {
    it('groups by type', () => {
      const files = [
        { file: 'src/Login.cs', additions: 50, deletions: 10 },
        { file: 'src/Auth.cs', additions: 30, deletions: 5 },
        { file: 'tests/LoginTests.cs', additions: 100, deletions: 0 },
        { file: 'README.md', additions: 20, deletions: 5 },
      ];
      
      const summary = summarizeChanges(files);
      
      expect(summary).toContain('source files');
      expect(summary).toContain('test files');
      expect(summary).toContain('documentation');
    });

    it('calculates totals', () => {
      const files = [
        { file: 'file1.cs', additions: 100, deletions: 50 },
        { file: 'file2.cs', additions: 50, deletions: 25 },
      ];
      
      const summary = summarizeChanges(files);
      
      expect(summary).toContain('150'); // Total additions
      expect(summary).toContain('75');  // Total deletions
    });
  });
});
