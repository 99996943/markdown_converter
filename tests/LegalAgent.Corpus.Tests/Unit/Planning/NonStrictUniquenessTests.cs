using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;

namespace LegalAgent.Corpus.Tests.Unit.Planning;

/// <summary>US5 (FR-103b): runs that need more content than the block variants give.</summary>
public sealed class NonStrictUniquenessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-rep-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    internal static RunParameters Parameters(bool strict, int minPages = 2, int maxPages = 2) => new()
    {
        Seed = 3,
        DocumentsPerType = 20,
        Types = ["regulaminy"],
        Pages = new PageRange(minPages, maxPages),
        StrictUniqueness = strict,
        MaxSharedShare = 100,
        VersionedShare = 0,
        OutdatedPerType = 0,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 0,
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
    public async Task StrictRun_ThatNeedsMoreBlocks_FailsNamingHowManyAreMissing()
    {
        var error = await Assert.ThrowsAsync<CorpusGenerationException>(() =>
            CorpusGenerator.GenerateAsync(Parameters(strict: true, minPages: 3, maxPages: 4), Options(), TestContext.Current.CancellationToken));

        Assert.Matches(@"brakuje (ok\. )?\d+ blok", error.Message);
        Assert.False(Directory.Exists(Path.Combine(_root, "out", "regulaminy")));
    }

    [Fact]
    public async Task NonStrictRun_RepeatsBlocksAcrossDocuments_AndReportsTheirShare()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(strict: false), Options(), TestContext.Current.CancellationToken);

        Assert.All(result.Documents, d =>
        {
            var blocks = d.Fit.Composition.Blocks.Select(b => b.BlockId).ToList();
            Assert.Equal(blocks.Count, blocks.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(2, d.Entry.Pages);
            Assert.NotNull(d.Entry.RepeatedWordShare);
        });
        Assert.Contains(result.Documents, d => d.Entry.RepeatedWordShare > 0);
        Assert.NotNull(result.Manifest.Run.RepeatedWordShare);
        Assert.InRange(result.Manifest.Run.RepeatedWordShare!.Value, 0.001, 1);
    }

    [Fact]
    public void CommittedRun_IsStrict()
    {
        Assert.True(Corpus.CorpusSampleTests.RecordedParameters().StrictUniqueness);
    }
}
