namespace LegalAgent.PdfParser.Layout;

/// <summary>A single positioned character extracted from the PDF.</summary>
/// <param name="Text">The character(s) of the glyph (a ligature may map to several characters).</param>
/// <param name="Box">Bounding box in points, Y down.</param>
/// <param name="Baseline">Baseline Y coordinate, Y down.</param>
/// <param name="PointSize">Effective font size in points.</param>
/// <param name="IsBold">True for bold faces.</param>
/// <param name="IsItalic">True for italic or oblique faces.</param>
public sealed record LayoutGlyph(string Text, Rect Box, double Baseline, double PointSize, bool IsBold, bool IsItalic);
