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
/// a word; it is split off into its own word carrying <see cref="LayoutWord.FootnoteId"/>.
/// </para>
/// <para>
/// The footnote area of a page is either everything below a short horizontal separator rule in the lower part of the
/// page, provided all of it is set in a small font, or — without a rule — the run of small-font lines at the bottom
/// of the page whose first line starts with a label referenced on that page. Lines of the area get
/// <see cref="LineRole.Footnote"/>; a labelled line starts a definition, an unlabelled line continues the previous
/// one, also across pages. Definitions without any reference are marked orphan and reported (FTN001).
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
            markers[page] = FindMarkers(page);
        }

        string[] exceptions = context.Options.Normalization.HyphenationExceptions.ToArray();
        var texts = new Dictionary<FootnoteDraft, StringBuilder>();
        FootnoteDraft? last = null;
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            HashSet<string> referenced = markers[page].Select(m => Key(m.Label)).ToHashSet(StringComparer.Ordinal);
            foreach (LayoutLine line in FootnoteArea(page, body.FontSize * options.MaxSizeRatio, referenced))
            {
                line.Role = LineRole.Footnote;
                last = AddLine(context, page, line, last, texts, exceptions);
            }
        }

        foreach ((FootnoteDraft draft, StringBuilder text) in texts)
        {
            draft.Inlines.Add(new TextRun(text.ToString()));
        }

        var used = new HashSet<FootnoteDraft>(ReferenceEqualityComparer.Instance);
        foreach (LayoutPage page in context.Pages)
        {
            foreach (IGrouping<LayoutLine, Marker> byLine in markers[page].GroupBy(m => m.Line))
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

    /// <summary>Finds raised small-glyph runs at the end of words in ordinary lines.</summary>
    private static List<Marker> FindMarkers(LayoutPage page)
    {
        var result = new List<Marker>();
        foreach (LayoutLine line in page.Lines.Where(l => l.Role == LineRole.Unknown))
        {
            double lineSize = line.Words.SelectMany(w => w.Glyphs).Select(g => g.PointSize).DefaultIfEmpty(0).Max();
            for (int w = 0; w < line.Words.Count; w++)
            {
                IReadOnlyList<LayoutGlyph> glyphs = line.Words[w].Glyphs;
                int start = glyphs.Count;
                while (start > 0
                    && glyphs[start - 1].PointSize <= MarkerSizeRatio * lineSize
                    && line.Baseline - glyphs[start - 1].Baseline >= MarkerRaiseEm * lineSize)
                {
                    start--;
                }

                if (start == glyphs.Count)
                {
                    continue;
                }

                string label = string.Concat(glyphs.Skip(start).Select(g => g.Text));
                if (MarkerLabel().IsMatch(label))
                {
                    result.Add(new Marker(line, w, start, label));
                }
            }
        }

        return result;
    }

    private static List<LayoutLine> FootnoteArea(LayoutPage page, double maxSize, HashSet<string> referenced)
    {
        List<LayoutLine> lines = page.Lines.Where(l => l.Role == LineRole.Unknown).ToList();

        IEnumerable<Segment> rules = page.Rulings
            .Where(r => r.IsHorizontal && r.Y1 >= RulingMinYRatio * page.Height && r.Length <= RulingMaxWidthRatio * page.Width)
            .OrderBy(r => r.Y1);
        foreach (Segment rule in rules)
        {
            List<LayoutLine> below = lines.Where(l => l.Box.Top > rule.Y1).ToList();
            if (below.Count > 0 && below.All(l => SizeOf(l) <= maxSize))
            {
                return below;
            }
        }

        var block = new List<LayoutLine>();
        for (int i = lines.Count - 1; i >= 0 && SizeOf(lines[i]) <= maxSize; i--)
        {
            block.Insert(0, lines[i]);
        }

        return block.Count > 0
            && DefinitionLabel().Match(block[0].Text) is { Success: true } first
            && referenced.Contains(Key(first.Groups["label"].Value))
                ? block
                : [];
    }

    private static FootnoteDraft AddLine(
        PipelineContext context,
        LayoutPage page,
        LayoutLine line,
        FootnoteDraft? last,
        Dictionary<FootnoteDraft, StringBuilder> texts,
        string[] exceptions)
    {
        Match labelled = DefinitionLabel().Match(line.Text);
        if (labelled.Success || last is null)
        {
            var draft = new FootnoteDraft(context.Footnotes.Count, labelled.Success ? labelled.Groups["label"].Value : string.Empty, page.Number);
            context.Footnotes.Add(draft);
            texts[draft] = new StringBuilder(labelled.Success ? labelled.Groups["text"].Value.Trim() : line.Text.Trim());
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

            parts.Add(MakeWord(glyphs[marker.GlyphStart..], word.Style) with { FootnoteId = draft.Id });
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

    private sealed record Marker(LayoutLine Line, int WordIndex, int GlyphStart, string Label);
}
