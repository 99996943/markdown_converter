using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects table-documents (spec 002, FR-080 – FR-084): a document made of one multi-page table with a full grid of
/// rulings and two columns — a narrow left one with section names and a wide right one with their content.
/// </summary>
/// <remarks>
/// A page frame is three vertical rulings with a common vertical extent (left edge, column divider, right edge); row
/// edges are horizontal rulings whose pieces cover both columns, so link underlines inside a cell do not split rows.
/// Consecutive pages with frames at the same positions form a region; it is a table-document when its left column is
/// narrow, it spans enough of the document, its cells are long and at least one of them holds a list, several
/// paragraphs or crosses a page boundary. Lines inside the frames of a table-document are annotated with
/// <see cref="LayoutAnnotations.TableDocumentIndex"/>; other stages skip or treat them as single-column text.
/// </remarks>
public sealed class TableDocumentStage : IPipelineStage
{
    private const double Tolerance = 3;
    private const double MinRulingLength = 20;
    private const double EdgeCoverage = 0.9;
    private const double ParagraphGapFactor = 1.5;
    private const int MaxHeaderWords = 5;

    /// <inheritdoc />
    public int Order => StageOrder.TableDocument;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TableOptions options = context.Options.Tables;
        if (!options.DetectTableDocuments)
        {
            return;
        }

        int pagesWithText = context.Pages.Count(p => p.Skipped is null && p.Lines.Any(l => l.Role != LineRole.Artifact && l.Words.Count > 0));
        foreach (List<PageFrame> region in Regions(context))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            List<LogicalRow> rows = LogicalRows(region);
            if (IsTableDocument(region, rows, pagesWithText, options))
            {
                Apply(context, region, rows);
            }
        }
    }

    /// <summary>Runs of consecutive pages whose frames have the same column positions; pages with step-scheme lines break a run.</summary>
    private static IEnumerable<List<PageFrame>> Regions(PipelineContext context)
    {
        var run = new List<PageFrame>();
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            PageFrame? frame = page.Skipped is null && !page.Lines.Any(l => l.Annotations.ContainsKey(LayoutAnnotations.StepIndex))
                ? FindFrame(page)
                : null;

            if (frame is null || (run.Count > 0 && !SameColumns(run[^1], frame)))
            {
                if (run.Count > 0)
                {
                    yield return run;
                }

                run = frame is null ? [] : [frame];
                continue;
            }

            run.Add(frame);
        }

        if (run.Count > 0)
        {
            yield return run;
        }
    }

    private static bool SameColumns(PageFrame a, PageFrame b) =>
        Math.Abs(a.Left - b.Left) <= Tolerance && Math.Abs(a.Divider - b.Divider) <= Tolerance && Math.Abs(a.Right - b.Right) <= Tolerance;

    /// <summary>The two-column frame of a page: exactly three vertical rulings over the extent of the longest one.</summary>
    private static PageFrame? FindFrame(LayoutPage page)
    {
        List<(double X, double Top, double Bottom)> verticals = Merge(
            page.Rulings.Where(r => r.IsVertical).Select(r => (X: (r.X1 + r.X2) / 2, Top: Math.Min(r.Y1, r.Y2), Bottom: Math.Max(r.Y1, r.Y2))));
        if (verticals.Count == 0)
        {
            return null;
        }

        (double _, double top, double bottom) = verticals.MaxBy(v => v.Bottom - v.Top);
        if (bottom - top < MinRulingLength)
        {
            return null;
        }

        List<double> xs = verticals
            .Where(v => Math.Abs(v.Top - top) <= Tolerance && Math.Abs(v.Bottom - bottom) <= Tolerance)
            .Select(v => v.X)
            .Order()
            .ToList();
        if (xs.Count != 3)
        {
            return null;
        }

        var frame = new PageFrame(page, xs[0], xs[1], xs[2], top, bottom);
        frame.Edges.AddRange(RowEdges(page, frame));
        frame.Rows.AddRange(Rows(page, frame));
        return frame;
    }

    /// <summary>Vertical rulings grouped by X (pieces of one ruling joined), with their combined extent.</summary>
    private static List<(double X, double Top, double Bottom)> Merge(IEnumerable<(double X, double Top, double Bottom)> pieces)
    {
        var merged = new List<(double X, double Top, double Bottom)>();
        foreach ((double x, double top, double bottom) in pieces.OrderBy(p => p.X).ThenBy(p => p.Top))
        {
            int i = merged.FindIndex(m => Math.Abs(m.X - x) <= Tolerance && top <= m.Bottom + Tolerance && bottom >= m.Top - Tolerance);
            if (i < 0)
            {
                merged.Add((x, top, bottom));
            }
            else
            {
                merged[i] = (merged[i].X, Math.Min(merged[i].Top, top), Math.Max(merged[i].Bottom, bottom));
            }
        }

        return merged;
    }

    /// <summary>Row edges: the frame's top and bottom and horizontal rulings whose pieces cover both columns.</summary>
    private static IEnumerable<double> RowEdges(LayoutPage page, PageFrame frame)
    {
        var edges = new List<double> { frame.Top, frame.Bottom };
        foreach (IGrouping<double, Segment> group in page.Rulings
            .Where(r => r.IsHorizontal && r.Y1 > frame.Top + Tolerance && r.Y1 < frame.Bottom - Tolerance)
            .GroupBy(r => Math.Round(r.Y1)))
        {
            List<(double From, double To)> spans = group.Select(r => (Math.Min(r.X1, r.X2), Math.Max(r.X1, r.X2))).ToList();
            if (Covered(spans, frame.Left, frame.Divider) >= EdgeCoverage && Covered(spans, frame.Divider, frame.Right) >= EdgeCoverage)
            {
                edges.Add(group.Average(r => r.Y1));
            }
        }

        return edges.Order();
    }

    /// <summary>Share of [<paramref name="from"/>, <paramref name="to"/>] covered by the spans.</summary>
    private static double Covered(List<(double From, double To)> spans, double from, double to)
    {
        double covered = 0;
        double cursor = from;
        foreach ((double start, double end) in spans.OrderBy(s => s.From))
        {
            double a = Math.Max(cursor, start - Tolerance);
            double b = Math.Min(to, end);
            if (b > a)
            {
                covered += b - a;
                cursor = b;
            }
        }

        return (to - from) <= 0 ? 0 : covered / (to - from);
    }

    /// <summary>The rows of a frame with the words of their left and right cells, line by line.</summary>
    private static IEnumerable<FrameRow> Rows(LayoutPage page, PageFrame frame)
    {
        List<LayoutLine> inside = page.Lines.Where(l => Inside(l, frame)).ToList();
        for (int i = 0; i + 1 < frame.Edges.Count; i++)
        {
            double top = frame.Edges[i];
            double bottom = frame.Edges[i + 1];
            var row = new FrameRow(frame, top, bottom);
            foreach (LayoutLine line in inside.Where(l => l.Box.CenterY > top && l.Box.CenterY < bottom))
            {
                row.Lines.Add(line);
            }

            yield return row;
        }
    }

    private static bool Inside(LayoutLine line, PageFrame frame) =>
        line.Words.Count > 0
        && line.Role != LineRole.Artifact
        && line.Box.CenterY > frame.Top && line.Box.CenterY < frame.Bottom
        && line.Box.Left >= frame.Left - Tolerance && line.Box.Right <= frame.Right + Tolerance;

    /// <summary>Rows joined across page boundaries: a page's first row with an empty left cell continues the previous row.</summary>
    private static List<LogicalRow> LogicalRows(List<PageFrame> region)
    {
        var rows = new List<LogicalRow>();
        foreach (PageFrame frame in region)
        {
            for (int i = 0; i < frame.Rows.Count; i++)
            {
                FrameRow row = frame.Rows[i];
                if (row.Lines.Count == 0)
                {
                    continue;
                }

                if (rows.Count > 0 && row.LeftWords.Count == 0 && (i == 0 || (i == 1 && IsHeaderLike(frame.Rows[0]))) && rows[^1].Parts[^1].Frame != frame)
                {
                    rows[^1].Parts.Add(row);
                    continue;
                }

                rows.Add(new LogicalRow(row));
            }
        }

        return rows;
    }

    /// <summary>A row with one short line in each cell (the column-name row, e.g. „Definicje | Wyjaśnienie”).</summary>
    private static bool IsHeaderLike(FrameRow row) =>
        row.Lines.Count == 1 && row.LeftWords.Count is > 0 and <= MaxHeaderWords && row.RightWords.Count is > 0 and <= MaxHeaderWords;

    private static bool IsTableDocument(List<PageFrame> region, List<LogicalRow> rows, int pagesWithText, TableOptions options)
    {
        PageFrame first = region[0];
        if ((first.Divider - first.Left) / (first.Right - first.Left) > options.TableDocumentMaxLeftColumnRatio
            || region.Count < options.TableDocumentMinPages
            || region.Count < options.TableDocumentMinPageRatio * pagesWithText)
        {
            return false;
        }

        List<LogicalRow> named = rows.Where(r => r.Parts[0].LeftWords.Count > 0 && !IsHeaderLike(r.Parts[0])).ToList();
        if (named.Count == 0)
        {
            return false;
        }

        List<int> counts = named.Select(r => r.Parts.Sum(p => p.RightWords.Count)).Order().ToList();
        double median = counts.Count % 2 == 1
            ? counts[counts.Count / 2]
            : (counts[(counts.Count / 2) - 1] + counts[counts.Count / 2]) / 2.0;
        if (median < options.TableDocumentMinMedianWords)
        {
            return false;
        }

        double leading = TypicalLeading(rows);
        return rows.Any(r => r.Parts.Count > 1 || r.Parts.Any(p => HasListOrParagraphs(p, leading)));
    }

    /// <summary>Median distance between consecutive right-cell lines of a row.</summary>
    private static double TypicalLeading(List<LogicalRow> rows)
    {
        List<double> gaps = rows
            .SelectMany(r => r.Parts)
            .SelectMany(p => Gaps(p.RightLines()))
            .Order()
            .ToList();
        return gaps.Count == 0 ? 0 : gaps[gaps.Count / 2];
    }

    private static IEnumerable<double> Gaps(List<(double Baseline, List<LayoutWord> Words)> lines) =>
        lines.Zip(lines.Skip(1), (a, b) => b.Baseline - a.Baseline);

    private static bool HasListOrParagraphs(FrameRow row, double leading)
    {
        List<(double Baseline, List<LayoutWord> Words)> lines = row.RightLines();
        return lines.Any(l => ListLabelPatterns.TryMatch(string.Join(' ', l.Words.Select(w => w.Text)), out _))
            || (leading > 0 && Gaps(lines).Any(g => g > ParagraphGapFactor * leading));
    }

    private static void Apply(PipelineContext context, List<PageFrame> region, List<LogicalRow> rows)
    {
        int index = context.TableDocuments.Count;
        string key = index.ToString(CultureInfo.InvariantCulture);
        List<LayoutWord> content = rows.SelectMany(r => r.Parts).SelectMany(p => p.RightWords).ToList();
        foreach (PageFrame frame in region)
        {
            foreach (LayoutLine line in frame.Rows.SelectMany(r => r.Lines))
            {
                line.Annotations[LayoutAnnotations.TableDocumentIndex] = key;
            }
        }

        PageFrame first = region[0];
        context.TableDocuments.Add(new TableDocumentRegion(
            index,
            first.Page.Number,
            region[^1].Page.Number,
            first.Top,
            first.Divider,
            content.Count > 0 ? content.Min(w => w.Box.Left) : first.Divider,
            content.Count > 0 ? content.Max(w => w.Box.Right) : first.Right,
            rows.Count(r => r.Parts[0].LeftWords.Count > 0 && !IsHeaderLike(r.Parts[0])),
            null,
            0));
    }

    /// <summary>The two-column frame of one page with its row edges (top to bottom) and rows.</summary>
    private sealed class PageFrame(LayoutPage page, double left, double divider, double right, double top, double bottom)
    {
        public LayoutPage Page { get; } = page;

        public double Left { get; } = left;

        public double Divider { get; } = divider;

        public double Right { get; } = right;

        public double Top { get; } = top;

        public double Bottom { get; } = bottom;

        public List<double> Edges { get; } = [];

        public List<FrameRow> Rows { get; } = [];
    }

    /// <summary>A row of one page between two row edges, with the lines whose middle lies in it.</summary>
    private sealed class FrameRow(PageFrame frame, double top, double bottom)
    {
        public PageFrame Frame { get; } = frame;

        public double Top { get; } = top;

        public double Bottom { get; } = bottom;

        public List<LayoutLine> Lines { get; } = [];

        public List<LayoutWord> LeftWords => Lines.SelectMany(l => l.Words).Where(w => w.Box.CenterX < Frame.Divider).ToList();

        public List<LayoutWord> RightWords => Lines.SelectMany(l => l.Words).Where(w => w.Box.CenterX >= Frame.Divider).ToList();

        /// <summary>Right-cell words of each line that has some, with the line's baseline.</summary>
        public List<(double Baseline, List<LayoutWord> Words)> RightLines() =>
            Lines
                .Select(l => (l.Baseline, Words: l.Words.Where(w => w.Box.CenterX >= Frame.Divider).ToList()))
                .Where(l => l.Words.Count > 0)
                .ToList();
    }

    /// <summary>A table row with its parts on consecutive pages (a cell crossing a page boundary).</summary>
    private sealed class LogicalRow(FrameRow first)
    {
        public List<FrameRow> Parts { get; } = [first];
    }
}
