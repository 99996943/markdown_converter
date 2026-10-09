using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Reorders the lines of two-column pages so that the left column is read before the right one (FR-031).
/// A gutter is a vertical strip at least <see cref="LayoutOptions.GutterMinWidthRatio"/> of the page wide that is
/// free of text over at least <see cref="LayoutOptions.GutterMinHeightRatio"/> of the height where text lies on both of
/// its sides (columns may differ in length), with
/// long lines (median ≥ <see cref="LayoutOptions.ColumnMinLineWidthRatio"/> of the page width) on both sides.
/// Lines crossing the gutter, table lines and step scheme lines (FR-067) stay in place and separate column bands. Pages without a gutter
/// keep their order. Line assembly merges side-by-side column lines that share a baseline into one line with
/// several segments, so the analysis works on segments and such lines are split at the gutter.
/// </summary>
public sealed class ReadingOrderStage : IPipelineStage
{
    private const int MinLinesPerColumn = 2;

    /// <summary>Coverage (share of the page height) within which bins count as equally empty.</summary>
    private const double EmptiestSlack = 0.02;

    /// <inheritdoc />
    public int Order => StageOrder.ReadingOrder;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        LayoutOptions options = context.Options.Layout;
        if (!options.DetectColumns)
        {
            return;
        }

        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (FindGutter(page, options) is { } gutter)
            {
                // Without the lines of a table inside one column, the free band can reach into that column: the gutter
                // measured on all lines before table detection, when this one contains it, is the real one.
                if (page.ColumnGutter is { } before && gutter.Start <= before.Start + 1 && gutter.End >= before.End - 1)
                {
                    gutter = before;
                }

                Reorder(page, gutter);
            }
        }
    }

    /// <summary>The column gutter of a two-column page (FR-031), or null; also used by table detection.</summary>
    internal static (double Start, double End)? FindGutter(LayoutPage page, LayoutOptions options)
    {
        List<LayoutLine> flow = page.Lines.Where(IsFlowText).ToList();
        List<Rect> pieces = flow.SelectMany(PiecesOf).ToList();
        if (pieces.Count < 2 * MinLinesPerColumn)
        {
            return null;
        }

        double regionTop = pieces.Min(p => p.Top);
        double regionBottom = pieces.Max(p => p.Bottom);
        double regionHeight = regionBottom - regionTop;
        double minLeft = pieces.Min(p => p.Left);
        double maxRight = pieces.Max(p => p.Right);
        if (regionHeight <= 0)
        {
            return null;
        }

        // Scan 1-pt bins between the outermost text edges and collect runs of "free" bins. A bin is measured over the
        // height where text lies on both of its sides, so a shorter column (the last page) does not look free itself;
        // a column has lines, so a lone piece beyond the bin (a centred unit label) does not make a column there.
        int binCount = (int)Math.Ceiling(maxRight - minLeft);
        var free = new bool[binCount];
        for (int i = 0; i < binCount; i++)
        {
            double x0 = minLeft + i;
            double x1 = x0 + 1;
            List<Rect> left = pieces.Where(p => p.Right <= x0).ToList();
            List<Rect> right = pieces.Where(p => p.Left >= x1).ToList();
            if (left.Count < MinLinesPerColumn || right.Count < MinLinesPerColumn)
            {
                continue;
            }

            double top = Math.Max(left.Min(p => p.Top), right.Min(p => p.Top));
            double bottom = Math.Min(left.Max(p => p.Bottom), right.Max(p => p.Bottom));
            if (bottom <= top)
            {
                continue;
            }

            double covered = CoveredHeight(pieces
                .Where(p => p.Left < x1 && p.Right > x0 && p.Bottom > top && p.Top < bottom)
                .Select(p => new Rect(p.Left, Math.Max(p.Top, top), p.Right, Math.Min(p.Bottom, bottom))));
            free[i] = 1 - (covered / (bottom - top)) >= options.GutterMinHeightRatio;
        }

        double minWidth = options.GutterMinWidthRatio * page.Width;
        (double Start, double End)? best = null;
        int runStart = -1;
        for (int i = 0; i <= binCount; i++)
        {
            bool isFree = i < binCount && free[i];
            if (isFree && runStart < 0)
            {
                runStart = i;
            }
            else if (!isFree && runStart >= 0)
            {
                (double Start, double End) run = (minLeft + runStart, minLeft + i);
                if (run.End - run.Start >= minWidth && !IsColumnSplit(flow, run, page.Width, options))
                {
                    run = EmptiestStretch(pieces, run, regionTop, regionBottom);
                }

                if (run.End - run.Start >= minWidth
                    && IsColumnSplit(flow, run, page.Width, options)
                    && (best is null || run.End - run.Start > best.Value.End - best.Value.Start))
                {
                    best = run;
                }

                runStart = -1;
            }
        }

        return best;
    }

    /// <summary>
    /// A free run that does not split the page into columns may reach into a column whose lines are ragged, beside a
    /// short other column (the band where text lies on both sides is then only a few lines high): its stretch least
    /// covered over the whole page height is the real gap between the columns.
    /// </summary>
    private static (double Start, double End) EmptiestStretch(List<Rect> pieces, (double Start, double End) run, double top, double bottom)
    {
        int bins = (int)Math.Round(run.End - run.Start);
        var coverage = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            double x0 = run.Start + i;
            double x1 = x0 + 1;
            coverage[i] = CoveredHeight(pieces.Where(p => p.Left < x1 && p.Right > x0)) / (bottom - top);
        }

        double least = coverage.Min() + EmptiestSlack;
        (int Start, int End) longest = (0, 0);
        int start = -1;
        for (int i = 0; i <= bins; i++)
        {
            bool empty = i < bins && coverage[i] <= least;
            if (empty && start < 0)
            {
                start = i;
            }
            else if (!empty && start >= 0)
            {
                if (i - start > longest.End - longest.Start)
                {
                    longest = (start, i);
                }

                start = -1;
            }
        }

        return (run.Start + longest.Start, run.Start + longest.End);
    }

    private static bool IsColumnSplit(List<LayoutLine> lines, (double Start, double End) gutter, double pageWidth, LayoutOptions options)
    {
        // The gutter edges may overlap a few protruding lines (only most of its height must be free), so line parts are
        // assigned to a column by the gutter's middle; the parts of one line on one side (a bullet and its text) count
        // as one line, and the median width is robust to short last lines of paragraphs.
        double middle = (gutter.Start + gutter.End) / 2;
        var left = new List<double>();
        var right = new List<double>();
        foreach (LayoutLine line in lines)
        {
            List<Rect> pieces = PiecesOf(line).OrderBy(p => p.Left).ToList();
            List<Rect> leftPieces = pieces.Where(p => p.Right <= middle).ToList();
            List<Rect> rightPieces = pieces.Where(p => p.Left >= middle).ToList();
            if (leftPieces.Count > 0 && rightPieces.Count > 0 && !GapIsWidest(pieces, leftPieces.Max(p => p.Right)))
            {
                // A justified line split into words: the gap at the gutter is just one of its stretched spaces.
                continue;
            }

            AddWidth(left, leftPieces);
            AddWidth(right, rightPieces);
        }

        double minLineWidth = options.ColumnMinLineWidthRatio * pageWidth;
        return left.Count >= MinLinesPerColumn
            && right.Count >= MinLinesPerColumn
            && Median(left) >= minLineWidth
            && Median(right) >= minLineWidth;
    }

    /// <summary>The gap after <paramref name="gapStart"/> is at least twice as wide as every other gap of the line.</summary>
    private static bool GapIsWidest(List<Rect> pieces, double gapStart)
    {
        double gutterGap = 0;
        double otherGap = 0;
        for (int i = 1; i < pieces.Count; i++)
        {
            double gap = pieces[i].Left - pieces[i - 1].Right;
            if (pieces[i - 1].Right == gapStart)
            {
                gutterGap = gap;
            }
            else
            {
                otherGap = Math.Max(otherGap, gap);
            }
        }

        return gutterGap >= 2 * otherGap;
    }

    private static void AddWidth(List<double> widths, List<Rect> pieces)
    {
        if (pieces.Count > 0)
        {
            widths.Add(pieces.Max(p => p.Right) - pieces.Min(p => p.Left));
        }
    }

    private static double Median(List<double> values)
    {
        List<double> sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }

    /// <summary>Total vertical extent covered by the boxes (overlapping intervals merged).</summary>
    private static double CoveredHeight(IEnumerable<Rect> boxes)
    {
        double total = 0;
        double currentTop = double.NaN;
        double currentBottom = double.NaN;
        foreach (Rect box in boxes.OrderBy(b => b.Top))
        {
            if (double.IsNaN(currentTop) || box.Top > currentBottom)
            {
                if (!double.IsNaN(currentTop))
                {
                    total += currentBottom - currentTop;
                }

                currentTop = box.Top;
                currentBottom = box.Bottom;
            }
            else
            {
                currentBottom = Math.Max(currentBottom, box.Bottom);
            }
        }

        return double.IsNaN(currentTop) ? total : total + (currentBottom - currentTop);
    }

    private static void Reorder(LayoutPage page, (double Start, double End) gutter)
    {
        var ordered = new List<LayoutLine>(page.Lines.Count);
        var left = new List<LayoutLine>();
        var right = new List<LayoutLine>();

        void Flush()
        {
            ordered.AddRange(left);
            ordered.AddRange(right);
            left.Clear();
            right.Clear();
        }

        double middle = (gutter.Start + gutter.End) / 2;
        static string TableOf(LayoutLine line) => line.Annotations.TryGetValue(LayoutAnnotations.TableIndex, out string? index) ? index : string.Empty;
        Dictionary<string, (double Top, double Bottom)> tableSpans = page.Lines
            .Where(l => l.Role == LineRole.Table)
            .GroupBy(TableOf)
            .ToDictionary(g => g.Key, g => (g.Min(l => l.Box.Top), g.Max(l => l.Box.Bottom)), StringComparer.Ordinal);
        foreach (LayoutLine line in page.Lines)
        {
            // A table inside one column beside text of the other column (FR-031) is read in its column; other non-flow
            // lines, and a table between the column blocks, stand between the columns.
            bool columnTable = line.Role == LineRole.Table && tableSpans.TryGetValue(TableOf(line), out (double Top, double Bottom) span)
                && (line.Box.Right <= middle || line.Box.Left >= middle)
                && page.Lines.Any(o => IsFlowText(o)
                    && (line.Box.Right <= middle ? o.Box.Left >= middle : o.Box.Right <= middle)
                    && o.Baseline > span.Top && o.Baseline < span.Bottom);
            if ((!IsFlowText(line) && !columnTable) || PiecesOf(line).Any(p => p.Left < middle && p.Right > middle))
            {
                Flush();
                ordered.Add(line);
                continue;
            }

            List<LineSegment> leftSegments = line.Segments.Where(s => s.Box.CenterX < middle).ToList();
            if (line.Segments.Count <= 1 || leftSegments.Count == 0 || leftSegments.Count == line.Segments.Count)
            {
                (line.Box.CenterX < middle ? left : right).Add(line);
                continue;
            }

            left.Add(SubLine(line, leftSegments));
            right.Add(SubLine(line, line.Segments.Where(s => s.Box.CenterX >= middle).ToList()));
        }

        Flush();

        AnnotateColumn(ordered, line => line.Box.Right <= middle);
        AnnotateColumn(ordered, line => line.Box.Left >= middle);

        page.Lines.Clear();
        foreach (LayoutLine line in ordered)
        {
            page.Lines.Add(line);
        }
    }

    /// <summary>Records the column bounds on every flow line of one column (consumed by paragraph assembly).</summary>
    private static void AnnotateColumn(List<LayoutLine> lines, Func<LayoutLine, bool> inColumn)
    {
        List<LayoutLine> column = lines.Where(l => IsFlowText(l) && inColumn(l)).ToList();
        if (column.Count == 0)
        {
            return;
        }

        double left = column.Min(l => l.Box.Left);
        double right = column.Max(l => l.Box.Right);
        foreach (LayoutLine line in column)
        {
            LayoutAnnotations.SetNumber(line, LayoutAnnotations.ColumnLeft, left);
            LayoutAnnotations.SetNumber(line, LayoutAnnotations.ColumnRight, right);
        }
    }

    private static IEnumerable<Rect> PiecesOf(LayoutLine line) =>
        line.Segments.Count > 1 ? line.Segments.Select(s => s.Box) : [line.Box];

    /// <summary>Part of a merged line made of the given segments; keeps zone, role and annotations.</summary>
    private static LayoutLine SubLine(LayoutLine line, List<LineSegment> segments)
    {
        List<LayoutWord> words = segments.SelectMany(s => s.Words).ToList();
        Rect box = segments.Skip(1).Aggregate(segments[0].Box, (acc, s) => acc.Union(s.Box));
        var part = new LayoutLine(words, box, line.Baseline) { Zone = line.Zone, Role = line.Role };
        foreach (LineSegment segment in segments)
        {
            part.Segments.Add(segment);
        }

        foreach (KeyValuePair<string, string> annotation in line.Annotations)
        {
            part.Annotations[annotation.Key] = annotation.Value;
        }

        return part;
    }

    private static bool IsFlowText(LayoutLine line) =>
        line.Role is not (LineRole.Table or LineRole.Artifact or LineRole.SideNote or LineRole.StepTitle)
        && !line.Annotations.ContainsKey(LayoutAnnotations.StepIndex)
        && !line.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex);
}
