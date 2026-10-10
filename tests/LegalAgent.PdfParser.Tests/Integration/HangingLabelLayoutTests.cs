using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 007, US2 (T009–T011) — replicas of corporate regulation pages (research R1): body text 7 pt with a leading of
/// 10 pt, ustęp labels „1.” at x 39.7 with text at 53.9, point labels „1/” at 53.9 with text at 68.0, letter labels
/// „a/” at 68.0 with text at 82.2. Labels sit in a hanging column 6–10 pt before their text.
/// </summary>
public sealed class HangingLabelLayoutTests
{
    private const double Size = 7;
    private const double Margin = 39.7;
    private const double Point = 53.9;
    private const double Letter = 68.0;
    private const double LetterText = 82.2;

    private const string Opening =
        "Bank prowadzi rachunki według zasad opisanych w tym regulaminie oraz w umowie zawartej z Klientem, a także w taryfie opłat i prowizji Banku.";

    private static SyntheticPdfBuilder Item(SyntheticPdfBuilder page, double labelX, double textX, double y, string label, string text) =>
        page.Text(labelX, y, label, Size).Text(textX, y, text, Size);

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>T009 — ustęp „2.” with points „1/”, „2/”, the second wrapped to the point text column, then ustęp „3.”.</summary>
    [Fact]
    public async Task Points_with_slash_labels_nest_under_their_ustep()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        Item(page, Margin, Point, 100, "1.", "Klient może otworzyć w Banku rachunek bieżący oraz dowolną liczbę rachunków pomocniczych w złotych lub w walutach obcych,");
        page.Text(Point, 110, "jeżeli spełnia warunki określone w umowie.", Size);
        Item(page, Margin, Point, 120, "2.", "Rachunek bieżący służy do:");
        Item(page, Point, Letter, 130, "1/", "przechowywania środków pieniężnych Klienta,");
        Item(page, Point, Letter, 140, "2/", "przeprowadzania rozliczeń pieniężnych w kraju i za granicą, które wiążą się z działalnością gospodarczą Klienta, w tym");
        page.Text(Letter, 150, "rozliczeń z kontrahentami i z urzędami.", Size);
        Item(page, Margin, Point, 160, "3.", "Rachunek pomocniczy służy do wyodrębnionych rozliczeń pieniężnych Klienta.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains(
            "- 1\\. Klient może otworzyć w Banku rachunek bieżący oraz dowolną liczbę rachunków pomocniczych w złotych lub w walutach obcych, jeżeli spełnia warunki określone w umowie.\n"
            + "- 2\\. Rachunek bieżący służy do:\n"
            + "  - 1/ przechowywania środków pieniężnych Klienta,\n"
            + "  - 2/ przeprowadzania rozliczeń pieniężnych w kraju i za granicą, które wiążą się z działalnością gospodarczą Klienta, w tym rozliczeń z kontrahentami i z urzędami.\n"
            + "- 3\\. Rachunek pomocniczy służy do wyodrębnionych rozliczeń pieniężnych Klienta.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>
    /// T010 — three levels „1.” → „1/” → „a/”, then an unlabelled line at the ustęp text column (the common part of the
    /// ustęp), and a point continued at the top of the next page.
    /// </summary>
    [Fact]
    public async Task Three_levels_common_part_and_page_continuation_stay_one_list()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        Item(page, Margin, Point, 100, "1.", "Klient może złożyć dyspozycję otwarcia rachunku:");
        Item(page, Point, Letter, 110, "1/", "w placówce Banku, jeżeli:");
        Item(page, Letter, LetterText, 120, "a/", "przedstawi ważny dokument tożsamości,");
        Item(page, Letter, LetterText, 130, "b/", "podpisze dyspozycję zgodnie z kartą wzorów podpisów,");
        page.Text(Point, 140, "a Bank potwierdza przyjęcie dyspozycji na jej kopii, którą przekazuje Klientowi w dniu złożenia dyspozycji.", Size);
        Item(page, Margin, Point, 150, "2.", "Klient może złożyć dyspozycję także w systemie bankowości elektronicznej, jeżeli:");
        Item(page, Point, Letter, 160, "1/", "zawarł z Bankiem umowę o korzystanie z systemu bankowości elektronicznej,");
        Item(page, Point, Letter, 170, "2/", "posiada aktywne środki dostępu do systemu bankowości elektronicznej i stosuje się do zasad ich");

        SyntheticPdfBuilder next = builder.Page().Text(Letter, 80, "bezpiecznego przechowywania opisanych w regulaminie.", Size);
        Item(next, Margin, Point, 90, "3.", "Bank może odmówić przyjęcia dyspozycji, jeżeli Klient nie spełnia warunków określonych w umowie.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains(
            "- 1\\. Klient może złożyć dyspozycję otwarcia rachunku:\n"
            + "  - 1/ w placówce Banku, jeżeli:\n"
            + "    - a/ przedstawi ważny dokument tożsamości,\n"
            + "    - b/ podpisze dyspozycję zgodnie z kartą wzorów podpisów,\n"
            + "\n"
            + "  a Bank potwierdza przyjęcie dyspozycji na jej kopii, którą przekazuje Klientowi w dniu złożenia dyspozycji.\n"
            + "\n"
            + "- 2\\. Klient może złożyć dyspozycję także w systemie bankowości elektronicznej, jeżeli:\n"
            + "  - 1/ zawarł z Bankiem umowę o korzystanie z systemu bankowości elektronicznej,\n"
            + "  - 2/ posiada aktywne środki dostępu do systemu bankowości elektronicznej i stosuje się do zasad ich <!-- page: 2 --> bezpiecznego przechowywania opisanych w regulaminie.\n"
            + "- 3\\. Bank może odmówić przyjęcia dyspozycji, jeżeli Klient nie spełnia warunków określonych w umowie.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>
    /// T012a — a long run of ustępy „9.”–„14.” with wrapped lines and two-digit labels, then an ustęp with points „1/”
    /// and letters „a/”: four label columns on one page are still one list, not a fallback table.
    /// </summary>
    [Fact]
    public async Task Long_run_of_usteps_with_points_and_letters_is_not_a_table()
    {
        string[] sentences =
        [
            "Klient może odwołać złożoną dyspozycję pod warunkiem, że Bank potwierdzi jej odwołanie przed upływem terminu jej ważności,",
            "Strony mogą uzgodnić warunki inne niż wskazane w opisie usługi, ale muszą to wyraźnie określić przy zawieraniu umowy oraz",
            "Jeżeli przepisy wymagają rozliczenia transakcji przez izbę rozliczeniową, Strony uzgadniają wybór izby przed zawarciem",
            "Strony mogą określić warunki transakcji w sposób odmienny od opisu, jeżeli opis nie obejmuje wszystkich elementów umowy,",
            "Osobami uprawnionymi do składania dyspozycji w imieniu Klienta są osoby wskazane w karcie informacyjnej Klienta, a także",
            "Pełnomocnictwo musi być udzielone na piśmie albo w systemie bankowości elektronicznej, w sposób określony w umowie, oraz",
        ];
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        double y = 100;
        for (int i = 0; i < sentences.Length; i++)
        {
            Item(page, Margin, 54.2, y, $"{9 + i}.", sentences[i]);
            page.Text(54.6, y + 10, "zawiera wszystkie dane potrzebne do jego wykonania przez Bank.", Size);
            y += 20;
        }

        Item(page, Margin, 54.2, y, "15.", "Pełnomocnik może:");
        Item(page, Point, Letter, y + 10, "1/", "składać dyspozycje, jeżeli:");
        Item(page, Letter, LetterText, y + 20, "a/", "pełnomocnictwo obejmuje rodzaj dyspozycji,");
        Item(page, Letter, LetterText, y + 30, "b/", "dyspozycja mieści się w limicie kwoty,");
        Item(page, Point, Letter, y + 40, "2/", "odbierać potwierdzenia transakcji.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains(
            "- 14\\. Pełnomocnictwo musi być udzielone na piśmie albo w systemie bankowości elektronicznej, w sposób określony w umowie, oraz zawiera wszystkie dane potrzebne do jego wykonania przez Bank.\n"
            + "- 15\\. Pełnomocnik może:\n"
            + "  - 1/ składać dyspozycje, jeżeli:\n"
            + "    - a/ pełnomocnictwo obejmuje rodzaj dyspozycji,\n"
            + "    - b/ dyspozycja mieści się w limicie kwoty,\n"
            + "  - 2/ odbierać potwierdzenia transakcji.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>
    /// T014a — ustępy whose enumeration uses „a.”, „b.” (letter and dot, not a list label of the parser) in the hanging
    /// column: the run is not a fallback table; the enumeration stays text of the ustęp.
    /// </summary>
    [Fact]
    public async Task Letter_dot_enumeration_in_the_hanging_column_is_not_a_table()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        Item(page, Margin, 54.2, 100, "1.", "Klient może udzielić pełnomocnictwa do zawierania transakcji w imieniu Klienta osobom wskazanym w umowie,");
        page.Text(54.6, 110, "jeżeli pełnomocnicy znają zasady zawierania transakcji.", Size);
        Item(page, Margin, 54.2, 120, "2.", "Pełnomocnictwo można udzielić:");
        Item(page, 54.3, Letter, 130, "a.", "w formie pisemnej w karcie informacyjnej Klienta,");
        Item(page, 54.6, Letter, 140, "b.", "w formie elektronicznej za pośrednictwem systemu bankowości elektronicznej,");
        Item(page, 54.2, Letter, 150, "c.", "przez złożenie oświadczenia w placówce w obecności pracownika");
        page.Text(68.8, 160, "Banku.", Size);
        Item(page, Margin, 54.2, 170, "3.", "Pełnomocnictwo wygasa z chwilą jego odwołania przez Klienta albo z upływem terminu, na który je udzielono.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains("- 2\\. Pełnomocnictwo można udzielić:", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("- 3\\. Pełnomocnictwo wygasa", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(" \\| ", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>
    /// T014b — a letter „a/” whose enumeration uses lower-case Roman numerals with a dot („i.”, „ii.”, „iii.”) in the
    /// next label column (D-A p. 43): the run is not a fallback table.
    /// </summary>
    [Fact]
    public async Task Roman_dot_enumeration_under_a_letter_is_not_a_table()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        Item(page, Margin, 54.7, 100, "10.", "Płatności wychodzące:");
        Item(page, Point, Letter, 110, "1/", "Bank wykona płatność wychodzącą po:");
        Item(page, Letter, LetterText, 120, "a/", "złożeniu prawidłowego zlecenia, które zawiera:");
        Item(page, 82.8, 97.1, 130, "i.", "numer rachunku Klienta, z którego wykonujemy płatność,");
        Item(page, 82.8, 97.0, 140, "ii.", "imię i nazwisko albo nazwę beneficjenta oraz jego dane teleadresowe, które pozwalają go zidentyfikować");
        page.Text(96.7, 150, "bez wątpliwości,", Size);
        Item(page, 82.8, 97.1, 160, "iii.", "kwotę i walutę przelewu,");
        Item(page, Letter, LetterText, 170, "b/", "autoryzacji zlecenia przez Klienta,");
        Item(page, Margin, 54.7, 180, "11.", "Bank może odmówić wykonania zlecenia, jeżeli zlecenie nie zawiera danych wymaganych przez regulamin.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains("- 10\\. Płatności wychodzące:\n  - 1/ Bank wykona płatność wychodzącą po:\n    - a/ złożeniu", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("- 11\\. Bank może odmówić", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(" \\| ", result.Markdown, StringComparison.Ordinal);
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>
    /// T012 — a two-column box (bold question left, answer right with „1/” and „1.” labels 14 pt before their text, D-A
    /// p. 26) followed by ustępy with points: the labels stay in their answer cell (no fallback table) and the ustępy
    /// below the box are a list, not rows of the box.
    /// </summary>
    [Fact]
    public async Task Labels_inside_a_table_cell_stay_with_their_text()
    {
        const double question = 59.7, answer = 201.6, answerText = 216.0;
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        page.Text(answer, 105, "Klient wpisuje dane użytkownika w systemie bankowości elektronicznej.", Size);
        page.Text(question, 115, "Jak ją uruchomić?", Size, bold: true);
        Item(page, answer, answerText, 115, "1/", "e-mail, na który wyślemy identyfikator tymczasowy,");
        Item(page, answer, answerText, 125, "2/", "telefon komórkowy, na który wyślemy kod aktywacyjny.");
        Item(page, answer, answerText, 145, "1.", "Aplikacja mobilna prowadzi użytkownika krok po kroku.");
        page.Text(question, 155, "Jak ją aktywować?", Size, bold: true);
        Item(page, answer, answerText, 155, "2.", "Użytkownik, który ukończy aktywację, dostaje identyfikator stały oraz potwierdzenie");
        page.Text(answerText, 165, "w aplikacji mobilnej.", Size);
        Item(page, Margin, 54.7, 185, "12.", "Usługa jest płatna zgodnie z taryfą. Opłatę pobieramy, jeżeli Klient zarejestrował i aktywował usługę na co najmniej");
        page.Text(53.2, 195, "jednym urządzeniu mobilnym.", Size);
        Item(page, Margin, 54.7, 205, "13.", "Klient może zrezygnować z usługi:");
        Item(page, Point, Letter, 215, "1/", "w systemie bankowości elektronicznej,");
        Item(page, Point, Letter, 225, "2/", "w placówce Banku.");

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains("1/ e-mail, na który wyślemy identyfikator tymczasowy,", result.Markdown, StringComparison.Ordinal);
        Assert.Contains(
            "- 12\\. Usługa jest płatna zgodnie z taryfą. Opłatę pobieramy, jeżeli Klient zarejestrował i aktywował usługę na co najmniej jednym urządzeniu mobilnym.\n"
            + "- 13\\. Klient może zrezygnować z usługi:\n"
            + "  - 1/ w systemie bankowości elektronicznej,\n"
            + "  - 2/ w placówce Banku.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" \\| ", result.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
    }

    /// <summary>T011 — control: a fee table with three text columns and „1/” in the first column stays a GFM table.</summary>
    [Fact]
    public async Task Data_table_with_slash_numbers_in_the_first_column_stays_a_table()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page().Text(Margin, 80, Opening, Size);
        (string Label, string Service, string Fee)[] rows =
        [
            ("Lp.", "Usługa", "Opłata"),
            ("1/", "Otwarcie rachunku bieżącego", "0 zł"),
            ("2/", "Prowadzenie rachunku bieżącego", "25 zł miesięcznie"),
            ("3/", "Przelew w placówce Banku", "10 zł"),
            ("4/", "Wydanie zaświadczenia", "50 zł"),
        ];
        double y = 100;
        foreach ((string label, string service, string fee) in rows)
        {
            page.Text(Margin, y, label, Size).Text(120, y, service, Size).Text(380, y, fee, Size);
            y += 10;
        }

        page.Text(Margin, y + 10, "Opłaty pobieramy z rachunku bieżącego Klienta w dniu wykonania usługi.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains("| Lp. | Usługa | Opłata |\n", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("| 2/ | Prowadzenie rachunku bieżącego | 25 zł miesięcznie |\n", result.Markdown, StringComparison.Ordinal);
        Assert.Equal(1, result.Report.TableCount);
        Assert.Equal(0, result.Report.FallbackTableCount);
    }
}
