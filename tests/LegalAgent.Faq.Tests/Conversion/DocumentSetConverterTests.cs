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

    private sealed class SyncProgress(List<ConversionEvent> events) : IProgress<ConversionEvent>
    {
        public void Report(ConversionEvent value) => events.Add(value);
    }
}
