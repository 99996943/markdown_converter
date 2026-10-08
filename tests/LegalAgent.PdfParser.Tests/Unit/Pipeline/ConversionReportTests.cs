using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;
using LegalAgent.PdfParser.Tests.Integration;

namespace LegalAgent.PdfParser.Tests.Unit.Pipeline;

/// <summary>T084 — the conversion report (FR-070): statistics, deterministic ordering and diagnostic warnings.</summary>
public sealed class ConversionReportTests
{
    [Fact]
    public void HeadingCounts_HaveAscendingKeys()
    {
        var builder = new ReportBuilder();
        foreach (int level in new[] { 3, 1, 2, 3, 1, 3 })
        {
            builder.AddHeading(level);
        }

        ConversionReport report = builder.Build(1, TimeSpan.Zero);

        Assert.Equal([1, 2, 3], report.HeadingCounts.Keys);
        Assert.Equal([2, 1, 3], report.HeadingCounts.Values);
    }

    [Fact]
    public void Counters_AreReportedAsCollected()
    {
        var builder = new ReportBuilder();
        builder.AddList();
        builder.AddList();
        builder.AddTable(isFallback: false);
        builder.AddTable(isFallback: true);
        builder.AddFootnote();
        builder.AddDroppedText(4);

        ConversionReport report = builder.Build(7, TimeSpan.FromSeconds(1));

        Assert.Equal((7, 2, 2, 1, 1, 4), (report.PageCount, report.ListCount, report.TableCount, report.FallbackTableCount, report.FootnoteCount, report.DroppedTextCount));
    }

    [Fact]
    public void TableDocuments_AreEmptyByDefault()
    {
        ConversionReport report = new ReportBuilder().Build(1, TimeSpan.Zero);

        Assert.Empty(report.TableDocuments);
    }

    [Fact]
    public void TableDocuments_AreReportedInPageOrder_WithoutCountingAsTables()
    {
        var builder = new ReportBuilder();
        builder.AddTableDocument(9, 12, 3, null, 0);
        builder.AddTableDocument(2, 6, 7, "Definicje | Wyjaśnienie", 4);

        ConversionReport report = builder.Build(12, TimeSpan.Zero);

        Assert.Equal(
            [new TableDocumentSummary(2, 6, 7, "Definicje | Wyjaśnienie", 4), new TableDocumentSummary(9, 12, 3, null, 0)],
            report.TableDocuments);
        Assert.Equal((0, 0), (report.TableCount, report.FallbackTableCount));
    }

    [Fact]
    public void SectionKind_TableDocumentSection_IsAddedLast()
    {
        Assert.Equal(SectionKind.TableDocumentSection, Enum.GetValues<SectionKind>()[^1]);
        Assert.Equal(SectionKind.Typographic + 1, SectionKind.TableDocumentSection);
    }

    [Fact]
    public void Warnings_AreSortedByPageThenCode()
    {
        var builder = new ReportBuilder();
        builder.AddWarning("TBL001_AmbiguousGrid", 3, "c");
        builder.AddWarning("IMG001_ImagesIgnored", 3, "b");
        builder.AddWarning("FTN001_OrphanFootnote", 1, "a");
        builder.AddWarning("TXT001_UnmappedGlyphs", null, "d");

        ConversionReport report = builder.Build(3, TimeSpan.Zero);

        Assert.Equal(
            [(null, "TXT001_UnmappedGlyphs"), (1, "FTN001_OrphanFootnote"), (3, "IMG001_ImagesIgnored"), (3, "TBL001_AmbiguousGrid")],
            report.Warnings.Select(w => (w.PageNumber, w.Code)));
    }

    [Fact]
    public void UnmappedGlyphs_AreReportedPerPage_TXT001()
    {
        PipelineContext context = PageSketch.Assemble(
            null,
            new PageSketch(1).Line("Tekst �� bez mapowania.", 50, 100),
            new PageSketch(2).Line("Zwykly tekst.", 50, 100));
        context.Pages[0].Lines.Clear();
        context.Pages[1].Lines.Clear();

        new TextNormalizationStage().Execute(context);

        ConversionWarning warning = Assert.Single(context.Report.Build(2, TimeSpan.Zero).Warnings);
        Assert.Equal(("TXT001_UnmappedGlyphs", (int?)1), (warning.Code, warning.PageNumber));
        Assert.Contains("2", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImagesNextToText_AreReported_IMG001()
    {
        byte[] pdf = new SyntheticPdfBuilder()
            .Page().Text(72, 100, "Strona z obrazem.").Image(72, 200, 100, 50)
            .Page().Text(72, 100, "Strona bez obrazu.")
            .Build();

        using StageHarness harness = StageHarness.Open(pdf).Run(new PageExtractionStage());

        ConversionWarning warning = Assert.Single(harness.Context.Report.Build(2, TimeSpan.Zero).Warnings);
        Assert.Equal(("IMG001_ImagesIgnored", (int?)1), (warning.Code, warning.PageNumber));
    }

    [Fact]
    public async Task FullConversion_ReportsListsAndHeadingLevels()
    {
        using var stream = new MemoryStream(ListsIntegrationTests.BuildUs3Pdf());
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Report.ListCount);
        Assert.Equal(new Dictionary<int, int> { [2] = 2 }, result.Report.HeadingCounts);
        Assert.Equal(2, result.Report.PageCount);
        Assert.True(result.IsComplete);
    }
}
