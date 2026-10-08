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

    [Fact]
    public void Truth_IsDeterministic()
    {
        foreach (string name in ExpectedNames)
        {
            DocumentTruth a = BankingCorpusGenerator.Truth(name);
            DocumentTruth b = BankingCorpusGenerator.Truth(name);
            Assert.Equal(a.ListItems, b.ListItems);
            Assert.Equal(a.TableRows, b.TableRows);
        }
    }

    [Fact]
    public void Truth_RegulaminRachunku_HasNestedListItems()
    {
        var items = BankingCorpusGenerator.Truth("regulamin-rachunku").ListItems;

        Assert.Equal([0, 1, 2], items.Select(i => i.Depth).Distinct().Order());
        Assert.All(items.Where(i => i.Depth == 0), i => Assert.Matches(@"^\d+\.$", i.Label));
        Assert.All(items.Where(i => i.Depth == 1), i => Assert.Matches(@"^\d+\)$", i.Label));
        Assert.All(items.Where(i => i.Depth == 2), i => Assert.Matches(@"^[a-z]\)$", i.Label));
    }

    [Theory]
    [InlineData("taryfa-z-siatka")]
    [InlineData("taryfa-bez-siatki")]
    public void Truth_Tariffs_HaveRowsWithFees(string name)
    {
        var rows = BankingCorpusGenerator.Truth(name).TableRows;

        Assert.True(rows.Count > 10, $"{name} has {rows.Count} rows");
        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Service));
            Assert.False(string.IsNullOrWhiteSpace(r.Fee));
            Assert.False(string.IsNullOrWhiteSpace(r.Frequency));
        });
    }

    [Fact]
    public void Truth_TwoColumnRegulamin_HasBullets()
    {
        var items = BankingCorpusGenerator.Truth("regulamin-dwie-kolumny").ListItems;

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("•", i.Label));
    }
}
