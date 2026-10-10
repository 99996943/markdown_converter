using System.Globalization;
using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// US1 independent test: a 6-page PDF with a running Dziennik Ustaw header, a page-number footer, a paragraph
/// continued across a page break and a hyphenated word converts to clean Markdown with page markers.
/// </summary>
public sealed class ArtifactCleanupIntegrationTests
{
    internal static byte[] BuildUs1Pdf()
    {
        var builder = new SyntheticPdfBuilder()
            .Title("Tytuł z metadanych, którego nie renderujemy")
            .RunningHeader("Dziennik Ustaw – {n} – Poz. 1234")
            .PageNumberFooter("Strona {n} z 6");

        builder.Page()
            .Text(72, 100, "Niniejszy regulamin określa zasady prowadzenia rachunków")
            .Text(72, 114, "płatniczych przez bank dla każdego przedsiębior-")
            .Text(72, 128, "cy oraz konsumenta, który zawarł umowę z bankiem.")
            .Text(72, 160, "Drugi akapit opisuje obowiązki stron umowy i zaczyna się")
            .Text(72, 174, "na pierwszej stronie, a jego dalsza część znajduje się");

        builder.Page()
            .Text(72, 100, "na kolejnej stronie dokumentu, co wymaga połączenia.")
            .Text(72, 132, "Trzeci akapit znajduje się w całości na drugiej stronie.");

        for (int n = 3; n <= 6; n++)
        {
            builder.Page().Text(72, 100, $"Treść strony {n.ToString(CultureInfo.InvariantCulture)} zawiera krótki akapit testowy.");
        }

        return builder.Build();
    }

    private static string ExpectedPath(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Expected", name);

    [Fact]
    public async Task SixPageDocument_ConvertsToCleanMarkdown()
    {
        using var stream = new MemoryStream(BuildUs1Pdf());

        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(
            stream,
            new PdfConversionRequest { SourceId = "us1.pdf" },
            TestContext.Current.CancellationToken);

        GoldenFile.AssertMatches(result.Markdown, ExpectedPath("us1-artifacts.expected.md"));
        Assert.True(result.IsComplete);
        Assert.Equal("us1.pdf", result.Document.Source.SourceId);
        Assert.Equal(6, result.Document.Source.PageCount);
        Assert.Contains(result.Report.RemovedArtifacts, a => a.Kind == ArtifactKind.RunningHeader && a.Occurrences == 6);
        Assert.Contains(result.Report.RemovedArtifacts, a => a.Kind == ArtifactKind.PageNumber && a.Occurrences == 6);
    }

    /// <summary>
    /// Spec 008 US1 replica: a 3-page A4 document with a stand-alone "n/3" footer at ≈ 0.905 H on pages 2–3 —
    /// just above the 0.92 H margin zone — and a body line on page 2 ending at ≈ 0.903 H.
    /// </summary>
    internal static byte[] BuildSlashFooterPdf()
    {
        var builder = new SyntheticPdfBuilder();

        builder.Page()
            .Text(72, 100, "Opłata wynosi 1/2 kwoty prowizji określonej w tabeli.")
            .Text(72, 130, "Zasady ustala uchwała nr 12/2024 zarządu banku.");

        builder.Page()
            .Text(72, 100, "Klient może złożyć dyspozycję:")
            .Text(72, 120, "1/ w oddziale banku,")
            .Text(72, 140, "2/ w serwisie internetowym.")
            .Text(72, 757, "Ostatni wiersz treści drugiej strony kończy się tuż nad stopką.")
            .Text(523, 768, "2/3", size: 8);

        builder.Page()
            .Text(72, 100, "Treść trzeciej strony zawiera krótki akapit testowy.")
            .Text(523, 768, "3/3", size: 8);

        return builder.Build();
    }

    [Fact]
    public async Task SlashFooterAboveMarginZone_IsRemoved_ContentIsKept()
    {
        using var stream = new MemoryStream(BuildSlashFooterPdf());

        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(
            stream,
            new PdfConversionRequest { SourceId = "us1-slash.pdf" },
            TestContext.Current.CancellationToken);

        string markdown = result.Markdown;
        Assert.DoesNotContain("2/3", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("3/3", markdown, StringComparison.Ordinal);
        Assert.Contains("<!-- page: 2 -->", markdown, StringComparison.Ordinal);
        Assert.Contains("<!-- page: 3 -->", markdown, StringComparison.Ordinal);
        Assert.Contains("Opłata wynosi 1/2 kwoty prowizji określonej w tabeli.", markdown, StringComparison.Ordinal);
        Assert.Contains("uchwała nr 12/2024 zarządu banku.", markdown, StringComparison.Ordinal);
        Assert.Contains("- 1/ w oddziale banku,", markdown, StringComparison.Ordinal);
        Assert.Contains("- 2/ w serwisie internetowym.", markdown, StringComparison.Ordinal);
        Assert.Contains("kończy się tuż nad stopką.", markdown, StringComparison.Ordinal);
        Assert.Contains("Treść trzeciej strony zawiera krótki akapit testowy.", markdown, StringComparison.Ordinal);
        Assert.Contains(result.Report.RemovedArtifacts, a => a.Kind == ArtifactKind.PageNumber && a.Pages.SequenceEqual([2, 3]));
    }

    [Fact]
    public async Task PageMarkersDisabled_ProducesNoMarkers()
    {
        using var stream = new MemoryStream(BuildUs1Pdf());

        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault(o => o.Rendering.PageMarkers = false)
            .ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<!--", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("znajduje się na kolejnej stronie", result.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerStream_IsNotDisposed()
    {
        using var stream = new MemoryStream(BuildUs1Pdf());

        await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
    }
}
