using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit;

/// <summary>US3 (FR-120, contracts/corpus-layout.md): files, covers, record cards and manifest entries of versions.</summary>
public sealed class CorpusGeneratorVersionsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-ver-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunParameters Parameters() => new()
    {
        Seed = 7,
        DocumentsPerType = 4,
        Pages = new PageRange(1, 3),
        StrictUniqueness = false,
        MaxSharedShare = 100,
        VersionedShare = 50,
        MaxVersions = 3,
        OutdatedPerType = 1,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 1,
        OutputDirectory = "out",
        ContentDirectory = "zrodla",
    };

    private CorpusGeneratorOptions Options()
    {
        string content = Path.Combine(_root, "zrodla");
        if (!Directory.Exists(content))
        {
            foreach (string file in Directory.EnumerateFiles(MiniContent.Path, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(content, Path.GetRelativePath(MiniContent.Path, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }

        return new CorpusGeneratorOptions { BaseDirectory = _root };
    }

    [Fact]
    public async Task Versions_AreFilesOfTheirType_WithTheLatestUnderTheBaseId()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        string output = Path.Combine(_root, "out");

        var latest = result.Manifest.Documents.Where(d => d.PreviousVersion is not null && !d.Id.Contains("-w", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, latest.Count);
        foreach (ManifestDocument doc in latest)
        {
            Assert.Equal($"{doc.Type}/{doc.Id}.pdf", doc.Pdf);
            for (int v = 1; v < doc.Version; v++)
            {
                string id = $"{doc.Id}-w{v}";
                ManifestDocument earlier = Assert.Single(result.Manifest.Documents, d => d.Id == id);
                Assert.Equal($"{doc.Type}/{id}.pdf", earlier.Pdf);
                Assert.Equal($"{doc.Type}/{id}.md", earlier.Markdown);
                Assert.True(File.Exists(Path.Combine(output, doc.Type, id + ".pdf")));
                Assert.True(File.Exists(Path.Combine(output, doc.Type, id + ".md")));
                Assert.Equal(v, earlier.Version);
                Assert.Equal(v == 1 ? null : $"{doc.Id}-w{v - 1}", earlier.PreviousVersion);
                Assert.Equal("nieaktualny", earlier.Status);
                Assert.Equal(doc.Designation, earlier.Designation);
                Assert.Equal(doc.Title, earlier.Title);
                Assert.Equal(v == 1, earlier.Changes is null);
            }

            Assert.Equal($"{doc.Id}-w{doc.Version - 1}", doc.PreviousVersion);
            Assert.NotNull(doc.Changes);
        }
    }

    [Fact]
    public async Task EarlierVersions_PrintTheirVersionAndPeriodOnTheCover()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        foreach (GeneratedDocument doc in result.Documents.Where(d => d.Plan.Status == DocumentStatus.Nieaktualny))
        {
            string text = string.Join(" ", doc.Fit.Typeset.Truth.Words);
            Assert.Contains("Wersja " + doc.Plan.Version, text, StringComparison.Ordinal);
            Assert.Contains("od " + PolishFormat.Date(doc.Plan.ValidFrom) + " do " + PolishFormat.Date(doc.Plan.ValidTo!.Value), text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RecordCardHistory_ListsEveryVersionUpToThisOne()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        var withCards = result.Documents.Where(d => d.Fit.Composition.Document.Front.RecordCard && d.Plan.Version > 1).ToList();
        Assert.NotEmpty(withCards);
        foreach (GeneratedDocument doc in withCards)
        {
            Assert.Equal(
                Enumerable.Range(1, doc.Plan.Version).Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                doc.Fit.Composition.Document.Front.History.Select(h => h.Version));
        }
    }
}
