using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>First and last page (1-based) on which an element is printed.</summary>
public readonly record struct PageSpan(int First, int Last);

/// <summary>Result of typesetting one composed document.</summary>
/// <param name="Pdf">PDF bytes (with a deterministic /ID).</param>
/// <param name="PageCount">Number of pages.</param>
/// <param name="Truth">Reference truth recorded while typesetting.</param>
/// <param name="ElementPages">Pages of every element with a non-empty id.</param>
/// <param name="BlockWordCounts">Number of printed words per source block.</param>
public sealed record TypesetResult(
    byte[] Pdf,
    int PageCount,
    DocumentTruth Truth,
    IReadOnlyDictionary<string, PageSpan> ElementPages,
    IReadOnlyDictionary<string, int> BlockWordCounts);

/// <summary>
/// Lays out a <see cref="ComposedDocument"/> on pages with <see cref="Pdf.SyntheticPdfBuilder"/> according to a
/// <see cref="LayoutStyle"/> (research R6) and records the reference truth (FR-106).
/// </summary>
public static class Typesetter
{
    /// <summary>Typesets <paramref name="document"/> in <paramref name="style"/>.</summary>
    public static TypesetResult Typeset(ComposedDocument document, LayoutStyle style) => throw new NotImplementedException();
}
