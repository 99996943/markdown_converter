using System.Text;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Assembles glyphs into words and visual lines (FR-011, FR-030), splits lines into segments on large
/// horizontal gaps (FR-060), assigns margin zones per page and computes the document body style. A narrow side-note
/// column at the page edge (FR-034) is assembled separately and its lines get <see cref="LineRole.SideNote"/>.
/// Lines are written to <see cref="LayoutPage.Lines"/> sorted top to bottom, then left to right.
/// </summary>
public sealed class LineAssemblyStage : IPipelineStage
{
    /// <summary>Space width as a fraction of the line font size when the line has no explicit space glyphs.</summary>
    private const double FallbackSpaceEm = 0.26;

    /// <summary>
    /// Advance gap (in ems of the smaller of the two glyphs) above which adjacent glyphs belong to different words.
    /// Advance positions are used rather than ink boxes, whose side bearings leave visible gaps inside words.
    /// </summary>
    private const double WordGapEm = 0.15;

    /// <summary>Glyphs at least this fraction of the seed size extend the core box of a line.</summary>
    private const double CoreSizeRatio = 0.9;

    /// <summary>
    /// Glyphs below this fraction of the seed size are markers (superscripts, footnote references) that may join a line
    /// by vertical overlap even far to the side; larger ones there are text of another line.
    /// </summary>
    private const double MarkerSizeRatio = 0.7;

    private const double MaxLeadingInFontSizes = 3.0;

    /// <summary>Horizontal distance (in ems) within which a glyph may join a line by vertical overlap alone.</summary>
    private const double NearGlyphEm = 3.0;

    /// <summary>Minimum width (in ems of the page's main text size) of the empty band before a side-note column.</summary>
    private const double SideNoteMinGapEm = 0.5;

    /// <summary>Maximum baseline distance (in note font sizes) between consecutive lines of one side-note column.</summary>
    private const double SideNoteLineGapEm = 1.8;

    /// <inheritdoc />
    public int Order => StageOrder.LineAssembly;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var charsBySize = new SortedDictionary<double, int>();
        var perPage = new List<List<(LayoutLine Line, double Size)>>();

        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            perPage.Add(Assemble(page, context.Options, charsBySize));
        }

        context.BodyStyle = ComputeBodyStyle(perPage, charsBySize);
    }

    private static List<(LayoutLine Line, double Size)> Assemble(
        LayoutPage page,
        PdfParserOptions options,
        SortedDictionary<double, int> charsBySize)
    {
        page.Lines.Clear();
        var result = new List<(LayoutLine Line, double Size)>();
        if (page.Glyphs.Count == 0)
        {
            return result;
        }

        HashSet<int> notes = FindSideNotes(page, options);
        IEnumerable<int> all = Enumerable.Range(0, page.Glyphs.Count);
        List<LineBuilder> builders = GroupIntoLines(page, all.Where(i => !notes.Contains(i)), options.Layout);
        List<LineBuilder> noteBuilders = GroupIntoLines(page, all.Where(notes.Contains), options.Layout);
        var assembled = new List<(LayoutLine Line, double Size, int Order)>();

        foreach (LineBuilder builder in builders.Concat(noteBuilders))
        {
            LayoutLine? line = BuildLine(builder, options.Tables.CellGapFactor, out double size, charsBySize);
            if (line is null)
            {
                continue;
            }

            if (noteBuilders.Contains(builder))
            {
                line.Role = LineRole.SideNote;
            }

            ClassifyZone(line, page, options.Artifacts.MarginZoneRatio);
            assembled.Add((line, size, builder.SeedIndex));
        }

        foreach ((LayoutLine line, double size, int _) in assembled
            .OrderBy(a => a.Line.Box.Top)
            .ThenBy(a => a.Line.Box.Left)
            .ThenBy(a => a.Line.Baseline)
            .ThenBy(a => a.Order))
        {
            page.Lines.Add(line);
            result.Add((line, size));
        }

        return result;
    }

    private static bool IsSpace(LayoutGlyph g) => string.IsNullOrWhiteSpace(g.Text);

    /// <summary>
    /// FR-034: indices of the glyphs of a side-note column — the body-zone glyphs beyond the outermost empty vertical band
    /// of the page when they form a narrow column (at most <see cref="LayoutOptions.SideNoteMaxWidthRatio"/> of the page
    /// width) of at least two lines in a font clearly smaller than the main text. Empty when there is none.
    /// </summary>
    private static HashSet<int> FindSideNotes(LayoutPage page, PdfParserOptions options)
    {
        var none = new HashSet<int>();
        LayoutOptions layout = options.Layout;
        if (!layout.DetectSideNotes)
        {
            return none;
        }

        double top = page.Height * options.Artifacts.MarginZoneRatio;
        double bottom = page.Height * (1 - options.Artifacts.MarginZoneRatio);
        List<int> body = Enumerable.Range(0, page.Glyphs.Count)
            .Where(i => page.Glyphs[i].Box.Top >= top && page.Glyphs[i].Box.Bottom <= bottom)
            .ToList();
        List<int> ink = body.Where(i => !IsSpace(page.Glyphs[i])).OrderBy(i => page.Glyphs[i].Start).ThenBy(i => i).ToList();
        if (ink.Count == 0)
        {
            return none;
        }

        var intervals = new List<(double Start, double End)>();
        foreach (int i in ink)
        {
            LayoutGlyph g = page.Glyphs[i];
            if (intervals.Count > 0 && g.Start <= intervals[^1].End)
            {
                intervals[^1] = (intervals[^1].Start, Math.Max(intervals[^1].End, g.End));
            }
            else
            {
                intervals.Add((g.Start, g.End));
            }
        }

        // Word gaps inside the note column are empty bands too, so look for the outermost band wide enough.
        double minGap = SideNoteMinGapEm * DominantSize(ink.Select(i => page.Glyphs[i]));
        int rightGap = Enumerable.Range(1, intervals.Count - 1).LastOrDefault(k => intervals[k].Start - intervals[k - 1].End >= minGap);
        if (rightGap > 0)
        {
            double edge = intervals[rightGap].Start;
            HashSet<int> right = body.Where(i => page.Glyphs[i].Start >= edge).ToHashSet();
            if (IsSideNoteColumn(page, right, ink, layout))
            {
                return ExtendIntoMarginZones(page, right, g => g.Start >= edge);
            }
        }

        int leftGap = Enumerable.Range(1, intervals.Count - 1).FirstOrDefault(k => intervals[k].Start - intervals[k - 1].End >= minGap);
        if (leftGap > 0)
        {
            double edge = intervals[leftGap - 1].End;
            HashSet<int> left = body.Where(i => page.Glyphs[i].End <= edge).ToHashSet();
            if (IsSideNoteColumn(page, left, ink, layout))
            {
                return ExtendIntoMarginZones(page, left, g => g.End <= edge);
            }
        }

        return none;
    }

    /// <summary>
    /// Adds glyphs beyond the column edge in the header and footer zones whose baseline is within
    /// <see cref="SideNoteLineGapEm"/> note sizes of a line already in the column (a note running into the margin).
    /// </summary>
    private static HashSet<int> ExtendIntoMarginZones(LayoutPage page, HashSet<int> column, Func<LayoutGlyph, bool> beyondEdge)
    {
        double reach = SideNoteLineGapEm * DominantSize(column.Select(i => page.Glyphs[i]).Where(g => !IsSpace(g)));
        List<int> candidates = Enumerable.Range(0, page.Glyphs.Count)
            .Where(i => !column.Contains(i) && beyondEdge(page.Glyphs[i]))
            .ToList();
        var baselines = new SortedSet<double>(column.Select(i => page.Glyphs[i].Baseline));

        bool added = true;
        while (added && candidates.Count > 0)
        {
            added = false;
            for (int k = candidates.Count - 1; k >= 0; k--)
            {
                double baseline = page.Glyphs[candidates[k]].Baseline;
                if (baselines.GetViewBetween(baseline - reach, baseline + reach).Count > 0)
                {
                    column.Add(candidates[k]);
                    baselines.Add(baseline);
                    candidates.RemoveAt(k);
                    added = true;
                }
            }
        }

        return column;
    }

    private static bool IsSideNoteColumn(LayoutPage page, HashSet<int> column, List<int> ink, LayoutOptions layout)
    {
        List<LayoutGlyph> note = ink.Where(column.Contains).Select(i => page.Glyphs[i]).ToList();
        List<LayoutGlyph> main = ink.Where(i => !column.Contains(i)).Select(i => page.Glyphs[i]).ToList();
        if (note.Count == 0 || note.Count >= main.Count)
        {
            return false;
        }

        double width = note.Max(g => g.End) - note.Min(g => g.Start);
        int lines = note.Select(g => Math.Round(g.Baseline)).Distinct().Count();
        return width <= layout.SideNoteMaxWidthRatio * page.Width
            && lines >= 2
            && DominantSize(note) <= layout.SideNoteMaxSizeRatio * DominantSize(main);
    }

    /// <summary>The most frequent glyph size (rounded to 0.5 pt, weighted by characters); ties go to the larger size.</summary>
    private static double DominantSize(IEnumerable<LayoutGlyph> glyphs)
    {
        var chars = new SortedDictionary<double, int>();
        foreach (LayoutGlyph g in glyphs)
        {
            double rounded = RoundHalf(g.PointSize);
            chars[rounded] = chars.GetValueOrDefault(rounded) + g.Text.Length;
        }

        return chars.Count == 0 ? 0 : chars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;
    }

    private static List<LineBuilder> GroupIntoLines(LayoutPage page, IEnumerable<int> indices, LayoutOptions layout)
    {
        int[] order = indices
            .OrderByDescending(i => page.Glyphs[i].PointSize)
            .ThenBy(i => page.Glyphs[i].Baseline)
            .ThenBy(i => page.Glyphs[i].Box.Left)
            .ThenBy(i => i)
            .ToArray();

        var lines = new List<LineBuilder>();
        foreach (int index in order)
        {
            LayoutGlyph glyph = page.Glyphs[index];
            LineBuilder? best = null;
            double bestDelta = double.MaxValue;

            foreach (LineBuilder line in lines)
            {
                if (!line.Accepts(glyph, layout))
                {
                    continue;
                }

                double delta = Math.Abs(line.Baseline - glyph.Baseline);
                if (delta < bestDelta)
                {
                    best = line;
                    bestDelta = delta;
                }
            }

            if (best is null)
            {
                best = new LineBuilder(glyph, index);
                lines.Add(best);
            }

            best.Add(glyph, index);
        }

        return lines;
    }

    private static LayoutLine? BuildLine(
        LineBuilder builder,
        double cellGapFactor,
        out double dominantSize,
        SortedDictionary<double, int> charsBySize)
    {
        dominantSize = 0;
        List<(LayoutGlyph Glyph, int Index)> glyphs = builder.Glyphs
            .OrderBy(g => g.Glyph.Box.Left)
            .ThenBy(g => g.Index)
            .ToList();

        var words = new List<LayoutWord>();
        var current = new List<LayoutGlyph>();
        LayoutGlyph? previous = null;
        double tracking = Tracking(glyphs.Select(g => g.Glyph).ToList());

        foreach ((LayoutGlyph glyph, int _) in glyphs)
        {
            if (IsSpace(glyph))
            {
                Flush(words, current);
                continue;
            }

            if (previous is not null
                && current.Count > 0
                && glyph.Start - previous.End > tracking + (WordGapEm * Math.Min(previous.PointSize, glyph.PointSize)))
            {
                Flush(words, current);
            }

            current.Add(glyph);
            previous = glyph;
        }

        Flush(words, current);
        if (words.Count == 0)
        {
            return null;
        }

        var sizeChars = new SortedDictionary<double, int>();
        Rect box = words[0].Box;
        foreach (LayoutWord word in words)
        {
            box = box.Union(word.Box);
            foreach (LayoutGlyph g in word.Glyphs)
            {
                double rounded = RoundHalf(g.PointSize);
                int chars = g.Text.Length;
                sizeChars[rounded] = sizeChars.GetValueOrDefault(rounded) + chars;
                charsBySize[rounded] = charsBySize.GetValueOrDefault(rounded) + chars;
            }
        }

        dominantSize = sizeChars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;

        var line = new LayoutLine(words, box, builder.Baseline);
        AddSegments(line, words, cellGapFactor * LineSpaceWidth(glyphs.Select(g => g.Glyph), dominantSize));
        return line;
    }

    /// <summary>
    /// Typical gap between adjacent letters of a line without space glyphs: the median advance gap (never negative). It
    /// is about zero for ordinary text and positive for letter-spaced text such as a tracked title (FR-011); lines with
    /// space glyphs split words on the spaces, so their tracking is zero.
    /// </summary>
    private static double Tracking(List<LayoutGlyph> glyphs)
    {
        if (glyphs.Any(IsSpace) || glyphs.Count < 3)
        {
            return 0;
        }

        List<double> gaps = glyphs.Zip(glyphs.Skip(1), (a, b) => b.Start - a.End).Order().ToList();
        return Math.Max(0, gaps[gaps.Count / 2]);
    }

    /// <summary>Mean advance width of the line's own space glyphs, or a fraction of its font size when it has none.</summary>
    private static double LineSpaceWidth(IEnumerable<LayoutGlyph> glyphs, double lineSize)
    {
        double sum = 0;
        int count = 0;
        foreach (LayoutGlyph g in glyphs)
        {
            double width = g.End - g.Start;
            if (IsSpace(g) && width > 0)
            {
                sum += width;
                count++;
            }
        }

        return count > 0 ? sum / count : FallbackSpaceEm * lineSize;
    }

    private static void Flush(List<LayoutWord> words, List<LayoutGlyph> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        var text = new StringBuilder();
        Rect box = current[0].Box;
        int total = 0;
        int bold = 0;
        int italic = 0;
        foreach (LayoutGlyph g in current)
        {
            text.Append(g.Text);
            box = box.Union(g.Box);
            total += g.Text.Length;
            if (g.IsBold)
            {
                bold += g.Text.Length;
            }

            if (g.IsItalic)
            {
                italic += g.Text.Length;
            }
        }

        TextStyle style = TextStyle.None;
        if (bold * 2 > total)
        {
            style |= TextStyle.Bold;
        }

        if (italic * 2 > total)
        {
            style |= TextStyle.Italic;
        }

        words.Add(new LayoutWord(current.ToArray(), box, text.ToString(), style));
        current.Clear();
    }

    private static void AddSegments(LayoutLine line, List<LayoutWord> words, double splitGap)
    {
        var segmentWords = new List<LayoutWord> { words[0] };
        for (int i = 1; i < words.Count; i++)
        {
            double gap = WordStart(words[i]) - WordEnd(words[i - 1]);
            if (gap > splitGap)
            {
                line.Segments.Add(MakeSegment(segmentWords));
                segmentWords = [];
            }

            segmentWords.Add(words[i]);
        }

        line.Segments.Add(MakeSegment(segmentWords));
    }

    private static double WordStart(LayoutWord word) => word.Glyphs.Count > 0 ? word.Glyphs[0].Start : word.Box.Left;

    private static double WordEnd(LayoutWord word) => word.Glyphs.Count > 0 ? word.Glyphs[^1].End : word.Box.Right;

    private static LineSegment MakeSegment(List<LayoutWord> words)
    {
        Rect box = words[0].Box;
        foreach (LayoutWord w in words)
        {
            box = box.Union(w.Box);
        }

        return new LineSegment(words.ToArray(), box);
    }

    private static void ClassifyZone(LayoutLine line, LayoutPage page, double marginRatio)
    {
        if (line.Box.Bottom <= page.Height * marginRatio)
        {
            line.Zone = LineZone.Header;
        }
        else if (line.Box.Top >= page.Height * (1 - marginRatio))
        {
            line.Zone = LineZone.Footer;
        }
        else
        {
            line.Zone = LineZone.Body;
        }
    }

    private static BodyStyle? ComputeBodyStyle(
        List<List<(LayoutLine Line, double Size)>> perPage,
        SortedDictionary<double, int> charsBySize)
    {
        if (charsBySize.Count == 0)
        {
            return null;
        }

        // SortedDictionary iterates by ascending size, so a tie resolves to the smaller size.
        double bodySize = 0;
        int best = -1;
        foreach ((double size, int count) in charsBySize)
        {
            if (count > best)
            {
                best = count;
                bodySize = size;
            }
        }

        var distances = new List<double>();
        foreach (List<(LayoutLine Line, double Size)> lines in perPage)
        {
            for (int i = 0; i + 1 < lines.Count; i++)
            {
                (LayoutLine a, double sa) = lines[i];
                (LayoutLine b, double sb) = lines[i + 1];
                if (a.Zone != LineZone.Body || b.Zone != LineZone.Body || sa != bodySize || sb != bodySize)
                {
                    continue;
                }

                double d = b.Baseline - a.Baseline;
                if (d > 0 && d <= bodySize * MaxLeadingInFontSizes)
                {
                    distances.Add(d);
                }
            }
        }

        double leading;
        if (distances.Count == 0)
        {
            leading = bodySize * 1.2;
        }
        else
        {
            distances.Sort();
            int mid = distances.Count / 2;
            leading = distances.Count % 2 == 1 ? distances[mid] : (distances[mid - 1] + distances[mid]) / 2;
        }

        return new BodyStyle(bodySize, Math.Round(leading, 2));
    }

    private static double RoundHalf(double value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;

    private sealed class LineBuilder
    {
        private readonly double _seedSize;
        private Rect _core;
        private double _left = double.MaxValue;
        private double _right = double.MinValue;

        public LineBuilder(LayoutGlyph seed, int index)
        {
            _seedSize = seed.PointSize;
            _core = seed.Box;
            Baseline = seed.Baseline;
            SeedIndex = index;
        }

        public double Baseline { get; }

        public int SeedIndex { get; }

        public List<(LayoutGlyph Glyph, int Index)> Glyphs { get; } = [];

        public bool Accepts(LayoutGlyph glyph, LayoutOptions layout)
        {
            double smaller = Math.Min(_seedSize, glyph.PointSize);
            if (Math.Abs(Baseline - glyph.Baseline) <= layout.BaselineToleranceRatio * smaller)
            {
                return true;
            }

            // Vertical overlap catches raised or lowered glyphs (superscripts, footnote markers); a full-size glyph far to
            // the side with another baseline belongs to a different line, e.g. of the other column (FR-030) — also body
            // text beside a larger heading there.
            double distance = Math.Max(0, Math.Max(_left - glyph.Box.Right, glyph.Box.Left - _right));
            if (distance > NearGlyphEm * smaller && glyph.PointSize >= MarkerSizeRatio * _seedSize)
            {
                return false;
            }

            double minHeight = Math.Min(_core.Height, glyph.Box.Height);
            return minHeight > 0 && _core.VerticalOverlap(glyph.Box) / minHeight >= layout.LineOverlapRatio;
        }

        public void Add(LayoutGlyph glyph, int index)
        {
            Glyphs.Add((glyph, index));
            _left = Math.Min(_left, glyph.Box.Left);
            _right = Math.Max(_right, glyph.Box.Right);
            if (glyph.PointSize >= _seedSize * CoreSizeRatio)
            {
                _core = _core.Union(glyph.Box);
            }
        }
    }
}
