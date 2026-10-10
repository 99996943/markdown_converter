using System.Runtime.CompilerServices;
using System.Text;

namespace LegalAgent.Faq.Tests.Fixtures;

/// <summary>
/// Golden-file assertions (as in the parser tests): byte-for-byte comparison (LF, UTF-8 without BOM); on mismatch the
/// actual output is written beside the expected file as <c>*.actual.md</c>; <c>UPDATE_GOLDEN=1</c> rewrites the
/// expected file instead of failing.
/// </summary>
internal static class GoldenFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Path of <c>Golden/&lt;name&gt;</c> in the source directory of the test project.</summary>
    public static string PathOf(string name, [CallerFilePath] string caller = "")
    {
        string? directory = Path.GetDirectoryName(caller);
        while (directory is not null && !File.Exists(Path.Combine(directory, "LegalAgent.Faq.Tests.csproj")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return Path.Combine(directory ?? throw new InvalidOperationException("Test project directory not found."), "Golden", name);
    }

    /// <summary>Asserts that <paramref name="actual"/> equals the content of <paramref name="expectedPath"/>.</summary>
    public static void AssertMatches(string actual, string expectedPath)
    {
        string fullPath = Path.GetFullPath(expectedPath);
        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_GOLDEN"), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, actual, Utf8NoBom);
            return;
        }

        string actualPath = Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            Path.GetFileName(fullPath).Replace(".expected.md", ".actual.md", StringComparison.Ordinal));
        string? expected = File.Exists(fullPath) ? File.ReadAllText(fullPath, Utf8NoBom) : null;
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        File.WriteAllText(actualPath, actual, Utf8NoBom);
        Assert.Fail(expected is null
            ? $"Golden file not found: {fullPath}. Actual output saved to {actualPath}."
            : $"Output differs from {fullPath} ({FirstDifference(expected, actual)}). Actual output saved to {actualPath}.");
    }

    private static string FirstDifference(string expected, string actual)
    {
        string[] e = expected.Split('\n');
        string[] a = actual.Split('\n');
        for (int i = 0; i < Math.Min(e.Length, a.Length); i++)
        {
            if (!string.Equals(e[i], a[i], StringComparison.Ordinal))
            {
                return $"first difference at line {i + 1}: expected \"{e[i]}\" but was \"{a[i]}\"";
            }
        }

        return $"line counts differ: expected {e.Length}, actual {a.Length}";
    }
}
