using System.Text;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Golden-file assertions: compares produced Markdown byte-for-byte (LF, UTF-8 without BOM)
/// with an expected file. On mismatch the actual output is written next to the expected file
/// as <c>*.actual.md</c>. Setting environment variable <c>UPDATE_GOLDEN=1</c> rewrites the
/// expected file instead of failing.
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
            WriteActual(actualPath, actual);
            Assert.Fail($"Golden file not found: {fullPath}. Actual output saved to {actualPath}.");
        }

        string expected = File.ReadAllText(fullPath, Utf8NoBom);
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        WriteActual(actualPath, actual);
        Assert.Fail($"Output differs from {fullPath} ({DescribeFirstDifference(expected, actual)}). Actual output saved to {actualPath}.");
    }

    /// <summary>Returns the path of the <c>*.actual.md</c> file written beside the expected file.</summary>
    internal static string ActualPathFor(string expectedPath)
    {
        string name = Path.GetFileName(expectedPath);
        const string expectedSuffix = ".expected.md";
        string baseName = name.EndsWith(expectedSuffix, StringComparison.Ordinal)
            ? name[..^expectedSuffix.Length]
            : Path.GetFileNameWithoutExtension(name);
        return Path.Combine(Path.GetDirectoryName(expectedPath)!, baseName + ".actual.md");
    }

    private static void WriteActual(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Utf8NoBom);
    }

    private static string DescribeFirstDifference(string expected, string actual)
    {
        string[] e = expected.Split('\n');
        string[] a = actual.Split('\n');
        int count = Math.Min(e.Length, a.Length);
        for (int i = 0; i < count; i++)
        {
            if (!string.Equals(e[i], a[i], StringComparison.Ordinal))
            {
                return $"first difference at line {i + 1}: expected \"{e[i]}\" but was \"{a[i]}\"";
            }
        }

        return $"line counts differ: expected {e.Length}, actual {a.Length}";
    }
}
