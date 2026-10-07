using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Layout;

/// <summary>Kind of a layout block.</summary>
public enum LayoutBlockKind
{
    /// <summary>Paragraph.</summary>
    Paragraph,

    /// <summary>List.</summary>
    List,

    /// <summary>Table.</summary>
    Table,

    /// <summary>Heading.</summary>
    Heading,

    /// <summary>Footnote definition.</summary>
    Footnote,
}

/// <summary>A group of lines forming one logical block.</summary>
public sealed class LayoutBlock
{
    /// <summary>Creates a block.</summary>
    /// <param name="kind">Kind of block.</param>
    /// <param name="pages">Source pages.</param>
    public LayoutBlock(LayoutBlockKind kind, PageRange pages)
    {
        Kind = kind;
        Pages = pages;
    }

    /// <summary>Kind of block.</summary>
    public LayoutBlockKind Kind { get; }

    /// <summary>Source pages.</summary>
    public PageRange Pages { get; set; }

    /// <summary>Lines of the block.</summary>
    public IList<LayoutLine> Lines { get; } = [];

    /// <summary>
    /// Inline content of paragraph blocks (text runs and page breaks), built by the block assembly stage
    /// with hyphenation already resolved.
    /// </summary>
    public IList<Inline> Inlines { get; } = [];

    /// <summary>Table column band boundaries (X coordinates), for table blocks.</summary>
    public IList<double> ColumnBands { get; } = [];

    /// <summary>Literal list label, for list blocks and items.</summary>
    public string? ListLabel { get; set; }

    /// <summary>Heading level, for heading blocks.</summary>
    public int? HeadingLevel { get; set; }
}
