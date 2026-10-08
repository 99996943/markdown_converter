using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// T077 — US4 independent test: a two-page fee schedule with a ruled table (a two-line cell), a table without a grid
/// continued on page 2 under a repeated header, and an ambiguous layout rendered in fallback mode. Tables share their
/// pages with running text, which must stay in reading order (tables do not make a page two-column, FR-031).
/// </summary>
public sealed class TablesIntegrationTests
{
    private const double Left = 72;
    private const double Col2 = 300;
    private const double Col3 = 420;

    internal static byte[] BuildUs4Pdf()
    {
        var builder = new SyntheticPdfBuilder().PageNumberFooter("{n}");

        builder.Page()
            .Text(Left, 80, "Taryfa opłat", 14, bold: true)
            .Text(Left, 110, "Tabela 1. Opłaty za prowadzenie rachunku.")
            .Text(Left, 140, "Usługa", bold: true).Text(Col2, 140, "Opłata", bold: true).Text(Col3, 140, "Częstotliwość", bold: true)
            .Text(Left, 160, "Prowadzenie rachunku").Text(Col2, 160, "0,00 zł").Text(Col3, 160, "miesięcznie")
            .Text(Left, 180, "Przelew natychmiastowy").Text(Col2, 180, "1,5%").Text(Col3, 180, "za transakcję")
            .Text(Left, 192, "w bankowości internetowej")
            .Text(Left, 212, "Wypłata z bankomatu").Text(Col2, 212, "min. 10 zł").Text(Col3, 212, "jednorazowo")
            .HLine(68, 540, 128).HLine(68, 540, 148).HLine(68, 540, 168).HLine(68, 540, 200).HLine(68, 540, 220)
            .VLine(68, 128, 220).VLine(295, 128, 220).VLine(415, 128, 220).VLine(540, 128, 220)
            .Text(Left, 280, "Tabela 2. Opłaty za karty.")
            .Text(Left, 330, "Karta", bold: true).Text(Col2, 330, "Opłata", bold: true).Text(Col3, 330, "Uwagi", bold: true)
            .Text(Left, 350, "Wydanie karty").Text(Col2, 350, "0 zł").Text(Col3, 350, "jednorazowo")
            .Text(Left, 370, "Wznowienie karty").Text(Col2, 370, "10 zł").Text(Col3, 370, "co 3 lata")
            .Text(Left, 382, "po upływie ważności");

        builder.Page()
            .Text(Left, 80, "Karta", bold: true).Text(Col2, 80, "Opłata", bold: true).Text(Col3, 80, "Uwagi", bold: true)
            .Text(Left, 100, "Zastrzeżenie karty").Text(Col2, 100, "0 zł").Text(Col3, 100, "na wniosek")
            .Text(Left, 120, "Duplikat karty").Text(Col2, 120, "15 zł").Text(Col3, 120, "na wniosek")
            .Text(Left, 180, "Tabela 3. Zestawienie niejednoznaczne.")
            .Text(Left, 230, "Usługa").Text(Col2, 230, "Kanał").Text(Col3, 230, "Opłata")
            .Text(Left, 250, "Przelew").Text(Col3, 250, "2 zł")
            .Text(Left, 270, "Wpłata").Text(Col2, 270, "oddział").Text(Col3, 270, "5 zł")
            .Text(Left, 290, "Czek").Text(Col3, 290, "7 zł")
            .Text(Left, 350, "Opłaty pobierane są z rachunku.");

        return builder.Build();
    }

    private static string ExpectedPath(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Expected", name);

    private static async Task<PdfConversionResult> ConvertAsync()
    {
        using var stream = new MemoryStream(BuildUs4Pdf());
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FeeSchedule_ConvertsToGfmAndFallbackTables()
    {
        PdfConversionResult result = await ConvertAsync();

        GoldenFile.AssertMatches(result.Markdown, ExpectedPath("us4-tables.expected.md"));
    }

    [Fact]
    public async Task FeeSchedule_ModelHasTablesWithPagesAndReportCounts()
    {
        PdfConversionResult result = await ConvertAsync();

        TableBlock[] tables = result.Document.Preamble.OfType<TableBlock>().ToArray();
        Assert.Equal("Taryfa opłat", result.Document.Title);
        Assert.Equal(3, tables.Length);
        Assert.Equal((new PageRange(1, 2), 4, false), (tables[1].Pages, tables[1].Rows.Count, tables[1].IsFallback));
        Assert.True(tables[2].IsFallback);
        Assert.Equal((3, 1), (result.Report.TableCount, result.Report.FallbackTableCount));
        Assert.Contains(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid" && w.PageNumber == 2);
    }

    [Fact]
    public async Task TwoColumnRegulation_IsReadColumnByColumnWithoutTables_FR031()
    {
        byte[] pdf = BankingCorpusGenerator.Documents().Single(d => d.Name == "regulamin-dwie-kolumny").Pdf;
        using var stream = new MemoryStream(pdf);

        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Report.TableCount);
        int leftColumn = result.Markdown.IndexOf("Regulamin określa zasady korzystania z", StringComparison.Ordinal);
        int rightColumn = result.Markdown.IndexOf("Dyspozycje złożone w systemie są realizowane", StringComparison.Ordinal);
        Assert.True(leftColumn >= 0 && rightColumn > leftColumn, "the left column must be read before the right one");
    }
}
