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
    public static IReadOnlyList<Unit> Collect(LegalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var units = new List<Unit>();
        List<ContentBlock> preamble = OwnBlocks(document.Preamble);
        if (preamble.Count > 0 || document.PreambleFootnotes.Count > 0)
        {
            units.Add(new Unit(null, preamble, document.PreambleFootnotes, ChunkUnitKind.Preamble, [PreambleSegment], []));
        }

        foreach (Section section in document.Sections)
        {
            Collect(section, [], units);
        }

        return units;
    }

    private static void Collect(Section section, IReadOnlyList<string> parentSegments, List<Unit> units)
    {
        List<string> segments = [.. parentSegments, Identity.UnitKeyBuilder.Segment(section.Designation, section.HeadingText)];
        List<ContentBlock> blocks = OwnBlocks(section.Blocks);
        // A section with neither content nor subsections keeps its heading as a unit of its own (FR-220).
        if (blocks.Count > 0 || section.Footnotes.Count > 0 || section.Children.Count == 0)
        {
            units.Add(new Unit(section, blocks, section.Footnotes, Kind(section.Kind), segments, section.Path));
        }

        foreach (Section child in section.Children)
        {
            Collect(child, segments, units);
        }
    }

    // A skipped page is rendered as a comment that is not text of the PDF; it never makes or joins a unit.
    private static List<ContentBlock> OwnBlocks(IReadOnlyList<ContentBlock> blocks) =>
        blocks.Where(b => b is not SkippedPageBlock).ToList();

    private static ChunkUnitKind Kind(SectionKind kind) => kind switch
    {
        SectionKind.DocumentTitle => ChunkUnitKind.DocumentTitle,
        SectionKind.Book => ChunkUnitKind.Book,
        SectionKind.Part => ChunkUnitKind.Part,
        SectionKind.Division => ChunkUnitKind.Division,
        SectionKind.Chapter => ChunkUnitKind.Chapter,
        SectionKind.Subchapter => ChunkUnitKind.Subchapter,
        SectionKind.Article => ChunkUnitKind.Article,
        SectionKind.Paragraph => ChunkUnitKind.Paragraph,
        SectionKind.TableDocumentSection => ChunkUnitKind.TableDocumentSection,
        _ => ChunkUnitKind.Typographic,
    };
}
