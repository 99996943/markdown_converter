using LegalAgent.PdfParser.Model;
using UglyToad.PdfPig;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>T092 — sanity checks of the synthetic banking corpus generator.</summary>
public sealed class BankingCorpusGeneratorTests
{
    private static readonly string[] ExpectedNames =
        ["regulamin-rachunku", "taryfa-z-siatka", "taryfa-bez-siatki", "regulamin-dwie-kolumny"];

    private static string[] PageTexts(byte[] pdf)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPages().Select(p => p.Text).ToArray();
    }

    [Fact]
    public void Documents_HaveExpectedNamesAndPageCounts()
    {
        var docs = BankingCorpusGenerator.Documents();

        Assert.Equal(ExpectedNames, docs.Select(d => d.Name));
        foreach ((string name, byte[] pdf) in docs)
        {
            int pages = PageTexts(pdf).Length;
            Assert.True(pages is >= 2 and <= 4, $"{name} has {pages} pages");
        }
    }

    [Fact]
    public void Documents_AreDeterministic()
    {
        var first = BankingCorpusGenerator.Documents();
        var second = BankingCorpusGenerator.Documents();

        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Name, second[i].Name);
            Assert.Equal(PageTexts(first[i].Pdf), PageTexts(second[i].Pdf));
        }
    }

    [Fact]
    public async Task Documents_ConvertCompletely()
    {
        foreach ((string name, byte[] pdf) in BankingCorpusGenerator.Documents())
        {
            using var stream = new MemoryStream(pdf);
            PdfConversionResult result = await PdfMarkdownConverter.CreateDefault()
                .ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.IsComplete, name);
        }
    }
}
