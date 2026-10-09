using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 003 (US2, T077–T089): layouts of the synthetic bank corpus that the library handled badly, each on a minimal
/// PDF built with <see cref="SyntheticPdfBuilder"/>.
/// </summary>
public sealed class CorpusLayoutsIntegrationTests
{
    private const double Left = 72;
    private const double Size = 10.5;
    private const double Leading = 14;

    private static async Task<string> MarkdownAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, new PdfConversionRequest(), TestContext.Current.CancellationToken);
        return result.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Running text lines so the page has a typical body size and leading.</summary>
    private static double Body(SyntheticPdfBuilder b, double y, int lines)
    {
        for (int i = 0; i < lines; i++)
        {
            b.Text(Left, y, $"Wiersz {i + 1} zwykłego tekstu regulaminu o stałej długości, który ustala typową interlinię.", Size);
            y += Leading;
        }

        return y + 8;
    }

    /// <summary>A ruled grid table (header + rows) whose top border is at <paramref name="top"/>.</summary>
    private static double GridTable(SyntheticPdfBuilder b, double top, string[] header, string[][] rows)
    {
        double[] x = [Left, 280, 400, 523];
        b.HLine(x[0], x[^1], top);
        double y = top;
        foreach (string[] row in rows.Prepend(header))
        {
            for (int c = 0; c < row.Length; c++)
            {
                b.Text(x[c] + 5, y + 14, row[c], 9, bold: row == header);
            }

            y += 20;
            b.HLine(x[0], x[^1], y);
        }

        foreach (double vx in x)
        {
            b.VLine(vx, top, y);
        }

        return y;
    }

    /// <summary>
    /// T083: a gridless tariff table (bold column-name row, „1.” in the Lp. column, wrapped service names, a note marker
    /// „1)” in a rate cell) right below numbered paragraphs: the paragraphs stay a list and the table is one GFM table.
    /// </summary>
    [Fact]
    public async Task GridlessTableBelowNumberedParagraphs_StartsAtItsBoldColumnNameRow()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 80, "I. Przelewy zagraniczne", 13, bold: true);
        double y = 110;
        foreach ((string label, string[] lines) in new[]
        {
            ("1.", new[] { "Opłaty pobiera się w dniu realizacji dyspozycji, z rachunku wskazanego", "przez Zleceniodawcę." }),
            ("2.", new[] { "Zleceniodawca ponosi odpowiedzialność za prawidłowość danych odbiorcy,", "w tym numeru rachunku i kodu banku." }),
        })
        {
            b.Text(Left, y, label, Size);
            foreach (string line in lines)
            {
                b.Text(Left + 18, y, line, Size);
                y += Leading;
            }

            y += 3;
        }

        double[] x = [Left, Left + 40, 330, 440];
        y += 8;
        string[] header = ["Lp.", "Wyszczególnienie czynności", "Tryb pobierania", "Stawka"];
        for (int c = 0; c < 4; c++)
        {
            b.Text(x[c], y, header[c], 9.5, bold: true);
        }

        y += 24;
        (string No, string[] Service, string Mode, string Rate)[] rows =
        [
            ("1.", ["Przelew SEPA w euro do rachunku w państwie", "członkowskim EOG"], "za operację", "2,00 zł"),
            ("2.", ["Przelew walutowy poza SEPA w opcji SHA"], "od kwoty", "0,2% 1)"),
            ("3.", ["Dopłata za opcję kosztów OUR"], "za operację", "60,00 zł"),
            ("4.", ["Zmiana lub anulowanie dyspozycji przelewu", "zagranicznego na wniosek Klienta"], "za dyspozycję", "40,00 zł"),
        ];
        foreach ((string no, string[] service, string mode, string rate) in rows)
        {
            b.Text(x[0], y, no, 9.5).Text(x[2], y, mode, 9.5).Text(x[3], y, rate, 9.5);
            for (int k = 0; k < service.Length; k++)
            {
                b.Text(x[1], y + (k * 12), service[k], 9.5);
            }

            y += (service.Length * 12) + 12;
        }

        b.Text(Left, y + 6, "1) Minimum 25,00 zł, maksimum 200,00 zł.", 8.5);
        Body(b, y + 40, 12);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("- 1\\. Opłaty pobiera się w dniu realizacji dyspozycji", md, StringComparison.Ordinal);
        Assert.Contains("- 2\\. Zleceniodawca ponosi odpowiedzialność", md, StringComparison.Ordinal);
        Assert.Contains("| **Lp.** | **Wyszczególnienie czynności** | **Tryb pobierania** | **Stawka** |", md, StringComparison.Ordinal);
        Assert.Contains("| 1. | Przelew SEPA w euro do rachunku w państwie członkowskim EOG | za operację | 2,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 4. | Zmiana lub anulowanie dyspozycji przelewu zagranicznego na wniosek Klienta | za dyspozycję | 40,00 zł |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
    }

    /// <summary>T089a: a ruled table right below numbered paragraphs („1.” + hanging text) must not absorb them.</summary>
    [Fact]
    public async Task RuledTableBelowNumberedParagraphs_KeepsTheListAndTheTableApart()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 80, "§ 4.", 11, bold: true);
        double y = 110;
        (string Label, double LabelX, double TextX, string Text)[] items =
        [
            ("1.", Left, Left + 18, "Bank oferuje następujące rodzaje lokat terminowych:"),
            ("1)", Left + 18, Left + 36, "lokatę klasyczną o stałym oprocentowaniu;"),
            ("2)", Left + 18, Left + 36, "lokatę negocjowaną dla kwot powyżej progu."),
            ("2.", Left, Left + 18, "Okresy i stawki przedstawia poniższa tabela:"),
        ];
        foreach ((string label, double labelX, double textX, string text) in items)
        {
            b.Text(labelX, y, label, Size).Text(textX, y, text, Size);
            y += Leading + 3;
        }

        y = GridTable(b, y + 2, ["Rodzaj lokaty", "Okres", "Oprocentowanie"],
        [
            ["Lokata klasyczna", "3 miesiące", "2,5%"],
            ["Lokata klasyczna", "12 miesięcy", "3,1%"],
            ["Lokata negocjowana", "6 miesięcy", "wg umowy"],
        ]);
        Body(b, y + 24, 12);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("- 1\\. Bank oferuje następujące rodzaje lokat terminowych:", md, StringComparison.Ordinal);
        Assert.Contains("  - 1\\) lokatę klasyczną o stałym oprocentowaniu;", md, StringComparison.Ordinal);
        Assert.Contains("- 2\\. Okresy i stawki przedstawia poniższa tabela:", md, StringComparison.Ordinal);
        Assert.Contains("| **Rodzaj lokaty** | **Okres** | **Oprocentowanie** |", md, StringComparison.Ordinal);
        Assert.Contains("| Lokata negocjowana | 6 miesięcy | wg umowy |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
    }
}
