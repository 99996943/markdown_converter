namespace LegalAgent.PdfParser.Layout;

/// <summary>Part of a line separated from its neighbours by a large horizontal gap (a potential table cell).</summary>
/// <param name="Words">Words of the segment.</param>
/// <param name="Box">Bounding box.</param>
public sealed record LineSegment(IReadOnlyList<LayoutWord> Words, Rect Box)
{
    /// <summary>Text of the segment with words joined by single spaces.</summary>
    public string Text => string.Join(' ', Words.Select(w => w.Text));
}
