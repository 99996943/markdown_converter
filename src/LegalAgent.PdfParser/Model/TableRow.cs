namespace LegalAgent.PdfParser.Model;

/// <summary>A table row.</summary>
/// <param name="Cells">
/// Cells of the row; the sum of their column spans equals the table column count. Spec 010: a cell with a row span k in
/// row r and column c is followed in rows r+1…r+k−1 by cells in column c that continue it (same inlines).
/// </param>
public sealed record TableRow(IReadOnlyList<TableCell> Cells)
{
    /// <summary>Page on which the row starts; null when unknown. Not rendered (spec 004, research R4).</summary>
    public int? Page { get; init; }
}
