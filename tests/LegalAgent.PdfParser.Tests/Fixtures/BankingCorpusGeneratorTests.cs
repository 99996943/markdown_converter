using LegalAgent.PdfParser.Model;
using UglyToad.PdfPig;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>T092 — sanity checks of the synthetic banking corpus generator.</summary>
public sealed class BankingCorpusGeneratorTests
{
    private static readonly string[] ExpectedNames =
        ["regulamin-rachunku", "taryfa-z-siatka", "taryfa-bez-siatki", "regulamin-dwie-kolumny", "regulamin-promocji-tabela", "regulamin-z-tabela-definicji"];

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
            Assert.True(pages is >= 2 and <= 6, $"{name} has {pages} pages");
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

/// <summary>Spec 002, T009–T010 — the synthetic table-document and the document with a short definitions table.</summary>
public sealed class TableDocumentCorpusTests
{
    private const string TableDocument = "regulamin-promocji-tabela";
    private const string DefinitionsTable = "regulamin-z-tabela-definicji";

    private static double[] VerticalRulings(UglyToad.PdfPig.Content.Page page) =>
        page.Paths
            .SelectMany(p => p)
            .Select(sp => sp.GetBoundingRectangle())
            .OfType<UglyToad.PdfPig.Core.PdfRectangle>()
            .Where(r => r.Right - r.Left <= 2 && r.Top - r.Bottom >= 5)
            .Select(r => Math.Round((r.Left + r.Right) / 2))
            .Distinct()
            .Order()
            .ToArray();

    private static List<List<UglyToad.PdfPig.Content.Letter>> Lines(UglyToad.PdfPig.Content.Page page) =>
        page.Letters
            .Where(l => !string.IsNullOrWhiteSpace(l.Value))
            .GroupBy(l => Math.Round(l.StartBaseLine.Y))
            .OrderByDescending(g => g.Key)
            .Select(g => g.OrderBy(l => l.StartBaseLine.X).ToList())
            .ToList();

    [Fact]
    public void TableDocument_HasCoverTablePagesAndStatementsPage()
    {
        using PdfDocument doc = PdfDocument.Open(BankingCorpusGenerator.Pdf(TableDocument));

        Assert.Equal(6, doc.NumberOfPages);
        Assert.Single(doc.GetPage(1).GetImages());
        Assert.Empty(VerticalRulings(doc.GetPage(1)));
        Assert.Empty(VerticalRulings(doc.GetPage(6)));
        for (int n = 2; n <= 5; n++)
        {
            Assert.Equal([54.0, 181.0, 541.0], VerticalRulings(doc.GetPage(n)));
        }
    }

    [Fact]
    public void TableDocument_RepeatsTheColumnNameRowOnPages2_3And5()
    {
        using PdfDocument doc = PdfDocument.Open(BankingCorpusGenerator.Pdf(TableDocument));

        foreach ((int page, bool header) in new[] { (2, true), (3, true), (4, false), (5, true) })
        {
            string first = string.Concat(Lines(doc.GetPage(page))[0].Select(l => l.Value));
            Assert.Equal(header, first.StartsWith("DefinicjeWyjaśnienie", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TableDocument_KeepsTheRightColumnInsideItsCell()
    {
        using PdfDocument doc = PdfDocument.Open(BankingCorpusGenerator.Pdf(TableDocument));

        for (int n = 2; n <= 5; n++)
        {
            UglyToad.PdfPig.Content.Page page = doc.GetPage(n);
            Assert.All(page.Letters.Where(l => l.StartBaseLine.X > 181), l => Assert.True(l.EndBaseLine.X <= 535, $"page {n}: „{l.Value}” ends at {l.EndBaseLine.X}"));
            Assert.All(page.Letters.Where(l => l.StartBaseLine.X < 181), l => Assert.True(l.EndBaseLine.X <= 176, $"page {n}: „{l.Value}” ends at {l.EndBaseLine.X}"));
        }
    }

    [Fact]
    public void TableDocument_SetsSubBulletsInMonospaceAndKeepsTheWordOInTheTextFont()
    {
        using PdfDocument doc = PdfDocument.Open(BankingCorpusGenerator.Pdf(TableDocument));
        var firstLetters = Enumerable.Range(2, 4).SelectMany(n => Lines(doc.GetPage(n))).Select(line => line).ToList();

        Assert.Contains(firstLetters, line => line[0].Value == "o" && (line[0].FontName ?? string.Empty).Contains("Mono", StringComparison.Ordinal));
        Assert.Contains(firstLetters, line =>
            line.Count > 1
            && line.SkipWhile(l => l.StartBaseLine.X < 181).FirstOrDefault() is { Value: "o" } o
            && !(o.FontName ?? string.Empty).Contains("Mono", StringComparison.Ordinal));
    }

    [Fact]
    public void Truth_TableDocument_ListsSectionNamesAndColumnNameRow()
    {
        DocumentTruth truth = BankingCorpusGenerator.Truth(TableDocument);

        Assert.Equal(
            ["Organizator promocji", "Uczestnik promocji", "Ważne pojęcia", "Korzyści promocji", "Warunki/zasady promocji", "Jak możesz złożyć reklamację dotyczącą promocji?"],
            truth.SectionNames);
        Assert.Equal(["Definicje", "Wyjaśnienie"], truth.HeaderRowWords);
        Assert.Contains(truth.ListItems, i => i.Label == "o" && i.Depth == 1);
    }

    [Fact]
    public void DefinitionsTable_HasTwoColumnGridOnTwoOfSixPages()
    {
        using PdfDocument doc = PdfDocument.Open(BankingCorpusGenerator.Pdf(DefinitionsTable));

        Assert.Equal(6, doc.NumberOfPages);
        int[] gridPages = Enumerable.Range(1, 6).Where(n => VerticalRulings(doc.GetPage(n)).Length == 3).ToArray();
        Assert.Equal([3, 4], gridPages);
    }

    [Theory]
    [InlineData(TableDocument)]
    [InlineData(DefinitionsTable)]
    public void Documents_AreDeterministic(string name)
    {
        using PdfDocument a = PdfDocument.Open(BankingCorpusGenerator.Pdf(name));
        using PdfDocument b = PdfDocument.Open(BankingCorpusGenerator.Pdf(name));

        Assert.Equal(a.GetPages().Select(p => p.Text), b.GetPages().Select(p => p.Text));
    }
}
