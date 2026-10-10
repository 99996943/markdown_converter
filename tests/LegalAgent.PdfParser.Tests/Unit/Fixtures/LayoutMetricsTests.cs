using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Fixtures;

/// <summary>T004 — layout measures of spec 007 (research R4) on short Markdown strings.</summary>
public sealed class LayoutMetricsTests
{
    private static ConversionWarning Warning(string code) => new(code, 1, "message");

    [Fact]
    public void Tbl001_counts_ambiguous_grid_warnings_only()
    {
        ConversionWarning[] warnings = [Warning("TBL001_AmbiguousGrid"), Warning("TBL001_AmbiguousGrid"), Warning("PDF001_NoTextLayer")];

        Assert.Equal(2, LayoutMetrics.Measure("Tekst.\n", warnings).Tbl001);
    }

    [Fact]
    public void PipeRows_counts_lines_with_an_escaped_cell_separator()
    {
        const string markdown = "1/ \\| tekst punktu\nzwykły tekst\n| a | b |\n2/ \\| drugi \\| trzeci\n";

        Assert.Equal(2, LayoutMetrics.Measure(markdown, []).PipeRows);
    }

    [Theory]
    [InlineData("1/ tekst punktu", 1)]
    [InlineData("a/ tekst litery", 1)]
    [InlineData("12a/ tekst", 1)]
    [InlineData("2\\. tekst ustępu", 1)]
    [InlineData("**3.** tekst ustępu", 1)]
    [InlineData("**1/** \\| tekst", 1)]
    [InlineData("- 1/ tekst punktu", 0)]
    [InlineData("  - a/ tekst litery", 0)]
    [InlineData("### 2. Rachunki bankowe", 0)]
    [InlineData("7/2017 z dnia", 0)]
    [InlineData("Klient/Klienci mogą", 0)]
    [InlineData("1/", 0)]
    public void LooseLabelRows_counts_label_lines_outside_lists(string line, int expected)
    {
        Assert.Equal(expected, LayoutMetrics.Measure(line + "\n", []).LooseLabelRows);
    }

    [Theory]
    [InlineData("§ 5", 1)]
    [InlineData("**§ 5**", 1)]
    [InlineData("**§ 3. Porady ogólne**", 1)]
    [InlineData("§ 12a.", 1)]
    [InlineData("- 3\\. Zawiadamiamy pisemnie. <!-- page: 8 --> **§ 20**", 1)]
    [InlineData("#### § 5", 0)]
    [InlineData("### § 3. Porady ogólne", 0)]
    [InlineData("Zgodnie z § 5 ust. 2 Klient może", 0)]
    [InlineData("§ 5 ust. 2 stosuje się odpowiednio do rachunków pomocniczych i rachunku VAT.", 0)]
    public void ParagraphText_counts_paragraph_designations_outside_headings(string line, int expected)
    {
        Assert.Equal(expected, LayoutMetrics.Measure(line + "\n", []).ParagraphText);
    }

    [Theory]
    [InlineData("## Postanowienia ogólne ........ 3", 1)]
    [InlineData("### 2. Rachunki bankowe . . . . . 12", 1)]
    [InlineData("## Spis treści… 4", 1)]
    [InlineData("## Postanowienia ogólne", 0)]
    [InlineData("Postanowienia ogólne ........ 3", 0)]
    public void TocHeadings_counts_headings_ending_with_leader_dots_and_a_number(string line, int expected)
    {
        Assert.Equal(expected, LayoutMetrics.Measure(line + "\n", []).TocHeadings);
    }

    [Theory]
    [InlineData("2/36", 1)]
    [InlineData("tekst ustępu 3/36", 1)]
    [InlineData("4/36 5/36 6/36 przypis", 3)]
    [InlineData("| 3/36 | opłata |", 0)]
    [InlineData("ustawa 7/2017", 0)]
    [InlineData("13/9", 0)]
    public void PageFooters_counts_page_of_pages_numbers_outside_tables(string line, int expected)
    {
        Assert.Equal(expected, LayoutMetrics.Measure(line + "\n", []).PageFooters);
    }

    [Fact]
    public void Measure_takes_the_warnings_of_a_report()
    {
        var report = new ConversionReport(1, [], [], new Dictionary<int, int>(), 0, 0, 0, 0, 0, [Warning("TBL001_AmbiguousGrid")], TimeSpan.Zero);

        Assert.Equal(new LayoutMeasures(1, 0, 0, 0, 0, 0), LayoutMetrics.Measure("Tekst.\n", report));
    }
}
