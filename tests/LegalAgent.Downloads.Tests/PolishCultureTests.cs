using System.Globalization;
using System.Runtime.CompilerServices;
using LegalAgent.Globalization;

namespace LegalAgent.Downloads.Tests;

/// <summary>Messages format numbers in pl-PL also without ICU (globalization-invariant mode, e.g. Linux without libicu).</summary>
public sealed class PolishCultureTests
{
    private static readonly string[] Formats = ["N0", "N1", "0.##", "0.0", "0.###"];
    private static readonly double[] Values = [0, 0.5, 1.25, 12.345, 1023.95, 1234.5, 1048576, 123456789.125, -2.5];

    [Fact]
    public void Create_WithoutPolishCulture_FormatsNumbersInPolish()
    {
        CultureInfo culture = PolishCulture.Create(name => throw new CultureNotFoundException(nameof(name), name, "invariant mode"));

        Assert.Equal("1 234,5", 1234.5.ToString("N1", culture));
        Assert.Equal("0,5", 0.5.ToString("0.##", culture));
        Assert.Equal("1,0", 1.0.ToString("0.0", culture));
    }

    [Fact]
    public void Create_WithoutPolishCulture_MatchesSystemPolishCulture()
    {
        CultureInfo system = CultureInfo.GetCultureInfo("pl-PL");
        CultureInfo fallback = PolishCulture.Create(name => throw new CultureNotFoundException(nameof(name), name, "invariant mode"));

        foreach (string format in Formats)
        {
            foreach (double value in Values)
            {
                Assert.Equal(value.ToString(format, system), value.ToString(format, fallback));
            }
        }
    }

    [Fact]
    public void Sources_GetPolishCultureOnlyThroughPolishCulture()
    {
        string src = Path.Combine(RepositoryRoot(), "src");
        string separator = Path.DirectorySeparatorChar.ToString();
        string[] offenders =
        [
            .. Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                    && !f.Contains(separator + "obj" + separator, StringComparison.Ordinal)
                    && !f.EndsWith(separator + "PolishCulture.cs", StringComparison.Ordinal)
                    && File.ReadAllText(f).Contains("GetCultureInfo(\"pl-PL\")", StringComparison.Ordinal))
                .Select(f => Path.GetRelativePath(src, f)),
        ];

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
