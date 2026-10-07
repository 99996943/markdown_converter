using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// T083 — US6: pages without text or unreadable pages are never a silent success (FR-009, FR-009a, FR-009b, FR-071).
/// </summary>
public sealed class ErrorHandlingTests
{
    private static string Fixture(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Corpus", "errors", name);

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf, Action<PdfParserOptions>? configure = null)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault(configure).ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static IEnumerable<ContentBlock> AllBlocks(LegalDocument document) =>
        document.Preamble.Concat(Flatten(document.Sections).SelectMany(s => s.Blocks));

    private static IEnumerable<Section> Flatten(IEnumerable<Section> sections) =>
        sections.SelectMany(s => new[] { s }.Concat(Flatten(s.Children)));

    [Fact]
    public async Task ImageOnlyPage_IsSkippedAndMarked_FR071()
    {
        byte[] pdf = new SyntheticPdfBuilder()
            .Page().Text(72, 100, "Pierwsza strona z tekstem.")
            .Page().Image(72, 100, 300, 400)
            .Page().Text(72, 100, "Trzecia strona z tekstem.")
            .Build();

        PdfConversionResult result = await ConvertAsync(pdf);

        Assert.False(result.IsComplete);
        SkippedPageBlock skipped = Assert.Single(AllBlocks(result.Document).OfType<SkippedPageBlock>());
        Assert.Equal((2, SkipReason.NoTextLayer), (skipped.PageNumber, skipped.Reason));
        Assert.Contains(result.Report.Warnings, w => w.Code == "PDF001_NoTextLayer" && w.PageNumber == 2);
        Assert.Contains("<!-- page 2 skipped: no-text-layer -->", result.Markdown, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(result.Report.SkippedPages).PageNumber);
    }

    [Fact]
    public async Task BlankPage_IsSkippedSilently()
    {
        byte[] pdf = new SyntheticPdfBuilder()
            .Page().Text(72, 100, "Pierwsza strona.")
            .BlankPage()
            .Page().Text(72, 100, "Trzecia strona.")
            .Build();

        PdfConversionResult result = await ConvertAsync(pdf);

        Assert.True(result.IsComplete);
        Assert.Empty(result.Report.SkippedPages);
        Assert.Empty(AllBlocks(result.Document).OfType<SkippedPageBlock>());
        Assert.DoesNotContain(result.Report.Warnings, w => w.PageNumber == 2);
    }

    [Fact]
    public async Task DocumentWithoutAnyText_Throws()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Image(72, 100, 300, 400).BlankPage().Build();

        await Assert.ThrowsAsync<PdfNoTextException>(() => ConvertAsync(pdf));
    }

    [Fact]
    public async Task UnreadablePage_FailsByDefault_FR009a()
    {
        byte[] pdf = File.ReadAllBytes(Fixture("broken-page.pdf"));

        PdfPageReadException ex = await Assert.ThrowsAsync<PdfPageReadException>(() => ConvertAsync(pdf));

        Assert.Equal(2, ex.PageNumber);
    }

    [Fact]
    public async Task UnreadablePage_IsSkippedInPartialMode_FR009a()
    {
        byte[] pdf = File.ReadAllBytes(Fixture("broken-page.pdf"));

        PdfConversionResult result = await ConvertAsync(pdf, o => o.AllowPartialResult = true);

        Assert.False(result.IsComplete);
        SkippedPage skipped = Assert.Single(result.Report.SkippedPages);
        Assert.Equal((2, SkipReason.PageReadError), (skipped.PageNumber, skipped.Reason));
        Assert.Contains(AllBlocks(result.Document).OfType<SkippedPageBlock>(), b => b.PageNumber == 2 && b.Reason == SkipReason.PageReadError);
        Assert.Contains("<!-- page 2 skipped: read-error -->", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("Art. 1.", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("Art. 3.", result.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllPagesUnreadable_FailEvenInPartialMode_FR009a()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLegalAgentPdfParser(o => o.AllowPartialResult = true)
            .ReplacePdfParserStage<PageExtractionStage, FailingExtractionStage>()
            .BuildServiceProvider();
        using var stream = new MemoryStream(new SyntheticPdfBuilder().Page().Text(72, 100, "Tekst.").Page().Text(72, 100, "Tekst.").Build());

        PdfPageReadException ex = await Assert.ThrowsAsync<PdfPageReadException>(() =>
            services.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, ex.PageNumber);
    }

    [Fact]
    public async Task LimitsApplyInPartialMode_FR009b()
    {
        byte[] pdf = File.ReadAllBytes(Fixture("broken-page.pdf"));

        PdfLimitExceededException ex = await Assert.ThrowsAsync<PdfLimitExceededException>(() =>
            ConvertAsync(pdf, o =>
            {
                o.AllowPartialResult = true;
                o.Limits.MaxPages = 2;
            }));

        Assert.Equal(PdfLimit.PageCount, ex.Limit);
    }

    /// <summary>Extraction stage whose every page read fails.</summary>
    private sealed class FailingExtractionStage : PageExtractionStage
    {
        internal override Page GetPage(PdfDocument document, int pageNumber) =>
            throw new InvalidOperationException($"Uszkodzona strona {pageNumber}.");
    }
}
