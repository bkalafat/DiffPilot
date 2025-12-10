/**
 * Commit Type Detection Tests
 * Ported from: tests/CommitTypeDetectionTests.cs
 * 
 * Tests for determining commit type from diff content and file changes.
 * Used in generate_commit_message tool.
 */

import { describe, it, expect } from 'vitest';

// ============================================================================
// Helper Functions (simulating DeveloperTools logic)
// ============================================================================

/**
 * Checks if a file is a test file.
 */
function isTestFile(path: string): boolean {
  const lower = path.toLowerCase();
  return lower.includes('test') || 
         lower.includes('spec') || 
         lower.includes('__tests__');
}

/**
 * Checks if a file is a documentation file.
 */
function isDocFile(path: string): boolean {
  const lower = path.toLowerCase();
  return lower.endsWith('.md') || 
         lower.includes('docs/') || 
         lower.includes('readme') || 
         lower.includes('changelog');
}

/**
 * Checks if a file is a configuration file.
 */
function isConfigFile(path: string): boolean {
  const lower = path.toLowerCase();
  const configPatterns = ['.json', '.yaml', '.yml', '.config', '.gitignore', 
                          '.github/', 'package.json', 'tsconfig'];
  return configPatterns.some(p => lower.includes(p));
}

/**
 * Determines the commit type based on file changes and diff content.
 */
function determineCommitType(files: string[], diff: string): string {
  const lower = files.map(f => f.toLowerCase());
  
  // Check for deleted files
  if (diff.includes('deleted file mode')) {
    return 'chore';
  }
    
  // Check for new files
  if (diff.includes('new file mode')) {
    return 'feat';
  }
  
  // All test files
  if (lower.every(f => isTestFile(f))) {
    return 'test';
  }
    
  // All docs files
  if (lower.every(f => isDocFile(f))) {
    return 'docs';
  }
    
  // All config files
  if (lower.every(f => isConfigFile(f))) {
    return 'chore';
  }
  
  // Default to feat for source changes
  return 'feat';
}

/**
 * Detects the scope from file paths.
 */
function detectScope(files: string[]): string | null {
  const scopes = new Set<string>();
  
  for (const file of files) {
    const parts = file.split('/');
    if (parts.length >= 2) {
      // Get the first meaningful directory (skip 'src')
      const scopeIndex = parts[0].toLowerCase() === 'src' ? 1 : 0;
      if (scopeIndex < parts.length - 1) {
        scopes.add(parts[scopeIndex]);
      }
    }
  }
  
  return scopes.size === 1 ? [...scopes][0] : null;
}

// ============================================================================
// Commit Type from File Changes Tests
// ============================================================================

describe('Commit Type Detection', () => {
  describe('Commit Type from File Changes', () => {
    it.each([
      [['tests/UserServiceTests.cs'], 'test'],
      [['src/__tests__/user.test.js'], 'test'],
      [['spec/models/user_spec.rb'], 'test'],
    ])('determineCommitType for test files %p returns "%s"', (files, expected) => {
      expect(determineCommitType(files, '')).toBe(expected);
    });

    it.each([
      [['README.md'], 'docs'],
      [['docs/guide.md', 'docs/api.md'], 'docs'],
      [['CHANGELOG.md'], 'docs'],
    ])('determineCommitType for docs files %p returns "%s"', (files, expected) => {
      expect(determineCommitType(files, '')).toBe(expected);
    });

    it.each([
      [['package.json'], 'chore'],
      [['.github/workflows/ci.yml'], 'chore'],
      [['.gitignore'], 'chore'],
      [['tsconfig.json'], 'chore'],
    ])('determineCommitType for config files %p returns "%s"', (files, expected) => {
      expect(determineCommitType(files, '')).toBe(expected);
    });
  });

  // ============================================================================
  // Commit Type from Diff Content Tests
  // ============================================================================

  describe('Commit Type from Diff Content', () => {
    it('returns "feat" for new files', () => {
      const diff = `
diff --git a/src/NewFeature.cs b/src/NewFeature.cs
new file mode 100644
+++ b/src/NewFeature.cs
+public class NewFeature { }`;
      
      const files = ['src/NewFeature.cs'];
      const type = determineCommitType(files, diff);
      
      expect(type).toBe('feat');
    });

    it('returns "chore" for deleted files', () => {
      const diff = `
diff --git a/src/OldFile.cs b/src/OldFile.cs
deleted file mode 100644
--- a/src/OldFile.cs
-public class OldFile { }`;
      
      const files = ['src/OldFile.cs'];
      const type = determineCommitType(files, diff);
      
      expect(type).toBe('chore');
    });
  });

  // ============================================================================
  // Mixed File Types Tests
  // ============================================================================

  describe('Mixed File Types', () => {
    it('returns "feat" for mixed test and source files', () => {
      const files = ['src/UserService.cs', 'tests/UserServiceTests.cs'];
      const type = determineCommitType(files, '');
      
      // When both source and test files change, it's typically a feature
      expect(type).toBe('feat');
    });

    it('returns "feat" for mixed docs and source files', () => {
      const files = ['src/UserService.cs', 'README.md'];
      const type = determineCommitType(files, '');
      
      // Source code changes take precedence
      expect(type).toBe('feat');
    });
  });

  // ============================================================================
  // Scope Detection Tests
  // ============================================================================

  describe('Scope Detection', () => {
    it.each([
      [['src/api/UserController.cs'], 'api'],
      [['src/ui/Button.tsx'], 'ui'],
      [['src/auth/AuthService.cs'], 'auth'],
      [['src/services/UserService.cs'], 'services'],
    ])('detectScope extracts "%s" from %p', (files, expectedScope) => {
      expect(detectScope(files)).toBe(expectedScope);
    });

    it('returns null for multiple directories', () => {
      const files = ['src/api/UserController.cs', 'src/services/UserService.cs'];
      const scope = detectScope(files);
      
      // Multiple directories = no single scope
      expect(scope).toBeNull();
    });

    it('returns null for root files', () => {
      const files = ['Program.cs', 'Startup.cs'];
      const scope = detectScope(files);
      
      expect(scope).toBeNull();
    });
  });

  // ============================================================================
  // File Type Detection Tests
  // ============================================================================

  describe('File Type Detection', () => {
    describe('isTestFile', () => {
      it.each([
        ['tests/UserServiceTests.cs', true],
        ['UserService.test.ts', true],
        ['user_service_test.py', true],
        ['UserServiceSpec.cs', true],
        ['__tests__/user.js', true],
        ['src/UserService.cs', false],
        ['models/user.py', false],
      ])('isTestFile("%s") returns %s', (path, expected) => {
        expect(isTestFile(path)).toBe(expected);
      });
    });

    describe('isDocFile', () => {
      it.each([
        ['README.md', true],
        ['CHANGELOG.md', true],
        ['docs/guide.md', true],
        ['src/code.ts', false],
      ])('isDocFile("%s") returns %s', (path, expected) => {
        expect(isDocFile(path)).toBe(expected);
      });
    });

    describe('isConfigFile', () => {
      it.each([
        ['package.json', true],
        ['tsconfig.json', true],
        ['.gitignore', true],
        ['config.yaml', true],
        ['.github/workflows/ci.yml', true],
        ['src/code.ts', false],
      ])('isConfigFile("%s") returns %s', (path, expected) => {
        expect(isConfigFile(path)).toBe(expected);
      });
    });
  });
});
