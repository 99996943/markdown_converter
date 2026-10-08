using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects typographic headings and legal units, assigns heading levels and splits „Art. 5. Treść…” lines into a
/// heading and its first paragraph (FR-040 – FR-047, FR-043a).
/// </summary>
/// <remarks>
/// Only lines with <see cref="LineRole.Unknown"/> are considered, so table and footnote lines are never promoted
/// (FR-047). A detected heading line gets <see cref="LineRole.Heading"/> and <see cref="LayoutLine.Heading"/>;
/// lines merged into it (a chapter title under „Rozdział 3”, the title block under „USTAWA”) get the role only.
/// </remarks>
public sealed partial class HeadingDetectionStage : IPipelineStage
{
    private const double CenterMarginRatio = 0.10;
    private const double TitleBlockGapFactor = 2.0;
    private const int MinCapsLetters = 3;
    private const int MaxLevel = 6;

    private const int RankTitle = 0;
    private const int RankTypographicInLegal = 6;
    private const int RankUnit = 7;
    private const int MaxMarkerLength = 4;
    private const double MarkerSizeRatio = 0.8;
    private const int RankTypographicBase = 10;
    private const double CaptionWidthMargin = 0.1;
    private const double CaptionGapInLineHeights = 3;
    private const double CaptionTolerance = 1;

    /// <inheritdoc />
    public int Order => StageOrder.HeadingDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HeadingOptions options = context.Options.Headings;
        if (!options.Enabled)
        {
            return;
        }

        List<Entry> entries = Collect(context);
        if (entries.Count == 0)
        {
            return;
        }

        (double bodySize, bool bodyBold) = BodyStyleOf(entries);
        double leading = context.BodyStyle?.Leading is > 0 and var l ? l : bodySize * 1.2;
        Analyse(context, entries, options, bodySize, bodyBold, leading);

        var headings = new List<Detected>();
        DetectTitle(entries, options, leading, headings);
        DetectHeadings(entries, options, leading, headings);
        AssignLevels(headings, options);
        Apply(headings);
    }

    private static List<Entry> Collect(PipelineContext context)
    {
        var entries = new List<Entry>();
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            LayoutLine? previous = null;
            foreach (LayoutLine line in page.Lines)
            {
                if (line.Role is LineRole.Artifact or LineRole.SideNote)
                {
                    continue;
                }

                if (line.Role == LineRole.Unknown)
                {
                    string text = string.Join(' ', line.Words.Where(w => w.FootnoteId is null).Select(w => w.Text)).Trim();
                    if (text.Length > 0)
                    {
                        entries.Add(new Entry(page, line, previous, text));
                    }
                }

                previous = line;
            }
        }

        return entries;
    }

    private static (double Size, bool Bold) BodyStyleOf(List<Entry> entries)
    {
        var counts = new SortedDictionary<(double Size, bool Bold), int>();
        foreach (Entry entry in entries)
        {
            foreach (LayoutWord word in entry.Line.Words)
            {
                foreach (LayoutGlyph glyph in word.Glyphs)
                {
                    (double, bool) key = (RoundHalf(glyph.PointSize), glyph.IsBold);
                    counts[key] = counts.GetValueOrDefault(key) + glyph.Text.Length;
                }
            }
        }

        if (counts.Count == 0)
        {
            return (0, false);
        }

        // SortedDictionary iterates by (size, bold) ascending, so a tie resolves to the smaller, regular style.
        KeyValuePair<(double Size, bool Bold), int> best = counts.First();
        foreach (KeyValuePair<(double Size, bool Bold), int> pair in counts)
        {
            if (pair.Value > best.Value)
            {
                best = pair;
            }
        }

        return best.Key;
    }

    private static void Analyse(
        PipelineContext context,
        List<Entry> entries,
        HeadingOptions options,
        double bodySize,
        bool bodyBold,
        double leading)
    {
        var columns = entries
            .GroupBy(e => e.Page.Number)
            .ToDictionary(g => g.Key, g => (Left: g.Min(e => e.Line.Box.Left), Right: g.Max(e => e.Line.Box.Right)));

        foreach (Entry entry in entries)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            LayoutLine line = entry.Line;
            List<LayoutWord> words = line.Words.Where(w => w.FootnoteId is null).ToList();
            entry.Size = DominantSize(words);
            entry.AllBold = words.Count > 0 && words.All(w => w.Style.HasFlag(TextStyle.Bold));
            entry.Enlarged = bodySize > 0 && entry.Size >= options.SizeRatio * bodySize;
            entry.BoldSignal = entry.AllBold && !bodyBold;
            entry.Caps = IsCaps(entry.Text);
            entry.Isolated = entry.Previous is null || line.Baseline - entry.Previous.Baseline > options.GapFactor * leading;

            (double left, double right) = ColumnOf(line, columns[entry.Page.Number]);
            double width = right - left;
            entry.Centered = width > 0
                && Math.Abs(line.Box.CenterX - ((left + right) / 2)) <= options.CenterTolerance * width
                && line.Box.Left - left >= CenterMarginRatio * width
                && right - line.Box.Right >= CenterMarginRatio * width;

            if (options.DetectLegalUnits && LegalUnitPatterns.TryMatch(entry.Text, out LegalUnitMatch? match))
            {
                entry.Legal = match;
            }

            entry.Candidate = entry.Legal is null
                && entry.Isolated
                && entry.Text.Length <= options.MaxLength
                && entry.Text.Any(char.IsLetter)
                && !entry.Text.EndsWith(',')
                && !entry.Text.EndsWith(';')
                && (entry.Enlarged || entry.BoldSignal || entry.Caps || entry.Centered);

            entry.Plain = InTableDocumentPart(context, entry)
                || (options.ValidityLineAsParagraph && entry.Page == entries[0].Page && ValidityLine().IsMatch(entry.Text))
                || (options.DetectImageCaptions && IsImageCaption(entry));
            if (entry.Plain)
            {
                entry.Candidate = false;
                entry.Centered = false;
            }
        }
    }

    /// <summary>
    /// Spec 002, FR-088: a caption — a line lying on an image or whose top is at most three line heights below it, within
    /// the image's width widened by 10% on each side (a logo with the publisher's address under it).
    /// </summary>
    private static bool IsImageCaption(Entry entry)
    {
        Rect line = entry.Line.Box;
        foreach (Rect image in entry.Page.ImageAreas)
        {
            double margin = CaptionWidthMargin * image.Width;
            bool below = line.Top >= image.Bottom - CaptionTolerance && line.Top - image.Bottom <= CaptionGapInLineHeights * line.Height;
            bool on = line.Top < image.Bottom && line.Bottom > image.Top;
            if ((on || below) && line.Left >= image.Left - margin && line.Right <= image.Right + margin)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Spec 002, FR-086/FR-087: from the top of the first table-document to the end of the document only section names
    /// (set by table-document detection) and legal units are headings.
    /// </summary>
    private static bool InTableDocumentPart(PipelineContext context, Entry entry)
    {
        if (context.TableDocuments.Count == 0)
        {
            return false;
        }

        TableDocumentRegion first = context.TableDocuments[0];
        return entry.Page.Number > first.FirstPage || (entry.Page.Number == first.FirstPage && entry.Line.Box.CenterY > first.Top);
    }

    private static (double Left, double Right) ColumnOf(LayoutLine line, (double Left, double Right) page) =>
        LayoutAnnotations.GetNumber(line, LayoutAnnotations.ColumnLeft) is double left
        && LayoutAnnotations.GetNumber(line, LayoutAnnotations.ColumnRight) is double right
            ? (left, right)
            : page;

    /// <summary>
    /// The document title (FR-043): a line of the first page starting with an act type in capitals („USTAWA”,
    /// „OBWIESZCZENIE”, …) — lines above it on that page (a journal masthead) stay plain text — or, without such a
    /// line, the first line of the document when it is an enlarged heading of the top size class. Lines of the title
    /// block directly below — centred, or in the title's own font (a multi-line title) — are joined to it.
    /// </summary>
    private static void DetectTitle(List<Entry> entries, HeadingOptions options, double leading, List<Detected> headings)
    {
        Entry first = entries.FirstOrDefault(e => e.Page == entries[0].Page && e.Legal is null && !e.Plain && e.Isolated && ActType().IsMatch(e.Text))
            ?? entries[0];
        bool actTitle = first != entries[0] || ActType().IsMatch(first.Text);
        if (actTitle)
        {
            foreach (Entry above in entries.TakeWhile(e => e != first))
            {
                above.Consumed = true;
            }
        }
        else if (!first.Candidate || !first.Enlarged || entries.Any(e => e.Candidate && e.Enlarged && e.Size > first.Size + options.SizeClusterTolerance))
        {
            return;
        }

        var title = new Detected(first, SectionKind.DocumentTitle) { Text = first.Text, Rank = RankTitle, Level = 1 };
        MergeTitleBlock(title, entries, options, leading);
        headings.Add(title);
    }

    /// <summary>Appends to a title the following lines of its block: close below, centred or in the title's font.</summary>
    private static void MergeTitleBlock(Detected title, List<Entry> entries, HeadingOptions options, double leading)
    {
        Entry first = title.Entry;
        Entry last = first;
        LayoutLine chain = first.Line;
        for (int i = entries.IndexOf(first) + 1; i < entries.Count; i++)
        {
            Entry next = entries[i];
            if (next.Plain)
            {
                break;
            }

            // A footnote marker set apart from a title line (a title is plain text, so the marker is dropped).
            if (next.Page == first.Page
                && next.Previous == chain
                && next.Text.Length <= MaxMarkerLength
                && next.Size <= MarkerSizeRatio * last.Size)
            {
                title.Merged.Add(next);
                next.Consumed = true;
                chain = next.Line;
                continue;
            }

            if (next.Page != first.Page
                || next.Previous != chain
                || next.Line.Baseline - last.Line.Baseline > TitleBlockGapFactor * Math.Max(leading, last.Size * 1.2)
                || !(next.Centered || SameFont(next, first, options))
                || next.Legal is not null
                || next.Text.Length > options.MaxLength)
            {
                break;
            }

            title.Merged.Add(next);
            title.Text += " " + next.Text;
            next.Consumed = true;
            last = next;
            chain = next.Line;
        }

        first.Consumed = true;
    }

    private static bool SameFont(Entry a, Entry b, HeadingOptions options) =>
        Math.Abs(a.Size - b.Size) <= options.SizeClusterTolerance && a.AllBold == b.AllBold;

    private static void DetectHeadings(List<Entry> entries, HeadingOptions options, double leading, List<Detected> headings)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry.Consumed)
            {
                continue;
            }

            Entry? next = i + 1 < entries.Count && entries[i + 1].Previous == entry.Line && entries[i + 1].Page == entry.Page
                ? entries[i + 1]
                : null;

            if (entry.Legal is { } unit)
            {
                headings.Add(LegalHeading(entry, unit, next, options, leading));
                entry.Consumed = true;
                continue;
            }

            // FR-043: the title block of an act further in the document (an act announced in an annex) is one heading.
            if ((entry.Candidate || entry.Centered) && ActType().IsMatch(entry.Text))
            {
                var block = new Detected(entry, SectionKind.Typographic) { Text = entry.Text };
                MergeTitleBlock(block, entries, options, leading);
                headings.Add(block);
                continue;
            }

            if (!entry.Candidate)
            {
                continue;
            }

            var heading = new Detected(entry, SectionKind.Typographic) { Text = entry.Text };
            entry.Consumed = true;

            // Multi-line typographic heading: following lines in the same enlarged or bold style (FR-041, MaxLines).
            Entry last = entry;
            for (int j = i + 1; j < entries.Count && heading.Merged.Count + 1 < options.MaxLines; j++)
            {
                Entry follower = entries[j];
                if (follower.Page != entry.Page
                    || follower.Plain
                    || follower.Previous != last.Line
                    || follower.Legal is not null
                    || !(entry.Enlarged || entry.BoldSignal)
                    || Math.Abs(follower.Size - entry.Size) > options.SizeClusterTolerance
                    || follower.AllBold != entry.AllBold
                    || follower.Line.Baseline - last.Line.Baseline > options.GapFactor * leading
                    || heading.Text.Length + 1 + follower.Text.Length > options.MaxLength
                    || follower.Text.EndsWith(',')
                    || follower.Text.EndsWith(';'))
                {
                    break;
                }

                heading.Merged.Add(follower);
                heading.Text += " " + follower.Text;
                follower.Consumed = true;
                last = follower;
            }

            headings.Add(heading);
        }
    }

    private static Detected LegalHeading(Entry entry, LegalUnitMatch unit, Entry? next, HeadingOptions options, double leading)
    {
        var heading = new Detected(entry, unit.Kind) { Designation = unit.Designation, Number = unit.Number };

        if (unit.Kind is SectionKind.Article or SectionKind.Paragraph)
        {
            heading.Text = unit.Prefix + unit.Designation + ".";
            heading.SplitRest = unit.Rest.Length > 0;
            return heading;
        }

        if (unit.Rest.Length > 0)
        {
            heading.Title = unit.Rest;
        }
        else if (next is not null
            && next.Legal is null
            && next.Text.Length <= options.MaxLength
            && next.Line.Baseline - entry.Line.Baseline <= options.GapFactor * leading
            && !next.Text.EndsWith('.')
            && !next.Text.EndsWith(',')
            && !next.Text.EndsWith(';')
            && (next.AllBold == entry.AllBold || next.AllBold || next.Caps))
        {
            // FR-044: „Rozdział 3” followed by its title line in the same style, or in bold (ISAP).
            heading.Title = next.Text;
            heading.Merged.Add(next);
            next.Consumed = true;
        }

        heading.Text = heading.Title is null ? unit.Designation : $"{unit.Designation}. {heading.Title}";
        return heading;
    }

    private static void AssignLevels(List<Detected> headings, HeadingOptions options)
    {
        bool legalDocument = headings.Any(h => h.Designation is not null);

        // FR-042: typographic size classes, largest first.
        var classes = new List<double>();
        // The document title takes part: it occupies the top class.
        foreach (double size in headings.Where(h => h.Kind is SectionKind.Typographic or SectionKind.DocumentTitle && h.Entry.Enlarged)
            .Select(h => h.Entry.Size).OrderDescending())
        {
            if (classes.Count == 0 || classes[^1] - size > options.SizeClusterTolerance)
            {
                classes.Add(size);
            }
        }

        // FR-043: structural kinds present get consecutive levels from 2; units one below the deepest.
        SectionKind[] structuralOrder = [SectionKind.Book, SectionKind.Part, SectionKind.Division, SectionKind.Chapter, SectionKind.Subchapter];
        var structuralLevels = new Dictionary<SectionKind, int>();
        foreach (SectionKind kind in structuralOrder.Where(k => headings.Any(h => h.Kind == k)))
        {
            structuralLevels[kind] = Math.Min(MaxLevel, 2 + structuralLevels.Count);
        }

        int unitLevel = Math.Min(MaxLevel, structuralLevels.Count == 0 ? 2 : structuralLevels.Values.Max() + 1);
        int firstLegal = headings.FindIndex(h => h.Designation is not null);
        int lastLegal = headings.FindLastIndex(h => h.Designation is not null);

        for (int i = 0; i < headings.Count; i++)
        {
            Detected h = headings[i];
            switch (h.Kind)
            {
                case SectionKind.DocumentTitle:
                    break;

                case SectionKind.Article or SectionKind.Paragraph:
                    h.Level = unitLevel;
                    h.Rank = RankUnit;
                    break;

                case SectionKind.Typographic when legalDocument:
                    // FR-043a: top level outside the legal structure, otherwise below the open section.
                    h.Level = i < firstLegal || i > lastLegal || i == 0 ? 2 : Math.Min(MaxLevel, headings[i - 1].Level + 1);
                    h.Rank = RankTypographicInLegal;
                    break;

                case SectionKind.Typographic:
                    int typographic = h.Entry.Enlarged
                        ? classes.FindIndex(c => Math.Abs(c - h.Entry.Size) <= options.SizeClusterTolerance) + 1
                        : Math.Max(2, classes.Count + 1);
                    h.Level = Math.Min(typographic, options.MaxTypographicDepth);
                    h.Rank = RankTypographicBase + h.Level;
                    break;

                default:
                    h.Level = structuralLevels[h.Kind];
                    h.Rank = Array.IndexOf(structuralOrder, h.Kind) + 1;
                    break;
            }
        }

        // A level never exceeds its parent's level + 1 (FR-043, FR-043a, contract invariant 4).
        var open = new Stack<Detected>();
        foreach (Detected h in headings)
        {
            while (open.Count > 0 && open.Peek().Rank >= h.Rank)
            {
                open.Pop();
            }

            if (open.Count > 0)
            {
                h.Level = Math.Min(h.Level, open.Peek().Level + 1);
            }

            open.Push(h);
        }
    }

    private static void Apply(List<Detected> headings)
    {
        foreach (Detected h in headings)
        {
            var info = new HeadingInfo(h.Level, h.Kind, h.Designation, h.Number, h.Title, h.Text);
            LayoutLine line = h.Entry.Line;
            foreach (Entry merged in h.Merged)
            {
                merged.Line.Role = LineRole.Heading;
            }

            if (h.SplitRest && Split(line, h.Designation!) is ({ } headingLine, { } contentLine))
            {
                int index = h.Entry.Page.Lines.IndexOf(line);
                h.Entry.Page.Lines[index] = headingLine;
                h.Entry.Page.Lines.Insert(index + 1, contentLine);
                line = headingLine;
            }

            line.Role = LineRole.Heading;
            line.Heading = info;
        }
    }

    /// <summary>Splits „Art. 5. Treść…” into a heading line made of the designation words and a content line.</summary>
    private static (LayoutLine Heading, LayoutLine Content)? Split(LayoutLine line, string designation)
    {
        for (int count = 1; count < line.Words.Count; count++)
        {
            string head = string.Join(' ', line.Words.Take(count).Select(w => w.Text));
            if (LegalUnitPatterns.TryMatch(head, out LegalUnitMatch? match)
                && match.Rest.Length == 0
                && string.Equals(match.Designation, designation, StringComparison.Ordinal))
            {
                return (LineSlicer.Slice(line, line.Words.Take(count).ToList()), LineSlicer.Slice(line, line.Words.Skip(count).ToList()));
            }
        }

        return null;
    }

    private static double DominantSize(List<LayoutWord> words)
    {
        var chars = new SortedDictionary<double, int>();
        foreach (LayoutGlyph glyph in words.SelectMany(w => w.Glyphs))
        {
            double size = RoundHalf(glyph.PointSize);
            chars[size] = chars.GetValueOrDefault(size) + glyph.Text.Length;
        }

        return chars.Count == 0 ? 0 : chars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;
    }

    private static bool IsCaps(string text)
    {
        int letters = 0;
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                if (!char.IsUpper(c))
                {
                    return false;
                }

                letters++;
            }
        }

        return letters >= MinCapsLetters;
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"^(USTAWA|ROZPORZĄDZENIE|OBWIESZCZENIE|ZARZĄDZENIE|UCHWAŁA|POSTANOWIENIE|DECYZJA|KODEKS|REGULAMIN|KOMUNIKAT)(\s|$)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ActType();

    /// <summary>FR-093: the validity line of a title block („Obowiązuje od 01.09.2026 r. do …”).</summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^obowiązuje\s+od\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ValidityLine();

    private static double RoundHalf(double value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;

    private sealed class Entry(LayoutPage page, LayoutLine line, LayoutLine? previous, string text)
    {
        public LayoutPage Page { get; } = page;

        public LayoutLine Line { get; } = line;

        /// <summary>The line directly above on the same page (any role except artifacts).</summary>
        public LayoutLine? Previous { get; } = previous;

        /// <summary>Line text without footnote reference markers.</summary>
        public string Text { get; } = text;

        public double Size { get; set; }

        public bool AllBold { get; set; }

        public bool Enlarged { get; set; }

        public bool BoldSignal { get; set; }

        public bool Caps { get; set; }

        public bool Centered { get; set; }

        public bool Isolated { get; set; }

        public bool Candidate { get; set; }

        public bool Consumed { get; set; }

        /// <summary>Plain text whatever its typography: table-document part, validity line or image caption (spec 002).</summary>
        public bool Plain { get; set; }

        public LegalUnitMatch? Legal { get; set; }
    }

    private sealed class Detected(Entry entry, SectionKind kind)
    {
        public Entry Entry { get; } = entry;

        public SectionKind Kind { get; } = kind;

        public string Text { get; set; } = string.Empty;

        public string? Designation { get; init; }

        public string? Number { get; init; }

        public string? Title { get; set; }

        public bool SplitRest { get; set; }

        public List<Entry> Merged { get; } = [];

        public int Level { get; set; }

        public int Rank { get; set; }
    }
}
