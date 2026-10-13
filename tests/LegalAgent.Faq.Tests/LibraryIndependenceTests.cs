using System.Runtime.CompilerServices;

namespace LegalAgent.Faq.Tests;

/// <summary>FR-1006 (spec 013): the library knows no bank, no download directory and no host.</summary>
public sealed class LibraryIndependenceTests
{
    [Fact]
    public void LibrarySource_NamesNoBankDirectoryOrHost()
    {
        string library = Path.Combine(RepositoryRoot(), "src", "LegalAgent.Faq");
        string separator = Path.DirectorySeparatorChar.ToString();
        string[] files =
        [
            .. Directory.EnumerateFiles(library, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                    && !f.Contains(separator + "obj" + separator, StringComparison.Ordinal)),
        ];

        Assert.NotEmpty(files);
        Assert.All(files, file =>
        {
            foreach (string line in File.ReadLines(file).Where(l => !l.TrimStart().StartsWith("///", StringComparison.Ordinal)))
            {
                Assert.DoesNotContain("mbank", line, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("downloads", line, StringComparison.Ordinal);
                Assert.DoesNotContain("http://", line, StringComparison.Ordinal);
                Assert.DoesNotContain("https://", line, StringComparison.Ordinal);
            }
        });
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
