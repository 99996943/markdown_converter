using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Detects list items and their continuations (FR-050 – FR-054) and records the list tree in line annotations
/// (<see cref="LayoutAnnotations.ListItemId"/>, <see cref="LayoutAnnotations.ListParent"/>,
/// <see cref="LayoutAnnotations.ListOwner"/>, <see cref="LayoutAnnotations.ListCommonPart"/>) for block assembly.
/// </summary>
/// <remarks>
/// Lines are walked in document order keeping a stack of open items. A labelled line opens an item: it is a child of
/// the open item when its label is indented further or — for legal labels — when it is lower in the hierarchy
/// ustęp „1.” → punkt „1)” → litera „a)” → tiret „–” (ISAP prints points of an ustęp left of the ustęp label).
/// An unlabelled line continues the innermost item when it is indented beyond that item's label (hanging indent, also
/// at the top of the next page), or when it wraps an ustęp printed with a first-line indent back to the margin. A line
/// aligned with the text of an outer item, or a „–” line aligned with the labels of a finished enumeration, is the
/// common part of the enclosing item (FR-054) — or, at the top level, an ordinary paragraph after the list.
/// „N.” is a label only in a sequence of consecutive numbers at one indent or as the ustęp of an article (FR-051), and
/// lines printed like headings (all bold or enlarged, set off by a gap — FR-041) are left to heading detection. An
/// article line „Art. 5. 1. Treść…” is split into the designation (left for heading detection) and the first ustęp.
/// </remarks>
public sealed class ListDetectionStage : IPipelineStage
{
    private const double SizeTolerance = 0.5;

    /// <inheritdoc />
    public int Order => StageOrder.ListDetection;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Options.Lists.Enabled)
        {
            return;
        }

        var articleUsteps = new HashSet<LayoutLine>(ReferenceEqualityComparer.Instance);
        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            SplitArticleUsteps(page, articleUsteps);
        }

        List<Entry> entries = Collect(context, articleUsteps);
        KeepLabelledBoldTextInLists(entries, context);
        AcceptArabicDotSequences(entries, context.Options.Lists.IndentTolerance);
        new Walker(context).Run(entries);
    }

    /// <summary>„Art. 5. 1. Treść…” → „Art. 5.” and „1. Treść…” (the first ustęp shares the article line).</summary>
    private static void SplitArticleUsteps(LayoutPage page, HashSet<LayoutLine> articleUsteps)
    {
        for (int i = 0; i < page.Lines.Count; i++)
        {
            LayoutLine line = page.Lines[i];
            if (line.Role != LineRole.Unknown
                || !LegalUnitPatterns.TryMatch(line.Text, out LegalUnitMatch? unit)
                || unit.Kind is not (SectionKind.Article or SectionKind.Paragraph)
                || !ListLabelPatterns.TryMatch(unit.Rest, out ListLabelMatch? label)
                || label.Kind != ListLabelKind.ArabicDot)
            {
                continue;
            }

            for (int count = 1; count < line.Words.Count; count++)
            {
                string head = string.Join(' ', line.Words.Take(count).Select(w => w.Text));
                if (LegalUnitPatterns.TryMatch(head, out LegalUnitMatch? match)
                    && match.Rest.Length == 0
                    && string.Equals(match.Designation, unit.Designation, StringComparison.Ordinal))
                {
                    LayoutLine content = LineSlicer.Slice(line, line.Words.Skip(count).ToList());
                    page.Lines[i] = LineSlicer.Slice(line, line.Words.Take(count).ToList());
                    page.Lines.Insert(i + 1, content);
                    articleUsteps.Add(content);
                    i++;
                    break;
                }
            }
        }
    }

    private static List<Entry> Collect(PipelineContext context, HashSet<LayoutLine> articleUsteps)
    {
        double bodySize = context.BodyStyle?.FontSize ?? 0;
        double sizeRatio = context.Options.Headings.SizeRatio;
        double gapFactor = context.Options.Headings.GapFactor;
        var entries = new List<Entry>();
        foreach (LayoutPage page in context.Pages)
        {
            if (page.Skipped is not null)
            {
                continue;
            }

            List<LayoutLine> body = page.Lines.Where(l => l.Role == LineRole.Unknown).ToList();
            double columnLeft = body.Count > 0 ? body.Min(l => l.Box.Left) : 0;
            LayoutLine? previous = null;
            foreach (LayoutLine line in page.Lines)
            {
                // Spec 002, FR-083/FR-085: the rest of a section name broken by a page boundary belongs to the heading on
                // the previous page and does not end the list of that section.
                if (line.Role is LineRole.Artifact or LineRole.Footnote or LineRole.SideNote || line.Words.Count == 0
                    || (line.Role == LineRole.Heading && line.Heading is null && line.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex)))
                {
                    continue;
                }

                var entry = new Entry(page, line, columnLeft);
                entries.Add(entry);
                LayoutLine? above = previous;
                previous = line;
                if (line.Role != LineRole.Unknown)
                {
                    continue;
                }

                entry.LegalUnit = LegalUnitPatterns.TryMatch(line.Text, out _);
                double leading = context.BodyStyle?.Leading is > 0 and double l ? l : 1.2 * line.Box.Height;
                bool isolated = above is null || line.Baseline - above.Baseline > gapFactor * leading;
                entry.HeadingLike = isolated && IsHeadingLike(line, bodySize, sizeRatio);
                entry.TitleStyle = entry.HeadingLike && IsTitleStyle(line, bodySize);
                entry.FirstOnPage = above is null;
                if (!entry.LegalUnit
                    && ListLabelPatterns.TryMatch(line.Text, out ListLabelMatch? label)
                    && string.Equals(label.Label, line.Words[0].Text, StringComparison.Ordinal))
                {
                    entry.Label = label;
                    entry.ArticleUstep = articleUsteps.Contains(line);
                    entry.Accepted = label.Kind != ListLabelKind.ArabicDot || entry.ArticleUstep;
                }
                else if (!entry.LegalUnit && IsFontBullet(line))
                {
                    entry.Label = new ListLabelMatch("o", ListLabelKind.Bullet, null, string.Join(' ', line.Words.Skip(1).Select(w => w.Text)));
                    entry.Accepted = true;
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// R10: word processors set the sub-bullet „o” in another font (Courier New) than the text; the preposition „o”
    /// is set in the text font and stays a word.
    /// </summary>
    private static bool IsFontBullet(LayoutLine line)
    {
        if (line.Words.Count < 2 || !string.Equals(line.Words[0].Text, "o", StringComparison.Ordinal))
        {
            return false;
        }

        string? bullet = FamilyOf(line.Words[0]);
        string? text = FamilyOf(line.Words[1]);
        return bullet is not null && text is not null && !string.Equals(bullet, text, StringComparison.Ordinal);
    }

    private static string? FamilyOf(LayoutWord word) =>
        word.Glyphs.Count > 0 ? FontFamily.Of(word.Glyphs[0].FontName) : null;

    /// <summary>
    /// A bold passage is not a run of headings: points, letters, tirets and bullets never number headings, and a
    /// labelled line whose sentence runs on into the next line (starting lowercase) is a paragraph of a list — unless it is
    /// set as a title (bold and larger than the body text) wrapped onto a second line.
    /// </summary>
    private static void KeepLabelledBoldTextInLists(List<Entry> entries, PipelineContext context)
    {
        double gapFactor = context.Options.Layout.ParagraphGapFactor;
        double bodySize = context.BodyStyle?.FontSize ?? 0;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (!entry.HeadingLike || entry.Label is not { } label)
            {
                continue;
            }

            Entry? next = i + 1 < entries.Count && entries[i + 1].Page == entry.Page ? entries[i + 1] : null;
            double leading = context.BodyStyle?.Leading is > 0 and double l ? l : 1.2 * entry.Line.Box.Height;
            bool runsOn = next is not null
                && !IsTitleStyle(entry.Line, bodySize)
                && next.Label is null
                && next.Line.Baseline - entry.Line.Baseline <= gapFactor * leading
                && StartsLowercase(next.Line.Text);
            if (label.Kind is ListLabelKind.ArabicParen or ListLabelKind.LetterParen or ListLabelKind.Dash or ListLabelKind.Bullet
                || runsOn)
            {
                entry.HeadingLike = false;
            }
        }
    }

    /// <summary>Bold and larger than the body text: a title, even when it wraps (a passage merely set larger is not).</summary>
    private static bool IsTitleStyle(LayoutLine line, double bodySize)
    {
        List<LayoutGlyph> glyphs = line.Words.SelectMany(w => w.Glyphs).ToList();
        return bodySize > 0
            && glyphs.Count > 0
            && line.Words.All(w => w.Style.HasFlag(TextStyle.Bold))
            && glyphs.Average(g => g.PointSize) > bodySize + SizeTolerance;
    }

    private static bool StartsLowercase(string text)
    {
        foreach (char c in text)
        {
            if (char.IsLetter(c))
            {
                return char.IsLower(c);
            }
        }

        return false;
    }

    private static bool IsHeadingLike(LayoutLine line, double bodySize, double sizeRatio)
    {
        if (line.Words.All(w => w.Style.HasFlag(TextStyle.Bold)))
        {
            return true;
        }

        int chars = 0;
        double weighted = 0;
        foreach (LayoutGlyph glyph in line.Words.SelectMany(w => w.Glyphs))
        {
            chars += glyph.Text.Length;
            weighted += glyph.PointSize * glyph.Text.Length;
        }

        return bodySize > 0 && chars > 0 && weighted / chars >= sizeRatio * bodySize;
    }

    /// <summary>
    /// FR-051: „N.” is a label when the nearest „N.” candidate before or after it (with no legal unit line in between,
    /// other than the article holding the first ustęp) continues the numbering at the same indent.
    /// </summary>
    private static void AcceptArabicDotSequences(List<Entry> entries, double tolerance)
    {
        List<int> candidates = [];
        var segmentOf = new Dictionary<int, int>();
        int segment = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.LegalUnit)
            {
                segment++;
            }

            if (e.Label?.Kind == ListLabelKind.ArabicDot)
            {
                candidates.Add(i);
                segmentOf[i] = segment;
            }
        }

        bool Follows(Entry previous, Entry next) =>
            NextInSequence(previous.Label!, next.Label!)
            && (previous.ArticleUstep || Math.Abs(previous.LabelX - next.LabelX) <= tolerance);

        for (int k = 0; k < candidates.Count; k++)
        {
            Entry e = entries[candidates[k]];
            if (e.Accepted)
            {
                continue;
            }

            bool before = k > 0 && segmentOf[candidates[k - 1]] == segmentOf[candidates[k]] && Follows(entries[candidates[k - 1]], e);
            bool after = k + 1 < candidates.Count && segmentOf[candidates[k + 1]] == segmentOf[candidates[k]] && Follows(e, entries[candidates[k + 1]]);
            e.Accepted = before || after;
        }
    }

    /// <summary>
    /// <paramref name="next"/> continues the numbering of <paramref name="previous"/>: the next number, or the same number
    /// when one of them is bracketed (repealed „[2.” followed by the future wording „&lt;2.” or an added „&lt;2a.”).
    /// </summary>
    private static bool NextInSequence(ListLabelMatch previous, ListLabelMatch next) =>
        previous.Kind == next.Kind
        && previous.Ordinal is int p
        && next.Ordinal is int n
        && (n == p + 1 || (n == p && (Bracketed(previous.Label) || Bracketed(next.Label))));

    private static bool Bracketed(string label) => label.Length > 1 && label[0] is '[' or '<';

    private static int? Rank(ListLabelKind kind) => kind switch
    {
        ListLabelKind.ArabicDot => 1,
        ListLabelKind.ArabicParen => 2,
        ListLabelKind.LetterParen => 3,
        ListLabelKind.Dash => 4,
        _ => null,
    };

    private static bool EndsWithColon(LayoutLine line) => line.Text.TrimEnd().EndsWith(':');

    /// <summary>Walks the entries keeping the stack of open items.</summary>
    private sealed class Walker(PipelineContext context)
    {
        private readonly double _tolerance = context.Options.Lists.IndentTolerance;
        private readonly double _gapFactor = context.Options.Layout.ParagraphGapFactor;
        private readonly List<OpenItem> _stack = [];
        private int _nextId = 1;
        private Entry? _previous;
        private OpenItem? _lastOwner;
        private OpenItem? _commonOwner;

        public void Run(List<Entry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                Entry entry = entries[i];
                Visit(entry, i + 1 < entries.Count ? entries[i + 1] : null);
                _previous = entry;
            }
        }

        private OpenItem? Top => _stack.Count > 0 ? _stack[^1] : null;

        private void Visit(Entry entry, Entry? next)
        {
            LayoutLine line = entry.Line;
            bool headingLike = entry.HeadingLike && !ContinuesOpenList(entry);
            if (line.Role != LineRole.Unknown || entry.LegalUnit || headingLike || !GapAllowsContinuation(entry))
            {
                Reset();
                if (line.Role != LineRole.Unknown || entry.LegalUnit || headingLike)
                {
                    return;
                }
            }

            if (entry.Label is { } label && entry.Accepted)
            {
                if (label.Kind == ListLabelKind.Dash)
                {
                    VisitDash(entry, next);
                }
                else
                {
                    OpenItem(entry);
                }

                return;
            }

            VisitUnlabelled(entry);
        }

        /// <summary>
        /// A line printed like a heading still belongs to an open list when it continues the numbering of an open item
        /// (ISAP prints future wording in bold) or, unlabelled, when it is the first line of a page.
        /// </summary>
        private bool ContinuesOpenList(Entry entry)
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            if (entry.Label is not { } label)
            {
                return entry.FirstOnPage;
            }

            // A numbered section heading set larger than the body („3. Odpowiedzialności” after items „1.”, „2.”) does
            // not continue the list even when its number would.
            if (entry.TitleStyle)
            {
                return false;
            }

            bool continues = _stack.Any(o => Math.Abs(o.LabelX - entry.LabelX) <= _tolerance && NextInSequence(o.Label, label));
            entry.Accepted |= continues;
            return continues;
        }

        private void VisitDash(Entry entry, Entry? next)
        {
            OpenItem? top = Top;
            double x = entry.LabelX;
            if (top is null)
            {
                bool introduced = _previous is not null && EndsWithColon(_previous.Line);
                bool sequence = next?.Label?.Kind == ListLabelKind.Dash && Math.Abs(next.LabelX - x) <= _tolerance;
                if (introduced || sequence)
                {
                    OpenItem(entry);
                }

                return;
            }

            if (top.Kind == ListLabelKind.Dash && Math.Abs(x - top.LabelX) <= _tolerance)
            {
                OpenItem(entry);
            }
            else if (x > top.LabelX + _tolerance)
            {
                if (_previous is not null && EndsWithColon(_previous.Line))
                {
                    OpenItem(entry);
                }
                else
                {
                    Continue(entry, top, common: false);
                }
            }
            else
            {
                CloseEnumerationAt(entry);
            }
        }

        /// <summary>„– …” aligned with the labels of an enumeration closes it; the line is the common part of its parent.</summary>
        private void CloseEnumerationAt(Entry entry)
        {
            int level = _stack.FindLastIndex(o => Math.Abs(o.LabelX - entry.LabelX) <= _tolerance);
            if (level < 0)
            {
                Reset();
                return;
            }

            _stack.RemoveRange(level, _stack.Count - level);
            StartCommonPart(entry);
        }

        private void VisitUnlabelled(Entry entry)
        {
            OpenItem? top = Top;
            if (top is null)
            {
                return;
            }

            double x = entry.Line.Box.Left;
            if (_commonOwner is not null)
            {
                Continue(entry, _commonOwner, common: true);
                return;
            }

            if (x > top.LabelX + _tolerance)
            {
                top.ContinuationX ??= x;
                Continue(entry, top, common: false);
                return;
            }

            // ISAP ustęp: first line indented, wrapped lines back at the margin.
            if (top.Kind == ListLabelKind.ArabicDot
                && top.LabelX > entry.ColumnLeft + _tolerance
                && x < top.LabelX - _tolerance
                && ReferenceEquals(_lastOwner, top))
            {
                Continue(entry, top, common: false);
                return;
            }

            int owner = _stack.FindLastIndex(o => Math.Abs((o.ContinuationX ?? o.TextX) - x) <= _tolerance);
            if (owner < 0)
            {
                Reset();
                return;
            }

            _stack.RemoveRange(owner + 1, _stack.Count - owner - 1);
            StartCommonPart(entry);
        }

        private void StartCommonPart(Entry entry)
        {
            OpenItem? owner = Top;
            if (owner is null)
            {
                Reset();
                return;
            }

            _commonOwner = owner;
            Continue(entry, owner, common: true);
        }

        private void OpenItem(Entry entry)
        {
            ListLabelMatch label = entry.Label!;
            var item = new OpenItem(
                _nextId++,
                label,
                entry.LabelX,
                entry.Line.Words.Count > 1 ? entry.Line.Words[1].Box.Left : entry.Line.Box.Right);

            while (Top is { } top && !IsParent(top, item))
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            LayoutLine line = entry.Line;
            line.Role = LineRole.ListItem;
            line.Annotations[LayoutAnnotations.ListLabel] = label.Label;
            line.Annotations[LayoutAnnotations.ListKind] = label.Kind.ToString();
            line.Annotations[LayoutAnnotations.ListItemId] = item.Id.ToString(CultureInfo.InvariantCulture);
            line.Annotations[LayoutAnnotations.ListParent] = Top?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            _stack.Add(item);
            _lastOwner = item;
            _commonOwner = null;
        }

        private bool IsParent(OpenItem open, OpenItem item) =>
            Rank(open.Kind) is int openRank && Rank(item.Kind) is int itemRank
                ? itemRank > openRank
                : item.LabelX > open.LabelX + _tolerance;

        private void Continue(Entry entry, OpenItem owner, bool common)
        {
            LayoutLine line = entry.Line;
            line.Role = LineRole.ListContinuation;
            line.Annotations[LayoutAnnotations.ListOwner] = owner.Id.ToString(CultureInfo.InvariantCulture);
            if (common)
            {
                line.Annotations[LayoutAnnotations.ListCommonPart] = "1";
            }

            _lastOwner = owner;
        }

        private bool GapAllowsContinuation(Entry entry)
        {
            if (_previous is null)
            {
                return true;
            }

            if (entry.Page != _previous.Page)
            {
                return entry.Page.Number == _previous.Page.Number + 1;
            }

            double leading = context.BodyStyle?.Leading is > 0 and double l ? l : 1.2 * _previous.Line.Box.Height;
            double gap = entry.Line.Baseline - _previous.Line.Baseline;
            return gap > 0 && gap <= _gapFactor * leading;
        }

        private void Reset()
        {
            _stack.Clear();
            _lastOwner = null;
            _commonOwner = null;
        }
    }

    private sealed class Entry(LayoutPage page, LayoutLine line, double columnLeft)
    {
        public LayoutPage Page { get; } = page;

        public LayoutLine Line { get; } = line;

        public double ColumnLeft { get; } = columnLeft;

        public double LabelX => Line.Box.Left;

        public bool LegalUnit { get; set; }

        public bool HeadingLike { get; set; }

        /// <summary>Isolated, bold and larger than the body text: a numbered section heading, not the next list item.</summary>
        public bool TitleStyle { get; set; }

        public bool FirstOnPage { get; set; }

        public ListLabelMatch? Label { get; set; }

        public bool ArticleUstep { get; set; }

        public bool Accepted { get; set; }
    }

    private sealed class OpenItem(int id, ListLabelMatch label, double labelX, double textX)
    {
        public int Id { get; } = id;

        public ListLabelMatch Label { get; } = label;

        public ListLabelKind Kind => Label.Kind;

        public double LabelX { get; } = labelX;

        public double TextX { get; } = textX;

        public double? ContinuationX { get; set; }
    }
}
