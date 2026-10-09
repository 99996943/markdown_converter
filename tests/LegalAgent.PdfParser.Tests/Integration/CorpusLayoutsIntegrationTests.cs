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

    /// <summary>
    /// T079: a procedure section heading „3. Odpowiedzialności” (larger bold) right after numbered items „1.”, „2.” of the
    /// previous section is a heading, not the next list item; steps „3.1.” below it are list items.
    /// </summary>
    [Fact]
    public async Task NumberedSectionHeadingAfterANumberedList_IsAHeading()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 60, "Procedura zastrzegania kart", 18, bold: true);
        double y = Body(b, 100, 6);
        void Heading(string text)
        {
            y += 10;
            b.Text(Left, y, text, 12.5, bold: true);
            y += 12.5 * 1.35 + 6;
        }

        void Item(string label, string text, double indent = 0)
        {
            b.Text(Left + indent, y, label, Size).Text(Left + indent + 30, y, text, Size);
            y += Leading + 3;
        }

        Heading("2. Zakres stosowania");
        Item("1.", "Procedurę stosują pracownicy placówek i infolinii.");
        Item("2.", "Procedura dotyczy kart debetowych i kredytowych.");
        y += 5;
        Heading("3. Odpowiedzialności");
        Item("3.1.", "Pracownik placówki przyjmuje zgłoszenie.");
        Item("3.1.1.", "Sprawdza tożsamość zgłaszającego.", 18);
        Item("3.2.", "Kierownik zatwierdza wyjątki.");
        Body(b, y + 20, 10);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t079.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("## 2. Zakres stosowania", md, StringComparison.Ordinal);
        Assert.Contains("## 3. Odpowiedzialności", md, StringComparison.Ordinal);
        Assert.Contains("- 3.1\\. Pracownik placówki przyjmuje zgłoszenie.", md, StringComparison.Ordinal);
        Assert.Contains("  - 3.1.1\\. Sprawdza tożsamość zgłaszającego.", md, StringComparison.Ordinal);
    }

    /// <summary>T081: a ruled checklist table „Lp. | Czynność | Wykonano” whose last column is empty in every row.</summary>
    [Fact]
    public async Task RuledChecklistWithAnEmptyColumn_IsAGfmTableWithEmptyCells()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 80, "Załącznik nr 1 Lista kontrolna", 13, bold: true);
        double y = GridTable(b, 110, ["Lp.", "Czynność", "Wykonano"],
        [
            ["1.", "Sprawdzono dokument tożsamości.", string.Empty],
            ["2.", "Zarejestrowano wniosek w systemie.", string.Empty],
            ["3.", "Wydano potwierdzenie Klientowi.", string.Empty],
        ]);
        Body(b, y + 30, 14);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("| **Lp.** | **Czynność** | **Wykonano** |", md, StringComparison.Ordinal);
        Assert.Contains("| 2. | Zarejestrowano wniosek w systemie. |  |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
    }

    /// <summary>A gridless tariff with a bold column-name row; rows (number, service lines, mode lines, rate).</summary>
    private static double GridlessTariff(SyntheticPdfBuilder b, double y, (string No, string[] Service, string[] Mode, string Rate)[] rows)
    {
        double[] x = [Left, Left + 40, 330, 440];
        string[] header = ["Lp.", "Wyszczególnienie czynności", "Tryb pobierania", "Stawka"];
        for (int c = 0; c < 4; c++)
        {
            b.Text(x[c], y, header[c], 9.5, bold: true);
        }

        y += 24;
        foreach ((string no, string[] service, string[] mode, string rate) in rows)
        {
            b.Text(x[0], y, no, 9.5).Text(x[3], y, rate, 9.5);
            for (int k = 0; k < service.Length; k++)
            {
                b.Text(x[1], y + (k * 12), service[k], 9.5);
            }

            for (int k = 0; k < mode.Length; k++)
            {
                b.Text(x[2], y + (k * 12), mode[k], 9.5);
            }

            y += (Math.Max(service.Length, mode.Length) * 12) + 12;
        }

        return y;
    }

    /// <summary>T083b: a gridless row whose service AND mode both wrap continues with a two-cell line; still one table.</summary>
    [Fact]
    public async Task GridlessRowWrappingInTwoColumns_StaysOneRowOfOneTable()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 60, "Taryfa opłat za karty", 18, bold: true);
        double y = GridlessTariff(b, 100,
        [
            ("1.", ["Zastrzeżenie karty kredytowej"], ["jednorazowo"], "0,00 zł"),
            ("2.", ["Upomnienie w związku z opóźnieniem w spłacie", "minimalnej kwoty"], ["za każde", "upomnienie"], "15,00 zł"),
            ("3.", ["Zmiana terminu spłaty zadłużenia na karcie"], ["za każdą zmianę"], "10,00 zł"),
            ("4.", ["Restrukturyzacja zadłużenia na wniosek", "klienta"], ["jednorazowo"], "50,00 zł"),
        ]);
        Body(b, y + 30, 12);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("| 2. | Upomnienie w związku z opóźnieniem w spłacie minimalnej kwoty | za każde upomnienie | 15,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 4. | Restrukturyzacja zadłużenia na wniosek klienta | jednorazowo | 50,00 zł |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
    }

    /// <summary>T083c: a paragraph close above the bold column-name row of a gridless table is not part of the table.</summary>
    [Fact]
    public async Task ParagraphCloseAboveAGridlessHeader_IsNotATableRow()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 60, "Taryfa opłat za karty", 18, bold: true);
        double y = Body(b, 100, 5);
        b.Text(Left, y, "Poniższe opłaty są niezależne od odsetek od wykorzystanego limitu kredytowego.", Size);
        y += 13.5;
        y = GridlessTariff(b, y,
        [
            ("1.", ["Wydanie karty kredytowej"], ["jednorazowo"], "0,00 zł"),
            ("2.", ["Roczna opłata za kartę kredytową"], ["rocznie"], "99,00 zł"),
            ("3.", ["Wydanie duplikatu karty kredytowej"], ["jednorazowo"], "25,00 zł"),
        ]);
        Body(b, y + 30, 8);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("\n| **Lp.** | **Wyszczególnienie czynności** | **Tryb pobierania** | **Stawka** |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("| Poniższe opłaty", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T083d: after a gridless tariff come its notes „1) …”, the next section heading „III. …” (larger bold), a paragraph
    /// and the next table: the notes and the paragraph are text, the heading is a heading, and there are two tables.
    /// </summary>
    [Fact]
    public async Task TextBetweenTwoGridlessTables_IsNotPartOfThem()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 60, "Taryfa opłat dla firm", 18, bold: true);
        b.Text(Left, 95, "II. Mikroprzedsiębiorstwa", 13, bold: true);
        double y = GridlessTariff(b, 125,
        [
            ("1.", ["Prowadzenie rachunku bieżącego 1)"], ["miesięcznie"], "29,00 zł"),
            ("2.", ["Prowadzenie rachunku pomocniczego"], ["miesięcznie"], "5,00 zł"),
            ("3.", ["Przelew elektroniczny do innego banku"], ["za przelew"], "0,50 zł"),
        ]);
        b.Text(Left, y, "1) Opłata nie jest pobierana w pierwszych trzech miesiącach od otwarcia rachunku bieżącego", 8.5);
        b.Text(Left, y + 11, "i w miesiącach, w których wpływy na rachunek przekroczyły 10 000,00 zł.", 8.5);
        b.Text(Left, y + 22, "2) Dotyczy przelewów w złotych realizowanych w systemie bankowości elektronicznej.", 8.5);
        y += 22 + 11 + 8 + 10;
        b.Text(Left, y, "III. Małe i średnie przedsiębiorstwa", 13, bold: true);
        y += 13 * 1.35 + 9;
        b.Text(Left, y, "Do segmentu należą Klienci zatrudniający średniorocznie od 10 do 249 pracowników, których", Size);
        b.Text(Left, y + 13.5, "roczny obrót netto nie przekracza równowartości 50 milionów euro.", Size);
        y += 13.5 + 13.5 + 8 + 10;
        y = GridlessTariff(b, y,
        [
            ("4.", ["Rachunek bieżący w pakiecie MSP"], ["miesięcznie"], "39,00 zł"),
            ("5.", ["Rachunek pomocniczy (kolejny)"], ["miesięcznie"], "8,00 zł"),
        ]);
        Body(b, y + 30, 6);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083d.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("## III. Małe i średnie przedsiębiorstwa", md, StringComparison.Ordinal);
        Assert.Equal(2, md.Split('\n').Count(l => l.StartsWith("| **Lp.**", StringComparison.Ordinal)));
        Assert.Contains("| 3. | Przelew elektroniczny do innego banku | za przelew | 0,50 zł |\n\n", md, StringComparison.Ordinal);
        Assert.DoesNotContain("| Do segmentu", md, StringComparison.Ordinal);
        Assert.DoesNotContain("| 1) Opłata", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T083e: a gridless tariff continued on the next page under a repeated bold column-name row (the first row there
    /// wraps) is one GFM table with the header once and all rows in order.
    /// </summary>
    [Fact]
    public async Task GridlessTableOverTwoPages_IsOneTableWithoutTheRepeatedHeader()
    {
        var b = new SyntheticPdfBuilder().PageNumberFooter("Strona {n}");
        b.Page();
        b.Text(Left, 60, "Taryfa opłat dla firm", 18, bold: true);
        b.Text(Left, 95, "II. Mikroprzedsiębiorstwa", 13, bold: true);
        string[] mode = ["za operację"];
        var first = Enumerable.Range(1, 26).Select(i => ($"{i}.", new[] { $"Czynność bankowa numer {i} w placówce" }, mode, $"{i},00 zł")).ToArray();
        GridlessTariff(b, 125, first);
        b.Page();
        double y = GridlessTariff(b, 70,
        [
            ("27.", ["Prowadzenie rachunku pomocniczego (każdy", "kolejny)"], ["miesięcznie"], "8,00 zł"),
            ("28.", ["Prowadzenie rachunku rozliczeń VAT"], ["miesięcznie"], "0,00 zł"),
            ("29.", ["Przelew elektroniczny do innego banku"], ["za przelew"], "0,40 zł"),
        ]);
        Body(b, y + 30, 10);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083e.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, md.Split('\n').Count(l => l.StartsWith("| **Lp.**", StringComparison.Ordinal)));
        Assert.Contains("| 26. | Czynność bankowa numer 26 w placówce | za operację | 26,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 27. | Prowadzenie rachunku pomocniczego (każdy kolejny) | miesięcznie | 8,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 29. | Przelew elektroniczny do innego banku | za przelew | 0,40 zł |", md, StringComparison.Ordinal);
        Assert.Equal(1, md.Split('\n').Count(l => l.StartsWith("| --- |", StringComparison.Ordinal)));
    }

    /// <summary>
    /// T083f: the continuation page of a gridless tariff as typeset in the corpus (TAR-06 page 4): sub-positions „2.1.”,
    /// „2.2.” with wrapped service names, then „3.” wrapped, „4.” … — all rows of the one table.
    /// </summary>
    [Fact]
    public async Task GridlessContinuationPageWithSubPositions_KeepsEveryRowInTheTable()
    {
        var b = new SyntheticPdfBuilder().PageNumberFooter("{n} / 2");
        b.Page();
        b.Text(64, 60, "Taryfa opłat dla firm", 18, bold: true);
        b.Text(64, 95, "II. Mikroprzedsiębiorstwa", 13, bold: true);
        double[] x = [64, 104, 343, 440];
        void Row(double y, string no, string service, string mode, string rate, string? wrap = null)
        {
            b.Text(x[0], y, no, 9.5).Text(x[1], y, service, 9.5).Text(x[2], y, mode, 9.5).Text(x[3], y, rate, 9.5);
            if (wrap is not null)
            {
                b.Text(x[1], y + 12, wrap, 9.5);
            }
        }

        void Header(double y)
        {
            b.Text(x[0], y, "Lp.", 9.5, bold: true).Text(x[1], y, "Wyszczególnienie czynności", 9.5, bold: true)
                .Text(x[2], y, "Tryb pobierania", 9.5, bold: true).Text(x[3], y, "Stawka", 9.5, bold: true);
        }

        Header(130);
        double top = 154;
        for (int i = 1; i <= 24; i++)
        {
            Row(top, $"{i}.", $"Czynność numer {i} w placówce Banku", "za operację", "1,00 zł");
            top += 24;
        }

        Row(top, "25.", "Prowadzenie rachunku bieżącego w pakiecie dla", "za miesiąc", "19,00 zł", "mikroprzedsiębiorstw");
        b.Page();
        Header(92);
        Row(116, "25.1.", "w pierwszych trzech miesiącach od otwarcia", "miesięcznie", "bez opłat", "rachunku");
        Row(152, "25.2.", "przy wpływach na rachunek co najmniej 5 000,00 zł", "miesięcznie", "bez opłat", "w miesiącu");
        Row(188, "26.", "Prowadzenie rachunku pomocniczego (każdy", "miesięcznie", "8,00 zł", "kolejny)");
        Row(224, "27.", "Prowadzenie rachunku rozliczeń VAT", "miesięcznie", "0,00 zł");
        Row(248, "28.", "Przelew elektroniczny w złotych do innego banku", "za przelew", "0,40 zł");
        Row(272, "28.1.", "pierwsze 10 przelewów w miesiącu", "miesięcznie", "bez opłat");
        double next = 296;
        for (int i = 29; i <= 46; i++)
        {
            Row(next, $"{i}.", $"Usługa dodatkowa numer {i}", "za operację", "2,00 zł");
            next += 24;
        }

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083f.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, md.Split('\n').Count(l => l.StartsWith("| **Lp.**", StringComparison.Ordinal)));
        Assert.Contains("| 26. | Prowadzenie rachunku pomocniczego (każdy kolejny) | miesięcznie | 8,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 28.1. | pierwsze 10 przelewów w miesiącu | miesięcznie | bez opłat |", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T083g: a gridless row whose cell gaps happen to be nearly equal (33.5, 39.2, 46 pt, as in TAR-06) is a table row,
    /// not a justified line — its service cell holds several words at normal spacing.
    /// </summary>
    [Fact]
    public async Task GridlessRowWithNearlyEqualCellGaps_IsATableRow()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(64, 60, "Taryfa opłat dla firm", 18, bold: true);
        b.Text(64, 92, "Lp.", 9.5, bold: true).Text(104.8, 92, "Wyszczególnienie czynności", 9.5, bold: true)
            .Text(343.9, 92, "Tryb pobierania", 9.5, bold: true).Text(440.3, 92, "Stawka", 9.5, bold: true);
        (string No, string Service, string Mode, string Rate)[] rows =
        [
            ("1.", "Otwarcie rachunku bieżącego", "jednorazowo", "0,00 zł"),
            ("2.", "Prowadzenie rachunku rozliczeń VAT", "miesięcznie", "0,00 zł"),
            ("3.", "Prowadzenie rachunku pomocniczego (każdy", "miesięcznie", "8,00 zł"),
            ("4.", "Przelew natychmiastowy", "za przelew", "5,00 zł"),
        ];
        double y = 116;
        foreach ((string no, string service, string mode, string rate) in rows)
        {
            b.Text(64.4, y, no, 9.5).Text(104.8, y, service, 9.5).Text(343.9, y, mode, 9.5).Text(440.3, y, rate, 9.5);
            if (no == "3.")
            {
                y += 12;
                b.Text(104.8, y, "kolejny)", 9.5);
            }

            y += 24;
        }

        Body(b, y + 30, 12);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083g.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("| 3. | Prowadzenie rachunku pomocniczego (każdy kolejny) | miesięcznie | 8,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 4. | Przelew natychmiastowy | za przelew | 5,00 zł |", md, StringComparison.Ordinal);
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
