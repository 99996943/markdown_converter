using System.Globalization;
using System.Text;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects tables (FR-060 – FR-066): lines whose segments (cells, FR-060) line up in column bands form a table region
/// (or a ruled-grid fragment of at least two rows, e.g. the start of a table at the bottom of a page), which becomes a
/// <see cref="TableBlock"/> in <see cref="PipelineContext.Tables"/>; its lines get <see cref="LineRole.Table"/> and
/// <see cref="LayoutAnnotations.TableIndex"/>.
/// </summary>
/// <remarks>
/// A region starts at a line with at least two cells and grows over the following body lines while the vertical gaps
/// stay table-like; it is a table when it holds at least <see cref="TableOptions.MinRows"/> multi-cell lines. A list
/// label followed by text („• tekst”) is one cell, and lines whose cells are all as wide as text columns are running
/// text (FR-031), not table rows, and so are words of a justified line split on widened spaces. Column bands are the
/// clusters of cell left edges shared by at least two rows, snapped to vertical rulings. Rows follow horizontal rulings when the region has a ruled grid; otherwise a line with a single
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
    private const double JustifiedGapSpread = 0.2;
    private const int JustifiedMinSegments = 4;

    /// <summary>Share of the lines of a region in two text columns that hold at most one cell per column.</summary>
    private const double TextColumnLineShare = 0.75;

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
            var layoutBlock = new LayoutBlock(LayoutBlockKind.Table, block.Pages) { Table = block };
            foreach (ColumnBand band in table.Bands)
            {
                layoutBlock.ColumnBands.Add(band.Left);
            }

            context.Tables.Add(layoutBlock);
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
        List<Table> found = TablesInTableDocumentCells(context, page, hyphenationExceptions);
        found.AddRange(FindPageTables(context, page, hyphenationExceptions));
        return found;
    }

    /// <summary>
    /// A table with a grid of its own inside the right cell of a table-document (vertical rulings right of the column
    /// divider, shorter than the frame's): its lines are searched with its rulings, the rest of the cell stays text.
    /// </summary>
    private static List<Table> TablesInTableDocumentCells(PipelineContext context, LayoutPage page, string[] hyphenationExceptions)
    {
        TableDocumentRegion? region = context.TableDocuments.FirstOrDefault(r => page.Number >= r.FirstPage && page.Number <= r.LastPage);
        List<Segment> verticals = page.Rulings.Where(r => r.IsVertical).ToList();
        if (region is null || verticals.Count == 0)
        {
            return [];
        }

        double frameHeight = verticals.Max(r => Math.Abs(r.Y2 - r.Y1));
        var inner = verticals
            .Where(r => r.X1 > region.Divider + RulingSlack && Math.Abs(r.Y2 - r.Y1) < 0.9 * frameHeight)
            .OrderBy(r => Math.Min(r.Y1, r.Y2))
            .ToList();
        var areas = new List<Rect>();
        foreach (Segment r in inner)
        {
            var box = new Rect(r.X1 - RulingSlack, Math.Min(r.Y1, r.Y2) - RulingSlack, r.X1 + RulingSlack, Math.Max(r.Y1, r.Y2) + RulingSlack);
            int i = areas.FindIndex(a => box.Top <= a.Bottom && box.Bottom >= a.Top);
            areas.Add(i < 0 ? box : areas[i].Union(box));
            if (i >= 0)
            {
                areas.RemoveAt(i);
            }
        }

        var tables = new List<Table>();
        foreach (Rect area in areas.Where(a => a.Width > 4 * RulingSlack))
        {
            bool Inside(double x, double y) => x > area.Left && x < area.Right && y > area.Top && y < area.Bottom;
            List<Segment> rulings = page.Rulings.Where(r => Inside((r.X1 + r.X2) / 2, (r.Y1 + r.Y2) / 2)).ToList();
            tables.AddRange(FindTables(
                context,
                page,
                l => l.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex) && Inside(l.Box.CenterX, l.Box.CenterY),
                rulings,
                null,
                hyphenationExceptions,
                inTableDocumentCell: true));
        }

        return tables;
    }

    private static List<Table> FindPageTables(PipelineContext context, LayoutPage page, string[] hyphenationExceptions)
    {
        (double Start, double End)? gutter = context.Options.Layout.DetectColumns ? ReadingOrderStage.FindGutter(page, context.Options.Layout) : null;
        page.ColumnGutter = gutter;
        if (gutter is not { } g)
        {
            return FindTables(context, page, _ => true, [.. page.Rulings], gutter, hyphenationExceptions);
        }

        // FR-031: on a page in columns a table may lie inside one column, beside lines of the other column that share
        // its baselines. Tables over the whole page are found first, without the rulings of tables inside one column
        // (those would take the other column's lines in); the remaining lines are split at the gutter and each column
        // is searched with its own rulings.
        double middle = (g.Start + g.End) / 2;
        bool Left(double from, double to) => to < g.End && from < middle;
        bool Right(double from, double to) => from > g.Start && to > middle;
        bool OneSided(Segment h) => Left(Math.Min(h.X1, h.X2), Math.Max(h.X1, h.X2)) || Right(Math.Min(h.X1, h.X2), Math.Max(h.X1, h.X2));
        var horizontals = page.Rulings.Where(r => r.IsHorizontal).ToList();
        bool InColumn(Segment r)
        {
            if (r.IsHorizontal)
            {
                return OneSided(r);
            }

            var touching = horizontals
                .Where(h => h.Y1 >= Math.Min(r.Y1, r.Y2) - RulingSlack && h.Y1 <= Math.Max(r.Y1, r.Y2) + RulingSlack
                    && r.X1 >= Math.Min(h.X1, h.X2) - RulingSlack && r.X1 <= Math.Max(h.X1, h.X2) + RulingSlack)
                .ToList();
            return touching.Count > 0 && touching.All(OneSided);
        }

        // Lines with a segment inside the ruled area of a column table are that table's: left to the column search.
        var columnAreas = page.Rulings.Where(InColumn)
            .GroupBy(r => Left(Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2)))
            .Select(group => new Rect(
                group.Min(r => Math.Min(r.X1, r.X2)) - RulingSlack,
                group.Min(r => Math.Min(r.Y1, r.Y2)) - RulingSlack,
                group.Max(r => Math.Max(r.X1, r.X2)) + RulingSlack,
                group.Max(r => Math.Max(r.Y1, r.Y2)) + RulingSlack))
            .ToList();
        bool InColumnTable(LayoutLine line) =>
            line.Segments.Any(seg => columnAreas.Any(a => seg.Box.CenterX > a.Left && seg.Box.CenterX < a.Right && seg.Box.CenterY > a.Top && seg.Box.CenterY < a.Bottom));
        List<Table> tables = FindTables(context, page, l => !InColumnTable(l), page.Rulings.Where(r => !InColumn(r)).ToList(), gutter, hyphenationExceptions);
        var taken = new HashSet<LayoutLine>(tables.SelectMany(t => t.Lines), ReferenceEqualityComparer.Instance);
        SplitAtGutter(page, Left, Right, taken);
        tables.AddRange(FindTables(context, page, l => !taken.Contains(l) && Left(l.Box.Left, l.Box.Right), page.Rulings.Where(r => Left(Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2))).ToList(), null, hyphenationExceptions));
        tables.AddRange(FindTables(context, page, l => !taken.Contains(l) && Right(l.Box.Left, l.Box.Right), page.Rulings.Where(r => Right(Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2))).ToList(), null, hyphenationExceptions));
        return tables;
    }

    /// <summary>A merged line with segments on both sides of the gutter (none across it) becomes one line per column.</summary>
    private static void SplitAtGutter(LayoutPage page, Func<double, double, bool> inLeft, Func<double, double, bool> inRight, HashSet<LayoutLine> taken)
    {
        for (int i = 0; i < page.Lines.Count; i++)
        {
            LayoutLine line = page.Lines[i];
            List<LineSegment> left = line.Segments.Where(s => inLeft(s.Box.Left, s.Box.Right)).ToList();
            List<LineSegment> right = line.Segments.Where(s => inRight(s.Box.Left, s.Box.Right)).ToList();
            if (line.Role != LineRole.Unknown || taken.Contains(line) || left.Count == 0 || right.Count == 0 || left.Count + right.Count != line.Segments.Count)
            {
                continue;
            }

            page.Lines[i] = LineSlicer.Slice(line, left.SelectMany(s => s.Words).ToList());
            page.Lines.Insert(i + 1, LineSlicer.Slice(line, right.SelectMany(s => s.Words).ToList()));
            i++;
        }
    }

    private static List<Table> FindTables(
        PipelineContext context,
        LayoutPage page,
        Func<LayoutLine, bool> inScope,
        IReadOnlyList<Segment> rulings,
        (double Start, double End)? gutter,
        string[] hyphenationExceptions,
        bool inTableDocumentCell = false)
    {
        TableOptions options = context.Options.Tables;
        double tolerance = options.ColumnTolerance * page.Width;
        double wideCell = context.Options.Layout.ColumnMinLineWidthRatio * page.Width;
        List<Row> flow = page.Lines
            .Where(l => l.Role == LineRole.Unknown && l.Segments.Count > 0 && !l.Annotations.ContainsKey(LayoutAnnotations.StepIndex) && (inTableDocumentCell || !l.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex)))
            .Where(inScope)
            .Select(l => new Row(l, CellsOf(l), wideCell))
            .ToList();

        var tables = new List<Table>();
        int start = 0;
        int free = 0;
        while (start < flow.Count)
        {
            if (!flow[start].IsMulti)
            {
                start++;
                continue;
            }

            List<Row> region = GrowRegion(context, flow, start);
            Table? table = Build(context, page, rulings, region, flow.GetRange(free, start - free), gutter, tolerance, hyphenationExceptions);
            if (table is null)
            {
                start++;
                continue;
            }

            tables.Add(table);
            start = flow.FindIndex(r => ReferenceEquals(r.Line, table.Lines[^1])) + 1;
            free = start;
        }

        return tables;
    }

    /// <summary>
    /// A bold column-name row and a few rows ending the page, with the same column-name row repeated at the top of the
    /// next page: the start of a table that continues there, however few rows fit.
    /// </summary>
    private static bool StartsAtPageEnd(PipelineContext context, LayoutPage page, List<Row> region)
    {
        Row header = region[0];
        if (!header.IsMulti || !header.IsAllBold
            || page.Lines.Any(l => l.Role == LineRole.Unknown && l.Baseline > region[^1].Line.Baseline))
        {
            return false;
        }

        LayoutLine? next = context.Pages.FirstOrDefault(p => p.Number == page.Number + 1)?.Lines
            .FirstOrDefault(l => l.Role == LineRole.Unknown && l.Segments.Count > 0);
        return next is not null && string.Equals(next.Text, header.Line.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The first lines of a page: a bold column-name row that the previous page also has, and a few rows — the end of a
    /// table continued from there, however few rows remain.
    /// </summary>
    private static bool ContinuesFromPreviousPage(PipelineContext context, LayoutPage page, List<Row> region)
    {
        Row header = region[0];
        if (!header.IsMulti || !header.IsAllBold
            || !ReferenceEquals(page.Lines.FirstOrDefault(l => l.Role == LineRole.Unknown && l.Segments.Count > 0), header.Line))
        {
            return false;
        }

        LayoutPage? previous = context.Pages.FirstOrDefault(p => p.Number == page.Number - 1);
        return previous is not null && previous.Lines.Any(l => string.Equals(l.Text, header.Line.Text, StringComparison.Ordinal));
    }

    /// <summary>Cells of a line: its segments, with a lone bullet or list label joined to the text it introduces.</summary>
    private static List<LineSegment> CellsOf(LayoutLine line)
    {
        var cells = new List<LineSegment>();
        LineSegment? label = null;
        foreach (LineSegment segment in line.Segments)
        {
            if (label is not null)
            {
                cells.Add(new LineSegment([.. label.Words, .. segment.Words], label.Box.Union(segment.Box)));
                label = null;
            }
            else if (segment.Words.Count == 1 && IsLabel(segment.Text))
            {
                label = segment;
            }
            else
            {
                cells.Add(segment);
            }
        }

        if (label is not null)
        {
            cells.Add(label);
        }

        return cells;
    }

    /// <summary>A bullet or „1)”/„a)” label; a lone dash is not one — in fee tables it is a value („no fee”).</summary>
    private static bool IsLabel(string text) =>
        ListLabelPatterns.TryMatch(text + " x", out ListLabelMatch? label)
        && label.Kind is ListLabelKind.Bullet or ListLabelKind.ArabicParen or ListLabelKind.LetterParen;

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

    /// <param name="context">Pipeline context.</param>
    /// <param name="page">The page.</param>
    /// <param name="pageRulings">Rulings of the page (of the column searched).</param>
    /// <param name="candidate">Lines from the seed on, as grown by <see cref="GrowRegion"/>.</param>
    /// <param name="above">Free body lines above the seed (not taken by an earlier table), top to bottom.</param>
    /// <param name="gutter">Column gutter of the page (FR-031), if any.</param>
    /// <param name="tolerance">Column tolerance in points.</param>
    /// <param name="exceptions">Hyphenation exceptions.</param>
    private static Table? Build(
        PipelineContext context,
        LayoutPage page,
        IReadOnlyList<Segment> pageRulings,
        List<Row> candidate,
        List<Row> above,
        (double Start, double End)? gutter,
        double tolerance,
        string[] exceptions)
    {
        TableOptions options = context.Options.Tables;
        IEnumerable<Segment> rulings = options.UseRulingLines ? pageRulings : [];
        var grid = new Grid(rulings, candidate);
        List<Row> region = CutAtGridGap(TrimTrailingLines(CutAtRunningText(candidate, page, pageRulings, context), options, grid, tolerance), grid);

        // Lines above the top border of a ruled grid are not part of it (numbered paragraphs introducing the table):
        // the seed moves on until it reaches the grid, and these lines stay running text.
        if (!grid.Contains(region[0].Line.Box.CenterY) && region.Any(r => grid.Contains(r.Line.Box.CenterY)))
        {
            return null;
        }

        // Likewise a gridless table starts at its bold column-name row: multi-cell lines above it (numbered clauses
        // with a hanging indent) are running text, so the seed moves on to the header.
        if (!region[0].IsAllBold && region.Skip(1).Any(r => r.IsMulti && r.IsAllBold))
        {
            return null;
        }

        int multiCount = region.Count(r => r.IsMulti);
        bool ruledFragment = multiCount >= 1 && grid.Rows(region) >= 2 && region.All(r => grid.Contains(r.Line.Box.CenterY));
        if (multiCount < options.MinRows && !ruledFragment && !(multiCount >= 2 && (StartsAtPageEnd(context, page, region) || ContinuesFromPreviousPage(context, page, region))))
        {
            return null;
        }

        // Single-cell lines above the seed belong to the table: inside a ruled grid every line in the same grid (a
        // section row), otherwise lines close above (the upper line of a two-line header cell).
        double rowGap = options.RowMergeGapFactor * TableLeading(region);
        bool seedInGrid = grid.Contains(region[0].Line.Box.CenterY);
        for (int i = above.Count - 1; i >= 0; i--)
        {
            Row row = above[i];
            double gap = region[0].Line.Baseline - row.Line.Baseline;
            bool belongs = seedInGrid ? grid.Contains(row.Line.Box.CenterY) : gap <= rowGap;

            // Above a bold column-name row, a line running across its column boundary is text, not a cell.
            bool headerLine = !region[0].IsAllBold
                || row.IsAllBold
                || region[0].Cells.Count < 2
                || row.Cells[^1].Box.Right <= region[0].Cells[1].Box.Left;
            if (row.IsMulti || gap <= 0 || !belongs || !headerLine)
            {
                break;
            }

            region.Insert(0, row);
        }

        // Inside a ruled grid the rulings make the rows: a line whose cells start in the table's columns is a row even when
        // they happen to be spaced like the words of a justified line.
        bool gridRegion = grid.Rows(region) >= 2 && region.All(r => grid.Contains(r.Line.Box.CenterY));
        List<Row> rowsOfCells = region.Where(r => r.IsMulti).ToList();
        List<Row> multi = region.Where(r => r.IsMulti || (gridRegion && IsAligned(r, rowsOfCells, tolerance))).ToList();

        double top = region.Min(r => r.Line.Box.Top);
        double bottom = region.Max(r => r.Line.Box.Bottom);
        double left = region.Min(r => r.Line.Box.Left);
        double right = region.Max(r => r.Line.Box.Right);
        List<double> verticals = rulings
            .Where(s => s.IsVertical && Math.Max(s.Y1, s.Y2) >= top - RulingSlack && Math.Min(s.Y1, s.Y2) <= bottom + RulingSlack)
            .Select(s => s.X1)
            .ToList();

        // A column needs cells in two rows. In a ruled grid the LAST column may be empty in every data row (a checklist's
        // „Wykonano”): its header cell alone makes it a column when a vertical ruling stands right before it.
        var clusters = ColumnClustering.ClusterLefts(multi.SelectMany(r => r.Cells.Select(c => c.Box.Left)), tolerance);
        List<double> lefts = clusters
            .Where(c => multi.Count(r => r.Cells.Any(cell => cell.Box.Left >= c && cell.Box.Left - c <= tolerance)) >= Math.Min(MinBandSupport, multi.Count))
            .ToList();
        List<double> ruledOnly = clusters.Where(c => !lefts.Contains(c) && verticals.Any(x => x <= c && c - x <= tolerance)).ToList();
        if (lefts.Count > 0 && ruledOnly.Count == 1 && ruledOnly[0] > lefts.Max())
        {
            lefts.Add(ruledOnly[0]);
        }

        if (lefts.Count < 2)
        {
            return null;
        }

        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands(lefts, right, verticals, tolerance);
        // Rows between rulings that cross the gutter are a table, not running text in two columns (T089k).
        if (IsHangingList(bands, multi) || (!gridRegion && IsTextColumns(region, gutter)))
        {
            return null;
        }

        // With a ruled grid the rulings define the rows, so lines of one row may carry different numbers of cells.
        List<double> horizontals = HorizontalRulings(rulings, region);
        bool inGrid = grid.Rows(region) >= 2 && region.All(r => grid.Contains(r.Line.Box.CenterY));
        bool ruled = inGrid || horizontals.Count >= 2;
        // Without rulings, a multi-cell line right below a row and empty in the first column continues that row (its
        // service name and its mode both wrap); it neither starts a row nor makes the grid ambiguous.
        var continuations = new HashSet<Row>();
        if (!ruled)
        {
            for (int i = 1; i < region.Count; i++)
            {
                Row row = region[i];
                if (row.IsMulti
                    && row.Line.Baseline - region[i - 1].Line.Baseline <= rowGap
                    && ColumnClustering.BandIndex(bands, row.Cells[0].Box.Left, tolerance) > 0
                    && region.Count(r => r.IsMulti && ColumnClustering.BandIndex(bands, r.Cells[0].Box.Left, tolerance) == 0) >= 2)
                {
                    continuations.Add(row);
                }
            }
        }

        bool ambiguous = (!ruled && multi.Where(r => !continuations.Contains(r)).Select(r => r.Cells.Count).Distinct().Count() > 1)
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

        if (ruled)
        {
            // Ruled grid: every line goes to the row between the rulings around its centre.
            foreach (IGrouping<int, Row> group in region.GroupBy(r => inGrid ? grid.RowIndex(r.Line.Box.CenterY) : horizontals.Count(y => y <= r.Line.Box.CenterY)))
            {
                table.StartRow();
                foreach (Row row in group)
                {
                    table.AddToRow(row, tolerance);
                }
            }

            return table;
        }

        Row? previous = null;
        foreach (Row row in region)
        {
            // A line with several segments starting in the first column opens a row even when its spacing made it
            // look like a justified line (not multi-cell).
            bool opensRow = row.Cells.Count >= 2 && ColumnClustering.BandIndex(bands, row.Cells[0].Box.Left, tolerance) == 0;
            bool continues = previous is not null
                && !opensRow
                && (!row.IsMulti || continuations.Contains(row))
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

    /// <summary>Two bands whose first one holds only list labels („1.”, „a)”): numbered paragraphs with a hanging indent.</summary>
    private static bool IsHangingList(IReadOnlyList<ColumnBand> bands, List<Row> multi) =>
        bands.Count == 2
        && multi.All(r => r.Cells.Count == 2 && r.Cells[0].Words.Count == 1 && ListLabelPatterns.TryMatch(r.Cells[0].Text + " x", out _));

    /// <summary>
    /// Running text in two columns is left to reading order (FR-031) even where lines of both columns share a baseline:
    /// on a page with a column gutter, a region whose cells all lie on either side of it (none crossing) is not a table.
    /// </summary>
    private static bool IsTextColumns(List<Row> region, (double Start, double End)? gutter)
    {
        if (gutter is not { } g)
        {
            return false;
        }

        double middle = (g.Start + g.End) / 2;
        List<LineSegment> cells = region.SelectMany(r => r.Cells).ToList();
        if (!cells.Any(c => c.Box.Left < middle) || !cells.Any(c => c.Box.Left >= middle))
        {
            return false;
        }

        if (cells.All(c => c.Box.Right <= middle || c.Box.Left >= middle))
        {
            return true;
        }

        // A ragged column can reach into the free band past its middle: then no cell may span the band, and the lines
        // must be running text — mostly one cell per column (a label joins its text), where table columns put several
        // cells on one side.
        int textLines = region.Count(r => TextCells(r.Cells.Where(c => c.Box.Left < middle).ToList()) <= 1 && TextCells(r.Cells.Where(c => c.Box.Left >= middle).ToList()) <= 1);
        return textLines >= TextColumnLineShare * region.Count
            && cells.All(c => c.Box.Right <= g.End || c.Box.Left >= g.Start);
    }

    /// <summary>Cells of one column of a line, a leading list label („1.”, „2)”) counted with the text it introduces.</summary>
    private static int TextCells(List<LineSegment> cells) =>
        cells.Count > 1 && ListLabelPatterns.TryMatch(cells[0].Text + " x", out ListLabelMatch? label) && string.Equals(label.Label, cells[0].Text, StringComparison.Ordinal)
            ? cells.Count - 1
            : cells.Count;

    /// <summary>
    /// Keeps the lines up to the last multi-cell line plus the single-cell lines that still belong to the last row: inside
    /// the ruled grid, or close below and within one column (FR-062).
    /// </summary>
    private static List<Row> TrimTrailingLines(List<Row> region, TableOptions options, Grid grid, double tolerance)
    {
        List<Row> rows = region.Where(r => r.IsMulti).ToList();
        int lastMulti = region.FindLastIndex(r => r.IsMulti || IsAligned(r, rows, tolerance));
        double rowGap = options.RowMergeGapFactor * TableLeading(region.Take(lastMulti + 1).ToList());
        bool multiInGrid = grid.Contains(region[lastMulti].Line.Box.CenterY);
        int end = lastMulti;
        for (int i = lastMulti + 1; i < region.Count; i++)
        {
            LayoutLine line = region[i].Line;
            bool insideGrid = multiInGrid && grid.Contains(line.Box.CenterY);
            bool closeBelow = line.Baseline - region[i - 1].Line.Baseline <= rowGap && region[i].Cells.Count == 1;
            if (!insideGrid && !closeBelow)
            {
                break;
            }

            end = i;
        }

        return region.Take(end + 1).ToList();
    }

    /// <summary>
    /// Distinct Y of horizontal rulings spanning a good part of the region width; collinear pieces (a border drawn cell
    /// by cell) are joined first.
    /// </summary>
    private static List<double> HorizontalRulings(IEnumerable<Segment> rulings, List<Row> region, double margin = RulingSlack)
    {
        double top = region.Min(r => r.Line.Box.Top) - margin;
        double bottom = region.Max(r => r.Line.Box.Bottom) + margin;
        double width = region.Max(r => r.Line.Box.Right) - region.Min(r => r.Line.Box.Left);
        List<(double Y, double X1, double X2)> pieces = rulings
            .Where(s => s.IsHorizontal && s.Y1 >= top && s.Y1 <= bottom)
            .Select(s => (s.Y1, Math.Min(s.X1, s.X2), Math.Max(s.X1, s.X2)))
            .OrderBy(p => p.Item1)
            .ThenBy(p => p.Item2)
            .ToList();

        var distinct = new List<double>();
        int i = 0;
        while (i < pieces.Count)
        {
            double y = pieces[i].Y;
            List<(double Y, double X1, double X2)> line = pieces.Skip(i).TakeWhile(p => p.Y - y <= RulingSlack).OrderBy(p => p.X1).ToList();
            i += line.Count;

            double longest = 0;
            double start = line[0].X1;
            double end = line[0].X2;
            foreach ((double _, double x1, double x2) in line.Skip(1))
            {
                if (x1 - end <= RulingSlack)
                {
                    end = Math.Max(end, x2);
                    continue;
                }

                longest = Math.Max(longest, end - start);
                (start, end) = (x1, x2);
            }

            longest = Math.Max(longest, end - start);
            if (longest >= RulingSpanRatio * width && (distinct.Count == 0 || y - distinct[^1] > RulingSlack))
            {
                distinct.Add(y);
            }
        }

        return distinct;
    }

    /// <summary>
    /// A gridless table ends before a single-cell line that starts at the table's left edge and runs across into its
    /// second column: a note under the table, the next section heading or a paragraph — running text, not a row.
    /// </summary>
    private static List<Row> CutAtRunningText(List<Row> region, LayoutPage page, IReadOnlyList<Segment> rulings, PipelineContext context)
    {
        if (rulings.Any(r => r.IsVertical))
        {
            return region;
        }

        double tolerance = context.Options.Tables.ColumnTolerance * page.Width;
        List<Row> rows = region.Where(r => r.IsMulti).ToList();
        if (rows.Count == 0)
        {
            return region;
        }

        double left = rows.Min(r => r.Cells[0].Box.Left);
        double second = rows.Min(r => r.Cells[1].Box.Left);

        // Below a bold column-name row, one row is enough to tell the table's columns from text crossing them.
        int minRows = region[0].IsMulti && region[0].IsAllBold ? 2 : context.Options.Tables.MinRows;
        int multi = 0;
        for (int j = 0; j < region.Count; j++)
        {
            Row row = region[j];
            if (row.IsMulti)
            {
                multi++;
                continue;
            }

            if (multi >= minRows
                && !IsAligned(row, rows, tolerance)
                && row.Line.Box.Right > second + tolerance
                && Math.Abs(row.Line.Box.Left - left) <= tolerance)
            {
                return region.Take(j).ToList();
            }
        }

        return region;
    }

    /// <summary>A line whose segments all start in the table's columns is a row (spaced evenly by chance), not text.</summary>
    private static bool IsAligned(Row row, List<Row> rows, double tolerance) =>
        row.Cells.Count >= 2
        && row.Cells.All(c => rows.Any(r => r.Cells.Any(m => Math.Abs(m.Box.Left - c.Box.Left) <= tolerance)));

    /// <summary>
    /// With a ruled grid, the region ends before the first line that leaves it after an earlier line was inside: text
    /// between two separately ruled tables, or below a ruled table.
    /// </summary>
    private static List<Row> CutAtGridGap(List<Row> region, Grid grid)
    {
        bool entered = false;
        for (int j = 0; j < region.Count; j++)
        {
            bool inside = grid.Contains(region[j].Line.Box.CenterY);
            if (entered && !inside)
            {
                return region.Take(j).ToList();
            }

            entered |= inside;
        }

        return region;
    }

    /// <summary>
    /// The ruled grids of a page: a Y lies inside when it is between two consecutive horizontal borders (spanning a good
    /// part of the region width) whose band a vertical ruling crosses.
    /// </summary>
    private sealed class Grid(IEnumerable<Segment> rulings, List<Row> region)
    {
        private readonly List<double> _borders = HorizontalRulings(rulings, region, margin: double.MaxValue / 4);
        private readonly List<Segment> _verticals = rulings.Where(s => s.IsVertical).ToList();

        /// <summary>Index of the band between horizontal borders that <paramref name="y"/> falls into.</summary>
        public int RowIndex(double y) => _borders.Count(b => b <= y);

        /// <summary>Number of distinct grid rows the lines of <paramref name="region"/> fall into.</summary>
        public int Rows(List<Row> region) =>
            region.Select(r => r.Line.Box.CenterY).Where(Contains).Select(y => _borders.Count(b => b <= y)).Distinct().Count();

        public bool Contains(double y)
        {
            int k = _borders.Count(b => b <= y);
            if (k == 0 || k == _borders.Count)
            {
                return false;
            }

            double middle = (_borders[k - 1] + _borders[k]) / 2;
            return _verticals.Any(v => Math.Min(v.Y1, v.Y2) <= middle && Math.Max(v.Y1, v.Y2) >= middle);
        }
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
                && SameColumns(previous.Bands, table.Bands, context.Options.Tables.ColumnTolerance * PageWidth(context, table.FirstPage))
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

    /// <summary>
    /// Every band of the continuation starts inside the corresponding band of the table it continues (a header or a
    /// ruling can move a band edge on one page only).
    /// </summary>
    private static bool SameColumns(IReadOnlyList<ColumnBand> previous, IReadOnlyList<ColumnBand> next, double tolerance) =>
        previous.Count == next.Count
        && next.Select((band, i) => ColumnClustering.BandIndex(previous, band.Left, tolerance) == i).All(match => match);

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

        /// <summary>Every glyph of the line is bold (a column-name row).</summary>
        public bool IsAllBold { get; } = line.Words.SelectMany(w => w.Glyphs).Where(g => !string.IsNullOrWhiteSpace(g.Text)).All(g => g.IsBold)
            && line.Words.Count > 0;

        /// <summary>
        /// At least two cells, not all of them as wide as a text column (two-column running text), separated by real cell
        /// gaps rather than the widened spaces of a justified line.
        /// </summary>
        public bool IsMulti { get; } = cells.Count >= 2
            && !cells.All(c => c.Box.Width >= wideCell)
            && HasCellGaps(cells)
            && !IsEvenlySpaced(cells);

        /// <summary>
        /// A sparse justified line can stretch its spaces beyond 1 em, but then all of them alike:
        /// <see cref="JustifiedMinSegments"/> or more segments whose gaps all lie within
        /// <see cref="JustifiedGapSpread"/> of their median are words, not cells.
        /// </summary>
        private static bool IsEvenlySpaced(List<LineSegment> cells)
        {
            if (cells.Count < JustifiedMinSegments)
            {
                return false;
            }

            List<double> gaps = cells.Zip(cells.Skip(1), (a, b) => b.Box.Left - a.Box.Right).Order().ToList();
            double median = gaps[gaps.Count / 2];
            return median > 0 && gaps.All(g => Math.Abs(g - median) <= JustifiedGapSpread * median);
        }

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
