using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Tests.Unit.Content;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using ManifestModel = LegalAgent.Corpus.Manifest.Manifest;

namespace LegalAgent.Corpus.Tests.Unit;

public sealed class CorpusGeneratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corpus-gen-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunParameters Parameters() => new()
    {
        DocumentsPerType = 2,
        Pages = new LegalAgent.Corpus.Planning.PageRange(1, 3),
        StrictUniqueness = false,
        MaxSharedShare = 100,
        VersionedShare = 0,
        OutdatedPerType = 0,
        ContradictionPairsPerType = 0,
        CrossTypeContradictionPairs = 0,
        OutputDirectory = "out",
        ContentDirectory = "zrodla",
    };

    private CorpusGeneratorOptions Options(IPdfMarkdownConverter? converter = null)
    {
        string content = Path.Combine(_root, "zrodla");
        if (!Directory.Exists(content))
        {
            CopyDirectory(MiniContent.Path, content);
        }

        return new CorpusGeneratorOptions { BaseDirectory = _root, Converter = converter };
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private Dictionary<string, byte[]> Snapshot() =>
        Directory.EnumerateFiles(Path.Combine(_root, "out"), "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(Path.Combine(_root, "out"), f).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    [Fact]
    public async Task Generate_WritesPdfMarkdownAndManifestPerType()
    {
        GenerationResult result = await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        var files = Snapshot().Keys.Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            [
                "manifest.json",
                "procedury/PRO-01.md", "procedury/PRO-01.pdf", "procedury/PRO-02.md", "procedury/PRO-02.pdf",
                "regulaminy/REG-01.md", "regulaminy/REG-01.pdf", "regulaminy/REG-02.md", "regulaminy/REG-02.pdf",
                "taryfy/TAR-01.md", "taryfy/TAR-01.pdf", "taryfy/TAR-02.md", "taryfy/TAR-02.pdf",
            ],
            files);
        Assert.Equal(6, result.Documents.Count);

        ManifestModel manifest = ManifestWriter.Read(await File.ReadAllTextAsync(Path.Combine(_root, "out", "manifest.json"), TestContext.Current.CancellationToken));
        ManifestDocument reg1 = manifest.Documents.Single(d => d.Id == "REG-01");
        Assert.Equal(("regulaminy", "regulaminy/REG-01.pdf", "regulaminy/REG-01.md", "BP/REG/01"), (reg1.Type, reg1.Pdf, reg1.Markdown, reg1.Designation));
        Assert.InRange(reg1.Pages, 1, 3);
        Assert.Equal("obowiazujacy", reg1.Status);
        Assert.NotNull(reg1.Template);
        Assert.NotNull(reg1.Layout);
        Assert.Equal(20261008UL, manifest.Run.Seed);
        Assert.DoesNotContain('+', manifest.Run.ParserVersion);
        Assert.Contains("\"documentsPerType\": 2", manifest.Run.ParametersJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_Twice_GivesIdenticalBytes_AndVerifyFindsNoDifference()
    {
        await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        Dictionary<string, byte[]> first = Snapshot();
        await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        Dictionary<string, byte[]> second = Snapshot();

        Assert.Equal(first.Keys.Order(StringComparer.Ordinal), second.Keys.Order(StringComparer.Ordinal));
        Assert.All(first, f => Assert.Equal(f.Value, second[f.Key]));

        CorpusDiff diff = await CorpusGenerator.VerifyAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        Assert.Empty(diff.Different);
        Assert.Empty(diff.Missing);
        Assert.Empty(diff.Extra);
    }

    [Fact]
    public async Task Verify_ReportsAChangedMarkdownFile()
    {
        await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        string md = Path.Combine(_root, "out", "taryfy", "TAR-01.md");
        byte[] bytes = await File.ReadAllBytesAsync(md, TestContext.Current.CancellationToken);
        bytes[^2] ^= 0x01;
        await File.WriteAllBytesAsync(md, bytes, TestContext.Current.CancellationToken);

        CorpusDiff diff = await CorpusGenerator.VerifyAsync(Parameters(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(["taryfy/TAR-01.md"], diff.Different);
    }

    [Fact]
    public async Task Markdown_IsTheLibraryOutputWithTheRelativeSourceId()
    {
        await CorpusGenerator.GenerateAsync(Parameters(), Options(), TestContext.Current.CancellationToken);
        await using FileStream pdf = File.OpenRead(Path.Combine(_root, "out", "procedury", "PRO-02.pdf"));
        PdfConversionResult expected = await PdfMarkdownConverter.CreateDefault().ConvertAsync(
            pdf,
            new PdfConversionRequest { SourceId = "procedury/PRO-02.pdf" },
            TestContext.Current.CancellationToken);

        string actual = await File.ReadAllTextAsync(Path.Combine(_root, "out", "procedury", "PRO-02.md"), TestContext.Current.CancellationToken);
        Assert.Equal(expected.Markdown.TrimEnd('\n') + "\n", actual);
    }

    [Fact]
    public async Task IncompleteConversion_FailsTheRunWithoutWritingTheCorpus()
    {
        var ex = await Assert.ThrowsAsync<ConversionFailedException>(() =>
            CorpusGenerator.GenerateAsync(Parameters(), Options(new IncompleteConverter()), TestContext.Current.CancellationToken));

        Assert.Contains("niekompletna", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, "out", "manifest.json")));
    }

    private sealed class IncompleteConverter : IPdfMarkdownConverter
    {
        public async Task<PdfConversionResult> ConvertAsync(Stream pdf, PdfConversionRequest? request = null, CancellationToken cancellationToken = default)
        {
            PdfConversionResult real = await PdfMarkdownConverter.CreateDefault().ConvertAsync(pdf, request, cancellationToken);
            return real with { IsComplete = false };
        }
    }
}
