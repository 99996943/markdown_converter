using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
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
    private const double FlowGapFactor = 1.5;
    private const int MinBoxes = 2;
    private const double MinBoldShare = 0.8;

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

        List<LayoutPage> pages = context.Pages.Where(p => p.Skipped is null).ToList();
        MarkConnectors(context, pages.SelectMany(p => FindAreas(p).Select(a => (Page: p, Area: a))).ToList());
        List<StepBox> boxes = pages.SelectMany(FindBoxes).ToList();

        int schemeIndex = 0;
        foreach (List<StepBox> scheme in GroupIntoSchemes(context, boxes).Where(s => s.Count >= MinBoxes || (s.Count == 1 && IsGuardedFrame(s[0]))))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            Apply(scheme, schemeIndex++);
        }

        foreach (LayoutPage page in context.Pages)
        {
            Rewrite(page, boxes.Where(b => b.Page == page && b.SchemeIndex is not null).ToList());
        }
    }

    /// <summary>
    /// Spec 008 (FR-633): a single box is a frame only when it holds a name of at least 80% bold characters (the box is
    /// narrow and shaded by <see cref="FindAreas"/>, and has text to its right by <see cref="FindBoxes"/>) and no ruling
    /// crosses its band — the cells of a ruled data table are not frames.
    /// </summary>
    private static bool IsGuardedFrame(StepBox box)
    {
        List<LayoutWord> name = box.Lines.SelectMany(l => l.Words).Where(w => w.Box.CenterX < box.Area.Right).ToList();
        int letters = name.Sum(w => w.Text.Length);
        int bold = name.Where(w => w.Style.HasFlag(TextStyle.Bold)).Sum(w => w.Text.Length);
        double top = box.Area.Top - EdgeTolerance;
        double bottom = box.Area.Bottom + EdgeTolerance;
        return letters > 0
            && bold >= MinBoldShare * letters
            && !box.Page.Rulings.Any(r => Math.Min(r.Y1, r.Y2) < bottom && Math.Max(r.Y1, r.Y2) > top);
    }

    /// <summary>Narrow shaded areas of a page not nested in another one, top to bottom: the candidate boxes.</summary>
    private static List<Rect> FindAreas(LayoutPage page)
    {
        List<Rect> areas = page.FilledAreas
            .Where(a => a.Width <= MaxBoxWidthRatio * page.Width && a.Height >= MinBoxHeight)
            .Distinct()
            .ToList();
        return areas.Where(a => !areas.Any(o => o != a && Contains(o, a))).OrderBy(a => a.Top).ToList();
    }

    /// <summary>
    /// Connectors (spec 008 FR-635): between two consecutive boxes of one column — on one page, or below the last box of
    /// a page and above the first box of the next — a line made only of characters without letters or digits (an arrow
    /// set in a symbol font) within the column of the boxes becomes an artifact, so it neither ends the scheme nor turns
    /// into a step name. Such a glyph sharing a line with explanation text to the right of the box is split off.
    /// </summary>
    private static void MarkConnectors(PipelineContext context, List<(LayoutPage Page, Rect Area)> areas)
    {
        for (int i = 0; i + 1 < areas.Count; i++)
        {
            (LayoutPage page, Rect area) = areas[i];
            (LayoutPage nextPage, Rect next) = areas[i + 1];
            if (Math.Abs(area.Left - next.Left) > EdgeTolerance || Math.Abs(area.Right - next.Right) > EdgeTolerance)
            {
                continue;
            }

            if (page == nextPage)
            {
                MarkConnectors(page, area, area.Top - 1, next.Bottom + 1);
            }
            else if (context.Pages.IndexOf(nextPage) == context.Pages.IndexOf(page) + 1)
            {
                MarkConnectors(page, area, area.Top - 1, double.MaxValue);
                MarkConnectors(nextPage, next, double.MinValue, next.Bottom + 1);
            }
        }
    }

    private static void MarkConnectors(LayoutPage page, Rect column, double top, double bottom)
    {
        double left = column.Left - EdgeTolerance;
        double right = column.Right + EdgeTolerance;
        for (int index = 0; index < page.Lines.Count; index++)
        {
            LayoutLine line = page.Lines[index];
            if (line.Role != LineRole.Unknown || line.Words.Count == 0 || line.Box.CenterY < top || line.Box.CenterY > bottom)
            {
                continue;
            }

            List<LayoutWord> glyphs = line.Words.Where(w => w.Box.CenterX < column.Right).ToList();
            if (glyphs.Count == 0 || !glyphs.All(IsSymbol) || glyphs.Any(w => w.Box.Left < left || w.Box.Right > right))
            {
                continue;
            }

            if (glyphs.Count == line.Words.Count)
            {
                line.Role = LineRole.Artifact;
                continue;
            }

            LayoutLine connector = LineSlicer.Slice(line, glyphs);
            connector.Role = LineRole.Artifact;
            List<LayoutWord> rest = line.Words.Where(w => w.Box.CenterX >= column.Right).ToList();
            LayoutLine text = LineSlicer.Slice(line, rest);
            text.Baseline = Baseline(rest, line.Baseline);
            page.Lines[index] = connector;
            page.Lines.Insert(++index, text);
        }
    }

    /// <summary>True for a word without letters and digits (an arrow or another symbol-font glyph).</summary>
    private static bool IsSymbol(LayoutWord word) => !word.Text.Any(char.IsLetterOrDigit);

    /// <summary>The baseline most glyphs of the words sit on (a part split off a line merged with a taller glyph).</summary>
    private static double Baseline(IReadOnlyList<LayoutWord> words, double fallback)
    {
        List<double> baselines = words.SelectMany(w => w.Glyphs).Select(g => Math.Round(g.Baseline, 1)).ToList();
        return baselines.Count == 0
            ? fallback
            : baselines.GroupBy(b => b).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
    }

    /// <summary>Shaded boxes of a page with text to their right, with the lines they own.</summary>
    private static List<StepBox> FindBoxes(LayoutPage page)
    {
        List<Rect> outer = FindAreas(page);
        List<LayoutLine> lines = page.Lines.Where(l => l.Role == LineRole.Unknown && l.Words.Count > 0).ToList();

        List<StepBox> boxes = outer
            .Select(area => new StepBox(page, area, lines.Where(l => l.Box.CenterY >= area.Top - 1 && l.Box.CenterY <= area.Bottom + 1).ToList()))
            .ToList();
        for (int i = 0; i < boxes.Count; i++)
        {
            double end = i + 1 < boxes.Count ? boxes[i + 1].Area.Top - 1 : double.MaxValue;
            boxes[i].Lines.AddRange(FlowBelow(lines, boxes[i], end));
        }

        boxes = boxes.Where(b => b.Lines.Any(l => l.Words.Any(w => w.Box.CenterX >= b.Area.Right))).ToList();
        var owned = new HashSet<LayoutLine>(boxes.SelectMany(b => b.Lines), ReferenceEqualityComparer.Instance);
        foreach (StepBox box in boxes)
        {
            box.Header.AddRange(FindHeader(lines.Where(l => !owned.Contains(l)), box.Area));
        }

        return boxes;
    }

    /// <summary>
    /// Spec 008 (FR-632): the explanation running on below the box — the lines below it lying wholly right of its edge,
    /// each closely below the previous one (gap ≤ 1.5 × line height), up to the next box, a line reaching left of the
    /// edge or a wider gap. Below an empty box (a frame broken by a page boundary) this is the whole explanation.
    /// </summary>
    private static List<LayoutLine> FlowBelow(List<LayoutLine> lines, StepBox box, double end)
    {
        var flow = new List<LayoutLine>();
        double previous = box.Lines.Select(l => l.Box.Bottom).Append(box.Area.Bottom).Max();
        foreach (LayoutLine line in lines.Where(l => l.Box.CenterY > box.Area.Bottom + 1 && l.Box.CenterY < end).OrderBy(l => l.Box.Top))
        {
            if (line.Box.Top - previous > FlowGapFactor * line.Box.Height
                || line.Words.Any(w => w.Box.Left < box.Area.Right - EdgeTolerance))
            {
                break;
            }

            flow.Add(line);
            previous = line.Box.Bottom;
        }

        return flow;
    }

    /// <summary>
    /// The column-name row above the box (e.g. „Kolejność działań | Wyjaśnienie”): the nearest lines above it (an arrow
    /// may stand between), taken upwards while closely spaced until together they have text over the box and to its
    /// right — the names may sit on slightly different baselines or wrap — plus lines overlapping that row vertically (a
    /// name centred beside a wrapped one). Empty when no such row is found.
    /// </summary>
    private static List<LayoutLine> FindHeader(IEnumerable<LayoutLine> candidates, Rect box)
    {
        var header = new List<LayoutLine>();
        double limit = box.Top + 1;
        bool complete = false;
        foreach (LayoutLine line in candidates.Where(l => l.Box.Bottom <= box.Top + 1).OrderByDescending(l => l.Box.Bottom))
        {
            // Spec 008 (FR-634): column names are printed wholly bold; a plain line above the box is text.
            if (!line.Words.All(w => w.Style.HasFlag(TextStyle.Bold)))
            {
                break;
            }

            if (complete)
            {
                // A name centred beside a wrapped one: the line above the wrapped name overlaps the collected row.
                if (line.Box.Bottom <= header.Min(l => l.Box.Top) || line.Words.Any(w => w.Box.CenterX < box.Left))
                {
                    break;
                }

                header.Add(line);
                continue;
            }

            // Column names stop at the box edge; a line running on across it (a chapter heading above the scheme, T067f)
            // is text, not a column-name row.
            double gap = header.Count == 0 ? HeaderGapFactor * line.Box.Height : HeaderLineGapFactor * line.Box.Height;
            if (limit - line.Box.Bottom > gap
                || line.Words.Any(w => w.Box.CenterX < box.Left)
                || line.Words.Any(w => w.Box.Left < box.Right - EdgeTolerance && w.Box.Right > box.Right + EdgeTolerance))
            {
                break;
            }

            header.Add(line);
            limit = line.Box.Top;
            List<LayoutWord> words = header.SelectMany(l => l.Words).ToList();
            complete = words.Any(w => w.Box.CenterX < box.Right) && words.Any(w => w.Box.CenterX >= box.Right);
        }

        return complete ? header : [];
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
