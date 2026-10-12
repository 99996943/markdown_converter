using LegalAgent.PdfParser.Layout;

namespace LegalAgent.PdfParser.Stages;

/// <summary>Spec 010: the cell grid of a ruled table (stub).</summary>
internal sealed class RuledTableGrid
{
    public IReadOnlyList<double> Columns { get; } = [];

    public IReadOnlyList<double> RowBoundaries { get; } = [];

    public IReadOnlyList<GridCell> Cells { get; } = [];

    public int ColumnCount => Columns.Count - 1;

    public int RowCount => RowBoundaries.Count - 1;

    public static RuledTableGrid? TryBuild(IEnumerable<Segment> rulings, double top, double bottom) => throw new NotImplementedException();

    public bool HasBoundary(int column, int boundary) => throw new NotImplementedException();

    public GridCell CellAt(int row, int column) => throw new NotImplementedException();

    public (int Row, int Column)? Locate(Rect box, double baseline) => throw new NotImplementedException();

    /// <summary>A cell of the grid: its first row and column and the rows and columns it spans.</summary>
    internal sealed record GridCell(int Row, int RowSpan, int Column, int ColumnSpan);
}
