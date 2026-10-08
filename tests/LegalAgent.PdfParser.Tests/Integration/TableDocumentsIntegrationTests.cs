using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 002 — table-documents end to end on the synthetic promotion terms (<c>regulamin-promocji-tabela</c>): a table
/// with a full grid over pages 2–5, section names on the left, their content on the right, cells crossing pages.
/// </summary>
public sealed partial class TableDocumentsIntegrationTests
{
    private const string Promotion = "regulamin-promocji-tabela";

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf, Action<PdfParserOptions>? configure = null)
    {
        using var stream = new MemoryStream(pdf);
        var request = new PdfConversionRequest { ConfigureOptions = configure };
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, request, TestContext.Current.CancellationToken);
    }

    /// <summary>Blocks of the Markdown separated by blank lines.</summary>
    private static List<string> Blocks(string markdown) =>
        markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(b => b.Trim()).ToList();

    [GeneratedRegex(@"^## (.+)$", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();

    // ---------------------------------------------------------------- US1 (T017)

    [Fact]
    public async Task SectionNames_AreLevel2HeadingsInOrder_WithoutTablesOrTheColumnNameRow()
    {
        DocumentTruth truth = BankingCorpusGenerator.Truth(Promotion);
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        // The cover (title block, caption) is US3 (FR-088, FR-093); here: the headings from the start of the table on.
        string body = md[md.IndexOf("## " + truth.SectionNames[0], StringComparison.Ordinal)..];
        Assert.Equal(truth.SectionNames, SectionHeading().Matches(body).Select(m => m.Groups[1].Value.Trim()));
        Assert.DoesNotContain(body.Split('\n'), l => l.StartsWith('|'));
        Assert.All(truth.HeaderRowWords, w => Assert.DoesNotContain(w, md, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParagraphBrokenByAPageBoundary_IsOneParagraphWithThePageMarker()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        string paragraph = Assert.Single(Blocks(md), b => b.Contains("Warunki promocyjne terminala", StringComparison.Ordinal));
        Assert.Contains("<!-- page: 4 -->", paragraph, StringComparison.Ordinal);
        Assert.EndsWith("w każdej placówce Banku.", paragraph, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulletBrokenByAPageBoundary_IsOneItemWithThePageMarker()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        string block = Assert.Single(Blocks(md), b => b.Contains("Jeśli spełnisz wszystkie warunki", StringComparison.Ordinal));
        Assert.Matches(@"(?m)^- Jeśli spełnisz wszystkie warunki[^\n]*(\n(?!- )[^\n]*)*<!-- page: 5 -->(\n(?!- )[^\n]*|[^\n])*w aplikacji mobilnej\.", block);
    }

    [Fact]
    public async Task SubBulletsSetInAnotherFont_AreNestedUnderTheirBullet()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        Assert.Matches(@"(?m)^- 1 zł netto miesięcznie[^\n]*\n  - pakiet Komfort – księgowość uproszczona,\n  - pakiet Start – księgowość pełna,", md);
        Assert.Matches(@"(?m)^- możliwość zamówienia terminala POS:\n  - 0 zł przez 24 miesiące od dnia podpisania umowy,\n  - 0 zł za instalację i aktywację terminala,", md);
    }

    [Fact]
    public async Task Definitions_AreSeparateParagraphs_AndAWebAddressKeepsItsHyphen()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;
        List<string> blocks = Blocks(md);

        Assert.Contains("Bank – Bank Przykładowy S.A.", blocks);
        Assert.Contains("Rachunek bieżący – Rachunek Firmowy Standard", blocks);
        Assert.Contains("Regulamin promocji – ten regulamin", blocks);
        Assert.Contains("Terminal POS – urządzenie do przyjmowania płatności kartami", blocks);
        Assert.Contains("Kod rabatowy – e-kod na zakup pierścienia płatniczego", blocks);
        Assert.Contains("https://example.org/products/pierscien-platniczy-mastercard", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_HasTableDocumentSections_AndTheReportDescribesTheTableDocument()
    {
        PdfConversionResult result = await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion));

        List<Section> sections = Flatten(result.Document.Sections).Where(s => s.Kind == SectionKind.TableDocumentSection).ToList();
        Assert.Equal(BankingCorpusGenerator.Truth(Promotion).SectionNames.Count, sections.Count);
        TableDocumentSummary summary = Assert.Single(result.Report.TableDocuments);
        Assert.Equal(new TableDocumentSummary(2, 5, 6, "Definicje | Wyjaśnienie", 3), summary);
        Assert.Equal(0, result.Report.TableCount);
    }

    // ---------------------------------------------------------------- T018: wide word gaps, link underlines, lowered names

    private static readonly string[] GapLines =
    [
        "w EUR (SEPA)|do krajów|strefy euro|realizujemy w",
        "ciągu jednego|dnia roboczego|od momentu|przyjęcia",
    ];

    private const string GapSentence = "w EUR (SEPA) do krajów strefy euro realizujemy w ciągu jednego dnia roboczego od momentu przyjęcia zlecenia.";

    private static List<string> WrapWords(string text, double width)
    {
        var lines = new List<string>();
        string line = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && SyntheticPdfBuilder.TextWidth(candidate, 10) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        lines.Add(line);
        return lines;
    }

    private const string LongA = "Przelew krajowy w złotych zlecony w dniu roboczym do godziny granicznej jest realizowany jeszcze tego samego dnia, a zlecony po tej godzinie następnego dnia roboczego. Dyspozycję możesz złożyć w serwisie transakcyjnym, w aplikacji mobilnej lub w placówce, a o jej statusie informujemy w historii rachunku oraz w powiadomieniach.";

    private const string LongB = "Przelew zlecony w dniu wolnym od pracy traktujemy tak, jakby wpłynął w pierwszym dniu roboczym. Aby uniknąć opóźnień, sprawdź poprawność numeru rachunku odbiorcy i tytułu przelewu przed zatwierdzeniem dyspozycji, ponieważ po jej wykonaniu nie możemy cofnąć środków bez zgody odbiorcy.";

    private static byte[] WideGapsDocument()
    {
        var b = new SyntheticPdfBuilder().Title("Regulamin przelewów");

        // Page 1: two rows. Names sit 1 pt lower than the content line beside them.
        b.Page();
        double y = 89;
        double top = 72;
        var edges = new List<double> { top };

        void Name(double yy, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                b.Text(60, yy + 1 + (i * 15), words[i], 10, bold: true);
            }
        }

        // Row 1: plain paragraph.
        Name(y, "Przelewy", "krajowe");
        foreach (string l in WrapWords(LongA, 343))
        {
            b.Text(186, y, l, 10);
            y += 15;
        }

        edges.Add(y - 15 + 8);
        y = edges[^1] + 17;

        // Row 2: wide gaps lined up like false columns, a link line with three underlines and a gray box, a bullet.
        double rowStart = y;
        Name(y, "Przelewy", "walutowe");
        double[] xs = [186, 272, 358, 444];
        foreach (string line in GapLines)
        {
            string[] parts = line.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                b.Text(xs[i], y, parts[i], 10);
            }

            y += 15;
        }

        const string Tail = "zlecenia. Kursy walut publikujemy w tabeli dostępnej pod adresem https://example.org/przelewy/waluty/tabela-kursow oraz formularz zlecenia przelewu zagranicznego i regulamin usługi przelewów walutowych";
        foreach (string l in WrapWords(Tail, 343))
        {
            if (l.Contains("https", StringComparison.Ordinal))
            {
                b.FilledRect(186, y - 9, 230, 12, 225);
            }

            b.Text(186, y, l, 10);
            b.HLine(204, 474, y + 2, 0.5);
            y += 15;
        }

        b.Text(190, y, "•", 10);
        b.Text(208, y, "opłaty za przelewy zagraniczne zgodne z aktualną taryfą opłat i prowizji Banku.", 10);
        y += 15;
        _ = rowStart;
        edges.Add(y - 15 + 8);

        Frame(b, edges, 1);

        // Page 2: one more row with a paragraph and a bullet list.
        b.Page();
        y = 89;
        edges = [72];
        Name(y, "Zasady", "bezpieczeństwa");
        foreach (string l in WrapWords(LongB, 343))
        {
            b.Text(186, y, l, 10);
            y += 15;
        }

        b.Text(190, y, "•", 10);
        b.Text(208, y, "nie udostępniaj nikomu danych do logowania ani kodów autoryzacyjnych,", 10);
        y += 15;
        b.Text(190, y, "•", 10);
        b.Text(208, y, "korzystaj wyłącznie z oficjalnej aplikacji i strony Banku.", 10);
        y += 15;
        edges.Add(y - 15 + 8);
        Frame(b, edges, 2);

        return b.Build();

        static void Frame(SyntheticPdfBuilder pb, List<double> es, int number)
        {
            foreach (double e in es)
            {
                pb.HLine(55, 181, e, 0.75);
                pb.HLine(181, 541, e, 0.75);
            }

            foreach (double x in new[] { 54.0, 181.0, 541.0 })
            {
                pb.VLine(x, 72, es[^1], 0.75);
            }

            pb.Text(517, 804, number + "/2", 8);
        }
    }

    [Fact]
    public async Task WideWordGapsAndLinkUnderlines_StayContinuousTextWithoutTablesOrWarnings()
    {
        PdfConversionResult result = await ConvertAsync(WideGapsDocument());
        string md = result.Markdown;
        string flat = md.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\n', ' ');

        TableDocumentSummary summary = Assert.Single(result.Report.TableDocuments);
        Assert.Equal(0, result.Report.FallbackTableCount);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
        Assert.DoesNotContain(md.Split('\n'), l => l.StartsWith('|'));
        Assert.DoesNotContain(" \\| ", md, StringComparison.Ordinal);
        Assert.Contains(GapSentence, flat, StringComparison.Ordinal);
        Assert.Contains("## Przelewy walutowe", md, StringComparison.Ordinal);
        Assert.Contains("## Przelewy krajowe", md, StringComparison.Ordinal);
        Assert.Contains("## Zasady bezpieczeństwa", md, StringComparison.Ordinal);
        Assert.Contains("formularz zlecenia przelewu zagranicznego i regulamin usługi przelewów walutowych", flat, StringComparison.Ordinal);
        Assert.Equal((1, 2, 3), (summary.FirstPage, summary.LastPage, summary.SectionCount));
    }

    [Fact]
    public async Task BulletsUnderAParagraph_KeepTheirOrderInTheLastRow()
    {
        string md = (await ConvertAsync(WideGapsDocument())).Markdown;

        Assert.Matches(@"(?m)^- nie udostępniaj nikomu[^
]*
- korzystaj wyłącznie z oficjalnej aplikacji", md);
    }

    private static IEnumerable<Section> Flatten(IEnumerable<Section> sections) =>
        sections.SelectMany(s => new[] { s }.Concat(Flatten(s.Children)));

    // ---------------------------------------------------------------- US3 (T028)

    [GeneratedRegex(@"^#+ (.+)$", RegexOptions.Multiline)]
    private static partial Regex AnyHeading();

    [Fact]
    public async Task OnlyTheTitleAndSectionNames_AreHeadings()
    {
        DocumentTruth truth = BankingCorpusGenerator.Truth(Promotion);
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;

        List<string> expected = ["Regulamin promocji „Konto firmowe z korzyściami – edycja 1”", .. truth.SectionNames];
        Assert.Equal(expected, AnyHeading().Matches(md).Select(m => m.Groups[1].Value.Trim()));
    }

    [Fact]
    public async Task SubtitlesAndTheStatementsAfterTheTable_AreBoldParagraphsOfTheLastSections()
    {
        string md = (await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion))).Markdown;
        List<string> blocks = Blocks(md);

        Assert.Contains("**Nie możesz uczestniczyć w promocji, jeśli:**", blocks);
        Assert.Contains("**Korzyści obowiązujące przez pierwsze 24 miesiące od dnia otwarcia rachunku bieżącego:**", blocks);
        string tail = md[md.IndexOf("## Jak możesz złożyć reklamację", StringComparison.Ordinal)..];
        Assert.Matches(@"\*\*MOJE OŚWIADCZENIA\*\*\s+- 1\\\) Wiem,[^\n]*\n- 2\\\) Otrzymałem", tail);
        Assert.EndsWith("data, miejsce i podpis Uczestnika promocji", md.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidityLineAndImageCaption_AreParagraphsOfThePreamble()
    {
        PdfConversionResult result = await ConvertAsync(BankingCorpusGenerator.Pdf(Promotion));

        List<string> preamble = result.Document.Preamble.OfType<ParagraphBlock>()
            .Select(p => string.Concat(p.Inlines.OfType<TextRun>().Select(r => r.Text)).Trim())
            .ToList();
        Assert.Equal(["Obowiązuje od 01.09.2026 r. do 30.11.2026 r.", "bank.example"], preamble);
    }
}
