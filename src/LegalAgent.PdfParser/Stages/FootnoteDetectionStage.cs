using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects footnote definitions at the bottom of pages and footnote reference markers in the text (FR-026).
/// </summary>
/// <remarks>
/// <para>
/// A reference marker is a run of smaller, raised glyphs (e.g. a superscript <c>1)</c>), possibly glued to the end of
/// a word — or, spec 010, to the start of one („³kurs”); it is split off into its own word carrying
/// <see cref="LayoutWord.FootnoteId"/>.
/// </para>
/// <para>
/// The footnote area of a page is either everything below a short horizontal separator rule in the lower part of the
/// page, provided all of it is set in a small font, or — without a rule — the run of small-font lines at the bottom
/// of the page whose first line starts with a label referenced on that page. Lines of the area get
/// <see cref="LineRole.Footnote"/>; a labelled line starts a definition, an unlabelled line continues the previous
/// one, also across pages. Definitions without any reference are marked orphan and reported (FTN001).
/// </para>
/// <para>
/// Spec 010 (T12): a label is also a run of smaller, raised digits opening the line, with or without a space; below a
/// rule a bare number followed by a space („8 Dotyczy…”) is a label when that number is referenced on the page. Below a
/// rule the definitions may be set as large as the body text when the first line opens with such a label. Raised
/// labels opening the lines below a rule are never references.
/// </para>
/// <para>
/// Spec 011: a raised label opening a footnote line never links a definition, with or without a rule, so a definition
/// whose reference is not recognised in the text is reported as orphan instead of linking itself.
/// </para>
/// </remarks>
public sealed partial class FootnoteDetectionStage : IPipelineStage
{
    private const double RulingMinYRatio = 0.6;
    private const double RulingMaxWidthRatio = 0.6;
    private const double MarkerSizeRatio = 0.85;
    private const double MarkerRaiseEm = 0.15;

    /// <inheritdoc />
    public int Order => StageOrder.FootnoteDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        FootnoteOptions options = context.Options.Footnotes;
        if (!options.Enabled || context.BodyStyle is not { FontSize: > 0 } body)
        {
            return;
        }

        var markers = new Dictionary<LayoutPage, List<Marker>>();
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            markers[page] = FindMarkers(page, BelowFirstRule(page));
        }

        string[] exceptions = context.Options.Normalization.HyphenationExceptions.ToArray();
        var texts = new Dictionary<FootnoteDraft, StringBuilder>();
        FootnoteDraft? last = null;
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            HashSet<string> referenced = markers[page].Select(m => Key(m.Label)).ToHashSet(StringComparer.Ordinal);
            (List<LayoutLine> area, bool belowRule) = FootnoteArea(page, body.FontSize * options.MaxSizeRatio, body.FontSize, referenced);
            for (int i = 0; i < area.Count; i++)
            {
                LayoutLine line = area[i];
                line.Role = LineRole.Footnote;

                // Spec 010 (firm-5): a raised label set as a line of its own, overlapping the definition text beside it.
                if (i + 1 < area.Count && LoneLabel(line, area[i + 1], referenced) is { } label)
                {
                    area[++i].Role = LineRole.Footnote;
                    last = AddLine(context, page, area[i], new DefinitionStart(label, area[i].Text.Trim()), last, texts, exceptions);
                    continue;
                }

                last = AddLine(context, page, line, Label(line, belowRule, referenced), last, texts, exceptions);
            }
        }

        foreach ((FootnoteDraft draft, StringBuilder text) in texts)
        {
            draft.Inlines.Add(new TextRun(text.ToString()));
        }

        var used = new HashSet<FootnoteDraft>(ReferenceEqualityComparer.Instance);
        foreach (LayoutPage page in context.Pages)
        {
            foreach (IGrouping<LayoutLine, Marker> byLine in markers[page].Where(m => !IsDefinitionLabel(m)).GroupBy(m => m.Line))
            {
                LinkMarkers(context, page, byLine.Key, byLine.ToList(), used);
            }
        }

        foreach (FootnoteDraft draft in context.Footnotes.Where(d => !used.Contains(d)))
        {
            draft.IsOrphan = true;
            context.Report.AddWarning(
                "FTN001_OrphanFootnote",
                draft.Page,
                $"Przypis „{draft.Label}” na stronie {draft.Page} nie ma odnośnika w tekście.");
        }
    }

    /// <summary>
    /// Finds raised small-glyph runs at the end of words in ordinary lines, or at the start of a word before a full-size
    /// letter or digit. A raised run opening a line below a rule labels a definition, not a reference.
    /// </summary>
    private static List<Marker> FindMarkers(LayoutPage page, HashSet<LayoutLine> belowRule)
    {
        var result = new List<Marker>();
        foreach (LayoutLine line in page.Lines.Where(l => l.Role == LineRole.Unknown))
        {
            double lineSize = line.Words.SelectMany(w => w.Glyphs).Select(g => g.PointSize).DefaultIfEmpty(0).Max();
            for (int w = 0; w < line.Words.Count; w++)
            {
                IReadOnlyList<LayoutGlyph> glyphs = line.Words[w].Glyphs;
                double baseline = SegmentBaseline(line, line.Words[w], lineSize);
                bool Raised(LayoutGlyph g) => g.PointSize <= MarkerSizeRatio * lineSize && baseline - g.Baseline >= MarkerRaiseEm * lineSize;
                if (w == 0 && glyphs.Count > 0 && Raised(glyphs[0]) && belowRule.Contains(line))
                {
                    continue;
                }
                int start = glyphs.Count;
                while (start > 0 && Raised(glyphs[start - 1]))
                {
                    start--;
                }

                if (start < glyphs.Count)
                {
                    string label = string.Concat(glyphs.Skip(start).Select(g => g.Text));
                    if (MarkerLabel().IsMatch(label))
                    {
                        result.Add(new Marker(line, w, start, glyphs.Count, label));
                    }

                    continue;
                }

                int end = 0;
                while (end < glyphs.Count && Raised(glyphs[end]))
                {
                    end++;
                }

                if (end > 0 && end < glyphs.Count && glyphs[end].Text.Length > 0 && char.IsLetterOrDigit(glyphs[end].Text[0]))
                {
                    string label = string.Concat(glyphs.Take(end).Select(g => g.Text));
                    if (LeadingMarkerLabel().IsMatch(label))
                    {
                        result.Add(new Marker(line, w, 0, end, label));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>The lines below the topmost short rule in the lower part of the page (a candidate footnote area).</summary>
    private static HashSet<LayoutLine> BelowFirstRule(LayoutPage page)
    {
        List<Segment> rules = Rules(page).ToList();
        return rules.Count > 0
            ? page.Lines.Where(l => l.Role == LineRole.Unknown && l.Box.Top > rules[0].Y1).ToHashSet<LayoutLine>(ReferenceEqualityComparer.Instance)
            : [];
    }

    private static IEnumerable<Segment> Rules(LayoutPage page) =>
        page.Rulings
            .Where(r => r.IsHorizontal && r.Y1 >= RulingMinYRatio * page.Height && r.Length <= RulingMaxWidthRatio * page.Width)
            .OrderBy(r => r.Y1);

    /// <summary>
    /// The baseline of the text around <paramref name="word"/>: of its segment's full-size glyphs. A line merged from
    /// two columns (FR-030) whose baselines are offset carries the baseline of one of them only.
    /// </summary>
    private static double SegmentBaseline(LayoutLine line, LayoutWord word, double lineSize)
    {
        LineSegment? segment = line.Segments.FirstOrDefault(s => s.Words.Contains(word));
        List<double> baselines = (segment?.Words ?? line.Words)
            .SelectMany(w => w.Glyphs)
            .Where(g => g.PointSize > MarkerSizeRatio * lineSize && !string.IsNullOrWhiteSpace(g.Text))
            .Select(g => g.Baseline)
            .Order()
            .ToList();
        return baselines.Count > 0 ? baselines[baselines.Count / 2] : line.Baseline;
    }

    /// <summary>
    /// The footnote area of a page and whether it lies below a rule. Below a rule the lines are small, or — spec 010 — no
    /// larger than the body text when the first opens with a label referenced on the page.
    /// </summary>
    private static (List<LayoutLine> Lines, bool BelowRule) FootnoteArea(LayoutPage page, double maxSize, double bodySize, HashSet<string> referenced)
    {
        List<LayoutLine> lines = page.Lines.Where(l => l.Role == LineRole.Unknown).ToList();

        foreach (Segment rule in Rules(page))
        {
            List<LayoutLine> below = lines.Where(l => l.Box.Top > rule.Y1).ToList();
            if (below.Count > 0
                && (below.All(l => SizeOf(l) <= maxSize)
                    || (below.All(l => SizeOf(l) <= bodySize) && Label(below[0], belowRule: true, referenced) is { } first && referenced.Contains(Key(first.Label)))))
            {
                return (below, true);
            }
        }

        var block = new List<LayoutLine>();
        for (int i = lines.Count - 1; i >= 0 && SizeOf(lines[i]) <= maxSize; i--)
        {
            block.Insert(0, lines[i]);
        }

        return block.Count > 0
            && Label(block[0], belowRule: false, referenced) is { } label
            && referenced.Contains(Key(label.Label))
                ? (block, false)
                : ([], false);
    }

    /// <summary>
    /// The label opening a definition line and the text after it: „1)”, „*)”; raised smaller digits; below a rule a bare
    /// number followed by a space, when it is referenced on the page.
    /// </summary>
    private static DefinitionStart? Label(LayoutLine line, bool belowRule, HashSet<string> referenced)
    {
        if (DefinitionLabel().Match(line.Text) is { Success: true } match)
        {
            return new DefinitionStart(match.Groups["label"].Value, match.Groups["text"].Value.Trim());
        }

        if (line.Words.Count > 0)
        {
            double lineSize = line.Words.SelectMany(w => w.Glyphs).Select(g => g.PointSize).DefaultIfEmpty(0).Max();
            IReadOnlyList<LayoutGlyph> glyphs = line.Words[0].Glyphs;
            double baseline = SegmentBaseline(line, line.Words[0], lineSize);
            int end = 0;
            while (end < glyphs.Count && glyphs[end].PointSize <= MarkerSizeRatio * lineSize && baseline - glyphs[end].Baseline >= MarkerRaiseEm * lineSize)
            {
                end++;
            }

            string raised = string.Concat(glyphs.Take(end).Select(g => g.Text));
            if (end > 0 && LeadingMarkerLabel().IsMatch(raised) && line.Text.StartsWith(raised, StringComparison.Ordinal) && line.Text.Length > raised.Length)
            {
                return new DefinitionStart(raised, line.Text[raised.Length..].Trim());
            }
        }

        return belowRule && BareLabel().Match(line.Text) is { Success: true } bare && referenced.Contains(bare.Groups["label"].Value)
            ? new DefinitionStart(bare.Groups["label"].Value, bare.Groups["text"].Value.Trim())
            : null;
    }

    /// <summary>A line holding only a label referenced on the page whose box overlaps the next line: that line's label.</summary>
    private static string? LoneLabel(LayoutLine line, LayoutLine next, HashSet<string> referenced) =>
        line.Words.Count == 1
        && LeadingMarkerLabel().IsMatch(line.Words[0].Text)
        && referenced.Contains(Key(line.Words[0].Text))
        && next.Box.Top < line.Box.Bottom
        && next.Words.Count > 0
            ? line.Words[0].Text
            : null;

    private static FootnoteDraft AddLine(
        PipelineContext context,
        LayoutPage page,
        LayoutLine line,
        DefinitionStart? labelled,
        FootnoteDraft? last,
        Dictionary<FootnoteDraft, StringBuilder> texts,
        string[] exceptions)
    {
        if (labelled is not null || last is null)
        {
            var draft = new FootnoteDraft(context.Footnotes.Count, labelled?.Label ?? string.Empty, page.Number);
            context.Footnotes.Add(draft);
            texts[draft] = new StringBuilder(labelled?.Text ?? line.Text.Trim());
            return draft;
        }

        StringBuilder text = texts[last];
        string next = line.Text.Trim();
        switch (Hyphenation.Decide(text.ToString(), next, exceptions))
        {
            case HyphenJoin.Remove:
                text.Length--;
                text.Append(next);
                break;
            case HyphenJoin.Keep:
                text.Append(next);
                break;
            default:
                text.Append(' ').Append(next);
                break;
        }

        return last;
    }

    /// <summary>Links the markers of one line to definitions and splits them off into reference words.</summary>
    private static void LinkMarkers(PipelineContext context, LayoutPage page, LayoutLine line, List<Marker> markers, HashSet<FootnoteDraft> used)
    {
        var replacement = new Dictionary<LayoutWord, List<LayoutWord>>(ReferenceEqualityComparer.Instance);
        foreach (Marker marker in markers)
        {
            FootnoteDraft? draft = context.Footnotes
                .Where(d => Key(d.Label) == Key(marker.Label) && d.Page >= page.Number)
                .OrderBy(d => d.Page)
                .ThenBy(d => d.Id)
                .FirstOrDefault();
            if (draft is null)
            {
                continue;
            }

            used.Add(draft);
            LayoutWord word = line.Words[marker.WordIndex];
            List<LayoutGlyph> glyphs = [.. word.Glyphs];
            var parts = new List<LayoutWord>();
            if (marker.GlyphStart > 0)
            {
                parts.Add(MakeWord(glyphs[..marker.GlyphStart], word.Style));
            }

            bool leading = marker.GlyphEnd < glyphs.Count;
            parts.Add(MakeWord(glyphs[marker.GlyphStart..marker.GlyphEnd], word.Style) with { FootnoteId = draft.Id, ReferencesNextWord = leading });
            if (leading)
            {
                parts.Add(MakeWord(glyphs[marker.GlyphEnd..], word.Style));
            }

            replacement[word] = parts;
        }

        if (replacement.Count == 0)
        {
            return;
        }

        List<LayoutWord> words = line.Words.SelectMany(w => replacement.TryGetValue(w, out List<LayoutWord>? parts) ? parts : [w]).ToList();
        var rebuilt = new LayoutLine(words, line.Box, line.Baseline) { Zone = line.Zone, Role = line.Role };
        foreach (LineSegment segment in line.Segments)
        {
            List<LayoutWord> segmentWords = segment.Words
                .SelectMany(w => replacement.TryGetValue(w, out List<LayoutWord>? parts) ? parts : [w])
                .ToList();
            rebuilt.Segments.Add(new LineSegment(segmentWords, segment.Box));
        }

        foreach (KeyValuePair<string, string> annotation in line.Annotations)
        {
            rebuilt.Annotations[annotation.Key] = annotation.Value;
        }

        page.Lines[page.Lines.IndexOf(line)] = rebuilt;
    }

    private static LayoutWord MakeWord(List<LayoutGlyph> glyphs, TextStyle style)
    {
        Rect box = glyphs.Skip(1).Aggregate(glyphs[0].Box, (acc, g) => acc.Union(g.Box));
        return new LayoutWord(glyphs, box, string.Concat(glyphs.Select(g => g.Text)), style);
    }

    private static double SizeOf(LayoutLine line)
    {
        var chars = new SortedDictionary<double, int>();
        foreach (LayoutGlyph glyph in line.Words.SelectMany(w => w.Glyphs))
        {
            double size = Math.Round(glyph.PointSize * 2, MidpointRounding.AwayFromZero) / 2;
            chars[size] = chars.GetValueOrDefault(size) + glyph.Text.Length;
        }

        return chars.Count == 0 ? double.MaxValue : chars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;
    }

    /// <summary>Label without the closing parenthesis: „1)” and „1” refer to the same footnote.</summary>
    private static string Key(string label) => label.TrimEnd(')');

    [GeneratedRegex(@"^(\d{1,3}\)?|\*{1,3}\)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkerLabel();

    [GeneratedRegex(@"^(?<label>\d{1,3}\)|\*{1,3}\)?)\s+(?<text>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DefinitionLabel();

    /// <summary>Spec 010: digits raised before a word („³kurs”) or opening a definition („³ Zapis…”).</summary>
    [GeneratedRegex(@"^\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingMarkerLabel();

    /// <summary>Spec 010: a bare number and a space opening a definition below a rule („8 Dotyczy…”).</summary>
    [GeneratedRegex(@"^(?<label>\d{1,3})\s+(?<text>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex BareLabel();

    /// <summary>
    /// Spec 011: a marker opening a footnote line is the label of that line's definition, never a reference — with or
    /// without a rule. It still counts as referenced for finding the footnote area; only linking skips it.
    /// </summary>
    private static bool IsDefinitionLabel(Marker marker) =>
        marker.Line.Role == LineRole.Footnote && marker.WordIndex == 0 && marker.GlyphStart == 0;

    /// <param name="Line">Line of the marker.</param>
    /// <param name="WordIndex">Word holding the marker.</param>
    /// <param name="GlyphStart">First glyph of the marker in the word.</param>
    /// <param name="GlyphEnd">Glyph after the marker: the word's end, or the start of the word it stands before.</param>
    /// <param name="Label">Marker text.</param>
    private sealed record Marker(LayoutLine Line, int WordIndex, int GlyphStart, int GlyphEnd, string Label);

    private sealed record DefinitionStart(string Label, string Text);
}
