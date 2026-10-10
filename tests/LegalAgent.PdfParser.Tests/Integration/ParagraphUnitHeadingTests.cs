using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 007, US1 (T018–T024) — replicas of corporate regulation pages with paragraphs „§ N” (research R2): body text
/// 7 pt with a leading of 10 pt, numbered chapters bold 9 pt at x 40, „§ N” bold 9 pt centred on the page about 24 pt
/// below the chapter, ustępy „1.” at x 39.7 with text at 53.9, points „1/” at 53.9 with text at 68.0.
/// </summary>
public sealed class ParagraphUnitHeadingTests
{
    private const double Size = 7;
    private const double HeadingSize = 9;
    private const double Margin = 39.7;
    private const double Point = 53.9;
    private const double PointText = 68.0;

    private static SyntheticPdfBuilder Item(SyntheticPdfBuilder page, double labelX, double textX, double y, string label, string text) =>
        page.Text(labelX, y, label, Size).Text(textX, y, text, Size);

    private static SyntheticPdfBuilder Centered(SyntheticPdfBuilder page, double y, string text) =>
        page.Text((595 - SyntheticPdfBuilder.TextWidth(text, HeadingSize, bold: true)) / 2, y, text, HeadingSize, bold: true);

    private static IEnumerable<Section> All(IEnumerable<Section> sections) =>
        sections.SelectMany(s => new[] { s }.Concat(All(s.Children)));

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>R1a page: two numbered chapters, each with a centred bold „§ N” and ustępy with points.</summary>
    internal static byte[] BuildR1a()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text((595 - SyntheticPdfBuilder.TextWidth("Regulamin rachunków bankowych", 14, bold: true)) / 2, 50, "Regulamin rachunków bankowych", 14, bold: true);
        page.Text(40, 80, "1. Zasady ogólne", HeadingSize, bold: true);
        Centered(page, 104, "§ 4");
        Item(page, Margin, Point, 120, "1.", "Regulamin określa zasady otwierania i prowadzenia rachunków bankowych dla przedsiębiorców oraz innych podmiotów,");
        page.Text(Point, 130, "które zawarły z Bankiem umowę rachunku.", Size);
        Item(page, Margin, Point, 140, "2.", "Bank udostępnia regulamin w placówkach oraz na swojej stronie internetowej.");
        page.Text(40, 165, "2. Rachunki bankowe oraz rachunek VAT", HeadingSize, bold: true);
        Centered(page, 189, "§ 5");
        Item(page, Margin, Point, 205, "1.", "Na podstawie umowy Klienci mogą otwierać rachunki bieżące i pomocnicze w złotych oraz w walutach obcych.");
        Item(page, Margin, Point, 215, "2.", "Rachunek bieżący służy do:");
        Item(page, Point, PointText, 225, "1/", "gromadzenia środków pieniężnych Klienta,");
        Item(page, Point, PointText, 235, "2/", "przeprowadzania rozliczeń pieniężnych związanych z działalnością gospodarczą Klienta.");
        Centered(page, 260, "§ 6");
        Item(page, Margin, Point, 276, "1.", "Klient może mieć jeden rachunek VAT do każdego rachunku bieżącego, który prowadzi dla niego Bank.");
        Item(page, Margin, Point, 286, "2.", "Bank otwiera rachunek VAT bez osobnej dyspozycji Klienta.");
        page.Text(Margin, 306, "Środki zgromadzone na rachunku VAT Bank wykorzystuje wyłącznie na cele określone w przepisach o podatku od towarów", Size);
        page.Text(Margin, 316, "i usług, a Klient nie może dysponować nimi w inny sposób, w szczególności nie może ich przekazać na inny rachunek ani", Size);
        page.Text(Margin, 326, "wypłacić w gotówce, chyba że przepisy na to pozwalają albo naczelnik urzędu skarbowego wyda na to zgodę.", Size);
        return builder.Build();
    }

    /// <summary>T018 — a centred bold „§ 5” is a unit heading printed as in the source, followed by the ustępy as a list.</summary>
    [Fact]
    public async Task Centred_bare_paragraph_is_a_unit_heading_without_added_text()
    {
        PdfConversionResult result = await ConvertAsync(BuildR1a());

        Assert.Matches(@"(?m)^#+ § 5\n\n- 1\\\. Na podstawie umowy", result.Markdown);
        Assert.Contains(
            "- 2\\. Rachunek bieżący służy do:\n"
            + "  - 1/ gromadzenia środków pieniężnych Klienta,\n"
            + "  - 2/ przeprowadzania rozliczeń pieniężnych związanych z działalnością gospodarczą Klienta.\n",
            result.Markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain("§ 5.", result.Markdown, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^#+ § 6$", result.Markdown);

        Section unit = Assert.Single(All(result.Document.Sections), s => s.Designation == "§ 5");
        Assert.Equal(("§ 5", SectionKind.Paragraph), (unit.HeadingText, unit.Kind));
    }

    /// <summary>T020 (C5) — a unit under an open numbered chapter („2. Rachunki…”) stands one level below it.</summary>
    [Fact]
    public async Task Paragraph_under_a_numbered_chapter_is_one_level_below_it()
    {
        PdfConversionResult result = await ConvertAsync(BuildR1a());

        Section chapter = Assert.Single(All(result.Document.Sections), s => s.HeadingText == "2. Rachunki bankowe oraz rachunek VAT");
        Assert.Equal(["§ 5", "§ 6"], chapter.Children.Select(c => c.Designation));
        Assert.All(chapter.Children, c => Assert.Equal(chapter.Level + 1, c.Level));
        Assert.Matches(@"(?m)^## 2\. Rachunki bankowe oraz rachunek VAT\n\n### § 5$", result.Markdown);
        Assert.Matches(@"(?m)^## 1\. Zasady ogólne\n\n### § 4$", result.Markdown);
    }

    /// <summary>
    /// T021a (D-C) — an unnumbered subheading (9 pt, regular) inside a numbered chapter stays below the chapter, and its
    /// units below the subheading; the next numbered chapter is a sibling of the first.
    /// </summary>
    [Fact]
    public async Task Subheading_inside_a_numbered_chapter_keeps_the_chapter_open()
    {
        const string Plain = "Bank świadczy usługi gotówkowe na podstawie umowy zawartej z klientem, w zakresie i na warunkach określonych w umowie oraz";
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text((595 - SyntheticPdfBuilder.TextWidth("Regulamin usług gotówkowych", 14, bold: true)) / 2, 50, "Regulamin usług gotówkowych", 14, bold: true);
        page.Text(40, 80, "1. Co znajdziesz w regulaminie", HeadingSize, bold: true);
        Centered(page, 100, "§ 1");
        page.Text(Margin, 116, Plain, Size);
        page.Text(Margin, 126, "w tym regulaminie, który klient otrzymuje przed zawarciem umowy w placówce albo w systemie bankowości elektronicznej.", Size);
        page.Text(40.7, 151, "Na jakich zasadach klient korzysta z usług", HeadingSize);
        Centered(page, 171, "§ 3");
        page.Text(Margin, 187, Plain, Size);
        page.Text(Margin, 197, "w cenniku usług, który Bank udostępnia w placówkach oraz na swojej stronie internetowej w formie elektronicznej.", Size);
        page.Text(40, 222, "2. Wpłaty zamknięte w sortowni", HeadingSize, bold: true);
        Centered(page, 242, "§ 8");
        page.Text(Margin, 258, Plain, Size);
        page.Text(Margin, 268, "w zasadach przyjmowania wpłat zamkniętych, które Bank przekazuje klientowi razem z umową i każdą zmianą tych zasad.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Matches(
            @"(?m)^## 1\. Co znajdziesz w regulaminie\n\n### § 1\n(?:.*\n)+### Na jakich zasadach klient korzysta z usług\n\n#### § 3\n(?:.*\n)+## 2\. Wpłaty zamknięte w sortowni\n\n### § 8$",
            result.Markdown);
    }

    /// <summary>
    /// T022 (R1c) — a centred bold „§ 3. Porady ogólne” above ustępy with hanging labels is one heading with the whole
    /// line; the designation is „§ 3” and the title does not go to the content.
    /// </summary>
    [Fact]
    public async Task Centred_paragraph_with_title_is_one_heading()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text((595 - SyntheticPdfBuilder.TextWidth("Zasady współpracy", 14, bold: true)) / 2, 50, "Zasady współpracy", 14, bold: true);
        Centered(page, 80, "§ 3. Porady ogólne");
        page.Text(40, 100, "Zasady udzielania porad ogólnych", Size);
        Item(page, Margin, Point, 116, "1.", "Bank może przekazywać Klientowi informacje o instrumentach finansowych, które nie są rekomendacją inwestycyjną, oraz");
        page.Text(Point, 126, "informacje o rynku, na którym Klient zawiera transakcje.", Size);
        Item(page, Margin, Point, 136, "2.", "Klient samodzielnie podejmuje decyzje o zawarciu transakcji i ponosi ich skutki, także gdy korzysta z informacji Banku.");
        page.Text(Margin, 156, "Bank nie odpowiada za decyzje Klienta podjęte na podstawie informacji, o których mowa w ust. 1, chyba że przepisy stanowią", Size);
        page.Text(Margin, 166, "inaczej albo Bank działał umyślnie, a także gdy informacje były nieaktualne z przyczyn, za które Bank odpowiada.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Matches(@"(?m)^#+ § 3\. Porady ogólne\n\nZasady udzielania porad ogólnych\n\n- 1\\\. Bank może", result.Markdown);
        Section unit = Assert.Single(All(result.Document.Sections), s => s.Kind == SectionKind.Paragraph);
        Assert.Equal(("§ 3", "§ 3. Porady ogólne"), (unit.Designation, unit.HeadingText));
        Assert.DoesNotContain(unit.Blocks, b => b is ParagraphBlock p && p.Inlines.OfType<TextRun>().Any(r => r.Text.Contains("Porady ogólne", StringComparison.Ordinal)));
    }

    /// <summary>
    /// T024 (R1b) — a centred bold „§ 5” right above a gridless two-column table (at the body leading) is a unit
    /// heading, not a row of the table; the table stays a GFM table.
    /// </summary>
    [Fact]
    public async Task Paragraph_right_above_a_gridless_table_is_not_a_table_row()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text((595 - SyntheticPdfBuilder.TextWidth("Zasady wyboru wskaźnika", 14, bold: true)) / 2, 50, "Zasady wyboru wskaźnika", 14, bold: true);
        page.Text(Margin, 80, "Bank stosuje wskaźnik alternatywny, gdy wskaźnik referencyjny przestaje być publikowany albo organ nadzoru zakaże jego", Size);
        page.Text(Margin, 90, "stosowania; o zmianie wskaźnika Bank informuje Klienta w sposób określony w umowie albo w tym regulaminie.", Size);
        Centered(page, 110, "§ 5");
        (string Event, string When)[] rows =
        [
            ("Rodzaj zdarzenia", "Od kiedy stosujemy wskaźnik alternatywny"),
            ("Wskaźnik nie jest publikowany", "od dnia, w którym wskaźnik nie został opublikowany"),
            ("Organ nadzoru zakazał stosowania", "od dnia wskazanego przez organ nadzoru"),
            ("Administrator zaprzestał publikacji", "od dnia następnego po ostatniej publikacji"),
        ];
        double y = 120;
        foreach ((string evt, string when) in rows)
        {
            bool header = y == 120;
            page.Text(Margin, y, evt, Size, bold: header).Text(250, y, when, Size, bold: header);
            y += 10;
        }

        page.Text(Margin, y + 15, "Wskaźnik alternatywny Bank stosuje do końca okresu odsetkowego, w którym wystąpiło zdarzenie, a także w kolejnych okresach.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Matches(@"(?m)^#+ § 5\n\n\| \*\*Rodzaj zdarzenia\*\* \|", result.Markdown);
        Assert.DoesNotMatch(@"\|[^\n]*§ 5", result.Markdown);
        Assert.Equal(1, result.Report.TableCount);
        Assert.Equal(0, result.Report.FallbackTableCount);
    }

    /// <summary>
    /// T025a (D-D) — a gridless table of contents „§ N. | Tytuł ....” whose row „§ 10. Tytuł ....” is one segment stays
    /// one table: a line with leader dots is an entry of the contents, not a unit.
    /// </summary>
    [Fact]
    public async Task Contents_row_with_leader_dots_is_not_a_unit_line()
    {
        string Dots(string title) => title + " " + new string('.', 120 - (2 * title.Length));
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text((595 - SyntheticPdfBuilder.TextWidth("Zasady współpracy", 14, bold: true)) / 2, 50, "Zasady współpracy", 14, bold: true);
        page.Text(40, 80, "Spis treści", HeadingSize, bold: true);
        string[] titles = ["Postanowienia ogólne", "Definicje", "Porady ogólne", "Ryzyka", "Zawieranie transakcji", "Platforma transakcyjna", "Potwierdzenie transakcji", "Rozliczanie transakcji", "Przypadki naruszenia", "Skutki naruszenia", "Przypadki rozwiązania"];
        double y = 100;
        for (int i = 0; i < titles.Length; i++)
        {
            string number = $"§ {i + 1}.";
            if (i == 9)
            {
                page.Text(40, y, number + " " + Dots(titles[i]), Size);
            }
            else
            {
                page.Text(40, y, number, Size).Text(70, y, Dots(titles[i]), Size);
            }

            y += 10;
        }

        page.Text(Margin, y + 20, "Regulamin określa zasady zawierania transakcji rynku finansowego między Bankiem a Klientem, który nie jest instytucją finansową.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.DoesNotMatch(@"(?m)^#+ § ", result.Markdown);
        Assert.Equal(1, result.Report.TableCount);
        Assert.Contains("§ 10.", result.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// T019a (D-B) — a centred bold „§ N” at the body leading right below the last line of a list item, and right below
    /// a chapter heading, is still a unit heading (centring sets it apart).
    /// </summary>
    [Fact]
    public async Task Centred_paragraph_at_body_leading_is_a_unit_heading()
    {
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text(40, 80, "Rozdział 2. Zawarcie umowy", HeadingSize, bold: true);
        Centered(page, 100, "§ 6");
        Item(page, Margin, Point, 116, "1.", "Umowę zawiera się na czas nieokreślony, w formie pisemnej albo w systemie bankowości elektronicznej.");
        Item(page, Margin, Point, 126, "2.", "Umowę zawiera się na wniosek Klienta. Wzór wniosku Bank udostępnia w placówkach oraz na stronie internetowej,");
        page.Text(54.6, 136, "a Klient składa go w placówce Banku.", Size);
        Centered(page, 146, "§ 7");
        page.Text(40.5, 156, "Integralną część umowy stanowi regulamin, który Bank przekazuje Klientowi przed zawarciem umowy.", Size);
        page.Text(40.5, 176, "Rozdział 3. Odpowiedzialność stron", HeadingSize, bold: true);
        Centered(page, 186, "§ 8");
        page.Text(40.5, 202, "Bank nie odpowiada za szkody, które powstały z przyczyn, za które odpowiedzialności nie ponosi.", Size);
        page.Text(Margin, 222, "Bank odpowiada za niewykonanie lub nienależyte wykonanie umowy na zasadach określonych w przepisach prawa, w szczególności", Size);
        page.Text(Margin, 232, "w przepisach o usługach płatniczych, a w sprawach, których przepisy nie regulują, na zasadach określonych w umowie oraz w regulaminie.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.Matches(@"(?m)^#+ § 7\n\nIntegralną część umowy", result.Markdown);
        Assert.Matches(@"(?m)^#+ § 8\n\nBank nie odpowiada", result.Markdown);
        Assert.DoesNotContain("**§", result.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    /// T018 (R1d) — negatives: „§ 5 ust. 2” starting a wrapped line, and a lone plain „§ 5” wrapped from a sentence, stay
    /// text of their paragraph.
    /// </summary>
    [Fact]
    public async Task Paragraph_references_in_running_text_are_not_headings()
    {
        const string Wide = "Bank może wypowiedzieć umowę z ważnych powodów, które opisuje regulamin, w szczególności w przypadkach wskazanych w";
        var builder = new SyntheticPdfBuilder();
        SyntheticPdfBuilder page = builder.Page();
        page.Text(40, 80, "3. Wypowiedzenie umowy", HeadingSize, bold: true);
        page.Text(Margin, 100, Wide, Size);
        page.Text(Margin, 110, "§ 5 ust. 2 oraz w umowie, z zachowaniem terminu wypowiedzenia określonego w umowie rachunku.", Size);
        page.Text(Margin, 130, Wide, Size);
        page.Text(Margin, 140, "§ 5", Size);
        page.Text(Margin, 150, "Klient może wypowiedzieć umowę w każdym czasie bez podania przyczyny, składając oświadczenie na piśmie.", Size);

        PdfConversionResult result = await ConvertAsync(builder.Build());

        Assert.DoesNotMatch(@"(?m)^#+ §", result.Markdown);
        Assert.Contains("wskazanych w § 5 ust. 2 oraz w umowie", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("wskazanych w § 5", result.Markdown, StringComparison.Ordinal);
    }
}
