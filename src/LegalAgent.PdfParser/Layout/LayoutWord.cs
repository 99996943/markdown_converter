using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Layout;

/// <summary>A word assembled from glyphs.</summary>
/// <param name="Glyphs">Glyphs of the word in reading order.</param>
/// <param name="Box">Bounding box.</param>
/// <param name="Text">Word text.</param>
/// <param name="Style">Dominant emphasis of the word.</param>
public sealed record LayoutWord(IReadOnlyList<LayoutGlyph> Glyphs, Rect Box, string Text, TextStyle Style);
