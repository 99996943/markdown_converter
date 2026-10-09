namespace LegalAgent.PdfParser.Model;

/// <summary>A table row.</summary>
/// <param name="Cells">Cells of the row; the sum of their column spans equals the table column count.</param>
public sealed record TableRow(IReadOnlyList<TableCell> Cells)
{
    /// <summary>Page on which the row starts; null when unknown. Not rendered (spec 004, research R4).</summary>
    public int? Page { get; init; }
}
