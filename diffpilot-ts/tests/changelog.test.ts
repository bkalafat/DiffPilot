/**
 * Changelog Generation Tests
 * Ported from: tests/ChangelogGenerationTests.cs
 * 
 * Tests for changelog generation logic including commit categorization and formatting.
 */

import { describe, it, expect } from 'vitest';

// ============================================================================
// Helper Functions (simulating DeveloperTools logic)
// ============================================================================

/**
 * Categorizes a commit message into a changelog category.
 */
function categorizeCommit(message: string): string {
  const lowerMessage = message.toLowerCase();

  if (lowerMessage.startsWith('feat')) return 'Added';
  if (lowerMessage.startsWith('fix')) return 'Fixed';
  if (lowerMessage.startsWith('docs')) return 'Documentation';
  
  return 'Changed';
}

/**
 * Cleans commit message by removing conventional commit prefix.
 */
function cleanCommitMessage(message: string): string {
  // Remove conventional commit prefix like "feat:", "fix(scope):", etc.
  const colonIndex = message.indexOf(':');
  if (colonIndex > 0 && colonIndex < 20) {
    const afterColon = message.slice(colonIndex + 1).trimStart();
    if (afterColon) {
      // Capitalize first letter
      return afterColon.charAt(0).toUpperCase() + afterColon.slice(1);
    }
  }
  return message;
}

/**
 * Formats changelog entries into markdown format.
 */
function formatChangelog(entries: Record<string, string[]>): string {
  const hasEntries = Object.values(entries).some(items => items.length > 0);
  if (!hasEntries) {
    return 'No changes.';
  }

  // Keep a Changelog order
  const order = ['Added', 'Changed', 'Deprecated', 'Removed', 'Fixed', 'Security', 'Documentation'];
  let result = '';

  for (const category of order) {
    const items = entries[category];
    if (items && items.length > 0) {
      result += `### ${category}\n`;
      for (const item of items) {
        result += `- ${item}\n`;
      }
      result += '\n';
    }
  }

  return result.trimEnd();
}

// ============================================================================
// Commit Categorization Tests
// ============================================================================

describe('Changelog Generation', () => {
  describe('Commit Categorization', () => {
    it.each([
      ['feat: add new feature', 'Added'],
      ['feat(ui): new button component', 'Added'],
      ['fix: resolve null pointer', 'Fixed'],
      ['fix(api): handle timeout', 'Fixed'],
      ['docs: update README', 'Documentation'],
      ['chore: update dependencies', 'Changed'],
      ['refactor: clean up code', 'Changed'],
      ['perf: optimize query', 'Changed'],
      ['test: add unit tests', 'Changed'],
      ['style: format code', 'Changed'],
    ])('categorizeCommit("%s") returns "%s"', (commitMessage, expectedCategory) => {
      expect(categorizeCommit(commitMessage)).toBe(expectedCategory);
    });

    it.each([
      ['random commit message', 'Changed'],
      ['Updated something', 'Changed'],
      ['WIP: work in progress', 'Changed'],
    ])('categorizeCommit defaults to "Changed" for "%s"', (commitMessage, expectedCategory) => {
      expect(categorizeCommit(commitMessage)).toBe(expectedCategory);
    });
  });

  // ============================================================================
  // Commit Message Cleaning Tests
  // ============================================================================

  describe('Commit Message Cleaning', () => {
    it.each([
      ['feat: add login', 'Add login'],
      ['fix(auth): handle null', 'Handle null'],
      ['docs(readme): update install', 'Update install'],
      ['chore!: breaking change', 'Breaking change'],
    ])('cleanCommitMessage("%s") returns "%s"', (input, expected) => {
      expect(cleanCommitMessage(input)).toBe(expected);
    });

    it('preserves regular messages without prefix', () => {
      const message = 'Update configuration';
      expect(cleanCommitMessage(message)).toBe('Update configuration');
    });
  });

  // ============================================================================
  // Changelog Formatting Tests
  // ============================================================================

  describe('Changelog Formatting', () => {
    it('groups entries correctly', () => {
      const entries: Record<string, string[]> = {
        Added: ['New feature A', 'New feature B'],
        Fixed: ['Bug fix X'],
        Changed: [],
      };

      const changelog = formatChangelog(entries);

      expect(changelog).toContain('### Added');
      expect(changelog).toContain('### Fixed');
      expect(changelog).toContain('- New feature A');
      expect(changelog).toContain('- New feature B');
      expect(changelog).toContain('- Bug fix X');
    });

    it('omits empty categories', () => {
      const entries: Record<string, string[]> = {
        Added: ['New feature'],
        Fixed: [], // Empty
        Changed: [], // Empty
      };

      const changelog = formatChangelog(entries);

      expect(changelog).toContain('### Added');
      expect(changelog).not.toContain('### Fixed');
      expect(changelog).not.toContain('### Changed');
    });

    it('handles no entries', () => {
      const entries: Record<string, string[]> = {};
      const changelog = formatChangelog(entries);

      expect(changelog).toBe('No changes.');
    });

    it('handles all empty categories', () => {
      const entries: Record<string, string[]> = {
        Added: [],
        Fixed: [],
        Changed: [],
      };
      const changelog = formatChangelog(entries);

      expect(changelog).toBe('No changes.');
    });
  });

  // ============================================================================
  // Keep A Changelog Order Tests
  // ============================================================================

  describe('Keep A Changelog Order', () => {
    it('follows Keep A Changelog order', () => {
      // Keep a Changelog order: Added, Changed, Deprecated, Removed, Fixed, Security
      const entries: Record<string, string[]> = {
        Fixed: ['Fix 1'],
        Added: ['Feature 1'],
        Security: ['Security fix'],
        Changed: ['Change 1'],
      };

      const changelog = formatChangelog(entries);

      const addedPos = changelog.indexOf('### Added');
      const changedPos = changelog.indexOf('### Changed');
      const fixedPos = changelog.indexOf('### Fixed');
      const securityPos = changelog.indexOf('### Security');

      expect(addedPos).toBeLessThan(changedPos);
      expect(changedPos).toBeLessThan(fixedPos);
      expect(fixedPos).toBeLessThan(securityPos);
    });

    it('includes all standard categories in correct order', () => {
      const entries: Record<string, string[]> = {
        Security: ['Security item'],
        Removed: ['Removed item'],
        Deprecated: ['Deprecated item'],
        Changed: ['Changed item'],
        Added: ['Added item'],
        Fixed: ['Fixed item'],
      };

      const changelog = formatChangelog(entries);
      const lines = changelog.split('\n');
      const categoryOrder = lines.filter(line => line.startsWith('###')).map(line => line.replace('### ', ''));

      expect(categoryOrder).toEqual(['Added', 'Changed', 'Deprecated', 'Removed', 'Fixed', 'Security']);
    });
  });
});
