namespace LegalAgent.PdfParser.Model;

/// <summary>Kind of a removed page artifact.</summary>
public enum ArtifactKind
{
    /// <summary>Running header.</summary>
    RunningHeader,

    /// <summary>Running footer.</summary>
    RunningFooter,

    /// <summary>Page number.</summary>
    PageNumber,
}

/// <summary>A page that was skipped.</summary>
/// <param name="PageNumber">Page number (1-based).</param>
/// <param name="Reason">Why it was skipped.</param>
/// <param name="Message">Human-readable detail.</param>
public sealed record SkippedPage(int PageNumber, SkipReason Reason, string Message);

/// <summary>Summary of one removed artifact pattern.</summary>
/// <param name="Pattern">The fingerprint of the removed text.</param>
/// <param name="Kind">Kind of artifact.</param>
/// <param name="Occurrences">Number of removed occurrences.</param>
/// <param name="Pages">Pages on which it was removed, ascending.</param>
public sealed record ArtifactSummary(string Pattern, ArtifactKind Kind, int Occurrences, IReadOnlyList<int> Pages);

/// <summary>A diagnostic warning.</summary>
/// <param name="Code">Stable code such as "PDF001_NoTextLayer".</param>
/// <param name="PageNumber">Related page, if any.</param>
/// <param name="Message">Human-readable message.</param>
public sealed record ConversionWarning(string Code, int? PageNumber, string Message);

/// <summary>A table-document (a document made of one multi-page, two-column bordered table) recognised in the PDF.</summary>
/// <param name="FirstPage">First page of the table.</param>
/// <param name="LastPage">Last page of the table.</param>
/// <param name="SectionCount">Number of sections (rows with a name in the left cell).</param>
/// <param name="HeaderRowText">Text of the dropped column-name row, cells joined with „ | ”; null when the table has none.</param>
/// <param name="DroppedHeaderRows">Number of dropped occurrences of the column-name row.</param>
public sealed record TableDocumentSummary(int FirstPage, int LastPage, int SectionCount, string? HeaderRowText, int DroppedHeaderRows);

/// <summary>Diagnostics of a conversion.</summary>
/// <param name="PageCount">Number of pages in the PDF.</param>
/// <param name="SkippedPages">Pages that were not converted.</param>
/// <param name="RemovedArtifacts">Removed artifacts ordered by first page, then pattern.</param>
/// <param name="HeadingCounts">Number of headings per level, keys ascending.</param>
/// <param name="ListCount">Number of lists.</param>
/// <param name="TableCount">Number of tables (including fallback tables).</param>
/// <param name="FallbackTableCount">Number of tables rendered in fallback mode.</param>
/// <param name="FootnoteCount">Number of footnotes.</param>
/// <param name="DroppedTextCount">Letters dropped as rotated or invisible.</param>
/// <param name="Warnings">Warnings.</param>
/// <param name="Elapsed">Conversion time; the only non-deterministic field.</param>
public sealed record ConversionReport(
    int PageCount,
    IReadOnlyList<SkippedPage> SkippedPages,
    IReadOnlyList<ArtifactSummary> RemovedArtifacts,
    IReadOnlyDictionary<int, int> HeadingCounts,
    int ListCount,
    int TableCount,
    int FallbackTableCount,
    int FootnoteCount,
    int DroppedTextCount,
    IReadOnlyList<ConversionWarning> Warnings,
    TimeSpan Elapsed)
{
    /// <summary>Recognised table-documents in page order (spec 002, FR-090); empty when none.</summary>
    public IReadOnlyList<TableDocumentSummary> TableDocuments { get; init; } = [];
}
