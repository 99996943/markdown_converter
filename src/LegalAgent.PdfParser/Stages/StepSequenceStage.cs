using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects step schemes (FR-067): shaded boxes holding step names, stacked in one column, with the explanation of each
/// step to the right of its box (the mBank „Kolejność działań | Wyjaśnienie” tables, drawn without borders). The names
/// are vertically centred in their boxes, so the lines of both columns interleave; this stage splits merged lines at the
/// box edge and reorders each page to name₁, explanation₁, name₂, … Name lines get <see cref="LineRole.StepTitle"/> and
/// <see cref="LayoutAnnotations.StepNumber"/>; explanation lines stay unclassified (paragraphs and lists are detected
/// as usual) and carry the explanation column bounds; the column-name row above a box becomes an artifact. A box
/// without a name continues the previous step (a step broken by a page boundary). Boxes form one scheme while only
/// column-name rows, artifacts and footnotes lie between them, also across a page boundary.
/// </summary>
public sealed class StepSequenceStage : IPipelineStage
{
    private const double EdgeTolerance = 3;
    private const double MaxBoxWidthRatio = 0.5;
    private const double MinBoxHeight = 8;
    private const double HeaderGapFactor = 4;
    private const double HeaderLineGapFactor = 1.5;
    private const int MinBoxes = 2;

    /// <inheritdoc />
    public int Order => StageOrder.StepSequence;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Options.Tables.DetectStepSequences)
        {
            return;
        }

        List<StepBox> boxes = context.Pages
            .Where(p => p.Skipped is null)
            .SelectMany(FindBoxes)
            .ToList();

        int schemeIndex = 0;
        foreach (List<StepBox> scheme in GroupIntoSchemes(context, boxes).Where(s => s.Count >= MinBoxes))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            Apply(scheme, schemeIndex++);
        }

        foreach (LayoutPage page in context.Pages)
        {
            Rewrite(page, boxes.Where(b => b.Page == page && b.SchemeIndex is not null).ToList());
        }
    }

    /// <summary>Shaded boxes of a page with text to their right, with the lines they own.</summary>
    private static List<StepBox> FindBoxes(LayoutPage page)
    {
        List<Rect> areas = page.FilledAreas
            .Where(a => a.Width <= MaxBoxWidthRatio * page.Width && a.Height >= MinBoxHeight)
            .Distinct()
            .ToList();
        List<Rect> outer = areas.Where(a => !areas.Any(o => o != a && Contains(o, a))).OrderBy(a => a.Top).ToList();
        List<LayoutLine> lines = page.Lines.Where(l => l.Role == LineRole.Unknown && l.Words.Count > 0).ToList();

        List<StepBox> boxes = outer
            .Select(area => new StepBox(page, area, lines.Where(l => l.Box.CenterY >= area.Top - 1 && l.Box.CenterY <= area.Bottom + 1).ToList()))
            .Where(b => b.Lines.Any(l => l.Words.Any(w => w.Box.CenterX >= b.Area.Right)))
            .ToList();
        var owned = new HashSet<LayoutLine>(boxes.SelectMany(b => b.Lines), ReferenceEqualityComparer.Instance);
        foreach (StepBox box in boxes)
        {
            box.Header.AddRange(FindHeader(lines.Where(l => !owned.Contains(l)), box.Area));
        }

        return boxes;
    }

    /// <summary>
    /// The column-name row above the box (e.g. „Kolejność działań | Wyjaśnienie”): the nearest lines above it (an arrow
    /// may stand between), taken upwards while closely spaced until together they have text over the box and to its
    /// right — the names may sit on slightly different baselines or wrap. Empty when no such row is found.
    /// </summary>
    private static List<LayoutLine> FindHeader(IEnumerable<LayoutLine> candidates, Rect box)
    {
        var header = new List<LayoutLine>();
        double limit = box.Top + 1;
        foreach (LayoutLine line in candidates.Where(l => l.Box.Bottom <= box.Top + 1).OrderByDescending(l => l.Box.Bottom))
        {
            double gap = header.Count == 0 ? HeaderGapFactor * line.Box.Height : HeaderLineGapFactor * line.Box.Height;
            if (limit - line.Box.Bottom > gap || line.Words.Any(w => w.Box.CenterX < box.Left))
            {
                break;
            }

            header.Add(line);
            limit = line.Box.Top;
            List<LayoutWord> words = header.SelectMany(l => l.Words).ToList();
            if (words.Any(w => w.Box.CenterX < box.Right) && words.Any(w => w.Box.CenterX >= box.Right))
            {
                return header;
            }
        }

        return [];
    }

    /// <summary>Consecutive boxes of one column with nothing but header rows, artifacts and footnotes between them.</summary>
    private static IEnumerable<List<StepBox>> GroupIntoSchemes(PipelineContext context, List<StepBox> boxes)
    {
        var scheme = new List<StepBox>();
        foreach (StepBox box in boxes)
        {
            if (scheme.Count > 0 && !Continues(context, scheme[^1], box, boxes))
            {
                yield return scheme;
                scheme = [];
            }

            scheme.Add(box);
        }

        if (scheme.Count > 0)
        {
            yield return scheme;
        }
    }

    private static bool Continues(PipelineContext context, StepBox previous, StepBox next, List<StepBox> boxes)
    {
        if (Math.Abs(previous.Area.Left - next.Area.Left) > EdgeTolerance
            || Math.Abs(previous.Area.Right - next.Area.Right) > EdgeTolerance)
        {
            return false;
        }

        var claimed = new HashSet<LayoutLine>(boxes.SelectMany(b => b.Lines.Concat(b.Header)), ReferenceEqualityComparer.Instance);
        bool Foreign(LayoutLine line) => line.Role == LineRole.Unknown && line.Words.Count > 0 && !claimed.Contains(line);

        if (previous.Page == next.Page)
        {
            return !previous.Page.Lines.Any(l => Foreign(l) && l.Box.Top >= previous.Area.Bottom && l.Box.Bottom <= next.Area.Top);
        }

        int previousIndex = context.Pages.IndexOf(previous.Page);
        return context.Pages.IndexOf(next.Page) == previousIndex + 1
            && !previous.Page.Lines.Any(l => Foreign(l) && l.Box.Top >= previous.Area.Bottom)
            && !next.Page.Lines.Any(l => Foreign(l) && l.Box.Bottom <= next.Area.Top);
    }

    private static void Apply(List<StepBox> scheme, int schemeIndex)
    {
        int number = 0;
        foreach (StepBox box in scheme)
        {
            box.SchemeIndex = schemeIndex;
            if (box.Lines.Any(l => l.Words.Any(w => w.Box.CenterX < box.Area.Right)))
            {
                box.Number = ++number;
            }
        }
    }

    /// <summary>Replaces the lines of each scheme box by its header rows, name lines and explanation lines, in this order.</summary>
    private static void Rewrite(LayoutPage page, List<StepBox> boxes)
    {
        if (boxes.Count == 0)
        {
            return;
        }

        var owner = new Dictionary<LayoutLine, StepBox>(ReferenceEqualityComparer.Instance);
        foreach (StepBox box in boxes)
        {
            foreach (LayoutLine line in box.Lines.Concat(box.Header))
            {
                owner.TryAdd(line, box);
            }
        }

        foreach (IGrouping<int, StepBox> scheme in boxes.GroupBy(b => b.SchemeIndex!.Value))
        {
            List<Rect> explanation = scheme
                .SelectMany(b => b.Lines.SelectMany(l => l.Words.Where(w => w.Box.CenterX >= b.Area.Right)))
                .Select(w => w.Box)
                .ToList();
            double left = explanation.Min(w => w.Left);
            double right = explanation.Max(w => w.Right);
            foreach (StepBox box in scheme)
            {
                box.ColumnLeft = left;
                box.ColumnRight = right;
            }
        }

        var ordered = new List<LayoutLine>(page.Lines.Count);
        var emitted = new HashSet<StepBox>(ReferenceEqualityComparer.Instance);
        foreach (LayoutLine line in page.Lines)
        {
            if (!owner.TryGetValue(line, out StepBox? box))
            {
                ordered.Add(line);
                continue;
            }

            if (emitted.Add(box))
            {
                ordered.AddRange(Emit(box));
            }
        }

        page.Lines.Clear();
        foreach (LayoutLine line in ordered)
        {
            page.Lines.Add(line);
        }
    }

    private static IEnumerable<LayoutLine> Emit(StepBox box)
    {
        string scheme = box.SchemeIndex!.Value.ToString(CultureInfo.InvariantCulture);
        foreach (LayoutLine header in box.Header)
        {
            header.Role = LineRole.Artifact;
            header.Annotations[LayoutAnnotations.StepIndex] = scheme;
            yield return header;
        }

        var titles = new List<LayoutLine>();
        var explanation = new List<LayoutLine>();
        foreach (LayoutLine line in box.Lines)
        {
            List<LayoutWord> name = line.Words.Where(w => w.Box.CenterX < box.Area.Right).ToList();
            List<LayoutWord> text = line.Words.Where(w => w.Box.CenterX >= box.Area.Right).ToList();
            if (name.Count > 0)
            {
                LayoutLine title = text.Count == 0 ? line : LineSlicer.Slice(line, name);
                title.Role = LineRole.StepTitle;
                title.Annotations[LayoutAnnotations.StepIndex] = scheme;
                if (box.Number is { } number)
                {
                    title.Annotations[LayoutAnnotations.StepNumber] = number.ToString(CultureInfo.InvariantCulture);
                }

                titles.Add(title);
            }

            if (text.Count > 0)
            {
                LayoutLine part = name.Count == 0 ? line : LineSlicer.Slice(line, text);
                part.Annotations[LayoutAnnotations.StepIndex] = scheme;
                LayoutAnnotations.SetNumber(part, LayoutAnnotations.ColumnLeft, box.ColumnLeft);
                LayoutAnnotations.SetNumber(part, LayoutAnnotations.ColumnRight, box.ColumnRight);
                explanation.Add(part);
            }
        }

        foreach (LayoutLine line in titles.Concat(explanation))
        {
            yield return line;
        }
    }

    private static bool Contains(Rect outer, Rect inner) =>
        inner.Left >= outer.Left - 1 && inner.Right <= outer.Right + 1
        && inner.Top >= outer.Top - 1 && inner.Bottom <= outer.Bottom + 1;

    private sealed class StepBox(LayoutPage page, Rect area, List<LayoutLine> lines)
    {
        public LayoutPage Page { get; } = page;

        public Rect Area { get; } = area;

        /// <summary>Unclassified lines whose vertical centre lies in the box (names and explanation).</summary>
        public List<LayoutLine> Lines { get; } = lines;

        /// <summary>Column-name rows right above the box.</summary>
        public List<LayoutLine> Header { get; } = [];

        public int? SchemeIndex { get; set; }

        /// <summary>Step number; null for a box without a name (continuation of the previous step).</summary>
        public int? Number { get; set; }

        public double ColumnLeft { get; set; }

        public double ColumnRight { get; set; }
    }
}
