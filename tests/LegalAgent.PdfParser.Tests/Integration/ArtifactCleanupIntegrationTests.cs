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
