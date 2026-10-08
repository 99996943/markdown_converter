using System.Text.Json;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Integration;

namespace LegalAgent.PdfParser.Tests;

/// <summary>FR-008, SC-006: identical input and options give identical Markdown, model and report (except Elapsed).</summary>
public sealed class DeterminismTests
{
    private static async Task<PdfConversionResult> ConvertAsync(IPdfMarkdownConverter converter, byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await converter.ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    [Fact]
    public async Task SameBytesTwice_SameConverter_GiveIdenticalResults()
    {
        byte[] pdf = ArtifactCleanupIntegrationTests.BuildUs1Pdf();
        IPdfMarkdownConverter converter = PdfMarkdownConverter.CreateDefault();

        PdfConversionResult first = await ConvertAsync(converter, pdf);
        PdfConversionResult second = await ConvertAsync(converter, pdf);

        AssertIdentical(first, second);
    }

    [Fact]
    public async Task SameBytes_FreshConverterInstances_GiveIdenticalResults()
    {
        byte[] pdf = ArtifactCleanupIntegrationTests.BuildUs1Pdf();

        PdfConversionResult first = await ConvertAsync(PdfMarkdownConverter.CreateDefault(), pdf);
        PdfConversionResult second = await ConvertAsync(PdfMarkdownConverter.CreateDefault(), pdf);

        AssertIdentical(first, second);
    }

    private static void AssertIdentical(PdfConversionResult first, PdfConversionResult second)
    {
        Assert.Equal(first.Markdown, second.Markdown);
        Assert.Equal(Json(first.Document), Json(second.Document));
        Assert.Equal(
            Json(first.Report with { Elapsed = TimeSpan.Zero }),
            Json(second.Report with { Elapsed = TimeSpan.Zero }));
        Assert.Equal(first.IsComplete, second.IsComplete);
    }
}
