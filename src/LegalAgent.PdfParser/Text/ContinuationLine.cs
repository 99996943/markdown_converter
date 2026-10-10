using LegalAgent.PdfParser.Layout;

namespace LegalAgent.PdfParser.Text;

/// <summary>
/// Spec 008 (FR-610, FR-612): a line that wraps the sentence of the line above — a unit designation at its start
/// („Rozdział I (…)” after „… Korporacji –”) is a word of that sentence, not a unit heading.
/// </summary>
internal static class ContinuationLine
{
    /// <summary>Whether <paramref name="line"/> continues the text of <paramref name="above"/> (stub, T016 red).</summary>
    public static bool Continues(LayoutLine line, LayoutLine? above, double leading, bool centred) => false;
}
