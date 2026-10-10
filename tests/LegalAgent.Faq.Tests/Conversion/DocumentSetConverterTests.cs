using System.Globalization;
using LegalAgent.Faq.Conversion;
using LegalAgent.Faq.Conversion.Model;
using LegalAgent.Faq.Tests.Fakes;
using LegalAgent.Faq.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Faq.Tests.Conversion;

public sealed class DocumentSetConverterTests : IDisposable
{
    private readonly TestDirectory directory = new();
    private readonly ServiceProvider services = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();

    public void Dispose()
    {
        services.Dispose();
        directory.Dispose();
    }

    private IPdfMarkdownConverter Parser => services.GetRequiredService<IPdfMarkdownConverter>();

    [Fact]
    public async Task ConvertAll_WritesMarkdownNextToEachPdf_EqualToParserOutput()
    {
        IReadOnlyList<PdfSource> sources = WriteFive();

        ConversionRun run = await new DocumentSetConverter(Parser).ConvertAllAsync(sources, cancellationToken: Ct);

        Assert.True(run.AllSucceeded);
        Assert.Equal(5, run.Documents.Count);
        for (int i = 1; i <= 5; i++)
        {
            string md = directory.Combine(Invariant($"reg-{i}.md"));
            Assert.True(File.Exists(md));
            await using FileStream pdf = File.OpenRead(directory.Combine(Invariant($"reg-{i}.pdf")));
            PdfConversionResult direct = await Parser.ConvertAsync(pdf, null, Ct);
            Assert.Equal(direct.Markdown, await File.ReadAllTextAsync(md, Ct));
            Assert.Equal(direct.Markdown, run.Documents[i - 1].Markdown);
        }
    }

    [Fact]
    public async Task ConvertAll_DescribesEachDocument()
    {
        IReadOnlyList<PdfSource> sources = WriteFive();

        ConversionRun run = await new DocumentSetConverter(Parser).ConvertAllAsync(sources, cancellationToken: Ct);

        ConvertedDocument third = run.Documents[2];
        Assert.Equal(3, third.Index);
        Assert.Equal(new Uri("https://example.test/pdf/reg-3.pdf"), third.Address);
        Assert.Equal("reg-3.pdf", third.PdfFileName);
        Assert.Equal("reg-3.md", third.MarkdownFileName);
        Assert.Equal("Regulamin 3", third.Title);
        Assert.Equal(1, third.PageCount);
        Assert.Equal("IMG001_ImagesIgnored", Assert.Single(third.Warnings).Code);
        Assert.Empty(run.Documents[0].Warnings);
        Assert.Equal(["Rozdział 1", "Rozdział 1. Postanowienia ogólne", "§ 1", "§ 1.", "§ 2", "§ 2."], third.Units);
    }

    [Fact]
    public async Task ConvertAll_ReportsEventsInIndexOrder()
    {
        var events = new List<ConversionEvent>();

        await new DocumentSetConverter(Parser).ConvertAllAsync(WriteFive(), new SyncProgress(events), Ct);

        Assert.Equal(10, events.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal((i + 1, ConversionEventKind.Started, Invariant($"reg-{i + 1}.pdf")), (events[2 * i].Index, events[2 * i].Kind, events[2 * i].FileName));
            Assert.Equal((i + 1, ConversionEventKind.Converted), (events[(2 * i) + 1].Index, events[(2 * i) + 1].Kind));
            Assert.NotNull(events[(2 * i) + 1].Document);
        }
    }

    [Fact]
    public async Task ConvertAll_OverwritesExistingMarkdown_WithoutTempFiles()
    {
        IReadOnlyList<PdfSource> sources = WriteFive();
        await File.WriteAllTextAsync(directory.Combine("reg-2.md"), "stara treść", Ct);

        await new DocumentSetConverter(Parser).ConvertAllAsync(sources, cancellationToken: Ct);

        Assert.DoesNotContain("stara treść", await File.ReadAllTextAsync(directory.Combine("reg-2.md"), Ct), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task ConvertAll_AllSucceeded_RemovesStaleMarkdownOnly()
    {
        List<PdfSource> sources = WriteFive();
        await File.WriteAllTextAsync(directory.Combine("stary.md"), "x", Ct);
        await File.WriteAllTextAsync(directory.Combine("inny.md"), "x", Ct);
        await File.WriteAllTextAsync(directory.Combine("manifest.json"), "{}", Ct);
        await File.WriteAllTextAsync(directory.Combine("notatki.txt"), "x", Ct);
        Directory.CreateDirectory(directory.Combine("podkatalog"));
        await File.WriteAllTextAsync(Path.Combine(directory.Combine("podkatalog"), "zostaje.md"), "x", Ct);

        ConversionRun run = await new DocumentSetConverter(Parser).ConvertAllAsync(sources, cancellationToken: Ct);

        Assert.Equal(["inny.md", "stary.md"], run.RemovedMarkdownFiles);
        Assert.False(File.Exists(directory.Combine("stary.md")));
        Assert.False(File.Exists(directory.Combine("inny.md")));
        Assert.True(File.Exists(directory.Combine("manifest.json")));
        Assert.True(File.Exists(directory.Combine("notatki.txt")));
        Assert.True(File.Exists(Path.Combine(directory.Combine("podkatalog"), "zostaje.md")));
        Assert.All(Enumerable.Range(1, 5), i => Assert.True(File.Exists(directory.Combine(Invariant($"reg-{i}.md")))));
    }

    [Fact]
    public async Task ConvertAll_CorruptedAndTextless_OthersConverted_FailuresReported()
    {
        List<PdfSource> sources = WriteFive();
        await File.WriteAllBytesAsync(sources[1].PdfPath, TestPdfs.Corrupted(), Ct);
        await File.WriteAllBytesAsync(sources[3].PdfPath, TestPdfs.NoText(), Ct);
        await File.WriteAllTextAsync(directory.Combine("stary.md"), "x", Ct);
        var events = new List<ConversionEvent>();

        ConversionRun run = await new DocumentSetConverter(Parser).ConvertAllAsync(sources, new SyncProgress(events), Ct);

        Assert.False(run.AllSucceeded);
        Assert.Equal([1, 3, 5], run.Documents.Select(d => d.Index));
        Assert.Equal([(2, "reg-2.pdf"), (4, "reg-4.pdf")], run.Failures.Select(f => (f.Index, f.PdfFileName)));
        Assert.All(run.Failures, f => Assert.False(string.IsNullOrWhiteSpace(f.Reason)));
        Assert.Contains("uszkodzona", run.Failures[0].Reason, StringComparison.Ordinal);
        Assert.Contains("warstwy tekstowej", run.Failures[1].Reason, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("reg-2.md")));
        Assert.True(File.Exists(directory.Combine("reg-3.md")));
        Assert.Equal(ConversionEventKind.Failed, events.Single(e => e.Index == 2 && e.Kind != ConversionEventKind.Started).Kind);
        Assert.Empty(run.RemovedMarkdownFiles);
        Assert.True(File.Exists(directory.Combine("stary.md")));
    }

    [Theory]
    [InlineData(false, "# Tytuł\n\nTreść.\n", "część stron nie została przekonwertowana")]
    [InlineData(true, "<!-- page: 1 -->\n\n<!-- page: 2 -->\n", "nie zawiera tekstu")]
    public async Task ConvertAll_IncompleteOrEmptyResult_IsFailure(bool complete, string markdown, string reason)
    {
        List<PdfSource> sources = WriteFive();
        var stub = new StubConverter(Parser, "reg-5.pdf", complete, markdown);

        ConversionRun run = await new DocumentSetConverter(stub).ConvertAllAsync(sources, cancellationToken: Ct);

        ConversionFailure failure = Assert.Single(run.Failures);
        Assert.Equal(5, failure.Index);
        Assert.Contains(reason, failure.Reason, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("reg-5.md")));
        Assert.Equal(4, run.Documents.Count);
    }

    [Fact]
    public void UnitExtractor_CollectsNestedSectionsWithoutDuplicates()
    {
        Section paragraph = Section(SectionKind.Paragraph, "§ 1", "§ 1.");
        Section duplicate = Section(SectionKind.Paragraph, "§ 1", "§ 1.");
        Section typographic = Section(SectionKind.Typographic, null, "Opłaty");
        Section chapter = Section(SectionKind.Chapter, "Rozdział 2", "Rozdział 2. Otwarcie rachunku", [paragraph, typographic]);
        var document = new LegalDocument(new SourceInfo("a.pdf", 1, null, 0, ""), "Tytuł", [], [], [chapter, duplicate]);

        Assert.Equal(["Rozdział 2", "Rozdział 2. Otwarcie rachunku", "§ 1", "§ 1.", "Opłaty"], UnitExtractor.FromDocument(document));
    }

    private List<PdfSource> WriteFive()
    {
        var sources = new List<PdfSource>();
        for (int i = 1; i <= 5; i++)
        {
            string name = Invariant($"reg-{i}.pdf");
            string title = Invariant($"Regulamin {i}");
            byte[] pdf = i == 3 ? TestPdfs.RegulationWithWarning(name, title) : TestPdfs.Regulation(name, title);
            File.WriteAllBytes(directory.Combine(name), pdf);
            sources.Add(new PdfSource(i, new Uri(Invariant($"https://example.test/pdf/{name}")), directory.Combine(name)));
        }

        return sources;
    }

    private static Section Section(SectionKind kind, string? designation, string heading, IReadOnlyList<Section>? children = null) =>
        new(2, kind, designation, null, null, heading, [heading], new PageRange(1, 1), [], [], children ?? []);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>The real parser, except for one file whose result is replaced (incomplete or without text).</summary>
    private sealed class StubConverter(IPdfMarkdownConverter real, string sourceId, bool complete, string markdown) : IPdfMarkdownConverter
    {
        public async Task<PdfConversionResult> ConvertAsync(Stream pdf, PdfConversionRequest? request = null, CancellationToken cancellationToken = default)
        {
            PdfConversionResult result = await real.ConvertAsync(pdf, request, cancellationToken);
            return string.Equals(request?.SourceId, sourceId, StringComparison.Ordinal)
                ? result with { Markdown = markdown, IsComplete = complete }
                : result;
        }
    }

    private sealed class SyncProgress(List<ConversionEvent> events) : IProgress<ConversionEvent>
    {
        public void Report(ConversionEvent value) => events.Add(value);
    }
}
