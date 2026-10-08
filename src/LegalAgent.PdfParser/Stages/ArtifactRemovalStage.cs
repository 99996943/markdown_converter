using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Removes page artifacts — running headers, running footers and page numbers — from the margin zones of
/// each page (FR-020 – FR-025) and records them in the report (FR-027). Removed lines are deleted from
/// <see cref="LayoutPage.Lines"/>; a line that merely embeds a confirmed artifact is replaced by its remainder.
/// </summary>
public sealed class ArtifactRemovalStage : IPipelineStage
{
    /// <summary>Shortest fingerprint that may be removed as an embedded part of a longer line.</summary>
    private const int MinEmbeddedFingerprintLength = 5;

    /// <inheritdoc />
    public int Order => StageOrder.ArtifactRemoval;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ArtifactOptions options = context.Options.Artifacts;
        if (!options.Enabled || context.Pages.Count == 0)
        {
            return;
        }

        List<Candidate> candidates = CollectCandidates(context, options);
        var removals = new Dictionary<LayoutLine, LayoutLine?>(ReferenceEqualityComparer.Instance);

        if (options.RemovePageNumbers)
        {
            RemovePageNumbers(context, candidates, removals);
        }

        if (context.Pages.Count >= options.MinPages)
        {
            RemoveRunningArtifacts(context, options, candidates, removals);
        }

        Apply(context, removals);
    }

    private static List<Candidate> CollectCandidates(PipelineContext context, ArtifactOptions options)
    {
        var candidates = new List<Candidate>();
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            double headerLimit = page.Height * options.MarginZoneRatio;
            double footerLimit = page.Height * (1 - options.MarginZoneRatio);
            foreach (LayoutLine line in page.Lines)
            {
                LineZone zone;
                if (line.Box.Bottom <= headerLimit)
                {
                    zone = LineZone.Header;
                }
                else if (line.Box.Top >= footerLimit)
                {
                    zone = LineZone.Footer;
                }
                else
                {
                    continue;
                }

                string text = line.Text;
                bool isPageNumber = PageNumberPatterns.TryParse(text, out int printed);
                candidates.Add(new Candidate(
                    page,
                    line,
                    zone,
                    line.Box.CenterY / page.Height,
                    LineFingerprint.Compute(text),
                    isPageNumber,
                    printed));
            }
        }

        return candidates;
    }

    private static void RemovePageNumbers(
        PipelineContext context,
        List<Candidate> candidates,
        Dictionary<LayoutLine, LayoutLine?> removals)
    {
        List<Candidate> numbers = candidates.Where(c => c.IsPageNumber).ToList();
        int? offset = PageNumberPatterns.DetectOffset(numbers.Select(c => (c.Page.Number, c.PrintedNumber)));
        if (offset is null)
        {
            return;
        }

        foreach (Candidate candidate in numbers.Where(c => c.PrintedNumber - c.Page.Number == offset))
        {
            removals[candidate.Line] = null;
            context.Report.AddRemovedArtifact(ArtifactKind.PageNumber, candidate.Fingerprint, candidate.Page.Number);
        }
    }

    private static void RemoveRunningArtifacts(
        PipelineContext context,
        ArtifactOptions options,
        List<Candidate> candidates,
        Dictionary<LayoutLine, LayoutLine?> removals)
    {
        // Page-number-like lines never form running artifacts: either they were removed above, or they do not
        // follow the page sequence (or removal is disabled) and must be kept.
        List<Candidate> pool = candidates
            .Where(c => !c.IsPageNumber && c.Fingerprint.Length > 0 && !removals.ContainsKey(c.Line))
            .ToList();

        var clusters = new List<Cluster>();
        foreach (Candidate candidate in pool)
        {
            Cluster? match = clusters.FirstOrDefault(cl =>
                cl.Zone == candidate.Zone
                && Math.Abs(cl.RelativeY - candidate.RelativeY) <= options.PositionTolerance
                && IsSimilar(cl.Fingerprint, candidate.Fingerprint, options.Similarity));
            if (match is null)
            {
                match = new Cluster(candidate.Zone, candidate.RelativeY, candidate.Fingerprint);
                clusters.Add(match);
            }

            match.Members.Add(candidate);
        }

        int totalPages = context.Pages.Count;
        List<Cluster> confirmed = clusters.Where(cl => IsConfirmed(cl, context, options, totalPages)).ToList();

        foreach (Cluster cluster in confirmed)
        {
            foreach (Candidate member in cluster.Members)
            {
                removals[member.Line] = null;
                context.Report.AddRemovedArtifact(KindOf(cluster.Zone), cluster.Fingerprint, member.Page.Number);
            }
        }

        RemoveEmbeddedArtifacts(context, pool, confirmed, removals);
    }

    private static bool IsConfirmed(Cluster cluster, PipelineContext context, ArtifactOptions options, int totalPages)
    {
        List<int> pages = cluster.Members.Select(m => m.Page.Number).Distinct().ToList();
        if (pages.Count < options.MinPages)
        {
            return false;
        }

        if ((double)pages.Count / totalPages >= options.MinPageRatio)
        {
            return true;
        }

        if (!options.SplitOddEven || pages.Select(p => p % 2).Distinct().Count() != 1)
        {
            return false;
        }

        int parity = pages[0] % 2;
        int parityPages = context.Pages.Count(p => p.Number % 2 == parity);
        return (double)pages.Count / parityPages >= options.MinPageRatio;
    }

    /// <summary>
    /// FR-022: a margin line on a page where the pattern was not confirmed (typically page 1) loses the part
    /// whose fingerprint equals a confirmed artifact; the rest of the line is kept as content.
    /// </summary>
    private static void RemoveEmbeddedArtifacts(
        PipelineContext context,
        List<Candidate> pool,
        List<Cluster> confirmed,
        Dictionary<LayoutLine, LayoutLine?> removals)
    {
        foreach (Candidate candidate in pool.Where(c => !removals.ContainsKey(c.Line)))
        {
            foreach (Cluster cluster in confirmed.Where(cl => cl.Zone == candidate.Zone))
            {
                string? target = cluster.Members
                    .Select(m => m.Fingerprint)
                    .Where(fp => fp.Length >= MinEmbeddedFingerprintLength)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .FirstOrDefault(fp => candidate.Fingerprint.Contains(fp, StringComparison.Ordinal));
                if (target is null || !TryFindWordSpan(candidate.Line.Words, target, out int start, out int end))
                {
                    continue;
                }

                removals[candidate.Line] = WithoutWords(candidate.Line, start, end);
                context.Report.AddRemovedArtifact(KindOf(cluster.Zone), cluster.Fingerprint, candidate.Page.Number);
                break;
            }
        }
    }

    private static bool TryFindWordSpan(IReadOnlyList<LayoutWord> words, string target, out int start, out int end)
    {
        for (start = 0; start < words.Count; start++)
        {
            for (end = start; end < words.Count; end++)
            {
                string fingerprint = LineFingerprint.Compute(string.Join(' ', words.Skip(start).Take(end - start + 1).Select(w => w.Text)));
                if (string.Equals(fingerprint, target, StringComparison.Ordinal))
                {
                    return true;
                }

                if (fingerprint.Length > target.Length)
                {
                    break;
                }
            }
        }

        start = end = -1;
        return false;
    }

    /// <summary>Returns the line without words <paramref name="start"/>..<paramref name="end"/>, or <c>null</c> when nothing meaningful remains.</summary>
    private static LayoutLine? WithoutWords(LayoutLine line, int start, int end)
    {
        List<LayoutWord> remaining = line.Words.Where((_, i) => i < start || i > end).ToList();
        while (remaining.Count > 0 && LineFingerprint.Compute(remaining[0].Text).Length == 0)
        {
            remaining.RemoveAt(0);
        }

        while (remaining.Count > 0 && LineFingerprint.Compute(remaining[^1].Text).Length == 0)
        {
            remaining.RemoveAt(remaining.Count - 1);
        }

        if (remaining.Count == 0)
        {
            return null;
        }

        var kept = new HashSet<LayoutWord>(remaining, ReferenceEqualityComparer.Instance);
        var replacement = new LayoutLine(remaining, UnionOf(remaining), line.Baseline)
        {
            Zone = line.Zone,
            Role = line.Role,
        };

        foreach (LineSegment segment in line.Segments)
        {
            List<LayoutWord> words = segment.Words.Where(kept.Contains).ToList();
            if (words.Count > 0)
            {
                replacement.Segments.Add(new LineSegment(words, UnionOf(words)));
            }
        }

        foreach (KeyValuePair<string, string> annotation in line.Annotations)
        {
            replacement.Annotations[annotation.Key] = annotation.Value;
        }

        return replacement;
    }

    private static Rect UnionOf(List<LayoutWord> words) =>
        words.Skip(1).Aggregate(words[0].Box, (acc, w) => acc.Union(w.Box));

    private static void Apply(PipelineContext context, Dictionary<LayoutLine, LayoutLine?> removals)
    {
        if (removals.Count == 0)
        {
            return;
        }

        foreach (LayoutPage page in context.Pages)
        {
            List<LayoutLine> lines = page.Lines.ToList();
            page.Lines.Clear();
            foreach (LayoutLine line in lines)
            {
                if (!removals.TryGetValue(line, out LayoutLine? replacement))
                {
                    page.Lines.Add(line);
                }
                else if (replacement is not null)
                {
                    page.Lines.Add(replacement);
                }
            }
        }
    }

    private static bool IsSimilar(string a, string b, double threshold)
    {
        int max = Math.Max(a.Length, b.Length);

        // Cheap upper bound: the length difference alone already costs that many edits.
        if (max > 0 && 1.0 - ((double)Math.Abs(a.Length - b.Length) / max) < threshold)
        {
            return false;
        }

        return Levenshtein.Similarity(a, b) >= threshold;
    }

    private static ArtifactKind KindOf(LineZone zone) =>
        zone == LineZone.Header ? ArtifactKind.RunningHeader : ArtifactKind.RunningFooter;

    private sealed record Candidate(
        LayoutPage Page,
        LayoutLine Line,
        LineZone Zone,
        double RelativeY,
        string Fingerprint,
        bool IsPageNumber,
        int PrintedNumber);

    private sealed class Cluster(LineZone zone, double relativeY, string fingerprint)
    {
        public LineZone Zone { get; } = zone;

        public double RelativeY { get; } = relativeY;

        public string Fingerprint { get; } = fingerprint;

        public List<Candidate> Members { get; } = [];
    }
}
