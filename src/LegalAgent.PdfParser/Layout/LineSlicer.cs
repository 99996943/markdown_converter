namespace LegalAgent.PdfParser.Layout;

/// <summary>Builds a line from a subset of another line's words, keeping its zone, segments and annotations.</summary>
internal static class LineSlicer
{
    /// <summary>
    /// A new line made of <paramref name="words"/> (a non-empty, ordered subset of <paramref name="line"/>'s words)
    /// with role <see cref="LineRole.Unknown"/>.
    /// </summary>
    public static LayoutLine Slice(LayoutLine line, IReadOnlyList<LayoutWord> words)
    {
        Rect box = words.Skip(1).Aggregate(words[0].Box, (acc, w) => acc.Union(w.Box));
        var part = new LayoutLine(words, box, line.Baseline) { Zone = line.Zone, Role = LineRole.Unknown };
        var kept = new HashSet<LayoutWord>(words, ReferenceEqualityComparer.Instance);
        foreach (LineSegment segment in line.Segments)
        {
            List<LayoutWord> inside = segment.Words.Where(kept.Contains).ToList();
            if (inside.Count > 0)
            {
                part.Segments.Add(new LineSegment(inside, inside.Skip(1).Aggregate(inside[0].Box, (acc, w) => acc.Union(w.Box))));
            }
        }

        foreach (KeyValuePair<string, string> annotation in line.Annotations)
        {
            part.Annotations[annotation.Key] = annotation.Value;
        }

        return part;
    }
}
