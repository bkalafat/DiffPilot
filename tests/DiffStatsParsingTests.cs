using Xunit;

namespace DiffPilot.Tests;

/// <summary>
/// Tests for git diff --numstat output parsing.
/// The numstat format is: "additions\tdeletions\tfilename"
/// </summary>
public class DiffStatsParsingTests
{
    #region Valid Numstat Parsing Tests

    [Fact]
    public void ParseNumstat_ParsesValidLine()
    {
        var line = "10\t5\tsrc/Program.cs";
        var (additions, deletions, file) = ParseNumstatLine(line);

        Assert.Equal(10, additions);
        Assert.Equal(5, deletions);
        Assert.Equal("src/Program.cs", file);
    }

    [Fact]
    public void ParseNumstat_HandlesZeroChanges()
    {
        var line = "0\t0\tREADME.md";
        var (additions, deletions, file) = ParseNumstatLine(line);

        Assert.Equal(0, additions);
        Assert.Equal(0, deletions);
        Assert.Equal("README.md", file);
    }

    [Fact]
    public void ParseNumstat_HandlesLargeNumbers()
    {
        var line = "1500\t2000\tsrc/LargeFile.cs";
        var (additions, deletions, file) = ParseNumstatLine(line);

        Assert.Equal(1500, additions);
        Assert.Equal(2000, deletions);
    }

    #endregion

    #region Binary File Handling Tests

    [Fact]
    public void ParseNumstat_HandlesBinaryFiles()
    {
        // Binary files show as "-\t-\tfilename"
        var line = "-\t-\timage.png";
        var (additions, deletions, file) = ParseNumstatLine(line);

        Assert.Equal(-1, additions); // -1 indicates binary
        Assert.Equal(-1, deletions);
        Assert.Equal("image.png", file);
    }

    [Theory]
    [InlineData("-\t-\tlogo.png")]
    [InlineData("-\t-\tdata.bin")]
    [InlineData("-\t-\tarchive.zip")]
    public void ParseNumstat_IdentifiesBinaryByDashes(string line)
    {
        var (additions, deletions, _) = ParseNumstatLine(line);
        Assert.True(
            additions == -1 && deletions == -1,
            "Binary files should have -1 for both counts"
        );
    }

    #endregion

    #region File Path Handling Tests

    [Theory]
    [InlineData("5\t3\tpath/to/file.cs", "path/to/file.cs")]
    [InlineData("1\t1\tfile with spaces.txt", "file with spaces.txt")]
    [InlineData("10\t0\t.gitignore", ".gitignore")]
    [InlineData("2\t2\tsrc/sub/deep/file.cs", "src/sub/deep/file.cs")]
    public void ParseNumstat_ExtractsFilePath(string line, string expectedPath)
    {
        var (_, _, file) = ParseNumstatLine(line);
        Assert.Equal(expectedPath, file);
    }

    [Fact]
    public void ParseNumstat_HandlesRenamedFiles()
    {
        // Renamed files show as "additions\tdeletions\told => new"
        var line = "5\t3\told/path.cs => new/path.cs";
        var (additions, deletions, file) = ParseNumstatLine(line);

        Assert.Equal(5, additions);
        Assert.Equal(3, deletions);
        Assert.Contains("=>", file);
    }

    #endregion

    #region Statistics Aggregation Tests

    [Fact]
    public void AggregateStats_CalculatesTotals()
    {
        var lines = new[] { "10\t5\tfile1.cs", "20\t10\tfile2.cs", "5\t15\tfile3.cs" };

        var stats = AggregateStats(lines);

        Assert.Equal(35, stats.TotalAdditions);
        Assert.Equal(30, stats.TotalDeletions);
        Assert.Equal(3, stats.FilesChanged);
        Assert.Equal(5, stats.NetChange); // 35 - 30
    }

    [Fact]
    public void AggregateStats_ExcludesBinaryFromLineCounts()
    {
        var lines = new[] { "10\t5\tcode.cs", "-\t-\timage.png" };

        var stats = AggregateStats(lines);

        Assert.Equal(10, stats.TotalAdditions); // Only from code.cs
        Assert.Equal(5, stats.TotalDeletions);
        Assert.Equal(2, stats.FilesChanged); // Both files count
        Assert.Equal(1, stats.BinaryFilesChanged);
    }

    [Fact]
    public void AggregateStats_HandlesEmptyInput()
    {
        var lines = Array.Empty<string>();
        var stats = AggregateStats(lines);

        Assert.Equal(0, stats.TotalAdditions);
        Assert.Equal(0, stats.TotalDeletions);
        Assert.Equal(0, stats.FilesChanged);
    }

    #endregion

    #region File Type Breakdown Tests

    [Fact]
    public void GroupByExtension_GroupsCorrectly()
    {
        var lines = new[]
        {
            "10\t5\tsrc/file1.cs",
            "20\t10\tsrc/file2.cs",
            "5\t2\ttests/test.cs",
            "100\t50\tdocs/readme.md",
        };

        var byExt = GroupByExtension(lines);

        Assert.Equal(2, byExt.Count);
        Assert.True(byExt.ContainsKey(".cs"));
        Assert.True(byExt.ContainsKey(".md"));
        Assert.Equal(3, byExt[".cs"].FileCount);
        Assert.Equal(35, byExt[".cs"].Additions); // 10 + 20 + 5
        Assert.Equal(1, byExt[".md"].FileCount);
    }

    #endregion

    #region Helper Methods (simulating DeveloperTools logic)

    private static (int additions, int deletions, string file) ParseNumstatLine(string line)
    {
        var parts = line.Split('\t', 3);
        if (parts.Length != 3)
            return (0, 0, string.Empty);

        var additions = parts[0] == "-" ? -1 : int.Parse(parts[0]);
        var deletions = parts[1] == "-" ? -1 : int.Parse(parts[1]);

        return (additions, deletions, parts[2]);
    }

    private static DiffStats AggregateStats(string[] lines)
    {
        var stats = new DiffStats();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var (additions, deletions, _) = ParseNumstatLine(line);
            stats.FilesChanged++;

            if (additions == -1)
            {
                stats.BinaryFilesChanged++;
            }
            else
            {
                stats.TotalAdditions += additions;
                stats.TotalDeletions += deletions;
            }
        }

        stats.NetChange = stats.TotalAdditions - stats.TotalDeletions;
        return stats;
    }

    private static Dictionary<string, ExtensionStats> GroupByExtension(string[] lines)
    {
        var result = new Dictionary<string, ExtensionStats>();

        foreach (var line in lines)
        {
            var (additions, deletions, file) = ParseNumstatLine(line);
            if (additions == -1)
                continue; // Skip binary

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                ext = "(no extension)";

            if (!result.ContainsKey(ext))
                result[ext] = new ExtensionStats();

            result[ext].FileCount++;
            result[ext].Additions += additions;
            result[ext].Deletions += deletions;
        }

        return result;
    }

    private class DiffStats
    {
        public int TotalAdditions { get; set; }
        public int TotalDeletions { get; set; }
        public int FilesChanged { get; set; }
        public int BinaryFilesChanged { get; set; }
        public int NetChange { get; set; }
    }

    private class ExtensionStats
    {
        public int FileCount { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
    }

    #endregion
}
