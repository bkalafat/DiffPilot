/**
 * Diff Stats Parsing Tests
 * Ported from: tests/DiffStatsParsingTests.cs
 * 
 * Tests for git diff --numstat output parsing.
 * The numstat format is: "additions\tdeletions\tfilename"
 */

import { describe, it, expect } from 'vitest';
import * as path from 'path';

// ============================================================================
// Helper Types
// ============================================================================

interface DiffStats {
  totalAdditions: number;
  totalDeletions: number;
  filesChanged: number;
  binaryFilesChanged: number;
  netChange: number;
}

interface ExtensionStats {
  fileCount: number;
  additions: number;
  deletions: number;
}

// ============================================================================
// Helper Functions (simulating DeveloperTools logic)
// ============================================================================

/**
 * Parses a single line of numstat output.
 * @returns [additions, deletions, filename] - additions/deletions are -1 for binary files
 */
function parseNumstatLine(line: string): [number, number, string] {
  const parts = line.split('\t');
  if (parts.length !== 3) {
    return [0, 0, ''];
  }

  const additions = parts[0] === '-' ? -1 : parseInt(parts[0], 10) || 0;
  const deletions = parts[1] === '-' ? -1 : parseInt(parts[1], 10) || 0;

  return [additions, deletions, parts[2]];
}

/**
 * Aggregates statistics from multiple numstat lines.
 */
function aggregateStats(lines: string[]): DiffStats {
  const stats: DiffStats = {
    totalAdditions: 0,
    totalDeletions: 0,
    filesChanged: 0,
    binaryFilesChanged: 0,
    netChange: 0,
  };

  for (const line of lines) {
    if (!line.trim()) continue;

    const [additions, deletions] = parseNumstatLine(line);
    stats.filesChanged++;

    if (additions === -1) {
      stats.binaryFilesChanged++;
    } else {
      stats.totalAdditions += additions;
      stats.totalDeletions += deletions;
    }
  }

  stats.netChange = stats.totalAdditions - stats.totalDeletions;
  return stats;
}

/**
 * Groups file changes by extension.
 */
function groupByExtension(lines: string[]): Map<string, ExtensionStats> {
  const result = new Map<string, ExtensionStats>();

  for (const line of lines) {
    const [additions, deletions, file] = parseNumstatLine(line);
    if (additions === -1) continue; // Skip binary

    const ext = path.extname(file).toLowerCase() || '(no extension)';

    const existing = result.get(ext) ?? { fileCount: 0, additions: 0, deletions: 0 };
    existing.fileCount++;
    existing.additions += additions;
    existing.deletions += deletions;
    result.set(ext, existing);
  }

  return result;
}

// ============================================================================
// Valid Numstat Parsing Tests
// ============================================================================

describe('Diff Stats Parsing', () => {
  describe('Valid Numstat Parsing', () => {
    it('parses valid line', () => {
      const line = '10\t5\tsrc/Program.cs';
      const [additions, deletions, file] = parseNumstatLine(line);

      expect(additions).toBe(10);
      expect(deletions).toBe(5);
      expect(file).toBe('src/Program.cs');
    });

    it('handles zero changes', () => {
      const line = '0\t0\tREADME.md';
      const [additions, deletions, file] = parseNumstatLine(line);

      expect(additions).toBe(0);
      expect(deletions).toBe(0);
      expect(file).toBe('README.md');
    });

    it('handles large numbers', () => {
      const line = '1500\t2000\tsrc/LargeFile.cs';
      const [additions, deletions] = parseNumstatLine(line);

      expect(additions).toBe(1500);
      expect(deletions).toBe(2000);
    });
  });

  // ============================================================================
  // Binary File Handling Tests
  // ============================================================================

  describe('Binary File Handling', () => {
    it('handles binary files with dash notation', () => {
      // Binary files show as "-\t-\tfilename"
      const line = '-\t-\timage.png';
      const [additions, deletions, file] = parseNumstatLine(line);

      expect(additions).toBe(-1); // -1 indicates binary
      expect(deletions).toBe(-1);
      expect(file).toBe('image.png');
    });

    it.each([
      ['-\t-\tlogo.png'],
      ['-\t-\tdata.bin'],
      ['-\t-\tarchive.zip'],
    ])('identifies binary by dashes: "%s"', (line) => {
      const [additions, deletions] = parseNumstatLine(line);
      expect(additions).toBe(-1);
      expect(deletions).toBe(-1);
    });
  });

  // ============================================================================
  // File Path Handling Tests
  // ============================================================================

  describe('File Path Handling', () => {
    it.each([
      ['5\t3\tpath/to/file.cs', 'path/to/file.cs'],
      ['1\t1\tfile with spaces.txt', 'file with spaces.txt'],
      ['10\t0\t.gitignore', '.gitignore'],
      ['2\t2\tsrc/sub/deep/file.cs', 'src/sub/deep/file.cs'],
    ])('extracts file path from "%s"', (line, expectedPath) => {
      const [, , file] = parseNumstatLine(line);
      expect(file).toBe(expectedPath);
    });

    it('handles renamed files', () => {
      // Renamed files show as "additions\tdeletions\told => new"
      const line = '5\t3\told/path.cs => new/path.cs';
      const [additions, deletions, file] = parseNumstatLine(line);

      expect(additions).toBe(5);
      expect(deletions).toBe(3);
      expect(file).toContain('=>');
    });
  });

  // ============================================================================
  // Statistics Aggregation Tests
  // ============================================================================

  describe('Statistics Aggregation', () => {
    it('calculates totals', () => {
      const lines = ['10\t5\tfile1.cs', '20\t10\tfile2.cs', '5\t15\tfile3.cs'];

      const stats = aggregateStats(lines);

      expect(stats.totalAdditions).toBe(35);
      expect(stats.totalDeletions).toBe(30);
      expect(stats.filesChanged).toBe(3);
      expect(stats.netChange).toBe(5); // 35 - 30
    });

    it('excludes binary from line counts', () => {
      const lines = ['10\t5\tcode.cs', '-\t-\timage.png'];

      const stats = aggregateStats(lines);

      expect(stats.totalAdditions).toBe(10); // Only from code.cs
      expect(stats.totalDeletions).toBe(5);
      expect(stats.filesChanged).toBe(2); // Both files count
      expect(stats.binaryFilesChanged).toBe(1);
    });

    it('handles empty input', () => {
      const lines: string[] = [];
      const stats = aggregateStats(lines);

      expect(stats.totalAdditions).toBe(0);
      expect(stats.totalDeletions).toBe(0);
      expect(stats.filesChanged).toBe(0);
    });
  });

  // ============================================================================
  // File Type Breakdown Tests
  // ============================================================================

  describe('File Type Breakdown', () => {
    it('groups by extension correctly', () => {
      const lines = [
        '10\t5\tsrc/file1.cs',
        '20\t10\tsrc/file2.cs',
        '5\t2\ttests/test.cs',
        '100\t50\tdocs/readme.md',
      ];

      const byExt = groupByExtension(lines);

      expect(byExt.size).toBe(2);
      expect(byExt.has('.cs')).toBe(true);
      expect(byExt.has('.md')).toBe(true);
      expect(byExt.get('.cs')?.fileCount).toBe(3);
      expect(byExt.get('.cs')?.additions).toBe(35); // 10 + 20 + 5
      expect(byExt.get('.md')?.fileCount).toBe(1);
    });

    it('handles files without extension', () => {
      const lines = ['10\t5\tMakefile', '5\t2\tDockerfile'];

      const byExt = groupByExtension(lines);

      expect(byExt.has('(no extension)')).toBe(true);
      expect(byExt.get('(no extension)')?.fileCount).toBe(2);
    });

    it('skips binary files in extension grouping', () => {
      const lines = ['10\t5\tcode.ts', '-\t-\timage.png'];

      const byExt = groupByExtension(lines);

      expect(byExt.has('.ts')).toBe(true);
      expect(byExt.has('.png')).toBe(false);
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles whitespace-only lines', () => {
      const lines = ['10\t5\tfile.cs', '   ', ''];
      const stats = aggregateStats(lines);

      expect(stats.filesChanged).toBe(1);
    });

    it('handles malformed lines gracefully', () => {
      const line = 'invalid line without tabs';
      const [additions, deletions, file] = parseNumstatLine(line);

      expect(additions).toBe(0);
      expect(deletions).toBe(0);
      expect(file).toBe('');
    });

    it('handles negative net change', () => {
      const lines = ['5\t20\tfile.cs']; // More deletions than additions

      const stats = aggregateStats(lines);

      expect(stats.netChange).toBe(-15);
    });
  });
});
