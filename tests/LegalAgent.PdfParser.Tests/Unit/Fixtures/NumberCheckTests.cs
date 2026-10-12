using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Fixtures;

/// <summary>Spec 009 (US2, T003): numbers of the PDF text layer against the numbers of the Markdown.</summary>
public sealed class NumberCheckTests
{
    private const double Size = 8;
    private const double Left = 72;
    private const double Top = 100;

    /// <summary>Three pages with a „N/3” page-number footer; page 1 has „art. 66” and a small raised „1”.</summary>
    private static byte[] ArticleWithSuperscript()
    {
        var b = new SyntheticPdfBuilder().PageNumberFooter("{n}/3").Page();
        double x = Left + SyntheticPdfBuilder.TextWidth("art. 66", Size);
        b.Text(Left, Top, "art. 66", Size)
            .Text(x, Top - 3, "1", Size / 2)
            .Text(x + SyntheticPdfBuilder.TextWidth("1", Size / 2) + 2, Top, "Kodeksu cywilnego stosuje się odpowiednio.", Size);
        b.Page().Text(Left, Top, "Druga strona zawiera tylko zwykłe słowa bez żadnych liczb.", Size);
        b.Page().Text(Left, Top, "Trzecia strona także zawiera tylko zwykłe słowa.", Size);
        return b.Build();
    }

    private static async Task<ConversionReport> ReportAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, new PdfConversionRequest(), TestContext.Current.CancellationToken);
        return result.Report;
    }

    private static Dictionary<string, int> Bag(params (string Value, int Count)[] entries) =>
        entries.ToDictionary(e => e.Value, e => e.Count, StringComparer.Ordinal);

    [Fact]
    public void FromMarkdown_SkipsMarkersFootnotesAndEscapes()
    {
        string markdown = "<!-- page: 3 -->\n\n- 1\\) Tekst[^2] i **150** zł.\n\n[^2]: Przypis 7.\n";

        Assert.Equal(Bag(("1", 1), ("150", 1), ("7", 1)), NumberCheck.FromMarkdown(markdown));
    }

    [Fact]
    public void FromMarkdown_SuperscriptIsNotADigit_AndSeparatorsAreKept()
    {
        string markdown = "art. 66¹ oraz 170 103 364 zł od 01.01.2022 r., limit 150.000 i 2,5%.";

        Assert.Equal(
            Bag(("66", 1), ("170", 1), ("103", 1), ("364", 1), ("01.01.2022", 1), ("150.000", 1), ("2,5", 1)),
            NumberCheck.FromMarkdown(markdown));
    }

    /// <summary>
    /// T007 (owner's decision): a number glued to a letter in the Markdown whose value is a superscript of the PDF is an
    /// undetected footnote reference („Tak4”, „1dodaniu”), counted apart from excess; a glued full-size number („CO2”)
    /// matches the PDF; a superscript glued to a number („661”) stays excess.
    /// </summary>
    [Fact]
    public async Task Compare_GluedFootnoteReference_IsNotExcess()
    {
        var b = new SyntheticPdfBuilder().Page();
        double x = Left + SyntheticPdfBuilder.TextWidth("Tak", Size);
        b.Text(Left, Top, "Tak", Size).Text(x, Top - 3, "4", Size / 2);
        b.Text(Left, Top + 12, "emisji CO2 oraz", Size);
        x = Left + SyntheticPdfBuilder.TextWidth("oraz ", Size);
        b.Text(Left, Top + 24, "oraz ", Size).Text(x, Top + 21, "17", Size / 2)
            .Text(x + SyntheticPdfBuilder.TextWidth("17", Size / 2), Top + 24, "dodaniu skanu faktury", Size);
        byte[] pdf = b.Build();

        NumberDiff diff = NumberCheck.Compare("Tak4 emisji CO2 oraz 17dodaniu skanu faktury art. 661", pdf, await ReportAsync(pdf));

        Assert.Equal(Bag(("661", 1)), diff.Excess);
        Assert.Equal(2, diff.GluedReferences);
    }

    /// <summary>T007 (private ind-4): a list of footnote references „⁹,¹⁰” glued to a word is one glued reference list.</summary>
    [Fact]
    public async Task Compare_GluedReferenceList_IsNotExcess()
    {
        var b = new SyntheticPdfBuilder().Page();
        double x = Left + SyntheticPdfBuilder.TextWidth("transakcje BLIK", Size);
        b.Text(Left, Top, "transakcje BLIK", Size).Text(x, Top - 3, "9,10", Size / 2);
        byte[] pdf = b.Build();

        NumberDiff diff = NumberCheck.Compare("transakcje BLIK9,10", pdf, await ReportAsync(pdf));

        Assert.Empty(diff.Excess);
        Assert.Empty(diff.Missing);
        Assert.Equal(2, diff.GluedReferences);
    }

    /// <summary>
    /// Spec 010 (T013b): with the document, the digits of footnote labels and references the parser turned into
    /// <c>[^n]</c> („³kurs”, a plain label „8 Dotyczy…”) are not missing numbers; without it they are.
    /// </summary>
    [Fact]
    public async Task Compare_WithTheDocument_FootnoteLabelsAreNotMissing()
    {
        var b = new SyntheticPdfBuilder().Page();
        for (int i = 0; i < 5; i++)
        {
            b.Text(Left, Top + (i * 12), "Postanowienia regulaminu stosuje się do rachunków prowadzonych przez bank.", Size);
        }

        double x = Left + SyntheticPdfBuilder.TextWidth("Stosujemy ", Size);
        b.Text(Left, Top + 60, "Stosujemy ", Size).Text(x, Top + 57, "3", 5).Text(x + SyntheticPdfBuilder.TextWidth("3", 5), Top + 60, "kurs z momentu transakcji.", Size);
        x = Left + SyntheticPdfBuilder.TextWidth("Przelew natychmiastowy Tak", Size);
        b.Text(Left, Top + 72, "Przelew natychmiastowy Tak", Size).Text(x, Top + 69, "8", 5);
        b.HLine(40, 184, 740);
        b.Text(Left, 755 - 3, "3", 5).Text(Left + SyntheticPdfBuilder.TextWidth("3", 5), 755, " Zapis obowiązuje od 1.09.2026 r.", Size);
        b.Text(Left, 765, "8 Dotyczy przelewów w złotych.", Size);
        byte[] pdf = b.Build();
        using var stream = new MemoryStream(pdf);
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, new PdfConversionRequest(), TestContext.Current.CancellationToken);
        Assert.Contains("[^2]: Dotyczy przelewów w złotych.", result.Markdown, StringComparison.Ordinal);

        NumberDiff without = NumberCheck.Compare(result.Markdown, pdf, result.Report);
        NumberDiff with = NumberCheck.Compare(result.Markdown, pdf, result.Report, result.Document);

        Assert.NotEmpty(without.Missing);
        Assert.Empty(with.Missing);
        Assert.Empty(with.Excess);
    }

    /// <summary>
    /// Spec 010 (T025, FR-758): the text of a merged cell printed in every row it covers, and of a header group printed
    /// before every column name it spans, is counted once — with the document; without it the repeats are excess.
    /// </summary>
    [Fact]
    public async Task Compare_WithTheDocument_RepeatsOfMergedCellsAndHeaderGroupsAreNotExcess()
    {
        var b = new SyntheticPdfBuilder().Page();
        for (int i = 0; i < 5; i++)
        {
            b.Text(Left, Top + (i * 12), "Postanowienia regulaminu stosuje się do rachunków prowadzonych przez bank.", Size);
        }

        // A group row „Kanały 2026” over columns 2–3 and a first-column cell „limit 500 zł” merged over three rows.
        double[] xs = [60, 200, 330, 460];
        b.HLine(60.2, 199.8, 200).HLine(200.2, 459.8, 200).HLine(200.2, 329.8, 212).HLine(330.2, 459.8, 212);
        foreach (double y in new double[] { 230, 250, 270, 290 })
        {
            b.HLine(200.2, 329.8, y).HLine(330.2, 459.8, y);
        }

        b.HLine(60.2, 199.8, 230).HLine(60.2, 199.8, 290);
        foreach ((double top, double bottom) in new[] { (212.0, 230.0), (230.0, 250.0), (250.0, 270.0), (270.0, 290.0) })
        {
            foreach (double x in xs)
            {
                b.VLine(x, top, bottom);
            }
        }

        b.VLine(60, 200, 212).VLine(200, 200, 212).VLine(460, 200, 212);
        b.Text(65, 220, "Usługa", Size, bold: true).Text(240, 209, "Kanały 2026", Size, bold: true);
        b.Text(205, 225, "serwis", Size, bold: true).Text(335, 225, "placówka", Size, bold: true);
        b.Text(65, 263, "limit 500 zł", Size);
        string[][] values = [["TAK", "NIE"], ["NIE", "TAK"], ["TAK", "TAK"]];
        for (int r = 0; r < 3; r++)
        {
            b.Text(205, 243 + (r * 20), values[r][0], Size).Text(335, 243 + (r * 20), values[r][1], Size);
        }

        byte[] pdf = b.Build();
        using var stream = new MemoryStream(pdf);
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, new PdfConversionRequest(), TestContext.Current.CancellationToken);
        Assert.Contains("| limit 500 zł | NIE | TAK |", result.Markdown, StringComparison.Ordinal);
        Assert.Contains("**Kanały 2026** **placówka**", result.Markdown, StringComparison.Ordinal);

        NumberDiff without = NumberCheck.Compare(result.Markdown, pdf, result.Report);
        NumberDiff with = NumberCheck.Compare(result.Markdown, pdf, result.Report, result.Document);

        Assert.Equal(Bag(("500", 2), ("2026", 1)), without.Excess);
        Assert.Empty(with.Excess);
        Assert.Empty(with.Missing);
    }

    [Fact]
    public async Task FromPdf_SeparatesSuperscriptDigits_AndSkipsRemovedPageNumbers()
    {
        byte[] pdf = ArticleWithSuperscript();

        IReadOnlyDictionary<string, int>? numbers = NumberCheck.FromPdf(pdf, await ReportAsync(pdf));

        Assert.Equal(Bag(("66", 1)), numbers);
    }

    /// <summary>
    /// T007 (private corp-3 last page): small footer text far to the right of a large logo is raised above the logo's
    /// baseline, but it is not a superscript of the logo — its digits are numbers.
    /// </summary>
    [Fact]
    public async Task FromPdf_SmallRaisedDigitFarFromThePreviousLetter_IsANumber()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .Text(Left, Top, "Zwykły tekst strony bez liczb.", Size)
            .Text(40, 780, "Bank.pl", 22.7, bold: true)
            .Text(175, 772, "2. wiersz stopki z danymi banku", 6.7)
            .Build();

        IReadOnlyDictionary<string, int>? numbers = NumberCheck.FromPdf(pdf, await ReportAsync(pdf));

        Assert.Equal(Bag(("2", 1)), numbers);
    }

    [Fact]
    public async Task Compare_SuperscriptInMarkdown_HasNoDifferences()
    {
        byte[] pdf = ArticleWithSuperscript();

        NumberDiff diff = NumberCheck.Compare("art. 66¹ Kodeksu cywilnego", pdf, await ReportAsync(pdf));

        Assert.Empty(diff.Excess);
        Assert.Empty(diff.Missing);
        Assert.Null(diff.Skipped);
        Assert.Equal(1, diff.PdfCount);
        Assert.Equal(1, diff.MarkdownCount);
    }

    [Fact]
    public async Task Compare_GluedSuperscript_IsExcess_AndTheArticleNumberIsMissing()
    {
        byte[] pdf = ArticleWithSuperscript();

        NumberDiff diff = NumberCheck.Compare("art. 661 Kodeksu cywilnego", pdf, await ReportAsync(pdf));

        Assert.Equal(Bag(("661", 1)), diff.Excess);
        Assert.Equal(Bag(("66", 1)), diff.Missing);
    }

    [Fact]
    public async Task Compare_WithoutTextLayer_IsSkipped()
    {
        // The parser rejects a PDF without text (PdfNoTextException), so the report comes from another document.
        byte[] pdf = new SyntheticPdfBuilder().Page().FilledRect(72, 100, 200, 50).Build();

        NumberDiff diff = NumberCheck.Compare("", pdf, await ReportAsync(ArticleWithSuperscript()));

        Assert.False(string.IsNullOrEmpty(diff.Skipped));
        Assert.Empty(diff.Excess);
    }

    private static NumberDiff Diff(Dictionary<string, int> excess, Dictionary<string, int>? missing = null) =>
        new(excess, missing ?? [], 0, 0, null);

    [Fact]
    public void RegulationGate_FailsOnlyListedDocuments_MinusAllowedNumbers()
    {
        string list = "mbank-a\nmbank-b;0,0\n# komentarz\n\nmbank-c   # zielony\n";
        var diffs = new Dictionary<string, NumberDiff>(StringComparer.Ordinal)
        {
            ["mbank-a"] = Diff(Bag(("682", 1), ("661", 1))),
            ["mbank-b"] = Diff(Bag(("0", 2))),
            ["mbank-c"] = Diff([], Bag(("5", 3))),
            ["mbank-d"] = Diff(Bag(("9", 1))),
        };

        IReadOnlyList<string> failures = RegulationGate.Evaluate(list, diffs);

        Assert.Equal(["mbank-a: nadmiarowe 661×1, 682×1"], failures);
    }

    [Fact]
    public void RegulationGate_AllowedNumberCoversOneOccurrence()
    {
        var diffs = new Dictionary<string, NumberDiff>(StringComparer.Ordinal) { ["mbank-b"] = Diff(Bag(("0", 3), ("1", 1))) };

        Assert.Equal(["mbank-b: nadmiarowe 0×1"], RegulationGate.Evaluate("mbank-b;0,0,1", diffs));
    }

    [Fact]
    public void RegulationGate_ListedDocumentWithoutResult_Fails()
    {
        IReadOnlyList<string> failures = RegulationGate.Evaluate("mbank-x", new Dictionary<string, NumberDiff>());

        string failure = Assert.Single(failures);
        Assert.Contains("mbank-x", failure, StringComparison.Ordinal);
        Assert.Contains("no such PDF", failure, StringComparison.Ordinal);
    }
}
