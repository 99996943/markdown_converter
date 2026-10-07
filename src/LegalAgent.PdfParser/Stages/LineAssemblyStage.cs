using System.Text;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Assembles glyphs into words and visual lines (FR-011, FR-030), splits lines into segments on large
/// horizontal gaps (FR-060), assigns margin zones per page and computes the document body style.
/// Lines are written to <see cref="LayoutPage.Lines"/> sorted top to bottom, then left to right.
/// </summary>
public sealed class LineAssemblyStage : IPipelineStage
{
    /// <summary>Space width as a fraction of the font size when a page has no explicit space glyphs.</summary>
    private const double FallbackSpaceEm = 0.26;

    /// <summary>Gap (in mean space widths) above which glyphs of a line without space glyphs form separate words.</summary>
    private const double ImplicitWordGapFactor = 0.6;

    /// <summary>Glyphs at least this fraction of the seed size extend the core box of a line.</summary>
    private const double CoreSizeRatio = 0.9;

    private const double MaxLeadingInFontSizes = 3.0;

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

        double meanSpace = MeanSpaceWidth(page);
        List<LineBuilder> builders = GroupIntoLines(page, options.Layout);
        var assembled = new List<(LayoutLine Line, double Size, int Order)>();

        foreach (LineBuilder builder in builders)
        {
            LayoutLine? line = BuildLine(builder, meanSpace, options.Tables.CellGapFactor, out double size, charsBySize);
            if (line is null)
            {
                continue;
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

    private static double MeanSpaceWidth(LayoutPage page)
    {
        double sum = 0;
        int count = 0;
        foreach (LayoutGlyph g in page.Glyphs)
        {
            if (IsSpace(g) && g.Box.Width > 0)
            {
                sum += g.Box.Width;
                count++;
            }
        }

        if (count > 0)
        {
            return sum / count;
        }

        double[] sizes = page.Glyphs.Select(g => g.PointSize).Order().ToArray();
        return FallbackSpaceEm * sizes[sizes.Length / 2];
    }

    private static bool IsSpace(LayoutGlyph g) => string.IsNullOrWhiteSpace(g.Text);

    private static List<LineBuilder> GroupIntoLines(LayoutPage page, LayoutOptions layout)
    {
        int[] order = Enumerable.Range(0, page.Glyphs.Count)
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
        double meanSpace,
        double cellGapFactor,
        out double dominantSize,
        SortedDictionary<double, int> charsBySize)
    {
        dominantSize = 0;
        List<(LayoutGlyph Glyph, int Index)> glyphs = builder.Glyphs
            .OrderBy(g => g.Glyph.Box.Left)
            .ThenBy(g => g.Index)
            .ToList();

        bool hasSpaces = glyphs.Any(g => IsSpace(g.Glyph));
        double wordGap = hasSpaces ? meanSpace : meanSpace * ImplicitWordGapFactor;

        var words = new List<LayoutWord>();
        var current = new List<LayoutGlyph>();
        LayoutGlyph? previous = null;

        foreach ((LayoutGlyph glyph, int _) in glyphs)
        {
            if (IsSpace(glyph))
            {
                Flush(words, current);
                continue;
            }

            if (previous is not null && current.Count > 0 && glyph.Box.Left - previous.Box.Right > wordGap)
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
        AddSegments(line, words, meanSpace * cellGapFactor);
        return line;
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
            double gap = words[i].Box.Left - words[i - 1].Box.Right;
            if (gap > splitGap)
            {
                line.Segments.Add(MakeSegment(segmentWords));
                segmentWords = [];
            }

            segmentWords.Add(words[i]);
        }

        line.Segments.Add(MakeSegment(segmentWords));
    }

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

            double minHeight = Math.Min(_core.Height, glyph.Box.Height);
            return minHeight > 0 && _core.VerticalOverlap(glyph.Box) / minHeight >= layout.LineOverlapRatio;
        }

        public void Add(LayoutGlyph glyph, int index)
        {
            Glyphs.Add((glyph, index));
            if (glyph.PointSize >= _seedSize * CoreSizeRatio)
            {
                _core = _core.Union(glyph.Box);
            }
        }
    }
}
