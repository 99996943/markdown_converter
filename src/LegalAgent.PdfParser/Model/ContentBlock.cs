using System.Text.Json.Serialization;

namespace LegalAgent.PdfParser.Model;

/// <summary>Reason a page was skipped.</summary>
public enum SkipReason
{
    /// <summary>The page has no text layer (for example a scan).</summary>
    NoTextLayer,

    /// <summary>The page could not be read.</summary>
    PageReadError,
}

/// <summary>Block-level content element.</summary>
/// <param name="Pages">Source pages of the block.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(SkippedPageBlock), "skippedPage")]
public abstract record ContentBlock(PageRange Pages);

/// <summary>A paragraph.</summary>
/// <param name="Pages">Source pages.</param>
/// <param name="Inlines">Paragraph content; may contain page breaks and footnote references.</param>
public sealed record ParagraphBlock(PageRange Pages, IReadOnlyList<Inline> Inlines) : ContentBlock(Pages);

/// <summary>A single-level list.</summary>
/// <param name="Pages">Source pages.</param>
/// <param name="Items">List items.</param>
public sealed record ListBlock(PageRange Pages, IReadOnlyList<ListItem> Items) : ContentBlock(Pages);

/// <summary>A table.</summary>
/// <param name="Pages">Source pages.</param>
/// <param name="Header">Header row, if detected.</param>
/// <param name="Rows">Body rows.</param>
/// <param name="ColumnCount">Number of columns.</param>
/// <param name="IsFallback">True when the grid was ambiguous and the table is rendered as separated lines.</param>
public sealed record TableBlock(
    PageRange Pages,
    TableRow? Header,
    IReadOnlyList<TableRow> Rows,
    int ColumnCount,
    bool IsFallback) : ContentBlock(Pages);

/// <summary>Placeholder for a page that was not converted.</summary>
/// <param name="Pages">The skipped page.</param>
/// <param name="PageNumber">Page number (1-based).</param>
/// <param name="Reason">Why the page was skipped.</param>
public sealed record SkippedPageBlock(PageRange Pages, int PageNumber, SkipReason Reason) : ContentBlock(Pages);
