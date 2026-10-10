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

    /// <summary>
    /// Spec 008, T025 (FR-620, FR-624) — replica of D-A p. 38 (7 pt): a ruled box „Sytuacja | Jak postępujemy?” (verticals
    /// at x 54.1, 304.6, 555.1) with items „a)”–„d)”, a bold caption row and one cell spanning the box with a numbered
    /// passage whose letter „a)” has Roman numerals „i.”, „ii.” with their text 14 pt to the right: the numerals join their
    /// text, so they are list items, not rows „i. | …” of a fallback table.
    /// </summary>
    [Fact]
    public async Task Roman_dot_items_below_a_two_column_box_are_list_items_not_table_rows()
    {
        const double size = 7, margin = 39.7, box = 59.5, boxText = 74.1, answer = 310.0;
        const double left = 54.1, middle = 304.6, right = 555.1;
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page()
            .Text(margin, 70, "Raz ustaloną korektę stosujemy przez cały czas stosowania wskaźnika alternatywnego, chyba że strony umówią się inaczej.", size)
            .Text(box, 105.5, "Zasady postępowania, gdy stosujemy wskaźnik alternatywny, który stosuje lub rekomenduje inny podmiot", size, bold: true)
            .Text(box, 122.5, "Sytuacja", size, bold: true).Text(answer, 122.5, "Jak postępujemy?", size, bold: true)
            .Text(box, 140, "a)", size).Text(boxText, 140, "podmiot rekomendował korektę", size).Text(answer, 140, "stosujemy taką korektę", size)
            .Text(box, 157.5, "b)", size).Text(boxText, 157.5, "podmiot nie rekomendował korekty", size).Text(answer, 157.5, "nie stosujemy korekty", size)
            .Text(answer, 174.5, "stosujemy korektę, którą wyznaczaliśmy w uzasadniony", size)
            .Text(box, 179.5, "c)", size).Text(boxText, 179.5, "podmiot nie odniósł się do kwestii korekty", size)
            .Text(answer, 184.5, "ekonomicznie sposób, z myślą o celu korekty", size)
            .Text(box, 201.5, "d)", size).Text(boxText, 201.5, "jako wskaźnik stosujemy średnią kwotowań", size).Text(answer, 201.5, "nie stosujemy korekty", size)
            .Text(box, 218.5, "Zasady postępowania, gdy jako wskaźnik alternatywny stosujemy stopę referencyjną banku centralnego", size, bold: true)
            .Text(59.3, 235.5, "1.", size).Text(74.2, 235.5, "Dodajemy korektę do wartości wskaźnika alternatywnego.", size)
            .Text(59.3, 245.5, "2.", size).Text(74.2, 245.5, "Korekta jest równa historycznej medianie różnic między wskaźnikiem oraz stopą referencyjną:", size)
            .Text(73.8, 255.5, "a)", size).Text(87.9, 255.5, "za okres 24 miesięcy (lub krótszy, jeśli wskaźnik publikowano krócej) przed:", size)
            .Text(88.1, 265.5, "i.", size).Text(102.0, 265.5, "dniem zamiany wskaźnika albo", size)
            .Text(88.1, 275.5, "ii.", size).Text(102.4, 275.5, "pierwszym dniem, w którym stosujemy wskaźnik alternatywny ze względu na brak publikacji;", size)
            .Text(74.1, 285.5, "b)", size).Text(87.9, 285.5, "dla różnic z każdego dnia w badanym okresie, w którym publikowano oba wskaźniki.", size)
            .Text(margin, 310, "Korektę stosujemy od dnia zamiany wskaźnika do dnia zakończenia umowy, o ile strony nie postanowią inaczej.", size);

        // Horizontal rules between the rows; the caption rows and the passage cell span the box (outer verticals only).
        double[] rules = [93.5, 110.5, 127.5, 145.6, 162.6, 189.6, 206.6, 223.5, 290.5];
        foreach (double y in rules)
        {
            page.HLine(left, right, y);
        }

        page.VLine(left, 93.5, 290.5).VLine(right, 93.5, 290.5).VLine(middle, 110.5, 206.6);

        using var stream = new MemoryStream(builder.Build());
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("i. \\|", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("i\\. \\|", result.Markdown, StringComparison.Ordinal);
        Assert.Contains(
            "- i\\. dniem zamiany wskaźnika albo\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.Contains(
            "- ii\\. pierwszym dniem, w którym stosujemy wskaźnik alternatywny ze względu na brak publikacji;\n",
            result.Markdown,
            StringComparison.Ordinal);
    }
}
