using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>A unit of splitting: the preamble or the own content of one section (research R2).</summary>
/// <param name="Section">The section; null for the preamble.</param>
/// <param name="Blocks">Own blocks without skipped-page placeholders.</param>
/// <param name="Footnotes">Own footnote definitions.</param>
/// <param name="Kind">Kind of the unit.</param>
/// <param name="SegmentPath">Key segments of the ancestors and the unit (research R6).</param>
/// <param name="SectionPath">Heading texts from the root to the unit; empty for the preamble.</param>
internal sealed record Unit(
    Section? Section,
    IReadOnlyList<ContentBlock> Blocks,
    IReadOnlyList<Footnote> Footnotes,
    ChunkUnitKind Kind,
    IReadOnlyList<string> SegmentPath,
    IReadOnlyList<string> SectionPath);

/// <summary>Collects the units of a document in document order (research R2, FR-220).</summary>
internal static class UnitCollector
{
    /// <summary>Key segment of the preamble.</summary>
    public const string PreambleSegment = Identity.UnitKeyBuilder.PreambleSegment;

    /// <summary>The preamble (when not empty) and every section with own blocks or footnotes, in document order.</summary>
    public static IReadOnlyList<Unit> Collect(LegalDocument document) => [];
}
