using LegalAgent.PdfParser.Layout;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Spec 010 (research R1, R2): the cell grid of a ruled table, built from the ruling pieces in its vertical range.
/// Column borders are the verticals standing in at least <see cref="MinColumnRows"/> rows; row borders are the
/// horizontal pieces covering most of at least one column band. A band without a piece on a row border joins the cells
/// above and below it (a vertically merged cell); a row without a vertical between two bands joins them (a cell spanning
/// columns, e.g. a group row over the column names).
/// </summary>
internal sealed class RuledTableGrid
{
    /// <summary>Rulings of one border drawn in pieces are joined within this distance (points).</summary>
    private const double Slack = 3;

    /// <summary>Share of a band's width a horizontal piece must cover to be a row border of that band.</summary>
    private const double BandCoverage = 0.8;

    /// <summary>A vertical standing in fewer rows is a divider inside one cell, not a column border.</summary>
    private const int MinColumnRows = 2;

    private readonly List<Vertical> _verticals;
    private readonly bool[][] _covered;
    private readonly GridCell[,] _cellAt;

    private RuledTableGrid(List<double> columns, List<double> rows, bool[][] covered, List<Vertical> verticals)
    {
        Columns = columns;
        RowBoundaries = rows;
        _covered = covered;
        _verticals = verticals;
        _cellAt = new GridCell[RowCount, ColumnCount];
        Cells = BuildCells();
    }

    /// <summary>X of the column borders, left to right (one more than the columns).</summary>
    public IReadOnlyList<double> Columns { get; }

    /// <summary>Y of the row borders, top to bottom (one more than the rows).</summary>
    public IReadOnlyList<double> RowBoundaries { get; }

    /// <summary>The cells, merged vertically and horizontally, in row and column order of their first unit.</summary>
    public IReadOnlyList<GridCell> Cells { get; }

    public int ColumnCount => Columns.Count - 1;

    public int RowCount => RowBoundaries.Count - 1;

    /// <summary>The grid of the rulings between <paramref name="top"/> and <paramref name="bottom"/>; null without one.</summary>
    public static RuledTableGrid? TryBuild(IEnumerable<Segment> rulings, double top, double bottom)
    {
        List<(double Y, double X1, double X2)> horizontals = rulings
            .Where(s => s.IsHorizontal && s.Y1 >= top - Slack && s.Y1 <= bottom + Slack)
            .Select(s => (s.Y1, Math.Min(s.X1, s.X2), Math.Max(s.X1, s.X2)))
            .OrderBy(h => h.Item1)
            .ToList();
        List<Vertical> verticals = rulings
            .Where(s => s.IsVertical && Math.Max(s.Y1, s.Y2) >= top - Slack && Math.Min(s.Y1, s.Y2) <= bottom + Slack)
            .Select(s => new Vertical(s.X1, Math.Min(s.Y1, s.Y2), Math.Max(s.Y1, s.Y2)))
            .OrderBy(v => v.X)
            .ToList();
        if (horizontals.Count < 2 || verticals.Count == 0)
        {
            return null;
        }

        List<List<(double Y, double X1, double X2)>> lines = Cluster(horizontals, h => h.Y);
        List<double> candidates = lines.Select(l => l.Min(h => h.Y)).ToList();

        var columns = new List<double>();
        foreach (List<Vertical> border in Cluster(verticals, v => v.X))
        {
            int rows = Enumerable.Range(0, candidates.Count - 1)
                .Count(k => border.Any(v => v.Crosses((candidates[k] + candidates[k + 1]) / 2)));
            if (rows >= MinColumnRows)
            {
                columns.Add(border.Average(v => v.X));
            }
        }

        if (columns.Count < 3)
        {
            return null;
        }

        var rowBorders = new List<double>();
        var covered = new List<bool[]>();
        foreach (List<(double Y, double X1, double X2)> line in lines)
        {
            List<(double X1, double X2)> runs = Runs(line);
            bool[] bands = Enumerable.Range(0, columns.Count - 1)
                .Select(c => runs.Any(r => Math.Min(r.X2, columns[c + 1]) - Math.Max(r.X1, columns[c]) >= BandCoverage * (columns[c + 1] - columns[c])))
                .ToArray();
            if (bands.Any(b => b))
            {
                rowBorders.Add(line.Min(h => h.Y));
                covered.Add(bands);
            }
        }

        if (rowBorders.Count < 2)
        {
            return null;
        }

        return new RuledTableGrid(columns, rowBorders, [.. covered], verticals);
    }

    /// <summary>Whether a ruling lies on row border <paramref name="boundary"/> over most of band <paramref name="column"/>.</summary>
    public bool HasBoundary(int column, int boundary) => _covered[boundary][column];

    /// <summary>The cell covering the unit at <paramref name="row"/>, <paramref name="column"/>.</summary>
    public GridCell CellAt(int row, int column) => _cellAt[row, column];

    /// <summary>The unit (row, column) holding a word: its baseline between row borders, its centre in a band.</summary>
    public (int Row, int Column)? Locate(Rect box, double baseline)
    {
        int row = Index(RowBoundaries, baseline);
        int column = Index(Columns, box.CenterX);
        return row < 0 || column < 0 ? null : (row, column);
    }

    private static int Index(IReadOnlyList<double> borders, double value)
    {
        for (int i = 0; i + 1 < borders.Count; i++)
        {
            if (value >= borders[i] && value < borders[i + 1])
            {
                return i;
            }
        }

        return -1;
    }

    private List<GridCell> BuildCells()
    {
        int rows = RowCount;
        int columns = ColumnCount;
        int[] parent = Enumerable.Range(0, rows * columns).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        void Join(int a, int b) => parent[Find(a)] = Find(b);

        for (int r = 0; r < rows; r++)
        {
            double middle = (RowBoundaries[r] + RowBoundaries[r + 1]) / 2;
            for (int c = 0; c < columns; c++)
            {
                if (c + 1 < columns && !_verticals.Any(v => Math.Abs(v.X - Columns[c + 1]) <= Slack && v.Crosses(middle)))
                {
                    Join((r * columns) + c, (r * columns) + c + 1);
                }

                if (r + 1 < rows && !_covered[r + 1][c])
                {
                    Join((r * columns) + c, ((r + 1) * columns) + c);
                }
            }
        }

        var cells = new List<GridCell>();
        foreach (IGrouping<int, int> unit in Enumerable.Range(0, rows * columns).GroupBy(Find).OrderBy(g => g.Min()))
        {
            int first = unit.Min(u => u / columns);
            int last = unit.Max(u => u / columns);
            int left = unit.Min(u => u % columns);
            int right = unit.Max(u => u % columns);
            var cell = new GridCell(first, last - first + 1, left, right - left + 1);
            cells.Add(cell);
            foreach (int u in unit)
            {
                _cellAt[u / columns, u % columns] = cell;
            }
        }

        return cells;
    }

    private static List<List<T>> Cluster<T>(List<T> items, Func<T, double> key)
    {
        var clusters = new List<List<T>>();
        foreach (T item in items.OrderBy(key))
        {
            if (clusters.Count > 0 && key(item) - key(clusters[^1][^1]) <= Slack)
            {
                clusters[^1].Add(item);
            }
            else
            {
                clusters.Add([item]);
            }
        }

        return clusters;
    }

    /// <summary>Collinear pieces of one horizontal border joined where they touch.</summary>
    private static List<(double X1, double X2)> Runs(List<(double Y, double X1, double X2)> line)
    {
        var runs = new List<(double X1, double X2)>();
        foreach ((double _, double x1, double x2) in line.OrderBy(h => h.X1))
        {
            if (runs.Count > 0 && x1 - runs[^1].X2 <= Slack)
            {
                runs[^1] = (runs[^1].X1, Math.Max(runs[^1].X2, x2));
            }
            else
            {
                runs.Add((x1, x2));
            }
        }

        return runs;
    }

    /// <summary>A cell of the grid: its first row and column and the rows and columns it spans.</summary>
    internal sealed record GridCell(int Row, int RowSpan, int Column, int ColumnSpan);

    private sealed record Vertical(double X, double Top, double Bottom)
    {
        public bool Crosses(double y) => Top <= y && Bottom >= y;
    }
}
