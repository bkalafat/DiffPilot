/**
 * Tool Result Types Tests - Phase 1 (Feature Tests)
 * 
 * Tests for the ToolResult helper functions.
 */

import { describe, it, expect } from 'vitest';
import { success, error, gitError, truncateContent, type ToolResult } from '../src/tools/types.js';

describe('Tool Result Types', () => {
  describe('success', () => {
    it('should create success result with text content', () => {
      const result = success('Hello, world!');

      expect(result.isError).toBeFalsy();
      expect(result.content).toHaveLength(1);
      expect(result.content[0].type).toBe('text');
      expect(result.content[0].text).toBe('Hello, world!');
    });

    it('should handle empty text', () => {
      const result = success('');

      expect(result.isError).toBeFalsy();
      expect(result.content[0].text).toBe('');
    });

    it('should handle multiline text', () => {
      const result = success('Line 1\nLine 2\nLine 3');

      expect(result.content[0].text).toContain('\n');
    });
  });

  describe('error', () => {
    it('should create error result', () => {
      const result = error('Something went wrong');

      expect(result.isError).toBe(true);
      expect(result.content[0].type).toBe('text');
      expect(result.content[0].text).toContain('Something went wrong');
    });

    it('should handle empty error message', () => {
      const result = error('');

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('Error');
    });
  });

  describe('gitError', () => {
    it('should create git error with message and details', () => {
      const result = gitError('Failed to fetch', 'fatal: remote not found');

      expect(result.isError).toBe(true);
      expect(result.content[0].text).toContain('Failed to fetch');
      expect(result.content[0].text).toContain('Git Error');
    });

    it('should format error message correctly', () => {
      const result = gitError('Command failed', 'exit code 1');

      expect(result.content[0].text).toContain('Command failed');
    });
  });

  describe('truncateContent', () => {
    it('should not truncate content under limit', () => {
      const content = 'Short content';
      const result = truncateContent(content, 100);

      expect(result).toBe(content);
    });

    it('should truncate content over limit', () => {
      const content = 'A'.repeat(200);
      const result = truncateContent(content, 100);

      expect(result.length).toBeLessThan(200);
      expect(result).toContain('Truncated');
    });

    it('should handle exact limit length', () => {
      const content = 'A'.repeat(100);
      const result = truncateContent(content, 100);

      expect(result).toBe(content);
    });

    it('should handle empty content', () => {
      const result = truncateContent('', 100);

      expect(result).toBe('');
    });

    it('should use default limit when not specified', () => {
      const content = 'A'.repeat(100);
      const result = truncateContent(content);

      expect(result).toBe(content); // Default is much larger
    });
  });
});
