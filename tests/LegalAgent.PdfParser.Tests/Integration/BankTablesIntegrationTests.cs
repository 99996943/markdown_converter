using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 010 — synthetic replicas of bank regulation pages (tables with rulings, merged cells, footnotes). Geometry
/// follows research.md; the text is invented and names no real bank.
/// </summary>
public sealed class BankTablesIntegrationTests
{
    private const double Left = 54;
    private const double Body = 8;
    private const double Marker = 5;
    private const double Raise = 3;

    private static async Task<string> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return result.Markdown;
    }

    /// <summary>Body text ending with a raised, smaller reference marker („Tak⁴”).</summary>
    private static SyntheticPdfBuilder TrailingMarker(SyntheticPdfBuilder page, double x, double y, string text, string marker) =>
        page.Text(x, y, text, Body).Text(x + SyntheticPdfBuilder.TextWidth(text, Body), y - Raise, marker, Marker);

    /// <summary>Body text with a raised marker glued to the start of the word after <paramref name="before"/> („³kurs”).</summary>
    private static SyntheticPdfBuilder LeadingMarker(SyntheticPdfBuilder page, double x, double y, string before, string marker, string after)
    {
        double start = x + SyntheticPdfBuilder.TextWidth(before, Body);
        return page.Text(x, y, before, Body)
            .Text(start, y - Raise, marker, Marker)
            .Text(start + SyntheticPdfBuilder.TextWidth(marker, Marker), y, after, Body);
    }

    [Fact]
    public async Task FootnotesWithLeadingReferences_AndBareLabelsBelowARule_BecomeFootnotes()
    {
        // T12 (firm-5 p. 13): 8 pt text, 5 pt markers raised by 3 pt before and after words; a short rule at the bottom
        // starting left of the margin; definitions set at 8 pt, labels ³ ⁴ raised and 8, 9 plain.
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(Left, 70, "Regulamin usług płatniczych", 14, bold: true);
        double y = 100;
        for (int i = 0; i < 6; i++, y += 10)
        {
            page.Text(Left, y, "Postanowienia regulaminu stosuje się do rachunków prowadzonych przez bank dla klientów.", Body);
        }

        LeadingMarker(page, Left, y, "Do przeliczeń stosujemy ", "3", "kurs z momentu rozliczenia transakcji.");
        TrailingMarker(page, Left, y += 10, "Usługa jest dostępna w serwisie transakcyjnym: Tak", "4");
        page.Text(Left, y += 10, "Opłata wynosi 1 000,00 zł i obowiązuje od 26.08.2026 r.", Body);
        page.Text(Left, y += 10, "1) pierwszy punkt listy,", Body);
        page.Text(Left, y += 10, "2) drugi punkt listy.", Body);
        string art = "Stosuje się przepisy art. 5";
        page.Text(Left, y += 10, art, Body).Text(Left + SyntheticPdfBuilder.TextWidth(art, Body), y - Raise, "1", Marker)
            .Text(Left + SyntheticPdfBuilder.TextWidth(art, Body) + SyntheticPdfBuilder.TextWidth("1", Marker) + 2, y, "ustawy.", Body);
        TrailingMarker(page, Left, y += 10, "Bank realizuje zlecenie stałe", "8");
        TrailingMarker(page, Left, y += 10, "Bank realizuje polecenie zapłaty", "9");

        page.HLine(39.7, 183.7, 740);
        page.Text(Left, 755 - Raise, "3", Marker).Text(Left + SyntheticPdfBuilder.TextWidth("3", Marker), 755, " Zapis obowiązuje od 26.08.2026 r.", Body);
        page.Text(Left, 765 - Raise, "4", Marker).Text(Left + SyntheticPdfBuilder.TextWidth("4", Marker), 765, "Dotyczy rachunków w złotych.", Body);
        page.Text(Left, 775, "8 Dotyczy zleceń stałych.", Body);
        page.Text(Left, 785, "9 Dotyczy polecenia zapłaty.", Body);

        string markdown = await ConvertAsync(page.Build());

        Assert.Contains("stosujemy [^1]kurs z momentu", markdown, StringComparison.Ordinal);
        Assert.Contains("transakcyjnym: Tak[^2]", markdown, StringComparison.Ordinal);
        Assert.Contains("zlecenie stałe[^3]", markdown, StringComparison.Ordinal);
        Assert.Contains("polecenie zapłaty[^4]", markdown, StringComparison.Ordinal);
        Assert.Contains("[^1]: Zapis obowiązuje od 26.08.2026 r.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^2]: Dotyczy rachunków w złotych.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^3]: Dotyczy zleceń stałych.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^4]: Dotyczy polecenia zapłaty.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("[^5]", markdown, StringComparison.Ordinal);
        Assert.Contains("Opłata wynosi 1 000,00 zł i obowiązuje od 26.08.2026 r.", markdown, StringComparison.Ordinal);
        Assert.Contains("art. 5¹ ustawy", markdown, StringComparison.Ordinal);
        Assert.Contains("1\\) pierwszy punkt listy", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FootnoteReferencesInARuledTable_StayInTheirCells()
    {
        // T12 (firm-5 p. 13, firm-1 p. 16): „Tak⁴”, „TAK/TAK⁵”, „³kurs z momentu…” in cells and a reference in a column
        // name; definitions below a short rule at the body size.
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(Left, 70, "Regulamin rachunków", 14, bold: true);
        double y = 100;
        for (int i = 0; i < 6; i++, y += 10)
        {
            page.Text(Left, y, "Postanowienia regulaminu stosuje się do rachunków prowadzonych przez bank dla klientów.", Body);
        }

        double[] xs = [59.3, 201.1, 378.3, 555.5];
        double[] borders = [250, 270, 290, 310, 330];
        foreach (double border in borders)
        {
            for (int c = 0; c + 1 < xs.Length; c++)
            {
                page.HLine(xs[c] + 0.2, xs[c + 1] - 0.2, border);
            }
        }

        foreach (double x in xs)
        {
            page.VLine(x, borders[0], borders[^1]);
        }

        page.Text(64, 263, "Czynność", Body, bold: true);
        page.Text(206, 263, "Serwis transakcyjny", Body, bold: true)
            .Text(206 + SyntheticPdfBuilder.TextWidth("Serwis transakcyjny", Body, bold: true), 263 - Raise, "6", Marker, bold: true);
        page.Text(383, 263, "Placówka", Body, bold: true);
        page.Text(64, 283, "Przelew krajowy", Body);
        TrailingMarker(page, 206, 283, "Tak", "4");
        page.Text(383, 283, "Tak", Body);
        page.Text(64, 303, "Zmiana typu rachunku", Body);
        TrailingMarker(page, 206, 303, "TAK/TAK", "5");
        page.Text(383, 303, "NIE", Body);
        page.Text(64, 323, "Przewalutowanie", Body);
        LeadingMarker(page, 206, 323, string.Empty, "3", "kurs z momentu transakcji");
        page.Text(383, 323, "Tak", Body);

        page.HLine(39.7, 183.7, 740);
        page.Text(Left, 755 - Raise, "3", Marker).Text(Left + SyntheticPdfBuilder.TextWidth("3", Marker), 755, " Zapis obowiązuje od 26.08.2026 r.", Body);
        page.Text(Left, 765 - Raise, "4", Marker).Text(Left + SyntheticPdfBuilder.TextWidth("4", Marker), 765, " Dotyczy przelewów w złotych.", Body);
        page.Text(Left, 775, "5 Zmiana na wniosek klienta.", Body);
        page.Text(Left, 785, "6 Nasza strona internetowa.", Body);

        string markdown = await ConvertAsync(page.Build());

        Assert.Equal(1, markdown.Split('\n').Count(l => l.StartsWith("| ---", StringComparison.Ordinal)));
        // References are numbered in reading order: the column name first, then the rows.
        Assert.Contains("| **Serwis transakcyjny**[^1] |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Tak[^2] | Tak |", markdown, StringComparison.Ordinal);
        Assert.Contains("| TAK/TAK[^3] | NIE |", markdown, StringComparison.Ordinal);
        Assert.Contains("| [^4]kurs z momentu transakcji | Tak |", markdown, StringComparison.Ordinal);
        Assert.Contains("[^1]: Nasza strona internetowa.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^2]: Dotyczy przelewów w złotych.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^3]: Zmiana na wniosek klienta.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^4]: Zapis obowiązuje od 26.08.2026 r.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("[^0]", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("[^5]", markdown, StringComparison.Ordinal);
    }

    /// <summary>Text centred between two column borders.</summary>
    private static SyntheticPdfBuilder Centred(SyntheticPdfBuilder page, double left, double right, double y, string text, double size, bool bold = false) =>
        page.Text(((left + right) / 2) - (SyntheticPdfBuilder.TextWidth(text, size, bold) / 2), y, text, size, bold);

    /// <summary>Horizontal borders drawn piece by piece per column and verticals between the first and last border.</summary>
    private static SyntheticPdfBuilder Grid(SyntheticPdfBuilder page, double[] xs, double[] borders)
    {
        foreach (double border in borders)
        {
            for (int c = 0; c + 1 < xs.Length; c++)
            {
                page.HLine(xs[c] + 0.2, xs[c + 1] - 0.2, border);
            }
        }

        foreach (double x in xs)
        {
            page.VLine(x, borders[0], borders[^1]);
        }

        return page;
    }

    private static string[] TableLines(string markdown) => markdown.Split('\n').Where(l => l.StartsWith('|')).ToArray();

    [Fact]
    public async Task RuledTableWithCentredMultiLineColumnNames_HasOneColumnPerBand()
    {
        // T3 (downloads-2 p. 20): five ruled columns, column names in bold 11 pt centred on two to four lines, the first
        // column's name on the baseline of the second line of the others; values centred, one on two lines.
        double[] xs = [45.7, 185.4, 278.9, 372.3, 463.1, 555.5];
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(Left, 70, "Regulamin kredytu", 14, bold: true);
        for (int i = 0; i < 6; i++)
        {
            page.Text(Left, 100 + (i * 14), "Postanowienia regulaminu stosuje się do kredytów udzielanych przez bank klientom.", 11);
        }

        double[] borders = [200, 262, 292, 322, 362, 392];
        Grid(page, xs, borders);
        page.Text(50, 232, "Co i gdzie możesz zrobić", 11, bold: true);
        Centred(page, xs[1], xs[2], 217, "serwis", 11, bold: true);
        Centred(page, xs[1], xs[2], 232, "transakcyjny", 11, bold: true);
        Centred(page, xs[1], xs[2], 245, "(nasza strona", 11);
        Centred(page, xs[1], xs[2], 258, "internetowa)", 11);
        Centred(page, xs[2], xs[3], 225, "aplikacja", 11, bold: true);
        Centred(page, xs[2], xs[3], 240, "mobilna", 11, bold: true);
        Centred(page, xs[3], xs[4], 232, "BOK", 11, bold: true);
        Centred(page, xs[4], xs[5], 232, "placówka", 11, bold: true);

        string[][] rows =
        [
            ["zmienić limit karty", "TAK", "TAK", "NIE", "TAK"],
            ["zastrzec kartę", "TAK", "TAK", "TAK", "TAK"],
        ];
        double[] baselines = [281, 311];
        for (int r = 0; r < rows.Length; r++)
        {
            page.Text(50, baselines[r], rows[r][0], 11);
            for (int c = 1; c < 5; c++)
            {
                Centred(page, xs[c], xs[c + 1], baselines[r], rows[r][c], 11);
            }
        }

        page.Text(50, 346, "spłacić kredyt", 11);
        Centred(page, xs[1], xs[2], 346, "TAK", 11);
        Centred(page, xs[2], xs[3], 346, "NIE", 11);
        Centred(page, xs[3], xs[4], 340, "TAK (na życzenie", 11);
        Centred(page, xs[3], xs[4], 353, "Klienta)", 11);
        Centred(page, xs[4], xs[5], 346, "TAK", 11);
        page.Text(50, 381, "zmienić dane", 11);
        for (int c = 1; c < 5; c++)
        {
            Centred(page, xs[c], xs[c + 1], 381, c == 3 ? "NIE" : "TAK", 11);
        }

        string markdown = await ConvertAsync(page.Build());

        string[] table = TableLines(markdown);
        Assert.Equal(
            [
                "| **Co i gdzie możesz zrobić** | **serwis transakcyjny** (nasza strona internetowa) | **aplikacja mobilna** | **BOK** | **placówka** |",
                "| --- | --- | --- | --- | --- |",
                "| zmienić limit karty | TAK | TAK | NIE | TAK |",
                "| zastrzec kartę | TAK | TAK | TAK | TAK |",
                "| spłacić kredyt | TAK | NIE | TAK (na życzenie Klienta) | TAK |",
                "| zmienić dane | TAK | TAK | NIE | TAK |",
            ],
            table);
        Assert.DoesNotContain(" \\| ", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuledTariffWhoseNamesAndValuesShareTheirLeftEdges_IsUnchanged()
    {
        // Guard: a ruled fee table with left-aligned names and values keeps its rows and columns.
        double[] xs = [68, 295, 415, 540];
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(72, 80, "Taryfa opłat", 14, bold: true);
        for (int i = 0; i < 4; i++)
        {
            page.Text(72, 100 + (i * 14), "Opłaty pobiera się z rachunku klienta w terminach wskazanych w tabeli.", 11);
        }

        Grid(page, xs, [170, 190, 210, 242, 262]);
        page.Text(72, 184, "Usługa", 11, bold: true).Text(300, 184, "Opłata", 11, bold: true).Text(420, 184, "Częstotliwość", 11, bold: true);
        page.Text(72, 204, "Prowadzenie rachunku", 11).Text(300, 204, "0,00 zł", 11).Text(420, 204, "miesięcznie", 11);
        page.Text(72, 224, "Przelew natychmiastowy", 11).Text(300, 224, "1,5%", 11).Text(420, 224, "za transakcję", 11);
        page.Text(72, 236, "w bankowości internetowej", 11);
        page.Text(72, 256, "Wypłata z bankomatu", 11).Text(300, 256, "min. 10 zł", 11).Text(420, 256, "jednorazowo", 11);

        string markdown = await ConvertAsync(page.Build());

        Assert.Equal(
            [
                "| **Usługa** | **Opłata** | **Częstotliwość** |",
                "| --- | --- | --- |",
                "| Prowadzenie rachunku | 0,00 zł | miesięcznie |",
                "| Przelew natychmiastowy w bankowości internetowej | 1,5% | za transakcję |",
                "| Wypłata z bankomatu | min. 10 zł | jednorazowo |",
            ],
            TableLines(markdown));
    }
}
