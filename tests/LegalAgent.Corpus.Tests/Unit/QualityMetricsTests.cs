using LegalAgent.Corpus.Tests.Corpus;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Tests.Unit;

public sealed class QualityMetricsTests
{
    private const string Id = "REG-03";

    // Normalised to LF so that the string replacements below do not depend on the checkout's line endings.
    private static readonly string PerfectMarkdown = Perfect.Replace("\r\n", "\n", StringComparison.Ordinal);

    private const string Perfect = """
        <!-- page: 1 -->

        ## Rozdział 1. Postanowienia ogólne

        ### § 1.

        Bank udziela kredytu.

        - 1\) Pierwszy punkt
          - a\) Drugi punkt

        | **Usługa** | **Opłata** |
        | --- | --- |
        | 4\.7\. Przelew | 10,00 zł |
        | 4\.8\. Wypłata | 5,00 zł |
        """;

    private static DocumentTruth PerfectTruth()
    {
        DocumentTruth t = new();
        t.Words.AddRange("Rozdział 1 Postanowienia ogólne § 1. Bank udziela kredytu. 1) Pierwszy punkt a) Drugi punkt Usługa Opłata 4.7. Przelew 10,00 zł 4.8. Wypłata 5,00 zł".Split(' '));
        t.Headings.Add(new TruthHeading(2, "Rozdział 1", "Postanowienia ogólne"));
        t.Headings.Add(new TruthHeading(3, "§ 1.", ""));
        t.ListItems.Add(new TruthListItem("1)", 0, "Pierwszy punkt"));
        t.ListItems.Add(new TruthListItem("a)", 1, "Drugi punkt"));
        t.Tables.Add(new TruthTable(["Usługa", "Opłata"], [["4.7. Przelew", "10,00 zł"], ["4.8. Wypłata", "5,00 zł"]]));
        t.Artifacts.Add("Strona 1 z 2");
        return t;
    }

    private static QualityReport Measure(string markdown, DocumentTruth? truth = null) =>
        QualityMetrics.Measure(Id, truth ?? PerfectTruth(), markdown);

    [Fact]
    public void Perfect_AllMetricsAreOne()
    {
        QualityReport r = Measure(PerfectMarkdown);

        Assert.Equal(Id, r.DocumentId);
        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(0, r.ExtraWords);
        Assert.Empty(r.ExtraWordSamples);
        Assert.Equal(1.0, r.ReadingOrder);
        Assert.Equal(1.0, r.HeadingRecall);
        Assert.Equal(0.0, r.FalseHeadingShare);
        Assert.Equal(1.0, r.ListRecall);
        Assert.Equal(1.0, r.RowsIntact);
        Assert.Equal(1.0, r.TablesAsSingleGfm);
        Assert.Equal(1.0, r.CellAgreement);
        Assert.Empty(r.Failures);
    }

    [Fact]
    public void EmptyTruthAndMarkdown_SharesAreOne()
    {
        QualityReport r = QualityMetrics.Measure(Id, new DocumentTruth(), "");

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(1.0, r.ReadingOrder);
        Assert.Equal(1.0, r.HeadingRecall);
        Assert.Equal(1.0, r.ListRecall);
        Assert.Equal(1.0, r.RowsIntact);
        Assert.Equal(1.0, r.TablesAsSingleGfm);
        Assert.Equal(1.0, r.CellAgreement);
        Assert.Equal(0, r.ExtraWords);
    }

    [Fact]
    public void MissingWord_LowersCompletenessOnly()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("Bank udziela kredytu.", "Bank udziela.", StringComparison.Ordinal));

        Assert.True(r.WordCompleteness < 1.0);
        Assert.True(r.WordCompleteness > 0.95);
        Assert.Equal(0, r.ExtraWords);
        Assert.True(r.ReadingOrder < 1.0);
    }

    [Fact]
    public void AddedWords_AreCountedAndSampled()
    {
        QualityReport r = Measure(PerfectMarkdown + "\n\nStrona 2 z 5\n");

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(4, r.ExtraWords);
        Assert.Contains("Strona", r.ExtraWordSamples);
        Assert.Contains(r.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("Strona", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtraWordSamples_AreCappedAtTwenty()
    {
        string extra = string.Join(' ', Enumerable.Range(0, 50).Select(i => "obce" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        QualityReport r = Measure(PerfectMarkdown + "\n\n" + extra + "\n");

        Assert.Equal(50, r.ExtraWords);
        Assert.Equal(20, r.ExtraWordSamples.Count);
    }

    [Fact]
    public void EscapedLabelsAndPageMarkersInTheMiddleOfALine_AreIgnored()
    {
        string md = PerfectMarkdown.Replace("Bank udziela kredytu.", "Bank udziela <!-- page: 2 --> kredytu.", StringComparison.Ordinal);

        QualityReport r = Measure(md);

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(0, r.ExtraWords);
        Assert.Equal(1.0, r.ReadingOrder);
    }

    [Fact]
    public void FootnotesAndEmphasis_AreNormalised()
    {
        DocumentTruth t = new();
        t.Words.AddRange(["Oprocentowanie", "wynosi", "5%", "rocznie.¹", "¹", "Przypis", "tekst"]);

        QualityReport r = QualityMetrics.Measure(Id, t, "**Oprocentowanie** wynosi *5%* rocznie.¹\n\n[^1]: ¹ Przypis tekst\n");

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(0, r.ExtraWords);
    }

    [Fact]
    public void SwappedParagraphs_LowerReadingOrderButNotCompleteness()
    {
        DocumentTruth t = new();
        t.Words.AddRange(["Alfa", "beta", "gamma", "Delta", "epsilon", "zeta"]);

        QualityReport r = QualityMetrics.Measure(Id, t, "Delta epsilon zeta\n\nAlfa beta gamma\n");

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(0.5, r.ReadingOrder);
    }

    [Fact]
    public void HeadingWithJoinedChapterLabel_IsFound()
    {
        DocumentTruth t = new();
        t.Headings.Add(new TruthHeading(2, "Rozdział 1", "Postanowienia ogólne"));

        QualityReport r = QualityMetrics.Measure(Id, t, "## Rozdział 1. Postanowienia ogólne\n");

        Assert.Equal(1.0, r.HeadingRecall);
        Assert.Equal(0.0, r.FalseHeadingShare);
    }

    [Fact]
    public void HeadingLevelsAreCompared_RelativeToTheLevelsPresent()
    {
        // Truth {2,3} versus Markdown {1,3}: ranks 0,1 on both sides.
        QualityReport ok = Measure(PerfectMarkdown.Replace("## Rozdział", "# Rozdział", StringComparison.Ordinal));
        Assert.Equal(1.0, ok.HeadingRecall);

        // The paragraph sign printed at the same level as the chapter: ranks differ.
        QualityReport wrong = Measure(PerfectMarkdown.Replace("### § 1.", "## § 1.", StringComparison.Ordinal));
        Assert.Equal(0.5, wrong.HeadingRecall);
        Assert.Equal(0.0, wrong.FalseHeadingShare);
        Assert.Contains(wrong.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("§ 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingHeading_IsReportedWithDocumentAndLabel()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("### § 1.", "§ 1.", StringComparison.Ordinal));

        Assert.Equal(0.5, r.HeadingRecall);
        Assert.Contains($"{Id}: nagłówek '§ 1.' nie znaleziony", r.Failures);
    }

    [Fact]
    public void FalseHeading_IsCounted()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("Bank udziela kredytu.", "## Bank udziela kredytu.", StringComparison.Ordinal));

        Assert.Equal(1.0, r.HeadingRecall);
        Assert.Equal(1.0 / 3.0, r.FalseHeadingShare, 6);
        Assert.Contains(r.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("Bank udziela kredytu", StringComparison.Ordinal));
    }

    [Fact]
    public void ListItemWithWrongDepth_IsNotRecalled()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("  - a\\)", "- a\\)", StringComparison.Ordinal));

        Assert.Equal(0.5, r.ListRecall);
    }

    [Fact]
    public void MissingListItem_IsReported()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("  - a\\) Drugi punkt\n", "", StringComparison.Ordinal));

        Assert.Equal(0.5, r.ListRecall);
        Assert.Contains(r.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("a)", StringComparison.Ordinal));
    }

    [Fact]
    public void ListItemWithAWrongLabel_IsNotRecalled()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("- 1\\)", "- 7\\)", StringComparison.Ordinal));

        Assert.Equal(0.5, r.ListRecall);
    }

    [Theory]
    [InlineData("- □ Dowód osobisty")]
    [InlineData("- Dowód osobisty")]
    public void ChecklistBoxRenderedAsAListMarker_IsNotAMissingWord(string line)
    {
        DocumentTruth t = new();
        t.Words.AddRange(["□", "Dowód", "osobisty"]);
        t.ListItems.Add(new TruthListItem("□", 0, "Dowód osobisty"));

        QualityReport r = QualityMetrics.Measure(Id, t, line + "\n");

        Assert.Equal(1.0, r.WordCompleteness);
        Assert.Equal(0, r.ExtraWords);
    }

    [Theory]
    [InlineData("- □ Dowód osobisty")]
    [InlineData("- Dowód osobisty")]
    [InlineData("- [ ] Dowód osobisty")]
    public void ChecklistItem_MatchesRegardlessOfTheLabel(string line)
    {
        DocumentTruth t = new();
        t.ListItems.Add(new TruthListItem("□", 0, "Dowód osobisty"));

        QualityReport r = QualityMetrics.Measure(Id, t, line + "\n");

        Assert.Equal(1.0, r.ListRecall);
    }

    [Fact]
    public void RowSplitAcrossTwoMarkdownRows_IsNotIntact()
    {
        string md = PerfectMarkdown.Replace("| 4\\.7\\. Przelew | 10,00 zł |", "| 4\\.7\\. | |\n| Przelew | 10,00 zł |", StringComparison.Ordinal);

        QualityReport r = Measure(md);

        Assert.Equal(0.5, r.RowsIntact);
        Assert.True(r.CellAgreement < 1.0);
        Assert.Contains(r.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("wiersz", StringComparison.Ordinal) && f.Contains("4.7.", StringComparison.Ordinal));
    }

    [Fact]
    public void TableSplitIntoTwoGfmTables_IsNotASingleTable()
    {
        string md = PerfectMarkdown.Replace("| 4\\.8\\.", "\n| **Usługa** | **Opłata** |\n| --- | --- |\n| 4\\.8\\.", StringComparison.Ordinal);

        QualityReport r = Measure(md);

        Assert.Equal(0.0, r.TablesAsSingleGfm);
        Assert.Equal(1.0, r.RowsIntact);
        Assert.Contains(r.Failures, f => f.Contains(Id, StringComparison.Ordinal) && f.Contains("tabel", StringComparison.Ordinal));
    }

    [Fact]
    public void HeaderRepeatedInsideTheTable_IsNotASingleCleanTable()
    {
        string md = PerfectMarkdown.Replace("| 4\\.8\\.", "| Usługa | Opłata |\n| 4\\.8\\.", StringComparison.Ordinal);

        QualityReport r = Measure(md);

        Assert.Equal(0.0, r.TablesAsSingleGfm);
    }

    [Fact]
    public void TableContinuedAfterAPageMarker_StaysOneTable()
    {
        string md = PerfectMarkdown.Replace("| 4\\.8\\.", "\n<!-- page: 2 -->\n\n| 4\\.8\\.", StringComparison.Ordinal);

        QualityReport r = Measure(md);

        Assert.Equal(1.0, r.TablesAsSingleGfm);
        Assert.Equal(1.0, r.RowsIntact);
    }

    [Fact]
    public void CellsInTheWrongColumns_LowerCellAgreementOnly()
    {
        QualityReport r = Measure(PerfectMarkdown.Replace("| 4\\.7\\. Przelew | 10,00 zł |", "| 10,00 zł | 4\\.7\\. Przelew |", StringComparison.Ordinal));

        Assert.Equal(1.0, r.RowsIntact);
        Assert.Equal(0.5, r.CellAgreement);
    }

    [Fact]
    public void KeyValueTable_CountsTheFirstPairAsARow()
    {
        DocumentTruth t = new();
        t.Tables.Add(new TruthTable([], [["Rok", "2024"], ["Kwota", "100"]]));

        QualityReport r = QualityMetrics.Measure(Id, t, "| **Rok** | 2024 |\n| --- | --- |\n| **Kwota** | 100 |\n");

        Assert.Equal(1.0, r.RowsIntact);
        Assert.Equal(1.0, r.TablesAsSingleGfm);
        Assert.Equal(1.0, r.CellAgreement);
    }
}
