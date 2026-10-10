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
