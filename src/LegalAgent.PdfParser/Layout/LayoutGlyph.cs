namespace LegalAgent.PdfParser.Layout;

/// <summary>A single positioned character extracted from the PDF.</summary>
/// <param name="Text">The character(s) of the glyph (a ligature may map to several characters).</param>
/// <param name="Box">Ink bounding box in points, Y down.</param>
/// <param name="Baseline">Baseline Y coordinate, Y down.</param>
/// <param name="PointSize">Effective font size in points.</param>
/// <param name="IsBold">True for bold faces.</param>
/// <param name="IsItalic">True for italic or oblique faces.</param>
/// <param name="AdvanceStart">X where the glyph's advance starts (pen position); <see cref="double.NaN"/> when unknown.</param>
/// <param name="AdvanceEnd">X where the glyph's advance ends (next pen position); <see cref="double.NaN"/> when unknown.</param>
/// <param name="FontName">Font name as written in the PDF (may carry a subset prefix); null when unknown.</param>
public sealed record LayoutGlyph(
    string Text,
    Rect Box,
    double Baseline,
    double PointSize,
    bool IsBold,
    bool IsItalic,
    double AdvanceStart = double.NaN,
    double AdvanceEnd = double.NaN,
    string? FontName = null)
{
    /// <summary>Left edge used for spacing: the advance start, or the ink box when the advance is unknown.</summary>
    public double Start => double.IsNaN(AdvanceStart) ? Box.Left : AdvanceStart;

    /// <summary>Right edge used for spacing: the advance end, or the ink box when the advance is unknown.</summary>
    public double End => double.IsNaN(AdvanceEnd) ? Box.Right : AdvanceEnd;
}
