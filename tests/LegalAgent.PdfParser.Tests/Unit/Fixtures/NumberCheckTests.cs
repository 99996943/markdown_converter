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
