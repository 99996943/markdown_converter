using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 007, US3 (T030–T031) — replica of a glossary of a corporate regulation (research R3): an introductory sentence
/// at x 39.7 crossing the column boundary 181.4; per entry a bold label at x 45.4 and a bold term at x 59.5 set
/// vertically in the middle of its definition at x 187.1 (enumerations „a/” at 187.1 with text at 201.3); horizontal
/// rulings split at the boundary (39.7–181.4 and 181.4–555.6) under each entry but the last, no vertical rulings.
/// </summary>
public sealed class GlossaryLayoutTests
{
    private const double Size = 7;
    private const double Margin = 39.7;
    private const double Boundary = 181.4;
    private const double Right = 555.6;
    private const double LabelX = 45.4;
    private const double TermX = 59.5;
    private const double DefinitionX = 187.1;
    private const double DefinitionTextX = 201.3;

    private static SyntheticPdfBuilder SplitRuling(SyntheticPdfBuilder page, double y) =>
        page.HLine(Margin, Boundary, y).HLine(Boundary, Right, y);

    private static SyntheticPdfBuilder Centered(SyntheticPdfBuilder page, double y, string text, double size, bool bold) =>
        page.Text((595 - SyntheticPdfBuilder.TextWidth(text, size, bold: bold)) / 2, y, text, size, bold: bold);

    internal static byte[] BuildGlossary(string[] labels)
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        Centered(page, 40, "Regulamin rachunku bankowego", 14, bold: true);
        page.Text(40.5, 70, "Rozdział 1. Postanowienia ogólne", 9, bold: true);
        Centered(page, 90, "§ 1", 9, bold: true);
        page.Text(Margin, 106, "Regulamin określa zasady, na których Bank otwiera i prowadzi rachunki bankowe dla przedsiębiorców oraz innych podmiotów,", Size);
        page.Text(Margin, 116, "które zawarły z Bankiem umowę rachunku bankowego, a także zasady korzystania z systemu bankowości elektronicznej.", Size);
        Centered(page, 136, "§ 2", 9, bold: true);
        page.Text(40.5, 152, "Definicje określeń, które występują w regulaminie:", Size);

        // Entry 1: definition with an enumeration, term in the middle.
        page.Text(DefinitionX, 168, "osoba fizyczna, którą Klient wskazał w umowie rachunku bankowego. Może ona w imieniu Klienta:", Size);
        page.Text(LabelX, 178, labels[0], Size, bold: true).Text(TermX, 178, "administrator (kontroler)", Size, bold: true);
        page.Text(DefinitionX, 178, "a/", Size).Text(DefinitionTextX, 178, "zarządzać uprawnieniami użytkowników systemu bankowości elektronicznej,", Size);
        page.Text(DefinitionX, 188, "b/", Size).Text(DefinitionTextX, 188, "uzyskiwać informacje o realizacji umowy,", Size);
        SplitRuling(page, 194);

        // Entry 2: one line.
        page.Text(LabelX, 206, labels[1], Size, bold: true).Text(TermX, 206, "Bank", Size, bold: true)
            .Text(DefinitionX, 206, "Bank Przykładowy S.A.; w tym regulaminie używamy także zwrotów typu „my” (np. „prowadzimy”),", Size);
        SplitRuling(page, 212);

        // Entry 3: a term wrapped over two lines beside a three-line definition; no ruling below the last entry.
        page.Text(DefinitionX, 224, "umowa bieżącego lub pomocniczego rachunku bankowego zawarta między Bankiem a Klientem na podstawie", Size);
        page.Text(LabelX, 229, labels[2], Size, bold: true).Text(TermX, 229, "umowa rachunku", Size, bold: true);
        page.Text(DefinitionX, 234, "regulaminu; na jej podstawie Bank prowadzi dla Klienta rachunek bankowy o numerze podanym", Size);
        page.Text(TermX, 239, "bankowego", Size, bold: true);
        page.Text(DefinitionX, 244, "w umowie.", Size);

        Centered(page, 270, "§ 3", 9, bold: true);
        page.Text(Margin, 286, "Bank otwiera rachunek po zawarciu umowy i przekazuje Klientowi jego numer w sposób uzgodniony w umowie, a w razie zmiany", Size);
        page.Text(Margin, 296, "numeru informuje o niej Klienta z wyprzedzeniem, chyba że zmiana wynika z przepisów prawa albo decyzji właściwego organu.", Size);
        return builder.Build();
    }

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>T030 — every definition is one list item „- 1/ **termin** definicja…” with its enumeration nested.</summary>
    [Fact]
    public async Task Glossary_with_slash_labels_is_a_list_of_definitions()
    {
        PdfConversionResult result = await ConvertAsync(BuildGlossary(["1/", "2/", "3/"]));

        Assert.Contains(
            "Definicje określeń, które występują w regulaminie:\n\n"
            + "- 1/ **administrator (kontroler)** osoba fizyczna, którą Klient wskazał w umowie rachunku bankowego. Może ona w imieniu Klienta:\n"
            + "  - a/ zarządzać uprawnieniami użytkowników systemu bankowości elektronicznej,\n"
            + "  - b/ uzyskiwać informacje o realizacji umowy,\n"
            + "- 2/ **Bank** Bank Przykładowy S.A.; w tym regulaminie używamy także zwrotów typu „my” (np. „prowadzimy”),\n"
            + "- 3/ **umowa rachunku bankowego** umowa bieżącego lub pomocniczego rachunku bankowego zawarta między Bankiem a Klientem na podstawie regulaminu; na jej podstawie Bank prowadzi dla Klienta rachunek bankowy o numerze podanym w umowie.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.Equal(0, result.Report.TableCount);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
    }

    /// <summary>T030 — the same glossary with „1.” labels (D-C, D-D).</summary>
    [Fact]
    public async Task Glossary_with_dot_labels_is_a_list_of_definitions()
    {
        PdfConversionResult result = await ConvertAsync(BuildGlossary(["1.", "2.", "3."]));

        Assert.Contains("- 1\\. **administrator (kontroler)** osoba fizyczna, którą Klient wskazał", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("  - b/ uzyskiwać informacje o realizacji umowy,\n- 2\\. **Bank** Bank Przykładowy S.A.;", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("- 3\\. **umowa rachunku bankowego** umowa bieżącego", result.Markdown, StringComparison.Ordinal);
        Assert.Equal(0, result.Report.TableCount);
    }

    /// <summary>T031 — control: a ruled tariff with an „Lp.” column („1.”, „2.”) stays a GFM table.</summary>
    [Fact]
    public async Task Ruled_tariff_with_numbered_rows_stays_a_table()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        Centered(page, 40, "Taryfa opłat", 14, bold: true);
        page.Text(Margin, 70, "Bank pobiera opłaty i prowizje według stawek z tej taryfy, w dniu wykonania usługi albo w terminie wskazanym w umowie.", Size);
        (string Lp, string Service, string Fee)[] rows =
        [
            ("Lp.", "Usługa", "Stawka"),
            ("1.", "Otwarcie rachunku bieżącego", "0 zł"),
            ("2.", "Prowadzenie rachunku bieżącego", "25 zł miesięcznie"),
            ("3.", "Przelew w placówce", "10 zł"),
        ];
        double top = 84;
        double[] columns = [Margin, 70, 290, Right];
        for (int i = 0; i < rows.Length; i++)
        {
            double y = top + (i * 14);
            page.HLine(Margin, Right, y).Text(45.4, y + 10, rows[i].Lp, Size, bold: i == 0)
                .Text(75, y + 10, rows[i].Service, Size, bold: i == 0).Text(295, y + 10, rows[i].Fee, Size, bold: i == 0);
        }

        double bottom = top + (rows.Length * 14);
        page.HLine(Margin, Right, bottom);
        foreach (double x in columns)
        {
            page.VLine(x, top, bottom);
        }

        page.Text(Margin, bottom + 20, "Opłaty za usługi niewymienione w taryfie Bank uzgadnia z Klientem indywidualnie przed wykonaniem usługi.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Contains("| **Lp.** | **Usługa** | **Stawka** |\n", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("| 2. | Prowadzenie rachunku bieżącego | 25 zł miesięcznie |\n", result.Markdown, StringComparison.Ordinal);
        Assert.Equal(1, result.Report.TableCount);
        Assert.Equal(0, result.Report.FallbackTableCount);
    }
}
