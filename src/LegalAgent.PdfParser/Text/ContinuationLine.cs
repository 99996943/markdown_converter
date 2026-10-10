using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Text;

/// <summary>
/// Spec 008 (FR-610, FR-612): a line that wraps the sentence of the line above — a unit designation at its start
/// („Rozdział I (…)” after „… Korporacji –”) is a word of that sentence, not a unit heading.
/// </summary>
/// <remarks>
/// A line continues the line above when all hold (data-model „Wiersz kontynuacji”): the line above is on the same page
/// and is text (unclaimed, list item or its continuation); the gap is at most 1.2 × the leading; both lines have the
/// same dominant size (± 0.5 pt) and the same all-bold state; the first word has the boldness of the last word above
/// (a bold „Art. 42.” below a regular „3. (uchylony)” is a unit); the line above does not end a sentence or clause
/// („.”, „:”, „;”, „!”, „?”, also before closing brackets or quotes); the line above is full — it reaches at least as
/// far right as the line (± 2 pt); the line starts at the left edge of the line above or at the start of its text after a
/// list label (± 2 pt); and the line is not centred. Real unit headings differ in boldness, size, centring or spacing.
/// </remarks>
internal static class ContinuationLine
{
    private const double GapFactor = 1.2;
    private const double SizeTolerance = 0.5;
    private const double EdgeTolerance = 2;

    /// <summary>Closing brackets and quotation marks that may follow the mark ending a sentence.</summary>
    private static readonly char[] ClosingMarks = [')', ']', '>', '”', '"', '\'', '»'];

    /// <summary>
    /// Whether <paramref name="line"/> continues the text of <paramref name="above"/>, the previous line on the same
    /// page (null at the top of a page).
    /// </summary>
    /// <param name="line">The line, typically starting with a unit designation.</param>
    /// <param name="above">The previous line on the same page, or null.</param>
    /// <param name="leading">The body leading.</param>
    /// <param name="centred">Whether the line is set like a centred line.</param>
    public static bool Continues(LayoutLine line, LayoutLine? above, double leading, bool centred)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (above is null || centred || leading <= 0
            || above.Role is not (LineRole.Unknown or LineRole.ListItem or LineRole.ListContinuation))
        {
            return false;
        }

        List<LayoutWord> words = TextWords(line);
        List<LayoutWord> previous = TextWords(above);
        if (words.Count == 0 || previous.Count == 0 || line.Baseline - above.Baseline > GapFactor * leading)
        {
            return false;
        }

        if (Math.Abs(DominantSize(words) - DominantSize(previous)) > SizeTolerance
            || AllBold(words) != AllBold(previous)
            || IsBold(words[0]) != IsBold(previous[^1]))
        {
            return false;
        }

        // „… naboru.]” ends its sentence like „… naboru.”; „3. (uchylony)” does not end with a sentence mark.
        string end = previous[^1].Text.TrimEnd().TrimEnd(ClosingMarks);
        if (end.Length == 0 || end[^1] is '.' or ':' or ';' or '!' or '?')
        {
            return false;
        }

        // A sentence wraps because the line above is full: a line reaching further right than it does not wrap it.
        if (above.Box.Right < line.Box.Right - EdgeTolerance)
        {
            return false;
        }

        double left = line.Box.Left;
        return Math.Abs(left - above.Box.Left) <= EdgeTolerance
            || (TextStartAfterLabel(previous) is double start && Math.Abs(left - start) <= EdgeTolerance);
    }

    /// <summary>The left edge of the text after a list label opening the line, or null without a label.</summary>
    private static double? TextStartAfterLabel(List<LayoutWord> words) =>
        words.Count > 1
        && ListLabelPatterns.TryMatch(string.Join(' ', words.Select(w => w.Text)), out ListLabelMatch? label)
        && string.Equals(label.Label, words[0].Text, StringComparison.Ordinal)
            ? words[1].Box.Left
            : null;

    private static List<LayoutWord> TextWords(LayoutLine line) => line.Words.Where(w => w.FootnoteId is null).ToList();

    private static bool IsBold(LayoutWord word) => word.Style.HasFlag(TextStyle.Bold);

    private static bool AllBold(List<LayoutWord> words) => words.All(IsBold);

    private static double DominantSize(List<LayoutWord> words)
    {
        var chars = new SortedDictionary<double, int>();
        foreach (LayoutGlyph glyph in words.SelectMany(w => w.Glyphs))
        {
            double size = Math.Round(glyph.PointSize * 2, MidpointRounding.AwayFromZero) / 2;
            chars[size] = chars.GetValueOrDefault(size) + glyph.Text.Length;
        }

        return chars.Count == 0 ? 0 : chars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;
    }
}
