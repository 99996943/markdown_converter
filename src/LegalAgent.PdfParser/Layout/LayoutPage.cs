using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Layout;

/// <summary>Working representation of one page, filled in by the pipeline stages.</summary>
public sealed class LayoutPage
{
    /// <summary>Creates a page.</summary>
    /// <param name="number">Page number (1-based).</param>
    /// <param name="width">Page width in points.</param>
    /// <param name="height">Page height in points.</param>
    public LayoutPage(int number, double width, double height)
    {
        Number = number;
        Width = width;
        Height = height;
    }

    /// <summary>Page number (1-based).</summary>
    public int Number { get; }

    /// <summary>Page width in points.</summary>
    public double Width { get; }

    /// <summary>Page height in points.</summary>
    public double Height { get; }

    /// <summary>Glyphs extracted from the page (before line assembly).</summary>
    public IList<LayoutGlyph> Glyphs { get; } = [];

    /// <summary>Lines of text.</summary>
    public IList<LayoutLine> Lines { get; } = [];

    /// <summary>Horizontal and vertical ruling segments.</summary>
    public IList<Segment> Rulings { get; } = [];

    /// <summary>True when the page contains images.</summary>
    public bool HasImages { get; set; }

    /// <summary>Set when the page was skipped; null otherwise.</summary>
    public SkipReason? Skipped { get; set; }
}
