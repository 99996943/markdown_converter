using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Reorders the lines of two-column pages so that the left column is read before the right one (FR-031).
/// A gutter is a vertical strip at least <see cref="LayoutOptions.GutterMinWidthRatio"/> of the page wide that is
/// free of text over at least <see cref="LayoutOptions.GutterMinHeightRatio"/> of the text region height, with
/// long lines (average ≥ <see cref="LayoutOptions.ColumnMinLineWidthRatio"/> of the page width) on both sides.
/// Lines crossing the gutter and table lines stay in place and separate column bands. Pages without a gutter
/// keep their order. Line assembly merges side-by-side column lines that share a baseline into one line with
/// several segments, so the analysis works on segments and such lines are split at the gutter.
/// </summary>
public sealed class ReadingOrderStage : IPipelineStage
{
    private const int MinLinesPerColumn = 2;

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
                Reorder(page, gutter);
            }
        }
    }

    private static (double Start, double End)? FindGutter(LayoutPage page, LayoutOptions options)
    {
        List<Rect> pieces = page.Lines.Where(IsFlowText).SelectMany(PiecesOf).ToList();
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

        // Scan 1-pt bins between the outermost text edges and collect runs of "free" bins.
        int binCount = (int)Math.Ceiling(maxRight - minLeft);
        var free = new bool[binCount];
        for (int i = 0; i < binCount; i++)
        {
            double x0 = minLeft + i;
            double x1 = x0 + 1;
            double covered = CoveredHeight(pieces.Where(p => p.Left < x1 && p.Right > x0));
            free[i] = 1 - (covered / regionHeight) >= options.GutterMinHeightRatio;
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
                if (run.End - run.Start >= minWidth
                    && IsColumnSplit(pieces, run, page.Width, options)
                    && (best is null || run.End - run.Start > best.Value.End - best.Value.Start))
                {
                    best = run;
                }

                runStart = -1;
            }
        }

        return best;
    }

    private static bool IsColumnSplit(List<Rect> pieces, (double Start, double End) gutter, double pageWidth, LayoutOptions options)
    {
        // The gutter edges may overlap a few protruding lines (only most of its height must be free), so lines are
        // assigned to a column by the gutter's middle.
        double middle = (gutter.Start + gutter.End) / 2;
        List<Rect> left = pieces.Where(p => p.Right <= middle).ToList();
        List<Rect> right = pieces.Where(p => p.Left >= middle).ToList();
        double minLineWidth = options.ColumnMinLineWidthRatio * pageWidth;

        return left.Count >= MinLinesPerColumn
            && right.Count >= MinLinesPerColumn
            && left.Average(p => p.Width) >= minLineWidth
            && right.Average(p => p.Width) >= minLineWidth;
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
        foreach (LayoutLine line in page.Lines)
        {
            if (!IsFlowText(line) || PiecesOf(line).Any(p => p.Left < middle && p.Right > middle))
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

    private static bool IsFlowText(LayoutLine line) => line.Role is not (LineRole.Table or LineRole.Artifact or LineRole.SideNote);
}
