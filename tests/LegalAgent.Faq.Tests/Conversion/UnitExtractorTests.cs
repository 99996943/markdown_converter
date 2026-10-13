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

    private async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await services.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(stream, null, TestContext.Current.CancellationToken);
    }
}
