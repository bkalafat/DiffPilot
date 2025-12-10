/**
 * Test Suggestion Analysis Tests
 * Ported from: tests/TestSuggestionAnalysisTests.cs
 * 
 * Tests for test suggestion logic - detecting patterns that need testing.
 */

import { describe, it, expect } from 'vitest';
import * as path from 'path';

// ============================================================================
// Helper Functions (simulating DeveloperTools logic)
// ============================================================================

/**
 * Checks if a file is a test file.
 */
function isTestFile(filePath: string): boolean {
  const lower = filePath.toLowerCase();
  return lower.includes('test') || 
         lower.includes('spec') || 
         lower.includes('__tests__') ||
         lower.startsWith('tests/');
}

/**
 * Checks if a file is a configuration file.
 */
function isConfigFile(filePath: string): boolean {
  const lower = filePath.toLowerCase();
  const configExtensions = ['.json', '.yaml', '.yml', '.env', '.config'];
  const configNames = ['config', 'settings', 'appsettings', 'package.json', 
                       'tsconfig', '.eslintrc', '.prettierrc', '.env'];

  return (configExtensions.some(e => lower.endsWith(e)) && 
          configNames.some(n => lower.includes(n))) ||
         (lower.startsWith('.') && !lower.includes('/'));
}

/**
 * Checks if a file is a documentation file.
 */
function isDocFile(filePath: string): boolean {
  const lower = filePath.toLowerCase();
  const docPatterns = ['readme', 'changelog', 'license', 'contributing', 
                       'docs/', 'documentation/', '.md'];
  return docPatterns.some(p => lower.includes(p));
}

/**
 * Checks for async code patterns.
 */
function hasAsyncPattern(code: string): boolean {
  const patterns = ['async ', 'await ', 'Task<', 'Task ', 'Promise', '.then('];
  return patterns.some(p => code.toLowerCase().includes(p.toLowerCase()));
}

/**
 * Checks for exception handling patterns.
 */
function hasExceptionPattern(code: string): boolean {
  const patterns = ['throw ', 'catch ', 'try ', 'Exception'];
  return patterns.some(p => code.toLowerCase().includes(p.toLowerCase()));
}

/**
 * Checks for null check patterns.
 */
function hasNullCheckPattern(code: string): boolean {
  const patterns = ['== null', '!= null', 'is null', '??', '?.', 'ThrowIfNull'];
  return patterns.some(p => code.toLowerCase().includes(p.toLowerCase()));
}

/**
 * Checks for loop patterns.
 */
function hasLoopPattern(code: string): boolean {
  const patterns = ['foreach ', 'for (', 'while (', '.Select(', '.Where(', 
                    '.ForEach(', '.Any(', '.All(', '.map(', '.filter(', '.forEach('];
  return patterns.some(p => code.toLowerCase().includes(p.toLowerCase()));
}

/**
 * Generates test suggestions based on code patterns.
 */
function generateTestSuggestions(code: string, fileName: string): string[] {
  const suggestions: string[] = [];

  if (hasAsyncPattern(code)) {
    suggestions.push('Test async/await behavior and Task completion');
  }

  if (hasNullCheckPattern(code)) {
    suggestions.push('Test with null inputs and verify null handling');
  }

  if (hasLoopPattern(code)) {
    suggestions.push('Test with empty collection and boundary cases');
  }

  if (hasExceptionPattern(code)) {
    suggestions.push('Test exception throwing conditions and error handling');
  }

  if (suggestions.length === 0) {
    suggestions.push(`Add unit tests for ${path.basename(fileName, path.extname(fileName))}`);
  }

  return suggestions;
}

// ============================================================================
// File Type Detection Tests
// ============================================================================

describe('Test Suggestion Analysis', () => {
  describe('File Type Detection', () => {
    describe('isTestFile', () => {
      it.each([
        ['UserServiceTests.cs', true],
        ['UserService.test.ts', true],
        ['user_service_test.py', true],
        ['UserServiceSpec.cs', true],
        ['tests/UserService.cs', true],
        ['__tests__/user.js', true],
      ])('detects test file "%s" as %s', (filePath, expected) => {
        expect(isTestFile(filePath)).toBe(expected);
      });

      it.each([
        ['UserService.cs', false],
        ['src/UserService.ts', false],
        ['models/user.py', false],
      ])('ignores non-test file "%s"', (filePath, expected) => {
        expect(isTestFile(filePath)).toBe(expected);
      });
    });

    describe('isConfigFile', () => {
      it.each([
        ['appsettings.json', true],
        ['config.yaml', true],
        ['.env', true],
        ['tsconfig.json', true],
        ['package.json', true],
        ['.eslintrc', true],
      ])('detects config file "%s"', (filePath, expected) => {
        expect(isConfigFile(filePath)).toBe(expected);
      });
    });

    describe('isDocFile', () => {
      it.each([
        ['README.md', true],
        ['CHANGELOG.md', true],
        ['docs/guide.md', true],
        ['LICENSE', true],
        ['CONTRIBUTING.md', true],
      ])('detects doc file "%s"', (filePath, expected) => {
        expect(isDocFile(filePath)).toBe(expected);
      });
    });
  });

  // ============================================================================
  // Code Pattern Detection Tests
  // ============================================================================

  describe('Code Pattern Detection', () => {
    describe('Async Patterns', () => {
      it.each([
        ['public async Task FetchData()', true],
        ['async function getData() {', true],
        ['await httpClient.GetAsync(url)', true],
        ['return Promise.resolve(data)', true],
      ])('hasAsyncPattern detects "%s"', (code, expected) => {
        expect(hasAsyncPattern(code)).toBe(expected);
      });
    });

    describe('Exception Patterns', () => {
      it.each([
        ['throw new ArgumentException()', true],
        ['catch (Exception ex)', true],
        ['try { DoSomething(); }', true],
        ['if (x == null) throw new Exception()', true],
      ])('hasExceptionPattern detects "%s"', (code, expected) => {
        expect(hasExceptionPattern(code)).toBe(expected);
      });
    });

    describe('Null Check Patterns', () => {
      it.each([
        ['if (user == null)', true],
        ['x ?? defaultValue', true],
        ['user?.Name', true],
        ['ArgumentNullException.ThrowIfNull(x)', true],
      ])('hasNullCheckPattern detects "%s"', (code, expected) => {
        expect(hasNullCheckPattern(code)).toBe(expected);
      });
    });

    describe('Loop Patterns', () => {
      it.each([
        ['foreach (var item in items)', true],
        ['for (int i = 0; i < 10; i++)', true],
        ['while (condition)', true],
        ['items.Select(x => x.Name)', true],
        ['items.Where(x => x.Active)', true],
        ['items.map(x => x.name)', true],
        ['items.filter(x => x.active)', true],
        ['items.forEach(x => console.log(x))', true],
      ])('hasLoopPattern detects "%s"', (code, expected) => {
        expect(hasLoopPattern(code)).toBe(expected);
      });
    });
  });

  // ============================================================================
  // Test Suggestion Generation Tests
  // ============================================================================

  describe('Test Suggestion Generation', () => {
    it('suggests async tests for async code', () => {
      const code = `
public async Task<User> GetUserAsync(int id)
{
    return await _repository.FindAsync(id);
}`;

      const suggestions = generateTestSuggestions(code, 'UserService.cs');

      expect(suggestions.some(s => s.toLowerCase().includes('async') || s.toLowerCase().includes('task'))).toBe(true);
    });

    it('suggests null tests for nullable code', () => {
      const code = `
public string GetName(User? user)
{
    return user?.Name ?? "Unknown";
}`;

      const suggestions = generateTestSuggestions(code, 'UserService.cs');

      expect(suggestions.some(s => s.toLowerCase().includes('null'))).toBe(true);
    });

    it('suggests boundary tests for loop code', () => {
      const code = `
public int Sum(IEnumerable<int> numbers)
{
    foreach (var n in numbers) { }
    return numbers.Where(x => x > 0).Count();
}`;

      const suggestions = generateTestSuggestions(code, 'Calculator.cs');

      expect(suggestions.some(s => s.toLowerCase().includes('empty') || s.toLowerCase().includes('collection'))).toBe(true);
    });

    it('suggests error tests for exception code', () => {
      const code = `
public void Validate(string input)
{
    if (string.IsNullOrEmpty(input))
        throw new ArgumentException("Input required");
}`;

      const suggestions = generateTestSuggestions(code, 'Validator.cs');

      expect(suggestions.some(s => 
        s.toLowerCase().includes('exception') || 
        s.toLowerCase().includes('throw') || 
        s.toLowerCase().includes('error')
      )).toBe(true);
    });

    it('provides default suggestion for code without patterns', () => {
      const code = `
public int Add(int a, int b)
{
    return a + b;
}`;

      const suggestions = generateTestSuggestions(code, 'Calculator.cs');

      expect(suggestions.some(s => s.includes('Calculator'))).toBe(true);
    });
  });

  // ============================================================================
  // Combined Pattern Tests
  // ============================================================================

  describe('Combined Patterns', () => {
    it('detects multiple patterns in complex code', () => {
      const code = `
public async Task<User?> GetUserAsync(int id)
{
    try
    {
        var users = await _repository.GetAllAsync();
        return users.FirstOrDefault(u => u.Id == id);
    }
    catch (Exception ex)
    {
        throw new UserNotFoundException(id);
    }
}`;

      const suggestions = generateTestSuggestions(code, 'UserService.cs');

      // Should have suggestions for async, null, and exception handling
      expect(suggestions.length).toBeGreaterThanOrEqual(2);
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles empty code', () => {
      const suggestions = generateTestSuggestions('', 'Empty.cs');
      
      expect(suggestions.length).toBe(1);
      expect(suggestions[0]).toContain('Empty');
    });

    it('handles code with only comments', () => {
      const code = `
// This is a comment
/* Another comment */
`;
      const suggestions = generateTestSuggestions(code, 'Comments.cs');
      
      expect(suggestions.length).toBe(1);
    });

    it('handles TypeScript/JavaScript async patterns', () => {
      const code = `
async function fetchData() {
  const response = await fetch('/api/data');
  return response.json();
}`;

      const suggestions = generateTestSuggestions(code, 'api.ts');

      expect(suggestions.some(s => s.toLowerCase().includes('async'))).toBe(true);
    });

    it('handles Promise patterns', () => {
      const code = `
function getData() {
  return new Promise((resolve, reject) => {
    resolve(data);
  });
}`;

      const suggestions = generateTestSuggestions(code, 'data.js');

      expect(suggestions.some(s => s.toLowerCase().includes('async'))).toBe(true);
    });
  });
});
