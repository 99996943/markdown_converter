namespace LegalAgent.PdfParser.Model;

/// <summary>A table cell.</summary>
/// <param name="Inlines">Cell content.</param>
/// <param name="ColumnSpan">Number of columns covered by the cell (at least 1).</param>
public sealed record TableCell(IReadOnlyList<Inline> Inlines, int ColumnSpan = 1);
