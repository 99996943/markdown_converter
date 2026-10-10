using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Spec 007 (FR-520, research R3): a glossary laid out as two columns — a label and a bold term on the left, the
/// definition on the right — whose only column marks are horizontal rulings split at the column boundary, one under
/// each entry. Its lines are sliced at the boundary and annotated with <see cref="LayoutAnnotations.DefListEntry"/> and
/// <see cref="LayoutAnnotations.DefListSide"/> instead of becoming a table; each entry's term lines are put before its
/// definition lines, so list detection builds one item per definition.
/// </summary>
/// <remarks>
/// An entry is the band between consecutive split rulings; the first one starts below the last line that is not part
/// of the glossary (the introductory sentence running across the boundary, a heading), the last one ends at a gap or a
/// unit designation. Every entry's left side must start with a label („1/”, „1.”, „1)”) followed by bold words; the
/// first band of a page without that label continues the last entry of the previous page. A region without at least
/// two split rulings, with a vertical ruling, or with an entry failing the check is left to table detection.
/// </remarks>
internal static class GlossaryDetection
{
    private const double Slack = 1.5;
    private const int MinRulings = 2;
    private const double MaxGapInLeadings = 2.0;

    /// <summary>Marks the glossaries of all pages; returns nothing, the lines carry the annotations.</summary>
    public static void Mark(PipelineContext context)
    {
        int entry = 0;
        int lastPage = -1;
        foreach (LayoutPage page in context.Pages)
        {
            if (page.Skipped is not null)
            {
                continue;
            }

            int before = entry;
            entry = MarkPage(context, page, entry, continues: lastPage == page.Number - 1);
            if (entry != before)
            {
                lastPage = page.Number;
            }
        }
    }

    private static int MarkPage(PipelineContext context, LayoutPage page, int entry, bool continues)
    {
        if (Boundary(page) is not ({ } boundary, { } rulings))
        {
            return entry;
        }

        double leading = context.BodyStyle?.Leading is > 0 and double l ? l : 10;
        double top = rulings[0] - (MaxGapInLeadings * leading);
        double bottom = rulings[^1] + (MaxGapInLeadings * leading);
        if (page.Rulings.Any(r => r.IsVertical && Math.Max(r.Y1, r.Y2) > top && Math.Min(r.Y1, r.Y2) < bottom))
        {
            return entry;
        }

        List<LayoutLine> body = page.Lines
            .Where(l => l.Role == LineRole.Unknown && l.Words.Count > 0
                && !l.Annotations.ContainsKey(LayoutAnnotations.StepIndex) && !l.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex))
            .OrderBy(l => l.Baseline)
            .ToList();

        // Bands between the rulings, then the band above the first and below the last one.
        var bands = new List<List<LayoutLine>>();
        for (int i = 0; i <= rulings.Count; i++)
        {
            double from = i == 0 ? double.NegativeInfinity : rulings[i - 1];
            double to = i == rulings.Count ? double.PositiveInfinity : rulings[i];
            bands.Add(body.Where(l => l.Box.CenterY > from && l.Box.CenterY < to).ToList());
        }

        bands[0] = Edge(bands[0], boundary, leading, upward: true, rulings[0]);
        bands[^1] = Edge(bands[^1], boundary, leading, upward: false, rulings[^1]);
        if (bands.Count(b => b.Count > 0) < MinRulings)
        {
            return entry;
        }

        // Every band must be an entry (label + bold term on the left), or the continuation of the previous page's entry.
        var numbers = new List<int>();
        int next = entry;
        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i].Count == 0)
            {
                numbers.Add(0);
                continue;
            }

            if (StartsEntry(bands[i], boundary))
            {
                numbers.Add(++next);
            }
            else if (i == 0 && continues && entry > 0)
            {
                numbers.Add(entry);
            }
            else
            {
                return entry;
            }
        }

        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i].Count > 0)
            {
                Apply(page, bands[i], boundary, numbers[i]);
            }
        }

        return next;
    }

    /// <summary>The x of the column boundary shared by at least two horizontal rulings split there, and their y values.</summary>
    private static (double? Boundary, List<double>? Rulings) Boundary(LayoutPage page)
    {
        List<Segment> horizontals = page.Rulings.Where(r => r.IsHorizontal).ToList();
        var splits = new List<(double X, double Y)>();
        foreach (Segment a in horizontals)
        {
            double aRight = Math.Max(a.X1, a.X2);
            foreach (Segment b in horizontals)
            {
                // b starts where a ends: the two pieces of one ruling split at the column boundary.
                if (Math.Abs(a.Y1 - b.Y1) <= Slack && Math.Abs(Math.Min(b.X1, b.X2) - aRight) <= Slack && Math.Max(b.X1, b.X2) > aRight + Slack)
                {
                    splits.Add((aRight, a.Y1));
                }
            }
        }

        var best = splits
            .GroupBy(s => Math.Round(s.X))
            .Select(g => (X: g.Average(s => s.X), Ys: g.Select(s => s.Y).Distinct().Order().ToList()))
            .Where(g => g.Ys.Count >= MinRulings)
            .OrderByDescending(g => g.Ys.Count)
            .FirstOrDefault();
        return best.Ys is null ? (null, null) : (best.X, best.Ys);
    }

    /// <summary>
    /// The lines of the band above the first ruling (upward) or below the last one (downward) that belong to the
    /// glossary: side lines (left part bold or empty) without a wide gap, up to a unit designation.
    /// </summary>
    private static List<LayoutLine> Edge(List<LayoutLine> band, double boundary, double leading, bool upward, double ruling)
    {
        var kept = new List<LayoutLine>();
        IEnumerable<LayoutLine> walk = upward ? band.OrderByDescending(l => l.Baseline) : band.OrderBy(l => l.Baseline);
        double last = ruling;
        foreach (LayoutLine line in walk)
        {
            double gap = Math.Abs(line.Baseline - last);
            if (gap > MaxGapInLeadings * leading
                || LegalUnitPatterns.TryMatch(line.Text, out _)
                || !IsSideLine(line, boundary))
            {
                break;
            }

            kept.Add(line);
            last = line.Baseline;
        }

        return upward ? Enumerable.Reverse(kept).ToList() : kept;
    }

    /// <summary>A line of the glossary: its words left of the boundary are bold (label and term) or there are none.</summary>
    private static bool IsSideLine(LayoutLine line, double boundary)
    {
        List<LayoutWord> left = line.Words.Where(w => w.Box.CenterX < boundary).ToList();
        return left.All(w => w.Style.HasFlag(TextStyle.Bold) && w.Box.Right <= boundary + Slack);
    }

    private static bool StartsEntry(List<LayoutLine> band, double boundary)
    {
        List<LayoutWord> left = band.SelectMany(l => l.Words.Where(w => w.Box.CenterX < boundary)).ToList();
        return left.Count >= 2
            && ListLabelPatterns.TryMatch(left[0].Text + " x", out ListLabelMatch? label)
            && label.Kind is ListLabelKind.ArabicSlash or ListLabelKind.ArabicDot or ListLabelKind.ArabicParen
            && left.Skip(1).All(w => w.Style.HasFlag(TextStyle.Bold) && w.Box.Right <= boundary + Slack);
    }

    /// <summary>Slices the band's lines at the boundary, annotates them and puts the term lines before the definition.</summary>
    private static void Apply(LayoutPage page, List<LayoutLine> band, double boundary, int number)
    {
        var terms = new List<LayoutLine>();
        var definitions = new List<LayoutLine>();
        foreach (LayoutLine line in band.OrderBy(l => l.Baseline))
        {
            List<LayoutWord> left = line.Words.Where(w => w.Box.CenterX < boundary).ToList();
            List<LayoutWord> right = line.Words.Where(w => w.Box.CenterX >= boundary).ToList();
            if (left.Count > 0)
            {
                terms.Add(Annotated(left.Count == line.Words.Count ? line : LineSlicer.Slice(line, left), number, LayoutAnnotations.DefListTerm));
            }

            if (right.Count > 0)
            {
                definitions.Add(Annotated(right.Count == line.Words.Count ? line : LineSlicer.Slice(line, right), number, LayoutAnnotations.DefListDefinition));
            }
        }

        int at = band.Min(l => page.Lines.IndexOf(l));
        foreach (LayoutLine line in band)
        {
            page.Lines.Remove(line);
        }

        int position = Math.Min(at, page.Lines.Count);
        foreach (LayoutLine line in terms.Concat(definitions))
        {
            page.Lines.Insert(position++, line);
        }
    }

    private static LayoutLine Annotated(LayoutLine line, int number, string side)
    {
        line.Annotations[LayoutAnnotations.DefListEntry] = number.ToString(CultureInfo.InvariantCulture);
        line.Annotations[LayoutAnnotations.DefListSide] = side;
        return line;
    }
}
