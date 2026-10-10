using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Text;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

/// <summary>
/// Spec 008, T016 (FR-610, FR-612, data-model „Wiersz kontynuacji”): a line starting with a unit designation continues
/// the line above when all conditions hold; each negative case breaks exactly one of them.
/// </summary>
public sealed class ContinuationLineTests
{
    private const double Leading = 10;
    private const double Size = 7;
    private const double LabelLeft = 39.7;
    private const double TextLeft = 54.7;
    private const double Top = 100;

    /// <summary>
    /// A line of words in <paramref name="size"/> pt; <paramref name="bold"/> marks bold words by index (all words when
    /// null and <paramref name="allBold"/>).
    /// </summary>
    private static LayoutLine Line(string text, double left, double top, double size = Size, bool allBold = false, int[]? bold = null)
    {
        LayoutLine plain = LayoutFactory.Line(text, left, top);
        var words = new List<LayoutWord>(plain.Words.Count);
        for (int i = 0; i < plain.Words.Count; i++)
        {
            LayoutWord word = plain.Words[i];
            bool isBold = allBold || (bold?.Contains(i) ?? false);
            var glyph = new LayoutGlyph(word.Text, word.Box, top + (LayoutFactory.LineHeight * 0.8), size, isBold, false);
            words.Add(new LayoutWord([glyph], word.Box, word.Text, isBold ? TextStyle.Bold : TextStyle.None));
        }

        return new LayoutLine(words, plain.Box, plain.Baseline);
    }

    /// <summary>„1.” item at the label column whose text (at 54.7) ends mid-sentence with „–”.</summary>
    private static LayoutLine Above(string last = "–", LineRole role = LineRole.Unknown, double size = Size, int[]? bold = null)
    {
        LayoutLine line = Line($"1. Prowizje pobieramy zgodnie z Taryfą prowizji i opłat bankowych dla firm {last}", LabelLeft, Top, size, bold: bold);
        line.Role = role;
        return line;
    }

    private static LayoutLine Continuation(double left = TextLeft, double gap = Leading, double size = Size, bool allBold = false, int[]? bold = null) =>
        Line("Rozdział I (Prowadzenie rachunków). Jest on załącznikiem do umowy.", left, Top + gap, size, allBold, bold);

    [Theory]
    [InlineData(LineRole.Unknown)]
    [InlineData(LineRole.ListItem)]
    [InlineData(LineRole.ListContinuation)]
    public void Wrapped_line_at_the_text_start_after_the_label_is_a_continuation(LineRole role) =>
        Assert.True(ContinuationLine.Continues(Continuation(), Above(role: role), Leading, centred: false));

    [Fact]
    public void Wrapped_line_at_the_left_edge_of_the_line_above_is_a_continuation()
    {
        LayoutLine above = Line("Bank pobiera prowizje zgodnie z Taryfą prowizji i opłat bankowych dla firm –", LabelLeft, Top);
        Assert.True(ContinuationLine.Continues(Continuation(left: LabelLeft + 1.5), above, Leading, centred: false));
    }

    [Fact]
    public void Line_at_the_top_of_a_page_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(), null, Leading, centred: false));

    [Theory]
    [InlineData(LineRole.Heading)]
    [InlineData(LineRole.Table)]
    [InlineData(LineRole.Artifact)]
    public void Line_below_a_line_that_is_not_text_is_not_a_continuation(LineRole role) =>
        Assert.False(ContinuationLine.Continues(Continuation(), Above(role: role), Leading, centred: false));

    [Fact]
    public void Line_set_off_by_a_gap_above_the_leading_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(gap: 12.5), Above(), Leading, centred: false));

    [Fact]
    public void Line_in_another_size_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(size: 9), Above(), Leading, centred: false));

    [Fact]
    public void All_bold_line_below_a_partly_bold_line_is_not_a_continuation()
    {
        // The last word of the line above is bold like the designation, so only AllBold differs.
        LayoutLine above = Above(bold: [12]);
        Assert.False(ContinuationLine.Continues(Continuation(allBold: true), above, Leading, centred: false));
    }

    [Fact]
    public void Bold_designation_word_after_a_regular_line_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(bold: [0]), Above(), Leading, centred: false));

    [Theory]
    [InlineData("firm.")]
    [InlineData("firm:")]
    [InlineData("firm;")]
    [InlineData("firm!")]
    [InlineData("firm?")]
    public void Line_after_a_finished_sentence_is_not_a_continuation(string last) =>
        Assert.False(ContinuationLine.Continues(Continuation(), Above(last), Leading, centred: false));

    [Fact]
    public void Line_aligned_with_neither_edge_of_the_line_above_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(left: TextLeft + 8), Above(), Leading, centred: false));

    [Fact]
    public void Centred_line_is_not_a_continuation() =>
        Assert.False(ContinuationLine.Continues(Continuation(), Above(), Leading, centred: true));
}
