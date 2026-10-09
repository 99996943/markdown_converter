using System.Text;

namespace LegalAgent.Chunking.Tests.Fixtures;

/// <summary>
/// Golden-file assertions for chunk files: compares the produced JSONL byte-for-byte (LF, UTF-8 without BOM) with
/// an expected <c>*.chunks.jsonl</c> file. On mismatch the actual output is written next to it as
/// <c>*.actual.jsonl</c>. Setting environment variable <c>UPDATE_GOLDEN=1</c> rewrites the expected file instead.
/// </summary>
public static class GoldenFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Asserts that <paramref name="actual"/> equals the content of <paramref name="expectedPath"/>.</summary>
    public static void AssertMatches(string actual, string expectedPath)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrEmpty(expectedPath);

        string fullPath = Path.GetFullPath(expectedPath);
        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_GOLDEN"), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, actual, Utf8NoBom);
            return;
        }

        string actualPath = ActualPathFor(fullPath);
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(actualPath, actual, Utf8NoBom);
            Assert.Fail($"Golden file not found: {fullPath}. Actual output saved to {actualPath}.");
        }

        string expected = File.ReadAllText(fullPath, Utf8NoBom);
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            if (File.Exists(actualPath))
            {
                File.Delete(actualPath);
            }

            return;
        }

        File.WriteAllText(actualPath, actual, Utf8NoBom);
        string[] expectedLines = expected.Split('\n');
        string[] actualLines = actual.Split('\n');
        int line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && string.Equals(expectedLines[line], actualLines[line], StringComparison.Ordinal))
        {
            line++;
        }

        Assert.Fail($"Golden mismatch in {fullPath} at line {line + 1}. Actual output saved to {actualPath}.");
    }

    private static string ActualPathFor(string expectedPath) =>
        expectedPath.EndsWith(".chunks.jsonl", StringComparison.Ordinal)
            ? expectedPath[..^".chunks.jsonl".Length] + ".actual.jsonl"
            : expectedPath + ".actual";
}
