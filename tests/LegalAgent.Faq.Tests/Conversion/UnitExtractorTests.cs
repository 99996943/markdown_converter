using System.Runtime.CompilerServices;
using LegalAgent.Corpus.Pdf;
using LegalAgent.Faq.Conversion;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Faq.Tests.Conversion;

/// <summary>Where the units of a parsed document start (spec 013).</summary>
public sealed class UnitExtractorTests : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();

    public void Dispose() => services.Dispose();

    [Fact]
    public async Task Locate_OneLocationPerUnitInOrder_WithFirstPageOfSection()
    {
        PdfConversionResult result = await ConvertAsync(TestPdfs.RegulationOnPages("A"));

        IReadOnlyList<FaqUnitLocation> locations = UnitExtractor.Locate(result.Document, result.Markdown);

        Assert.Equal(UnitExtractor.FromDocument(result.Document), locations.Select(l => l.Unit));
        Assert.Equal(
            [
                ("Rozdział 1", 1), ("Rozdział 1. Postanowienia ogólne", 1), ("§ 1", 1), ("§ 1.", 1),
                ("§ 2", 2), ("§ 2.", 2), ("§ 3", 3), ("§ 3.", 3),
            ],
            locations.Select(l => (l.Unit, l.Page ?? 0)));
    }

    [Fact]
    public async Task Locate_AnchorOfSectionHeading()
    {
        PdfConversionResult result = await ConvertAsync(TestPdfs.RegulationOnPages("A"));

        IReadOnlyList<FaqUnitLocation> locations = UnitExtractor.Locate(result.Document, result.Markdown);

        Assert.Equal(
            [
                ("Rozdział 1", "rozdział-1-postanowienia-ogólne"), ("Rozdział 1. Postanowienia ogólne", "rozdział-1-postanowienia-ogólne"),
                ("§ 1", "-1"), ("§ 1.", "-1"), ("§ 2", "-2"), ("§ 2.", "-2"), ("§ 3", "-3"), ("§ 3.", "-3"),
            ],
            locations.Select(l => (l.Unit, l.Anchor)));
        AssertAnchorsInMarkdown(locations, result.Markdown);
    }

    [Fact]
    public async Task Locate_RepeatedHeading_UnitPointsToFirstSection()
    {
        byte[] pdf = new SyntheticPdfBuilder()
            .Title("Regulamin")
            .Page()
            .Text(200, 80, "Regulamin", 14, bold: true)
            .Text(72, 130, "Rozdział 1. Konto", 12, bold: true)
            .Text(72, 160, "§ 1.", 11, bold: true)
            .Text(72, 180, "Bank prowadzi konto osobiste dla klienta.")
            .Text(72, 220, "Rozdział 2. Karta", 12, bold: true)
            .Text(72, 250, "§ 1.", 11, bold: true)
            .Text(72, 270, "Bank wydaje kartę debetową do konta.")
            .Build();
        PdfConversionResult result = await ConvertAsync(pdf);

        IReadOnlyList<FaqUnitLocation> locations = UnitExtractor.Locate(result.Document, result.Markdown);

        Assert.Equal("-1", locations.Single(l => l.Unit == "§ 1.").Anchor);
        Assert.Contains("-1-1", MarkdownAnchors.ForDocument(result.Markdown));
        AssertAnchorsInMarkdown(locations, result.Markdown);
    }

    [Fact]
    public async Task Locate_HeadingCountDiffers_NoAnchors_PagesKept()
    {
        PdfConversionResult result = await ConvertAsync(TestPdfs.RegulationOnPages("A"));

        IReadOnlyList<FaqUnitLocation> locations = UnitExtractor.Locate(result.Document, result.Markdown + "\n## Dopisany nagłówek\n");

        Assert.All(locations, l => Assert.Null(l.Anchor));
        Assert.All(locations, l => Assert.NotNull(l.Page));
    }

    [Theory]
    [InlineData("tests/LegalAgent.PdfParser.Tests/Corpus/acts/dz-u-2020-287-prawa-konsumenta.pdf")]
    [InlineData("tests/LegalAgent.PdfParser.Tests/Corpus/acts/dz-u-2024-1497-kredyt-konsumencki.pdf")]
    [InlineData("tests/LegalAgent.PdfParser.Tests/Corpus/acts/dz-u-2024-1646-prawo-bankowe.pdf")]
    [InlineData("corpus/regulaminy/REG-01.pdf")]
    [InlineData("corpus/regulaminy/REG-05.pdf")]
    public async Task Locate_RealLayouts_EverySectionHasHeading(string path)
    {
        PdfConversionResult result = await ConvertAsync(await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), path), TestContext.Current.CancellationToken));

        IReadOnlyList<FaqUnitLocation> locations = UnitExtractor.Locate(result.Document, result.Markdown);

        Assert.NotEmpty(locations);
        Assert.All(locations, l => Assert.NotNull(l.Anchor));
        AssertAnchorsInMarkdown(locations, result.Markdown);
    }

    /// <summary>SC-141: every anchor is the anchor of a heading of the Markdown.</summary>
    private static void AssertAnchorsInMarkdown(IReadOnlyList<FaqUnitLocation> locations, string markdown)
    {
        IReadOnlyList<string> anchors = MarkdownAnchors.ForDocument(markdown);
        Assert.All(locations.Where(l => l.Anchor is not null), l => Assert.Contains(l.Anchor!, anchors));
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    private async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await services.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(stream, null, TestContext.Current.CancellationToken);
    }
}
