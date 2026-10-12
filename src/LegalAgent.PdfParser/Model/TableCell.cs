namespace LegalAgent.PdfParser.Model;

/// <summary>A table cell.</summary>
/// <param name="Inlines">Cell content.</param>
/// <param name="ColumnSpan">Number of columns covered by the cell (at least 1).</param>
/// <param name="RowSpan">
/// Spec 010: number of rows covered by a vertically merged cell (at least 1); greater than 1 only in the first row it
/// covers. A header cell never has a row span greater than 1.
/// </param>
/// <param name="ContinuesAbove">
/// Spec 010: the cell continues a merged cell from the row above — true only in rows 2…<see cref="RowSpan"/> of the
/// group; such a cell has a row span of 1, the column span of the starting cell and the same inlines (the text of the
/// merged cell is printed in every row it covers).
/// </param>
public sealed record TableCell(IReadOnlyList<Inline> Inlines, int ColumnSpan = 1, int RowSpan = 1, bool ContinuesAbove = false);
