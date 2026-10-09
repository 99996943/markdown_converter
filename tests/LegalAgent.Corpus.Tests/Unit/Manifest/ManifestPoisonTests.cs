using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit.Manifest;

/// <summary>US4 (FR-142, contracts/manifest.md): poisoned documents in the manifest and on disk.</summary>
public sealed class ManifestPoisonTests : IDisposable
{
    private static readonly string[] Elements = ["akapit", "przypis", "komorka-tabeli", "metryczka", "okladka", "ramka"];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-zat-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunParameters Parameters() => new()
    {
        Seed = 5,
        DocumentsPerType = 3,
        Types = ["regulaminy", "taryfy"],
        Pages = new PageRange(1, 9),
        StrictUniqueness = false,
        MaxSharedShare = 100,
        VersionedShare = 0,
        OutdatedPerType = 0,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 0,
        Poison = [new PoisonQuota("polecenia-dla-ai", 2)],
        OutputDirectory = "out",
        ContentDirectory = "zrodla",
    };

    private CorpusGeneratorOptions Options()
    {
        string content = Path.Combine(_root, "zrodla");
        foreach (string file in Directory.EnumerateFiles(MiniContent.Path, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(content, Path.GetRelativePath(MiniContent.Path, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return new CorpusGeneratorOptions { BaseDirectory = _root };
    }

    [Fact]
    public async Task PoisonedDocuments_AreUnderTheirKind_WithPoisonInfo()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        var poisoned = result.Manifest.Documents.Where(d => d.Poison is not null).ToList();
        Assert.Equal(["ZAT-REG-POL-01", "ZAT-REG-POL-02", "ZAT-TAR-POL-01", "ZAT-TAR-POL-02"], poisoned.Select(d => d.Id));
        foreach (ManifestDocument doc in poisoned)
        {
            Assert.Equal($"zatrute/{doc.Type}/polecenia-dla-ai/{doc.Id}.pdf", doc.Pdf);
            Assert.Equal($"zatrute/{doc.Type}/polecenia-dla-ai/{doc.Id}.md", doc.Markdown);
            Assert.True(File.Exists(Path.Combine(_root, "out", doc.Pdf)));
            Assert.True(File.Exists(Path.Combine(_root, "out", doc.Markdown)));

            PoisonInfo info = doc.Poison!;
            Assert.Equal("polecenia-dla-ai", info.Kind);
            ManifestDocument imitated = Assert.Single(result.Manifest.Documents, d => d.Id == info.Imitates);
            Assert.Null(imitated.Poison);
            Assert.Equal(imitated.Type, doc.Type);
            Assert.Equal(imitated.Designation, doc.Designation);
            Assert.Equal(imitated.Status, doc.Status);
            Assert.False(string.IsNullOrWhiteSpace(info.Description));

            PoisonPlace place = Assert.Single(info.Places);
            Assert.Contains(place.Element, Elements);
            Assert.InRange(place.Page, 1, doc.Pages);
            Assert.False(string.IsNullOrWhiteSpace(place.Unit));
            Assert.Equal("zmiana-odpowiedzi", place.Goal);
            string markdown = await File.ReadAllTextAsync(Path.Combine(_root, "out", doc.Markdown), TestContext.Current.CancellationToken);
            Assert.Contains(Unescaped(place.Text), Unescaped(markdown), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PoisonedDocuments_ComeLastInTheManifest()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        int first = result.Manifest.Documents.ToList().FindIndex(d => d.Poison is not null);
        Assert.True(first > 0);
        Assert.All(result.Manifest.Documents.Skip(first), d => Assert.NotNull(d.Poison));
    }

    /// <summary>Markdown text with backslash escapes and line breaks removed, whitespace collapsed.</summary>
    private static string Unescaped(string text) =>
        string.Join(' ', System.Text.RegularExpressions.Regex.Replace(text, @"\\(.)", "$1").Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
}
