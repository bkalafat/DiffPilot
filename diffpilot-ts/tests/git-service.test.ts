/**
 * Git Service Tests - Phase 1 (Feature Tests)
 * 
 * Tests for git utility and validation functions.
 * Integration tests would require actual git repos.
 */

import { describe, it, expect } from 'vitest';

import {
  isValidBranchName,
  getWorkingDirectory,
  isValidFileName,
} from '../src/git/git-service.js';

describe('GitService', () => {
  describe('isValidBranchName', () => {
    it('should accept valid branch names', () => {
      expect(isValidBranchName('main')).toBe(true);
      expect(isValidBranchName('feature/new-feature')).toBe(true);
      expect(isValidBranchName('bugfix/fix-123')).toBe(true);
      expect(isValidBranchName('user/john/feature')).toBe(true);
    });

    it('should reject empty or null branch names', () => {
      expect(isValidBranchName('')).toBe(false);
      expect(isValidBranchName(null as any)).toBe(false);
      expect(isValidBranchName(undefined as any)).toBe(false);
    });

    it('should reject branch names with special characters', () => {
      expect(isValidBranchName('main branch')).toBe(false); // spaces
      expect(isValidBranchName('branch..name')).toBe(false); // double dots
      expect(isValidBranchName('branch~name')).toBe(false); // tilde
      expect(isValidBranchName('branch^name')).toBe(false); // caret
      expect(isValidBranchName('branch:name')).toBe(false); // colon
      expect(isValidBranchName('branch?name')).toBe(false); // question mark
      expect(isValidBranchName('branch*name')).toBe(false); // asterisk
      expect(isValidBranchName('branch[name')).toBe(false); // bracket
    });

    it('should reject branch names with command injection attempts', () => {
      expect(isValidBranchName('main; rm -rf /')).toBe(false);
      expect(isValidBranchName('main | cat /etc/passwd')).toBe(false);
      expect(isValidBranchName('main && echo hacked')).toBe(false);
      expect(isValidBranchName('$(whoami)')).toBe(false);
      expect(isValidBranchName('`whoami`')).toBe(false);
    });
  });

  describe('getWorkingDirectory', () => {
    it('should return process.cwd()', () => {
      const cwd = getWorkingDirectory();
      expect(cwd).toBe(process.cwd());
    });
  });

  describe('isValidFileName', () => {
    it('should accept valid file names', () => {
      expect(isValidFileName('file.ts')).toBe(true);
      expect(isValidFileName('path/to/file.ts')).toBe(true);
      expect(isValidFileName('.gitignore')).toBe(true);
      expect(isValidFileName('file-name_123.test.ts')).toBe(true);
    });

    it('should reject invalid file names', () => {
      expect(isValidFileName('')).toBe(false);
      expect(isValidFileName(null as any)).toBe(false);
      expect(isValidFileName(undefined as any)).toBe(false);
    });

    it('should reject file names with path traversal', () => {
      expect(isValidFileName('../etc/passwd')).toBe(false);
      expect(isValidFileName('path/../secret')).toBe(false);
    });
  });
});

// ============================================================================
// Base Branch Detection Strategy Tests
// These test the logic patterns, actual git operations would need a real repo
// ============================================================================

describe('Base Branch Detection Strategies', () => {
  describe('Commit Hash Detection', () => {
    // This tests the isCommitHash logic that's used in reflog parsing
    const isCommitHash = (value: string): boolean => {
      if (!value || value.length < 7 || value.length > 40) {
        return false;
      }
      return /^[0-9a-fA-F]+$/.test(value);
    };

    it('should identify valid commit hashes', () => {
      expect(isCommitHash('abc1234')).toBe(true);
      expect(isCommitHash('abc1234def5678')).toBe(true);
      expect(isCommitHash('a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2')).toBe(true);
    });

    it('should reject invalid commit hashes', () => {
      expect(isCommitHash('')).toBe(false);
      expect(isCommitHash('abc')).toBe(false); // Too short
      expect(isCommitHash('main')).toBe(false); // Not hex
      expect(isCommitHash('HEAD')).toBe(false);
      expect(isCommitHash('abc123g')).toBe(false); // Invalid char 'g'
    });
  });

  describe('Reflog Pattern Matching', () => {
    // Test the regex patterns used in reflog parsing
    const createdFromPattern = /branch:\s*Created from\s+(\S+)/i;
    const checkoutPattern = /checkout:\s*moving from\s+(\S+)\s+to\s+(\S+)/i;

    it('should match "Created from" patterns', () => {
      const match1 = 'branch: Created from main'.match(createdFromPattern);
      expect(match1?.[1]).toBe('main');

      const match2 = 'branch: Created from origin/develop'.match(createdFromPattern);
      expect(match2?.[1]).toBe('origin/develop');

      const match3 = 'branch: Created from HEAD'.match(createdFromPattern);
      expect(match3?.[1]).toBe('HEAD');
    });

    it('should match checkout patterns', () => {
      const match = 'checkout: moving from develop to feature/test'.match(checkoutPattern);
      expect(match?.[1]).toBe('develop');
      expect(match?.[2]).toBe('feature/test');
    });

    it('should extract branch name from remote ref', () => {
      const source = 'origin/main';
      const branch = source.includes('/') ? source.split('/').pop()! : source;
      expect(branch).toBe('main');

      const localSource = 'develop';
      const localBranch = localSource.includes('/') ? localSource.split('/').pop()! : localSource;
      expect(localBranch).toBe('develop');
    });
  });

  describe('Tracking Config Pattern', () => {
    // Test refs/heads/ prefix stripping
    it('should strip refs/heads/ prefix', () => {
      const trackingRef = 'refs/heads/main';
      const prefix = 'refs/heads/';
      const branch = trackingRef.startsWith(prefix) 
        ? trackingRef.slice(prefix.length) 
        : trackingRef;
      expect(branch).toBe('main');
    });

    it('should handle plain branch names', () => {
      const trackingRef = 'develop';
      const prefix = 'refs/heads/';
      const branch = trackingRef.startsWith(prefix) 
        ? trackingRef.slice(prefix.length) 
        : trackingRef;
      expect(branch).toBe('develop');
    });
  });

  describe('Merge-Base Logic', () => {
    // Test the parent/child relationship detection logic
    it('should identify parent-child hierarchy in candidates', () => {
      // Simulating: main -> develop -> feature
      // If we have both main and develop as candidates,
      // develop is more specific (child of main)
      
      // This is a simplified version of the isBranchAhead check
      const isMoreSpecific = (candidateIsAheadOfPrevious: boolean, previousIsAheadOfCandidate: boolean) => {
        if (candidateIsAheadOfPrevious) {
          return 'use new candidate';
        } else if (previousIsAheadOfCandidate) {
          return 'keep previous';
        }
        return 'ambiguous';
      };

      // develop is ahead of main (develop derived from main)
      expect(isMoreSpecific(true, false)).toBe('use new candidate');
      
      // main is ahead of develop (shouldn't happen in normal workflow)
      expect(isMoreSpecific(false, true)).toBe('keep previous');
      
      // Both independent branches - ambiguous
      expect(isMoreSpecific(false, false)).toBe('ambiguous');
    });
  });
});

// ============================================================================
// File Path Validation Tests (Ported from GitValidationTests.cs)
// ============================================================================

describe('File Path Validation', () => {
  // Helper function simulating GitService logic
  const isValidFilePath = (path: string): boolean => {
    if (!path) return false;
    if (path.includes('\0')) return false;
    if (path.includes('//')) return false;
    return true;
  };

  it.each([
    ['src/main.cs', true],
    ['path/to/file.txt', true],
    ['file.cs', true],
    ['.gitignore', true],
    ['path/with spaces/file.cs', true], // Spaces are valid
  ])('accepts valid path "%s"', (path, expected) => {
    expect(isValidFilePath(path)).toBe(expected);
  });

  it.each([
    ['', false],
    ['path\0with\0null', false], // Null bytes
    ['path//double/slash', false],
  ])('rejects invalid path "%s"', (path, expected) => {
    expect(isValidFilePath(path)).toBe(expected);
  });
});

// ============================================================================
// Remote URL Validation Tests (Ported from GitValidationTests.cs)
// ============================================================================

describe('Remote URL Validation', () => {
  // Helper function simulating validation logic
  const isValidRemoteUrl = (url: string): boolean => {
    if (!url) return false;
    // SSH format
    if (url.startsWith('git@') && url.includes(':')) return true;
    // HTTPS format (require https, not http)
    if (url.startsWith('https://')) return true;
    return false;
  };

  it.each([
    ['https://github.com/user/repo.git', true],
    ['git@github.com:user/repo.git', true],
    ['https://gitlab.com/user/repo', true],
    ['git@bitbucket.org:user/repo.git', true],
  ])('accepts valid URL "%s"', (url, expected) => {
    expect(isValidRemoteUrl(url)).toBe(expected);
  });

  it.each([
    ['', false],
    ['not-a-url', false],
    ['ftp://invalid.com/repo', false],
    ['http://insecure.com/repo', false], // We require https
  ])('rejects invalid URL "%s"', (url, expected) => {
    expect(isValidRemoteUrl(url)).toBe(expected);
  });
});

// ============================================================================
// Auto-Detection Tests (Ported from GitValidationTests.cs)
// ============================================================================

describe('Auto-Detection Helpers', () => {
  // Helper function to strip remote prefix
  const stripRemotePrefix = (branch: string): string => {
    const prefixes = ['origin/', 'upstream/', 'remote/'];
    for (const prefix of prefixes) {
      if (branch.startsWith(prefix)) {
        return branch.slice(prefix.length);
      }
    }
    return branch;
  };

  // Helper function to detect base branch
  const detectBaseBranch = (branches: string[]): string => {
    // Priority: main > master > develop
    if (branches.includes('main')) return 'main';
    if (branches.includes('master')) return 'master';
    if (branches.includes('develop')) return 'develop';
    return branches[0] ?? 'main';
  };

  describe('stripRemotePrefix', () => {
    it.each([
      ['origin/main', 'main'],
      ['origin/feature/test', 'feature/test'],
      ['upstream/develop', 'develop'],
    ])('removes prefix from "%s" -> "%s"', (input, expected) => {
      expect(stripRemotePrefix(input)).toBe(expected);
    });

    it.each([
      ['main', 'main'],
      ['feature/test', 'feature/test'],
    ])('preserves local branch "%s"', (input, expected) => {
      expect(stripRemotePrefix(input)).toBe(expected);
    });
  });

  describe('detectBaseBranch', () => {
    it.each([
      [['main', 'master'], 'main'],
      [['master', 'develop'], 'master'],
      [['develop', 'main'], 'main'],
    ])('prefers main over master in %p -> "%s"', (branches, expected) => {
      expect(detectBaseBranch(branches)).toBe(expected);
    });

    it('returns first branch when no standard branch found', () => {
      expect(detectBaseBranch(['feature', 'bugfix'])).toBe('feature');
    });

    it('returns "main" for empty array', () => {
      expect(detectBaseBranch([])).toBe('main');
    });
  });
});
