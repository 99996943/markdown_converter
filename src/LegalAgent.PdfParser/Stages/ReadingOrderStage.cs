using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Reorders the lines of two-column pages so that the left column is read before the right one (FR-031).
/// A gutter is a vertical strip at least <see cref="LayoutOptions.GutterMinWidthRatio"/> of the page wide that is
/// free of text over at least <see cref="LayoutOptions.GutterMinHeightRatio"/> of the text region height, with
/// long lines (average ≥ <see cref="LayoutOptions.ColumnMinLineWidthRatio"/> of the page width) on both sides.
/// Lines crossing the gutter and table lines stay in place and separate column bands. Pages without a gutter
/// keep their order.
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
        List<LayoutLine> lines = page.Lines.Where(IsFlowText).ToList();
        if (lines.Count < 2 * MinLinesPerColumn)
        {
            return null;
        }

        double regionTop = lines.Min(l => l.Box.Top);
        double regionBottom = lines.Max(l => l.Box.Bottom);
        double regionHeight = regionBottom - regionTop;
        double minLeft = lines.Min(l => l.Box.Left);
        double maxRight = lines.Max(l => l.Box.Right);
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
            double covered = CoveredHeight(lines.Where(l => l.Box.Left < x1 && l.Box.Right > x0));
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
                    && IsColumnSplit(lines, run, page.Width, options)
                    && (best is null || run.End - run.Start > best.Value.End - best.Value.Start))
                {
                    best = run;
                }

                runStart = -1;
            }
        }

        return best;
    }

    private static bool IsColumnSplit(List<LayoutLine> lines, (double Start, double End) gutter, double pageWidth, LayoutOptions options)
    {
        List<LayoutLine> left = lines.Where(l => l.Box.Right <= gutter.Start).ToList();
        List<LayoutLine> right = lines.Where(l => l.Box.Left >= gutter.End).ToList();
        double minLineWidth = options.ColumnMinLineWidthRatio * pageWidth;

        return left.Count >= MinLinesPerColumn
            && right.Count >= MinLinesPerColumn
            && left.Average(l => l.Box.Width) >= minLineWidth
            && right.Average(l => l.Box.Width) >= minLineWidth;
    }

    /// <summary>Total vertical extent covered by the lines (overlapping intervals merged).</summary>
    private static double CoveredHeight(IEnumerable<LayoutLine> lines)
    {
        double total = 0;
        double currentTop = double.NaN;
        double currentBottom = double.NaN;
        foreach (LayoutLine line in lines.OrderBy(l => l.Box.Top))
        {
            if (double.IsNaN(currentTop) || line.Box.Top > currentBottom)
            {
                if (!double.IsNaN(currentTop))
                {
                    total += currentBottom - currentTop;
                }

                currentTop = line.Box.Top;
                currentBottom = line.Box.Bottom;
            }
            else
            {
                currentBottom = Math.Max(currentBottom, line.Box.Bottom);
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

        foreach (LayoutLine line in page.Lines)
        {
            bool crossesGutter = line.Box.Left < gutter.End && line.Box.Right > gutter.Start;
            if (!IsFlowText(line) || crossesGutter)
            {
                Flush();
                ordered.Add(line);
            }
            else if (line.Box.CenterX < (gutter.Start + gutter.End) / 2)
            {
                left.Add(line);
            }
            else
            {
                right.Add(line);
            }
        }

        Flush();

        page.Lines.Clear();
        foreach (LayoutLine line in ordered)
        {
            page.Lines.Add(line);
        }
    }

    private static bool IsFlowText(LayoutLine line) => line.Role is not (LineRole.Table or LineRole.Artifact);
}
