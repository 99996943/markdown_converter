using System.Globalization;
using System.Text;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects tables (FR-060 – FR-066): lines whose segments (cells, FR-060) line up in column bands form a table region,
/// which becomes a <see cref="TableBlock"/> in <see cref="PipelineContext.Tables"/>; its lines get
/// <see cref="LineRole.Table"/> and <see cref="LayoutAnnotations.TableIndex"/>.
/// </summary>
/// <remarks>
/// A region starts at a line with at least two cells and grows over the following body lines while the vertical gaps
/// stay table-like; it is a table when it holds at least <see cref="TableOptions.MinRows"/> multi-cell lines. A list
/// label followed by text („• tekst”) is one cell, and lines whose cells are all as wide as text columns are running
/// text (FR-031), not table rows, and so are words of a justified line split on widened spaces. Column bands are the clusters of cell left edges shared by at least two rows, snapped
/// to vertical rulings. Rows follow horizontal rulings when the region has a ruled grid; otherwise a line with a single
/// partial cell close below the previous line continues the previous row (FR-062). Multi-cell lines with a varying
/// number of cells, or two cells in one band, make the grid ambiguous: the table is kept line by line as a fallback
/// with warning <c>TBL001_AmbiguousGrid</c> (FR-064). A table ending a page continues with a table starting the next
/// page when the bands match, without its repeated header (FR-065).
/// </remarks>
public sealed class TableDetectionStage : IPipelineStage
{
    private const double MaxRowGapInLeadings = 2.5;
    private const int MinBandSupport = 2;
    private const double RulingSpanRatio = 0.4;
    private const double RulingSlack = 3;
    private const double MinCellGapEm = 1.0;

    /// <inheritdoc />
    public int Order => StageOrder.TableDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TableOptions options = context.Options.Tables;
        if (!options.Enabled)
        {
            return;
        }

        string[] hyphenationExceptions = context.Options.Normalization.HyphenationExceptions.ToArray();
        var tables = new List<Table>();
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (page.Skipped is null)
            {
                tables.AddRange(FindTables(context, page, hyphenationExceptions));
            }
        }

        if (options.MergeAcrossPages)
        {
            tables = MergeAcrossPages(context, tables);
        }

        foreach (Table table in tables)
        {
            int index = context.Tables.Count;
            TableBlock block = table.ToBlock();
            context.Tables.Add(new LayoutBlock(LayoutBlockKind.Table, block.Pages) { Table = block });
            foreach (LayoutLine line in table.Lines)
            {
                line.Role = LineRole.Table;
                line.Annotations[LayoutAnnotations.TableIndex] = index.ToString(CultureInfo.InvariantCulture);
            }

            if (block.IsFallback)
            {
                context.Report.AddWarning(
                    "TBL001_AmbiguousGrid",
                    block.Pages.First,
                    $"Tabela na stronie {block.Pages.First} nie ma jednoznacznej siatki kolumn; zapisano ją wierszami z separatorem „ | ”.");
            }
        }
    }

    private static List<Table> FindTables(PipelineContext context, LayoutPage page, string[] hyphenationExceptions)
    {
        TableOptions options = context.Options.Tables;
        double tolerance = options.ColumnTolerance * page.Width;
        double wideCell = context.Options.Layout.ColumnMinLineWidthRatio * page.Width;
        List<Row> flow = page.Lines
            .Where(l => l.Role == LineRole.Unknown && l.Segments.Count > 0)
            .Select(l => new Row(l, CellsOf(l), wideCell))
            .ToList();

        var tables = new List<Table>();
        int start = 0;
        while (start < flow.Count)
        {
            if (!flow[start].IsMulti)
            {
                start++;
                continue;
            }

            List<Row> region = GrowRegion(context, flow, start);
            if (region.Count(r => r.IsMulti) < options.MinRows)
            {
                start++;
                continue;
            }

            Table? table = Build(context, page, region, tolerance, hyphenationExceptions);
            if (table is null)
            {
                start++;
                continue;
            }

            tables.Add(table);
            start = flow.IndexOf(region[0]) + table.Lines.Count;
        }

        return tables;
    }

    /// <summary>Cells of a line: its segments, with a leading bullet or list label joined to the text it introduces.</summary>
    private static List<LineSegment> CellsOf(LayoutLine line)
    {
        List<LineSegment> cells = [.. line.Segments];
        if (cells.Count == 2
            && cells[0].Words.Count == 1
            && ListLabelPatterns.TryMatch(cells[0].Text + " x", out ListLabelMatch? label)
            && label.Kind is ListLabelKind.Bullet or ListLabelKind.Dash or ListLabelKind.ArabicParen or ListLabelKind.LetterParen)
        {
            return [new LineSegment([.. cells[0].Words, .. cells[1].Words], cells[0].Box.Union(cells[1].Box))];
        }

        return cells;
    }

    /// <summary>Lines from the seed while the gaps stay table-like (trailing lines are settled by <see cref="Build"/>).</summary>
    private static List<Row> GrowRegion(PipelineContext context, List<Row> flow, int start)
    {
        double leading = context.BodyStyle?.Leading is > 0 and double l ? l : 1.2 * flow[start].Line.Box.Height;
        var region = new List<Row> { flow[start] };
        for (int i = start + 1; i < flow.Count; i++)
        {
            double gap = flow[i].Line.Baseline - region[^1].Line.Baseline;
            if (gap <= 0 || gap > MaxRowGapInLeadings * leading)
            {
                break;
            }

            region.Add(flow[i]);
        }

        return region;
    }

    private static Table? Build(PipelineContext context, LayoutPage page, List<Row> candidate, double tolerance, string[] exceptions)
    {
        TableOptions options = context.Options.Tables;
        List<Row> region = TrimTrailingLines(candidate, page, options);
        List<Row> multi = region.Where(r => r.IsMulti).ToList();

        var clusters = ColumnClustering.ClusterLefts(multi.SelectMany(r => r.Cells.Select(c => c.Box.Left)), tolerance);
        List<double> lefts = clusters
            .Where(c => multi.Count(r => r.Cells.Any(cell => cell.Box.Left >= c && cell.Box.Left - c <= tolerance)) >= MinBandSupport)
            .ToList();
        if (lefts.Count < 2)
        {
            return null;
        }

        double top = region.Min(r => r.Line.Box.Top);
        double bottom = region.Max(r => r.Line.Box.Bottom);
        double left = region.Min(r => r.Line.Box.Left);
        double right = region.Max(r => r.Line.Box.Right);
        IEnumerable<Segment> rulings = options.UseRulingLines ? page.Rulings : [];
        IEnumerable<double> verticals = rulings
            .Where(s => s.IsVertical && Math.Max(s.Y1, s.Y2) >= top - RulingSlack && Math.Min(s.Y1, s.Y2) <= bottom + RulingSlack)
            .Select(s => s.X1);
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands(lefts, right, verticals, tolerance);

        bool ambiguous = multi.Select(r => r.Cells.Count).Distinct().Count() > 1
            || multi.Any(r => r.Cells.Select(c => ColumnClustering.BandIndex(bands, c.Box.Left, tolerance)).Distinct().Count() < r.Cells.Count);

        var table = new Table(page.Number, bands, exceptions) { IsFallback = ambiguous };
        foreach (Row row in region)
        {
            table.Lines.Add(row.Line);
        }

        if (ambiguous)
        {
            foreach (Row row in region)
            {
                table.AddVisualRow(row);
            }

            return table;
        }

        List<double> horizontals = HorizontalRulings(rulings, region);

        if (horizontals.Count >= 2)
        {
            // Ruled grid: every line goes to the row between the rulings around its centre.
            foreach (IGrouping<int, Row> group in region.GroupBy(r => horizontals.Count(y => y <= r.Line.Box.CenterY)))
            {
                table.StartRow();
                foreach (Row row in group)
                {
                    table.AddToRow(row, tolerance);
                }
            }

            return table;
        }

        double rowGap = options.RowMergeGapFactor * TableLeading(region);
        Row? previous = null;
        foreach (Row row in region)
        {
            bool continues = previous is not null
                && !row.IsMulti
                && row.Line.Baseline - previous.Line.Baseline <= rowGap
                && row.Cells.All(c => ColumnClustering.Span(bands, c.Box.Left, c.Box.Right, tolerance) <= 1)
                && !horizontals.Any(y => y > previous.Line.Box.CenterY && y < row.Line.Box.CenterY);
            if (!continues)
            {
                table.StartRow();
            }

            table.AddToRow(row, tolerance);
            previous = row;
        }

        return table;
    }

    /// <summary>
    /// Keeps the lines up to the last multi-cell line plus the single-cell lines that still belong to the last row: inside
    /// the ruled grid, or close below and within one column (FR-062).
    /// </summary>
    private static List<Row> TrimTrailingLines(List<Row> region, LayoutPage page, TableOptions options)
    {
        int lastMulti = region.FindLastIndex(r => r.IsMulti);
        List<double> horizontals = HorizontalRulings(options.UseRulingLines ? page.Rulings : [], region);
        double rowGap = options.RowMergeGapFactor * TableLeading(region.Take(lastMulti + 1).ToList());
        int end = lastMulti;
        for (int i = lastMulti + 1; i < region.Count; i++)
        {
            LayoutLine line = region[i].Line;
            bool insideGrid = horizontals.Count >= 2 && line.Box.CenterY > horizontals[0] && line.Box.CenterY < horizontals[^1];
            bool closeBelow = line.Baseline - region[i - 1].Line.Baseline <= rowGap && region[i].Cells.Count == 1;
            if (!insideGrid && !closeBelow)
            {
                break;
            }

            end = i;
        }

        return region.Take(end + 1).ToList();
    }

    /// <summary>Distinct Y of horizontal rulings across the region spanning a good part of its width.</summary>
    private static List<double> HorizontalRulings(IEnumerable<Segment> rulings, List<Row> region)
    {
        double top = region.Min(r => r.Line.Box.Top);
        double bottom = region.Max(r => r.Line.Box.Bottom);
        double width = region.Max(r => r.Line.Box.Right) - region.Min(r => r.Line.Box.Left);
        var distinct = new List<double>();
        foreach (double y in rulings
            .Where(s => s.IsHorizontal
                && s.Y1 >= top - RulingSlack && s.Y1 <= bottom + RulingSlack
                && Math.Abs(s.X2 - s.X1) >= RulingSpanRatio * width)
            .Select(s => s.Y1)
            .Order())
        {
            if (distinct.Count == 0 || y - distinct[^1] > RulingSlack)
            {
                distinct.Add(y);
            }
        }

        return distinct;
    }

    /// <summary>Typical line distance inside a table: the lower quartile of the gaps between consecutive lines.</summary>
    private static double TableLeading(List<Row> region)
    {
        List<double> gaps = region.Zip(region.Skip(1), (a, b) => b.Line.Baseline - a.Line.Baseline).Where(g => g > 1).Order().ToList();
        return gaps.Count == 0 ? region[0].Line.Box.Height * 1.2 : gaps[(gaps.Count - 1) / 4];
    }

    /// <summary>FR-065: a table ending a page and a table opening the next page with the same bands are one table.</summary>
    private static List<Table> MergeAcrossPages(PipelineContext context, List<Table> tables)
    {
        var merged = new List<Table>();
        foreach (Table table in tables)
        {
            Table? previous = merged.Count > 0 ? merged[^1] : null;
            if (previous is not null
                && !previous.IsFallback
                && !table.IsFallback
                && table.FirstPage == previous.LastPage + 1
                && previous.Bands.Count == table.Bands.Count
                && previous.Bands.Zip(table.Bands).All(p => Math.Abs(p.First.Left - p.Second.Left) <= context.Options.Tables.ColumnTolerance * PageWidth(context, table.FirstPage))
                && EndsPage(context, previous)
                && StartsPage(context, table))
            {
                previous.Append(table);
                continue;
            }

            merged.Add(table);
        }

        return merged;
    }

    private static double PageWidth(PipelineContext context, int page) => context.Pages.First(p => p.Number == page).Width;

    private static bool EndsPage(PipelineContext context, Table table)
    {
        LayoutPage page = context.Pages.First(p => p.Number == table.LastPage);
        LayoutLine last = table.Lines[^1];
        return !page.Lines.Any(l => l.Role == LineRole.Unknown && !table.Lines.Contains(l) && l.Box.Top > last.Box.Top);
    }

    private static bool StartsPage(PipelineContext context, Table table)
    {
        LayoutPage page = context.Pages.First(p => p.Number == table.FirstPage);
        LayoutLine first = table.Lines[0];
        return !page.Lines.Any(l => l.Role == LineRole.Unknown && !table.Lines.Contains(l) && l.Box.Top < first.Box.Top);
    }

    /// <summary>A body line with its cells.</summary>
    private sealed class Row(LayoutLine line, List<LineSegment> cells, double wideCell)
    {
        public LayoutLine Line { get; } = line;

        public List<LineSegment> Cells { get; } = cells;

        /// <summary>
        /// At least two cells, not all of them as wide as a text column (two-column running text), separated by real cell
        /// gaps rather than the widened spaces of a justified line.
        /// </summary>
        public bool IsMulti { get; } = cells.Count >= 2 && !cells.All(c => c.Box.Width >= wideCell) && HasCellGaps(cells);

        /// <summary>
        /// Cell gaps are clearly wider than word spaces: the narrowest gap between segments is at least
        /// <see cref="MinCellGapEm"/> of the font size and twice the widest space between words inside a segment.
        /// Justification stretches spaces just past the cell threshold, which splits running text into words.
        /// </summary>
        private static bool HasCellGaps(List<LineSegment> cells)
        {
            double size = cells.SelectMany(c => c.Words).SelectMany(w => w.Glyphs).Select(g => g.PointSize).DefaultIfEmpty(0).Max();
            if (size <= 0)
            {
                size = cells.Max(c => c.Box.Height);
            }

            double minGap = cells.Zip(cells.Skip(1), (a, b) => b.Box.Left - a.Box.Right).Min();
            double maxSpace = cells
                .SelectMany(c => c.Words.Zip(c.Words.Skip(1), (a, b) => b.Box.Left - a.Box.Right))
                .DefaultIfEmpty(0)
                .Max();
            return minGap >= MinCellGapEm * size && minGap >= 2 * maxSpace;
        }
    }

    /// <summary>A table under construction: rows of per-column word lists.</summary>
    private sealed class Table(int page, IReadOnlyList<ColumnBand> bands, string[] exceptions)
    {
        private readonly List<RowDraft> _rows = [];

        public int FirstPage { get; } = page;

        public int LastPage { get; private set; } = page;

        public IReadOnlyList<ColumnBand> Bands { get; } = bands;

        public bool IsFallback { get; init; }

        public List<LayoutLine> Lines { get; } = [];

        public void StartRow() => _rows.Add(new RowDraft(Bands.Count));

        public void AddToRow(Row row, double tolerance)
        {
            RowDraft draft = _rows[^1];
            foreach (LineSegment cell in row.Cells)
            {
                int column = Math.Max(0, ColumnClustering.BandIndex(Bands, cell.Box.Left, tolerance));
                int span = Math.Max(1, ColumnClustering.Span(Bands, cell.Box.Left, cell.Box.Right, tolerance));
                draft.Add(column, span, cell, exceptions);
            }
        }

        public void AddVisualRow(Row row)
        {
            var draft = new RowDraft(row.Cells.Count);
            for (int i = 0; i < row.Cells.Count; i++)
            {
                draft.Add(i, 1, row.Cells[i], exceptions);
            }

            _rows.Add(draft);
        }

        public void Append(Table next)
        {
            List<RowDraft> rows = next._rows;
            if (rows.Count > 0 && _rows.Count > 0 && rows[0].Key == _rows[0].Key)
            {
                rows = rows.Skip(1).ToList();
            }

            _rows.AddRange(rows);
            Lines.AddRange(next.Lines);
            LastPage = next.LastPage;
        }

        public TableBlock ToBlock()
        {
            int columns = IsFallback ? _rows.Max(r => r.Columns) : Bands.Count;
            List<TableRow> rows = _rows.Select(r => r.ToRow(columns, IsFallback)).ToList();
            TableRow? header = _rows.Count > 1 && _rows[0].AllBold ? rows[0] : null;
            return new TableBlock(
                new PageRange(FirstPage, LastPage),
                header,
                header is null ? rows : rows.Skip(1).ToList(),
                columns,
                IsFallback);
        }
    }

    /// <summary>Words of one table row per column, with the span of the cell starting in each column.</summary>
    private sealed class RowDraft(int columns)
    {
        private readonly List<Token>[] _tokens = Enumerable.Range(0, columns).Select(_ => new List<Token>()).ToArray();
        private readonly string?[] _lastLine = new string?[columns];
        private readonly int[] _spans = Enumerable.Repeat(1, columns).ToArray();

        public int Columns => columns;

        public bool AllBold => _tokens.Any(t => t.Count > 0) && _tokens.All(t => t.All(w => w.Style.HasFlag(TextStyle.Bold)));

        /// <summary>Row text per column, used to recognise a header repeated on the next page.</summary>
        public string Key => string.Join("|", _tokens.Select(t => string.Join(' ', t.Select(w => w.Text))));

        /// <summary>Adds a cell segment; a cell continued on a further line is joined with hyphenation rules (FR-012).</summary>
        public void Add(int column, int span, LineSegment cell, string[] exceptions)
        {
            column = Math.Min(column, columns - 1);
            _spans[column] = Math.Max(_spans[column], Math.Min(span, columns - column));
            List<Token> tokens = _tokens[column];
            bool glue = false;
            if (_lastLine[column] is { } previous)
            {
                HyphenJoin join = Hyphenation.Decide(previous, cell.Text, exceptions);
                glue = join != HyphenJoin.None;
                if (join == HyphenJoin.Remove && tokens.Count > 0 && tokens[^1].Text.EndsWith('-'))
                {
                    tokens[^1] = tokens[^1] with { Text = tokens[^1].Text[..^1] };
                }
            }

            for (int i = 0; i < cell.Words.Count; i++)
            {
                tokens.Add(new Token(cell.Words[i].Text, cell.Words[i].Style, Glue: i == 0 && glue));
            }

            _lastLine[column] = cell.Text;
        }

        public TableRow ToRow(int tableColumns, bool fallback)
        {
            var cells = new List<TableCell>();
            int c = 0;
            while (c < Math.Min(columns, tableColumns))
            {
                int span = fallback ? 1 : _spans[c];

                // Text in a column the span would cover is never hidden (FR-066): the span stops before it.
                for (int k = 1; k < span; k++)
                {
                    if (_tokens[c + k].Count > 0)
                    {
                        span = k;
                        break;
                    }
                }

                cells.Add(new TableCell(Inlines(_tokens[c]), span));
                c += span;
            }

            while (cells.Sum(cell => cell.ColumnSpan) < tableColumns)
            {
                cells.Add(new TableCell([]));
            }

            return new TableRow(cells);
        }

        /// <summary>Text runs of a cell; a separating space belongs to the run before it.</summary>
        private static List<Inline> Inlines(List<Token> tokens)
        {
            var inlines = new List<Inline>();
            var text = new StringBuilder();
            TextStyle style = TextStyle.None;
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                string separator = i == 0 || token.Glue ? string.Empty : " ";
                if (text.Length > 0 && token.Style != style)
                {
                    inlines.Add(new TextRun(text.Append(separator).ToString(), style));
                    text.Clear();
                    separator = string.Empty;
                }

                text.Append(separator).Append(token.Text);
                style = token.Style;
            }

            if (text.Length > 0)
            {
                inlines.Add(new TextRun(text.ToString(), style));
            }

            return inlines;
        }

        private sealed record Token(string Text, TextStyle Style, bool Glue);
    }
}
