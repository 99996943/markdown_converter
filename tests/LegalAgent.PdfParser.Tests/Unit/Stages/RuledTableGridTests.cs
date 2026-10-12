using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Stages;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>Spec 010 (T005) — the cell grid of a ruled table built from ruling pieces (research R1, R2).</summary>
public sealed class RuledTableGridTests
{
    private static Segment H(double x1, double x2, double y) => new(x1, y, x2, y);

    private static Segment V(double x, double y1, double y2) => new(x, y1, x, y2);

    /// <summary>A full grid: verticals at <paramref name="xs"/> drawn as one piece per row, rows at <paramref name="ys"/>.</summary>
    private static List<Segment> FullGrid(double[] xs, double[] ys)
    {
        var rulings = new List<Segment>();
        foreach (double y in ys)
        {
            for (int c = 0; c + 1 < xs.Length; c++)
            {
                rulings.Add(H(xs[c] + 0.2, xs[c + 1] - 0.2, y));
            }
        }

        for (int r = 0; r + 1 < ys.Length; r++)
        {
            foreach (double x in xs)
            {
                rulings.Add(V(x, ys[r], ys[r + 1]));
            }
        }

        return rulings;
    }

    [Fact]
    public void Columns_AreTheVerticalsOfAtLeastTwoRows_JoinedWithinThreePoints()
    {
        // Pieces of one vertical drawn row by row, slightly offset (≤ 3 pt), are one column border; a vertical
        // standing in one row only (a divider inside a single cell) is not.
        double[] ys = [100, 120, 140, 160];
        var rulings = FullGrid([59.3, 201.1, 555.6], ys);
        rulings.RemoveAll(s => s.IsVertical && Math.Abs(s.X1 - 201.1) < 0.1 && s.Y1 == 140);
        rulings.Add(V(202.4, 140, 160));
        rulings.Add(V(400, 100, 120));

        RuledTableGrid grid = Assert.IsType<RuledTableGrid>(RuledTableGrid.TryBuild(rulings, 90, 170));

        Assert.Equal(3, grid.Columns.Count);
        Assert.Equal(59.3, grid.Columns[0], 1);
        Assert.InRange(grid.Columns[1], 201.0, 202.5);
        Assert.Equal(555.6, grid.Columns[2], 1);
        Assert.Equal(2, grid.ColumnCount);
        Assert.Equal([100, 120, 140, 160], grid.RowBoundaries);
        Assert.Equal(3, grid.RowCount);
    }

    [Fact]
    public void RowBoundaries_AreTheUnionOfPiecesCoveringMostOfOneBand_AndAColumnWithoutAPieceIsMerged()
    {
        // firm-5 p. 13: the first column is ruled at group borders only, the second under every row.
        double[] xs = [59.3, 201.1, 555.6];
        var rulings = new List<Segment>();
        foreach (double y in new double[] { 100, 160 })
        {
            rulings.Add(H(59.5, 200.8, y));
        }

        foreach (double y in new double[] { 100, 120, 140, 160 })
        {
            rulings.Add(H(201.3, 555.3, y));
        }

        // A short piece (a link underline, 30% of the band) is no row border.
        rulings.Add(H(210, 300, 130));
        for (int r = 0; r < 3; r++)
        {
            double top = 100 + (20 * r);
            foreach (double x in xs)
            {
                rulings.Add(V(x, top, top + 20));
            }
        }

        RuledTableGrid grid = Assert.IsType<RuledTableGrid>(RuledTableGrid.TryBuild(rulings, 90, 170));

        Assert.Equal([100, 120, 140, 160], grid.RowBoundaries);
        Assert.False(grid.HasBoundary(column: 0, boundary: 1));
        Assert.False(grid.HasBoundary(column: 0, boundary: 2));
        Assert.True(grid.HasBoundary(column: 1, boundary: 1));
        Assert.True(grid.HasBoundary(column: 0, boundary: 3));

        RuledTableGrid.GridCell group = grid.CellAt(row: 1, column: 0);
        Assert.Equal((0, 3, 0, 1), (group.Row, group.RowSpan, group.Column, group.ColumnSpan));
        RuledTableGrid.GridCell value = grid.CellAt(row: 1, column: 1);
        Assert.Equal((1, 1, 1, 1), (value.Row, value.RowSpan, value.Column, value.ColumnSpan));
        Assert.Equal(4, grid.Cells.Count);
    }

    [Fact]
    public void APieceAcrossSeveralBandsWithoutVerticalsInItsRow_IsOneCellSpanningTheColumns()
    {
        // firm-3 p. 8: „Gdzie można zrobić?” over columns 2–4 (piece 215.4–555.2, no inner vertical in its row); the line
        // under it runs over columns 2–4 only, so column 1 („Co można zrobić?”) spans both header rows.
        double[] xs = [59.2, 215.2, 328.6, 442.1, 555.5];
        var rulings = new List<Segment> { H(59.4, 215.0, 100), H(215.4, 555.2, 100) };
        rulings.AddRange([H(215.4, 328.4, 115), H(328.8, 441.9, 115), H(442.3, 555.3, 115)]);
        foreach (double y in new double[] { 140, 160 })
        {
            for (int c = 0; c + 1 < xs.Length; c++)
            {
                rulings.Add(H(xs[c] + 0.2, xs[c + 1] - 0.2, y));
            }
        }

        rulings.AddRange([V(59.2, 100, 115), V(215.2, 100, 115), V(555.5, 100, 115)]);
        foreach ((double top, double bottom) in new[] { (115.0, 140.0), (140.0, 160.0) })
        {
            foreach (double x in xs)
            {
                rulings.Add(V(x, top, bottom));
            }
        }

        RuledTableGrid grid = Assert.IsType<RuledTableGrid>(RuledTableGrid.TryBuild(rulings, 90, 170));

        Assert.Equal(4, grid.ColumnCount);
        Assert.Equal([100, 115, 140, 160], grid.RowBoundaries);
        RuledTableGrid.GridCell groupCell = grid.CellAt(row: 0, column: 2);
        Assert.Equal((0, 1, 1, 3), (groupCell.Row, groupCell.RowSpan, groupCell.Column, groupCell.ColumnSpan));
        RuledTableGrid.GridCell first = grid.CellAt(row: 1, column: 0);
        Assert.Equal((0, 2, 0, 1), (first.Row, first.RowSpan, first.Column, first.ColumnSpan));
        RuledTableGrid.GridCell header = grid.CellAt(row: 1, column: 3);
        Assert.Equal((1, 1, 3, 1), (header.Row, header.RowSpan, header.Column, header.ColumnSpan));
    }

    [Fact]
    public void Locate_UsesTheCentreAndTheBaseline_NotTheLeftEdge()
    {
        RuledTableGrid grid = RuledTableGrid.TryBuild(FullGrid([45.7, 185.4, 278.9, 372.3], [100, 140, 160]), 90, 170)!;

        // A centred word whose left edge lies in the previous band belongs to the band of its centre.
        Assert.Equal((0, 1), grid.Locate(new Rect(180, 110, 290, 121), baseline: 119));
        Assert.Equal((1, 2), grid.Locate(new Rect(310, 145, 340, 156), baseline: 154));
        Assert.Null(grid.Locate(new Rect(310, 175, 340, 186), baseline: 184));
        Assert.Null(grid.Locate(new Rect(400, 145, 420, 156), baseline: 154));
    }

    [Fact]
    public void VerticalsInOneRowOnly_AreNoGrid()
    {
        var rulings = new List<Segment> { H(50, 500, 100), H(50, 500, 120), H(50, 500, 140) };
        rulings.AddRange([V(50, 100, 120), V(250, 100, 120), V(500, 100, 120)]);

        Assert.Null(RuledTableGrid.TryBuild(rulings, 90, 150));
    }

    [Fact]
    public void RulingsOutsideTheVerticalRange_AreIgnored()
    {
        var rulings = FullGrid([50, 250, 500], [100, 120, 140]);
        rulings.Add(H(40, 180, 700)); // a footnote separator further down the page

        RuledTableGrid grid = RuledTableGrid.TryBuild(rulings, 90, 150)!;

        Assert.Equal([100, 120, 140], grid.RowBoundaries);
    }
}
