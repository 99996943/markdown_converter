namespace LegalAgent.PdfParser.Model;

/// <summary>A table row.</summary>
/// <param name="Cells">Cells of the row; the sum of their column spans equals the table column count.</param>
public sealed record TableRow(IReadOnlyList<TableCell> Cells);
