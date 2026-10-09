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
    /// T083h: a gridless tariff starting at the bottom of a page with only its bold column-name row and one row (TAR-06
    /// page 3), continued on the next page under the repeated header: one GFM table, not a bold paragraph.
    /// </summary>
    [Fact]
    public async Task GridlessTableStartingWithOneRowAtPageBottom_IsOneTableWithItsContinuation()
    {
        var b = new SyntheticPdfBuilder().PageNumberFooter("Strona {n}");
        b.Page();
        b.Text(Left, 60, "Taryfa opłat dla firm", 18, bold: true);
        double y = Body(b, 100, 40);
        b.Text(Left, y, "II. Mikroprzedsiębiorstwa", 13, bold: true);
        y += 26;
        b.Text(Left, y, "Do segmentu mikroprzedsiębiorstw należą Klienci zatrudniający mniej niż 10 pracowników.", Size);
        y += 22;
        GridlessTariff(b, y, [("1.", ["Otwarcie rachunku bieżącego"], ["jednorazowo"], "0,00 zł")]);
        b.Page();
        y = GridlessTariff(b, 70,
        [
            ("2.", ["Prowadzenie rachunku bieżącego"], ["miesięcznie"], "29,00 zł"),
            ("3.", ["Prowadzenie rachunku pomocniczego"], ["miesięcznie"], "5,00 zł"),
            ("4.", ["Przelew elektroniczny do innego banku"], ["za przelew"], "0,50 zł"),
        ]);
        Body(b, y + 30, 10);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083h.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain("**Lp. Wyszczególnienie", md, StringComparison.Ordinal);
        Assert.Equal(1, md.Split('\n').Count(l => l.StartsWith("| **Lp.**", StringComparison.Ordinal)));
        Assert.Contains("| 1. | Otwarcie rachunku bieżącego | jednorazowo | 0,00 zł |", md, StringComparison.Ordinal);
        Assert.Contains("| 4. | Przelew elektroniczny do innego banku | za przelew | 0,50 zł |", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T083i: a gridless tariff continued on the next page with only the repeated column-name row and one row (TAR-09
    /// page 18), followed there by its notes, the next section heading, a paragraph and the next table: the notes and
    /// the paragraph are text, the heading is a heading, and the row joins the table of the previous page.
    /// </summary>
    [Fact]
    public async Task GridlessContinuationWithOneRowThenTheNextSection_EndsAfterThatRow()
    {
        var b = new SyntheticPdfBuilder().PageNumberFooter("Strona {n}");
        b.Page();
        b.Text(Left, 60, "Taryfa opłat dla młodzieży", 18, bold: true);
        b.Text(Left, 95, "V. Karty", 13, bold: true);
        string[] mode = ["za operację"];
        var first = Enumerable.Range(1, 26).Select(i => ($"{i}.", new[] { $"Czynność bankowa numer {i} w placówce" }, mode, $"{i},00 zł")).ToArray();
        GridlessTariff(b, 125, first);
        b.Page();
        double y = GridlessTariff(b, 70, [("27.", ["Przeliczenie transakcji w walucie obcej według", "Tabeli kursów walut"], ["od kwoty"], "1,5%")]);
        b.Text(Left, y, "1) Opłata nie jest pobierana w miesiącu, w którym wartość transakcji bezgotówkowych wykonanych kartą", 8.5);
        b.Text(Left, y + 11, "wyniosła co najmniej kwotę wskazaną w opisie sekcji.", 8.5);
        y += 11 + 8 + 30;
        b.Text(Left, y, "VI. Przelewy", 13, bold: true);
        y += 24;
        b.Text(Left, y, "Dzienne limity przelewów małoletniego wynikają ze zgody przedstawiciela ustawowego i nie mogą", Size);
        b.Text(Left, y + 13.5, "przekroczyć kwot wskazanych w Taryfie.", Size);
        y += 13.5 + 22;
        y = GridlessTariff(b, y,
        [
            ("28.", ["Przelew wewnętrzny w Banku"], ["za operację"], "bez opłat"),
            ("29.", ["Przelew krajowy w złotych do innego banku"], ["za operację"], "0,00 zł"),
            ("30.", ["Przelew natychmiastowy w złotych"], ["za operację"], "1,00 zł"),
        ]);
        Body(b, y + 30, 6);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t083i.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("## VI. Przelewy", md, StringComparison.Ordinal);
        Assert.Contains("| 27. | Przeliczenie transakcji w walucie obcej według Tabeli kursów walut | od kwoty | 1,5% |", md, StringComparison.Ordinal);
        Assert.Equal(2, md.Split('\n').Count(l => l.StartsWith("| **Lp.**", StringComparison.Ordinal)));
        Assert.DoesNotContain("| 1) Opłata", md, StringComparison.Ordinal);
        Assert.DoesNotContain("| Dzienne limity", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T089b: a chapter title wrapped over two lines under „Rozdział 6” (REG-06 page 10) is one heading with the whole
    /// title, followed by the unit „§ 21.”.
    /// </summary>
    [Fact]
    public async Task ChapterTitleWrappedOverTwoLines_IsOneHeading()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(Left, 60, "Regulamin rachunków bankowych dla przedsiębiorców", 18, bold: true);
        double y = Body(b, 100, 8);
        b.Text(Left, y + 20, "Rozdział 6", 12.5, bold: true);
        b.Text(Left, y + 37.5, "Przelewy, w tym podzielona płatność i przelewy do urzędu", 12.5, bold: true);
        b.Text(Left, y + 55, "skarbowego", 12.5, bold: true);
        b.Text(286, y + 82.5, "§ 21.", 11, bold: true);
        Body(b, y + 104, 8);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("## Rozdział 6. Przelewy, w tym podzielona płatność i przelewy do urzędu skarbowego\n", md, StringComparison.Ordinal);
        Assert.DoesNotContain("**skarbowego**", md, StringComparison.Ordinal);
        Assert.Contains("### § 21.", md, StringComparison.Ordinal);
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

    /// <summary>
    /// T083j: the last row of a gridless tariff has nearly equal cell gaps (TAR-03 row 121, wrapped service name) and is
    /// followed by the next section heading: the row stays the last row of the table.
    /// </summary>
    [Fact]
    public async Task LastGridlessRowWithNearlyEqualCellGaps_StaysInTheTable()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(64, 60, "Taryfa opłat dla firm", 18, bold: true);
        b.Text(64, 92, "Lp.", 9.5, bold: true).Text(104, 92, "Wyszczególnienie czynności", 9.5, bold: true)
            .Text(343, 92, "Tryb pobierania", 9.5, bold: true).Text(440, 92, "Stawka", 9.5, bold: true);
        (string No, string Service, string Wrap, string Mode, string Rate)[] rows =
        [
            ("118.", "Wpłata gotówki w walucie obcej na rachunek", "przedsiębiorcy z tytułu dewizowego utargu", "od kwoty wpłaty", "0,25% kwoty"),
            ("119.", "Wpłata i wypłata gotówki w tej samej walucie", "tego samego dnia", "od kwoty operacji", "2,5% kwoty"),
            ("120.", "Przeliczenie wpłaty walutowej niezgodnej", "z deklaracją klienta", "za operację", "bez opłat"),
            ("121.", "Złożenie oświadczenia o pochodzeniu środków", "przy wpłacie dewizowej", "za oświadczenie", "bez opłat"),
        ];
        double y = 116;
        foreach ((string no, string service, string wrap, string mode, string rate) in rows)
        {
            b.Text(64, y, no, 9.5).Text(104, y, service, 9.5).Text(343, y, mode, 9.5).Text(440, y, rate, 9.5);
            b.Text(104, y + 12, wrap, 9.5);
            y += 36;
        }

        b.Text(64, y + 36, "V. Wymiana i liczenie wartości pieniężnych", 13, bold: true);
        Body(b, y + 60, 8);

        string md = await MarkdownAsync(b.Build());

        Assert.Contains("| 121. | Złożenie oświadczenia o pochodzeniu środków przy wpłacie dewizowej | za oświadczenie | bez opłat |", md, StringComparison.Ordinal);
        Assert.Contains("## V. Wymiana i liczenie wartości pieniężnych", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T087: two columns (REG-02 page 2) — the left one opens with „Rozdział 1” and its title in a larger bold font,
    /// the right one continues a numbered list in body text whose baselines fall between the heading lines: the
    /// heading stays whole and the list item keeps its wrapped line.
    /// </summary>
    [Fact]
    public async Task TwoColumnsWithAChapterHeadingBesideBodyLines_KeepBothColumnsApart()
    {
        var b = new SyntheticPdfBuilder().Page();
        b.Text(50, 42, "Regulamin kart debetowych dla klientów indywidualnych", 7.5);
        b.Text(50, 92, "Rozdział 1", 12, bold: true);
        b.Text(50, 108, "Postanowienia ogólne", 12, bold: true);
        b.Text(158, 134, "§ 1.", 10.5, bold: true);
        string[] left =
        [
            "1. Regulamin określa zasady wydawania",
            "i używania kart debetowych wydawanych przez",
            "Bank osobom fizycznym niebędącym",
            "przedsiębiorcami, a także prawa i obowiązki",
            "stron umowy o kartę debetową.",
            "2. Karta debetowa jest instrumentem płatniczym,",
            "który umożliwia dysponowanie środkami",
            "zgromadzonymi na rachunku płatniczym, do",
            "którego została wydana. Transakcje wykonane",
            "kartą obciążają ten rachunek bez udzielania",
            "kredytu.",
        ];
        double y = 155;
        foreach (string line in left)
        {
            b.Text(line.Length > 2 && char.IsDigit(line[0]) ? 50 : 68, y, line, 9.5);
            y += 12.5;
        }

        (double Y, double X, string Text)[] right =
        [
            (92, 313, "5)"), (92, 331, "Taryfa — obowiązująca w Banku taryfa opłat"),
            (104, 331, "i prowizji;"),
            (120, 313, "6)"), (120, 331, "Placówka — punkt Banku obsługujący"),
            (132, 331, "Klientów;"),
            (148, 313, "7)"), (148, 331, "Bankowość elektroniczna — usługa"),
            (160, 331, "umożliwiająca dostęp do rachunku i składanie"),
            (173, 331, "dyspozycji przez Internet lub aplikację mobilną;"),
            (188, 313, "8)"), (188, 331, "Infolinia — telefoniczny punkt obsługi"),
            (201, 331, "Klientów, dostępny pod numerem 800 000 001;"),
            (216, 313, "9)"), (216, 331, "Instrument płatniczy — zindywidualizowane"),
            (229, 331, "urządzenie lub zestaw procedur, za pomocą"),
            (242, 331, "którego Klient składa zlecenie płatnicze;"),
        ];
        foreach ((double ry, double rx, string text) in right)
        {
            b.Text(rx, ry, text, 9.5);
        }

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t087.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("## Rozdział 1. Postanowienia ogólne\n", md, StringComparison.Ordinal);
        Assert.Contains("5\\) Taryfa — obowiązująca w Banku taryfa opłat i prowizji;", md, StringComparison.Ordinal);
        Assert.Contains("6\\) Placówka — punkt Banku obsługujący Klientów;", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T087b: two columns of numbered clauses whose baselines are offset by half a line (REG-02): the body leading is the
    /// line spacing within a column, so the interleaved lines are neither a table nor broken paragraphs.
    /// </summary>
    [Fact]
    public async Task TwoColumnsWithOffsetBaselines_AreTwoColumnsOfClauses()
    {
        var b = new SyntheticPdfBuilder().Page().Page();
        string[] words = ["Bank", "wydaje", "kartę", "na", "wniosek", "Klienta", "po", "zawarciu", "umowy", "rachunku", "płatniczego", "w", "placówce", "albo", "przez", "Internet"];
        foreach ((double x, double top, int first) in new[] { (50.0, 92.0, 1), (313.0, 98.0, 31) })
        {
            double y = top;
            int n = first;
            for (int k = 0; k < 10; k++, n++)
            {
                for (int line = 0; line < 4; line++)
                {
                    string text = string.Join(' ', Enumerable.Range(0, 6).Select(i => words[(n + line + i) % words.Length]));
                    if (line == 0)
                    {
                        b.Text(x, y, $"{n}.", 9.5).Text(x + 18, y, text, 9.5);
                    }
                    else
                    {
                        b.Text(x + 18, y, line == 3 ? text + "." : text, 9.5);
                    }

                    y += 12.5;
                }
            }
        }

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t087b.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
        Assert.DoesNotContain("| ", md, StringComparison.Ordinal);
        Assert.All(md.Split('\n').Where(l => l.Length > 0 && !l.StartsWith("<!--", StringComparison.Ordinal)), l => Assert.StartsWith("- ", l, StringComparison.Ordinal));
        Assert.Equal(20, md.Split('\n').Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^- \d+\\\. ")));
    }

    /// <summary>
    /// T087c: page 2 of REG-02 (two columns, a centred „§ 3.” in the right one): the gutter is the gap between the
    /// columns, not a band reaching into the right column where only the centred unit label lies beyond it.
    /// </summary>
    [Fact]
    public async Task TwoColumnPageWithACentredUnitInTheRightColumn_IsReadAsTwoColumns()
    {
        var b = new SyntheticPdfBuilder().Page().Page();
        b.Text(50.0, 92.0, "Rozdział 1", 12.0, bold: true);
        b.Text(313.0, 92.0, "5)", 9.5);
        b.Text(331.0, 92.0, "Taryfa", 9.5, bold: true);
        b.Text(363.7, 92.0, "— obowiązująca w Banku taryfa opłat", 9.5);
        b.Text(331.0, 104.5, "i prowizji;", 9.5);
        b.Text(50.0, 108.2, "Postanowienia", 12.0, bold: true);
        b.Text(141.4, 108.2, "ogólne", 12.0, bold: true);
        b.Text(313.0, 120.0, "6)", 9.5);
        b.Text(331.0, 120.0, "Placówka", 9.5, bold: true);
        b.Text(378.4, 120.0, "— punkt Banku obsługujący", 9.5);
        b.Text(331.0, 132.5, "Klientów;", 9.5);
        b.Text(157.6, 134.4, "§ 1.", 10.5, bold: true);
        b.Text(313.0, 148.0, "7)", 9.5);
        b.Text(331.0, 148.0, "Bankowość", 9.5, bold: true);
        b.Text(387.2, 148.0, "elektroniczna", 9.5, bold: true);
        b.Text(454.5, 148.0, "— usługa", 9.5);
        b.Text(50.0, 154.6, "1. Regulamin określa zasady wydawania", 9.5);
        b.Text(331.0, 160.5, "umożliwiająca dostęp do rachunku i składanie", 9.5);
        b.Text(68.0, 167.1, "i używania kart debetowych wydawanych", 9.5);
        b.Text(253.9, 167.1, "przez", 9.5);
        b.Text(331.0, 173.0, "dyspozycji przez Internet lub aplikację mobilną;", 9.5);
        b.Text(68.0, 179.6, "Bank Przykładowy S.A. (zwany dalej „Bankiem”)", 9.5);
        b.Text(313.0, 188.5, "8)", 9.5);
        b.Text(331.0, 188.5, "Infolinia", 9.5, bold: true);
        b.Text(373.3, 188.5, "— telefoniczny punkt obsługi", 9.5);
        b.Text(68.0, 192.1, "osobom fizycznym niebędącym", 9.5);
        b.Text(331.0, 201.0, "Klientów, dostępny pod numerem", 9.5);
        b.Text(485.1, 201.0, "800 000 001;", 9.5);
        b.Text(68.0, 204.6, "przedsiębiorcami, a także prawa i obowiązki", 9.5);
        b.Text(313.0, 216.5, "9)", 9.5);
        b.Text(331.0, 216.5, "Instrument płatniczy", 9.5, bold: true);
        b.Text(433.1, 216.5, "— zindywidualizowane", 9.5);
        b.Text(68.0, 217.1, "stron umowy o kartę debetową.", 9.5);
        b.Text(331.0, 229.0, "urządzenie lub zestaw procedur, za pomocą", 9.5);
        b.Text(50.0, 232.6, "2. Karta debetowa jest instrumentem płatniczym,", 9.5);
        b.Text(331.0, 241.5, "którego Klient składa zlecenie płatnicze;", 9.5);
        b.Text(68.0, 245.1, "który umożliwia dysponowanie środkami", 9.5);
        b.Text(313.0, 257.0, "10)", 9.5);
        b.Text(331.7, 257.0, "Uwierzytelnienie", 9.5, bold: true);
        b.Text(414.4, 257.0, "— procedura umożliwiająca", 9.5);
        b.Text(68.0, 257.6, "zgromadzonymi", 9.5);
        b.Text(142.8, 257.6, "na rachunku płatniczym, do", 9.5);
        b.Text(331.7, 269.5, "Bankowi weryfikację tożsamości Klienta lub", 9.5);
        b.Text(68.0, 270.1, "którego została wydana. Transakcje wykonane", 9.5);
        b.Text(331.7, 282.0, "ważności instrumentu płatniczego;", 9.5);
        b.Text(68.0, 282.6, "kartą obciążają ten rachunek bez udzielania", 9.5);
        b.Text(68.0, 295.1, "kredytu, a jej użycie jest ograniczone do", 9.5);
        b.Text(313.0, 297.5, "11)", 9.5);
        b.Text(331.7, 297.5, "Dyspozycja", 9.5, bold: true);
        b.Text(386.8, 297.5, "— oświadczenie woli Klienta", 9.5);
        b.Text(68.0, 307.6, "wysokości dostępnych środków i ustalonych", 9.5);
        b.Text(331.7, 310.0, "dotyczące produktu Banku.", 9.5);
        b.Text(103.4, 316.5, "1", 5.7);
        b.Text(68.0, 320.1, "limitów.", 9.5);
        b.Text(420.6, 334.5, "§ 3.", 10.5, bold: true);
        b.Text(50.0, 335.6, "3. W zakresie nieuregulowanym", 9.5);
        b.Text(201.9, 335.6, "w Regulaminie", 9.5);
        b.Text(68.0, 348.1, "mają zastosowanie postanowienia umowy", 9.5);
        b.Text(313.0, 354.7, "Ponadto, na potrzeby stosowania Regulaminu,", 9.5);
        b.Text(68.0, 360.6, "rachunku, do którego wydano kartę, a także", 9.5);
        b.Text(313.0, 367.2, "poszczególne określenia oznaczają:", 9.5);
        b.Text(68.0, 373.1, "ustawa z dnia 19 sierpnia 2011 r. o usługach", 9.5);
        b.Text(68.0, 385.6, "płatniczych (Dz. U. 2024 poz. 30). W razie", 9.5);
        b.Text(313.0, 387.7, "1)", 9.5);
        b.Text(331.0, 387.7, "Karta", 9.5, bold: true);
        b.Text(359.5, 387.7, "— karta debetowa wydana przez Bank,", 9.5);
        b.Text(68.0, 398.1, "rozbieżności między Regulaminem", 9.5);
        b.Text(224.9, 398.1, "a umową", 9.5);
        b.Text(331.0, 400.2, "w formie plastikowej lub wirtualna, powiązana", 9.5);
        b.Text(68.0, 410.6, "szczególne znaczenie mają postanowienia", 9.5);
        b.Text(331.0, 412.7, "z rachunkiem Posiadacza;", 9.5);
        b.Text(68.0, 423.1, "umowy o kartę debetową.", 9.5);
        b.Text(313.0, 428.2, "2)", 9.5);
        b.Text(331.0, 428.2, "Posiadacz", 9.5, bold: true);
        b.Text(379.9, 428.2, "— konsument, która zawarła", 9.5);
        b.Text(50.0, 438.6, "4. Regulamin jest przekazywany Klientowi przed", 9.5);
        b.Text(331.0, 440.7, "z Bankiem umowę", 9.5);
        b.Text(415.3, 440.7, "rachunku i umowę", 9.5);
        b.Text(500.9, 440.7, "o kartę;", 9.5);
        b.Text(68.0, 451.1, "zawarciem umowy na trwałym nośniku,", 9.5);
        b.Text(313.0, 456.2, "3)", 9.5);
        b.Text(331.0, 456.2, "Użytkownik", 9.5, bold: true);
        b.Text(389.7, 456.2, "— osoba wskazana przez", 9.5);
        b.Text(68.0, 463.6, "w placówce oraz na stronie", 9.5);
        b.Text(331.0, 468.7, "Posiadacza we wniosku, której Bank wydał", 9.5);
        b.Text(68.0, 476.1, "https://bank.example. Klient ma prawo", 9.5);
        b.Text(331.0, 481.2, "kartę i która jest uprawniona do jej używania;", 9.5);
        b.Text(68.0, 488.6, "w każdym czasie otrzymać jego bezpłatnego", 9.5);
        b.Text(313.0, 496.7, "4)", 9.5);
        b.Text(331.0, 496.7, "PIN", 9.5, bold: true);
        b.Text(350.8, 496.7, "— poufny, zindywidualizowany numer", 9.5);
        b.Text(68.0, 501.1, "odpisu.", 9.5);
        b.Text(331.0, 509.2, "identyfikacyjny, służący do autoryzacji", 9.5);
        b.Text(331.0, 521.7, "transakcji;", 9.5);
        b.Text(157.6, 525.6, "§ 2.", 10.5, bold: true);
        b.Text(313.0, 537.2, "5)", 9.5);
        b.Text(331.0, 537.2, "Organizacja płatnicza", 9.5, bold: true);
        b.Text(435.2, 537.2, "— podmiot, którego", 9.5);
        b.Text(50.0, 545.8, "Użyte w Regulaminie określenia oznaczają:", 9.5);
        b.Text(331.0, 549.7, "zasady określają sposób przetwarzania", 9.5);
        b.Text(331.0, 562.2, "transakcji kartowych i rozliczeń między", 9.5);
        b.Text(50.0, 566.2, "1)", 9.5);
        b.Text(68.0, 566.2, "Bank", 9.5, bold: true);
        b.Text(94.5, 566.2, "— Bank Przykładowy S.A. z siedzibą pod", 9.5);
        b.Text(331.0, 574.7, "bankami;", 9.5);
        b.Text(68.0, 578.8, "adresem ul. Przykładowa 1, 00-001 Warszawa,", 9.5);
        b.Text(313.0, 590.2, "6)", 9.5);
        b.Text(331.0, 590.2, "Akceptant", 9.5, bold: true);
        b.Text(382.6, 590.2, "— przedsiębiorca przyjmujący", 9.5);
        b.Text(68.0, 591.2, "wpisany do rejestru przedsiębiorców pod", 9.5);
        b.Text(331.0, 602.7, "płatności kartą, w tym przez terminal, stronę", 9.5);
        b.Text(68.0, 603.8, "numerem", 9.5);
        b.Text(114.6, 603.8, "KRS 0000000000, NIP 000-000-00-00;", 9.5);
        b.Text(331.0, 615.2, "internetową lub aplikację;", 9.5);
        b.Text(50.0, 619.2, "2)", 9.5);
        b.Text(68.0, 619.2, "Klient", 9.5, bold: true);
        b.Text(98.4, 619.2, "— osoba fizyczna lub inny podmiot, który", 9.5);
        b.Text(313.0, 630.7, "7)", 9.5);
        b.Text(331.0, 630.7, "Transakcja bezgotówkowa", 9.5, bold: true);
        b.Text(459.4, 630.7, "— płatność kartą", 9.5);
        b.Text(68.0, 631.8, "zawarł z Bankiem umowę;", 9.5);
        b.Text(331.0, 643.2, "za towary lub usługi, wykonana w punkcie", 9.5);
        b.Text(50.0, 647.2, "3)", 9.5);
        b.Text(68.0, 647.2, "Dzień roboczy", 9.5, bold: true);
        b.Text(136.4, 647.2, "— dzień od poniedziałku do", 9.5);
        b.Text(331.0, 655.7, "handlowym, przez Internet lub w inny sposób", 9.5);
        b.Text(68.0, 659.8, "piątku, z wyłączeniem dni ustawowo wolnych", 9.5);
        b.Text(331.0, 668.2, "na odległość;", 9.5);
        b.Text(68.0, 672.2, "od pracy;", 9.5);
        b.Text(313.0, 683.7, "8)", 9.5);
        b.Text(331.0, 683.7, "Transakcja gotówkowa", 9.5, bold: true);
        b.Text(443.1, 683.7, "— wypłata lub wpłata", 9.5);
        b.Text(50.0, 687.8, "4)", 9.5);
        b.Text(68.0, 687.8, "Trwały nośnik", 9.5, bold: true);
        b.Text(136.6, 687.8, "— nośnik umożliwiający", 9.5);
        b.Text(331.0, 696.2, "gotówki w bankomacie, wpłatomacie albo", 9.5);
        b.Text(68.0, 700.2, "przechowywanie informacji w sposób dostępny", 9.5);
        b.Text(331.0, 708.7, "u akceptanta oferującego taką usługę;", 9.5);
        b.Text(68.0, 712.8, "do późniejszego wykorzystania i odtworzenia", 9.5);
        b.Text(313.0, 724.2, "9)", 9.5);
        b.Text(331.0, 724.2, "Transakcja zbliżeniowa", 9.5, bold: true);
        b.Text(442.8, 724.2, "— transakcja", 9.5);
        b.Text(68.0, 725.2, "bez zmian;", 9.5);
        b.Text(331.0, 736.7, "inicjowana przez zbliżenie karty lub urządzenia", 9.5);
        b.Text(50.0, 769.0, "1) Zasady prowadzenia rachunku, do którego wydano kartę, określa odrębny regulamin rachunków; nie wyłącza on stosowania", 8.0);
        b.Text(61.0, 779.0, "Regulaminu kart debetowych w zakresie obsługi karty.", 8.0);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t087c.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain("\\|", md, StringComparison.Ordinal);
        Assert.Contains("## Rozdział 1. Postanowienia ogólne\n", md, StringComparison.Ordinal);
        Assert.Contains("### § 3.", md, StringComparison.Ordinal);
    }

    /// <summary>
    /// T087d: two columns (REG-02 pages 3–4) — clause „1.” with sub-points „1)” … „3)” ends the right column, clause „2.”
    /// opens the left column of the next page: labels are compared within their column, so both clauses are items of
    /// one list and the sub-points nest under „1.”.
    /// </summary>
    [Fact]
    public async Task ClauseContinuedFromTheRightColumnToTheNextPage_StaysInTheList()
    {
        var b = new SyntheticPdfBuilder().Page().Page().Page();
        static void Filler(SyntheticPdfBuilder b, double x, double y, int lines)
        {
            for (int i = 0; i < lines; i++)
            {
                b.Text(x, y + (i * 12.5), "Treść postanowienia regulaminu w kolumnie strony,", 9.5);
            }
        }

        Filler(b, 50, 92, 50);
        b.Text(421, 92, "§ 6.", 10.5, bold: true);
        b.Text(313, 112, "1.", 9.5).Text(331, 112, "Bank udostępnia klientom indywidualnym", 9.5);
        b.Text(331, 124.5, "następujące rodzaje kart debetowych, różniące", 9.5);
        b.Text(331, 137, "się zakresem funkcji, formą i okresem ważności:", 9.5);
        b.Text(331, 152.5, "1)", 9.5).Text(349, 152.5, "karta fizyczna — karta plastikowa", 9.5);
        b.Text(349, 165, "z układem elektronicznym (chipem);", 9.5);
        b.Text(331, 180.5, "2)", 9.5).Text(349, 180.5, "karta wirtualna — karta bez postaci", 9.5);
        b.Text(349, 193, "fizycznej;", 9.5);
        b.Text(331, 208.5, "3)", 9.5).Text(349, 208.5, "karta dla osoby niepełnoletniej.", 9.5);
        Filler(b, 331, 240, 40);
        b.Page();
        b.Text(50, 92, "2.", 9.5).Text(68, 92, "Karta jest wydawana na okres wskazany", 9.5);
        b.Text(68, 104.5, "na awersie karty.", 9.5);
        b.Text(50, 120, "3.", 9.5).Text(68, 120, "Bank może zmienić rodzaje wydawanych", 9.5);
        b.Text(68, 132.5, "kart w trybie zmiany Regulaminu.", 9.5);
        Filler(b, 50, 160, 45);
        Filler(b, 313, 92, 50);

        string md = await MarkdownAsync(b.Build());
        if (Environment.GetEnvironmentVariable("PROBE_OUT") is { } probe)
        {
            await File.WriteAllTextAsync(Path.Combine(probe, "t087d.md"), md, TestContext.Current.CancellationToken);
        }

        Assert.Contains("- 1\\. Bank udostępnia klientom indywidualnym następujące rodzaje kart debetowych, różniące się zakresem funkcji, formą i okresem ważności:\n", md, StringComparison.Ordinal);
        Assert.Contains("  - 1\\) karta fizyczna — karta plastikowa z układem elektronicznym (chipem);\n", md, StringComparison.Ordinal);
        Assert.Contains("- 2\\. Karta jest wydawana na okres wskazany na awersie karty.\n", md, StringComparison.Ordinal);
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
