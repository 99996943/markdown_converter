using System.Globalization;
using System.Text;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Joins consecutive lines into paragraphs (FR-032), continues paragraphs across pages (FR-033), resolves
/// line-end hyphenation (FR-012) and builds the inline content with <see cref="PageBreak"/> markers (FR-002a).
/// Lines are consumed in the order they appear in <see cref="LayoutPage.Lines"/> (earlier stages may have
/// reordered them); lines with role <see cref="LineRole.Artifact"/> or <see cref="LineRole.Footnote"/> are skipped,
/// list item and continuation lines are assembled into <see cref="ListBlock"/> trees (FR-052 – FR-054), tables found by
/// table detection are placed at their first line, and lines with
/// any other role end the current paragraph or list and are left to the stage that owns them.
/// </summary>
public sealed class BlockAssemblyStage : IPipelineStage
{
    private const double SizeTolerance = 0.5;
    private const double MaxFirstLineOutdentInFontSizes = 4;
    private const double MinLeadingInFontSizes = 1.2;
    private const double NoteLineGapInFontSizes = 1.8;

    /// <inheritdoc />
    public int Order => StageOrder.BlockAssembly;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new ParagraphBuilder(context.Options.Normalization.HyphenationExceptions.ToArray());
        Paragraph? current = null;
        ListAssembler? list = null;
        var notes = new SideNoteBuffer(builder);
        var emittedTables = new HashSet<int>();
        LayoutLine? lastHeading = null;
        string? currentStep = null;
        double previousColumnLeft = 0;

        foreach (LayoutPage page in context.Pages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (page.Skipped is not null)
            {
                continue;
            }

            List<LayoutLine> consumable = page.Lines
                .Where(l => l.Role is LineRole.Unknown or LineRole.Body)
                .ToList();
            double columnLeft = consumable.Count > 0 ? consumable.Min(l => l.Box.Left) : 0;
            double columnWidth = consumable.Count > 0 ? consumable.Max(l => l.Box.Right) - columnLeft : page.Width;

            foreach (LayoutLine line in page.Lines)
            {
                // Footnote definitions are collected by footnote detection; like artifacts they must not interrupt a
                // paragraph that continues on the next page.
                if (line.Role is LineRole.Artifact or LineRole.Footnote)
                {
                    continue;
                }

                // FR-083: the rest of a section name broken by a page boundary is part of the heading already placed on
                // the previous page; it must not interrupt that section's content.
                if (line.Role == LineRole.Heading && line.Heading is null && InTableDocument(line))
                {
                    continue;
                }

                // FR-034: side notes wait until the block they stand beside is finished.
                if (line.Role == LineRole.SideNote)
                {
                    notes.Add(line, page.Number);
                    continue;
                }

                notes.ReadingAt(page.Number, line.Baseline);

                if (line.Role is LineRole.ListItem or LineRole.ListContinuation)
                {
                    Finish(context, ref current, notes);
                    list ??= new ListAssembler(builder);
                    if (list.TryAdd(line, page.Number))
                    {
                        continue;
                    }

                    // A line whose item is unknown (for example annotated by a custom stage) is kept as ordinary text.
                    FinishList(context, ref list, notes);
                }
                else
                {
                    FinishList(context, ref list, notes);
                }

                // A table (FR-063) is placed at its first line; its other lines, also on following pages, are skipped.
                if (line.Role == LineRole.Table
                    && line.Annotations.TryGetValue(LayoutAnnotations.TableIndex, out string? tableIndex)
                    && int.TryParse(tableIndex, NumberStyles.None, CultureInfo.InvariantCulture, out int table)
                    && table < context.Tables.Count)
                {
                    Finish(context, ref current, notes);
                    if (emittedTables.Add(table))
                    {
                        context.Blocks.Add(context.Tables[table]);
                        notes.Flush(context);
                    }

                    continue;
                }

                // FR-067: the name lines of one step form one bold paragraph with the original name; nothing else joins it.
                if (line.Role == LineRole.StepTitle)
                {
                    string step = StepKey(line);
                    if (current is not null && currentStep == step)
                    {
                        builder.AppendLine(current, Bold(line), page.Number);
                    }
                    else
                    {
                        Finish(context, ref current, notes);
                        current = ParagraphBuilder.Start(Bold(line), page.Number, DominantSize(line));
                        currentStep = step;
                    }

                    current.LastPage = page.Number;
                    continue;
                }

                if (currentStep is not null)
                {
                    Finish(context, ref current, notes);
                    currentStep = null;
                }

                if (line.Role == LineRole.Heading)
                {
                    Finish(context, ref current, notes);
                    lastHeading = line;
                    if (line.Heading is { } heading)
                    {
                        var block = new LayoutBlock(LayoutBlockKind.Heading, new PageRange(page.Number, page.Number))
                        {
                            Heading = heading,
                            HeadingLevel = heading.Level,
                        };
                        block.Lines.Add(line);
                        context.Blocks.Add(block);
                    }

                    continue;
                }

                if (line.Role is not (LineRole.Unknown or LineRole.Body or LineRole.ListItem or LineRole.ListContinuation))
                {
                    Finish(context, ref current, notes);
                    continue;
                }

                double size = DominantSize(line);
                if (current is not null && ContinuesParagraph(context, current, line, size, page, columnLeft, columnWidth, previousColumnLeft))
                {
                    builder.AppendLine(current, line, page.Number);
                }
                else
                {
                    Finish(context, ref current, notes);
                    current = ParagraphBuilder.Start(line, page.Number, size);
                    current.FollowsHeadingOnItsLine = lastHeading is not null
                        && page.Lines.Contains(lastHeading)
                        && Math.Abs(lastHeading.Baseline - line.Baseline) <= SizeTolerance;
                }

                current.LastPage = page.Number;
                current.LastLine = line;
                current.LastSize = size;
            }

            previousColumnLeft = columnLeft;
        }

        Finish(context, ref current, notes);
        FinishList(context, ref list, notes);
        notes.Flush(context, all: true);
    }

    private static string StepKey(LayoutLine line) =>
        (line.Annotations.TryGetValue(LayoutAnnotations.StepIndex, out string? scheme) ? scheme : string.Empty) + ":"
        + (line.Annotations.TryGetValue(LayoutAnnotations.StepNumber, out string? number) ? number : string.Empty);

    private static LayoutLine Bold(LayoutLine line) =>
        line.Words.All(w => w.Style.HasFlag(TextStyle.Bold))
            ? line
            : new LayoutLine(line.Words.Select(w => w with { Style = w.Style | TextStyle.Bold }).ToList(), line.Box, line.Baseline);

    private static void FinishList(PipelineContext context, ref ListAssembler? list, SideNoteBuffer notes)
    {
        if (list is null)
        {
            return;
        }

        ListBlock block = list.Build();
        context.Blocks.Add(new LayoutBlock(LayoutBlockKind.List, block.Pages) { List = block });
        list = null;
        notes.Flush(context);
    }

    private static void Finish(PipelineContext context, ref Paragraph? paragraph, SideNoteBuffer notes)
    {
        if (paragraph is null)
        {
            return;
        }

        var block = new LayoutBlock(LayoutBlockKind.Paragraph, new PageRange(paragraph.FirstPage, paragraph.LastPage));
        foreach (LayoutLine line in paragraph.Lines)
        {
            block.Lines.Add(line);
        }

        foreach (Inline inline in paragraph.Inlines.Complete())
        {
            block.Inlines.Add(inline);
        }

        context.Blocks.Add(block);
        paragraph = null;
        notes.Flush(context);
    }

    /// <summary>
    /// Side-note lines (FR-034) collected while a block is open and flushed as paragraphs after it. Consecutive note lines
    /// of one page closer than <see cref="NoteLineGapInFontSizes"/> font sizes form one note; a note whose next line may
    /// still follow (reading has not passed its last line) waits for the next finished block, so it is never cut in two.
    /// </summary>
    private sealed class SideNoteBuffer(ParagraphBuilder builder)
    {
        private readonly List<(LayoutLine Line, int Page)> _lines = [];
        private int _page;
        private double _baseline;

        public void Add(LayoutLine line, int page) => _lines.Add((line, page));

        /// <summary>Records the position of the main-text line being read.</summary>
        public void ReadingAt(int page, double baseline)
        {
            _page = page;
            _baseline = baseline;
        }

        public void Flush(PipelineContext context, bool all = false)
        {
            var notes = new List<(Paragraph Note, int Lines)>();
            Paragraph? note = null;
            int count = 0;
            foreach ((LayoutLine line, int page) in _lines)
            {
                if (note is not null
                    && page == note.LastPage
                    && line.Baseline - note.LastLine.Baseline is > 0 and var gap
                    && gap <= NoteLineGapInFontSizes * NoteSize(note.LastLine))
                {
                    builder.AppendLine(note, line, page);
                    notes[^1] = (note, ++count);
                    continue;
                }

                note = ParagraphBuilder.Start(line, page, NoteSize(line));
                count = 1;
                notes.Add((note, count));
            }

            if (!all && notes.Count > 0 && MayContinue(notes[^1].Note))
            {
                notes.RemoveAt(notes.Count - 1);
            }

            int emitted = notes.Sum(n => n.Lines);
            foreach ((Paragraph complete, int _) in notes)
            {
                Emit(context, complete);
            }

            _lines.RemoveRange(0, emitted);
        }

        private bool MayContinue(Paragraph note) =>
            note.LastPage == _page
            && _baseline <= note.LastLine.Baseline + (NoteLineGapInFontSizes * NoteSize(note.LastLine));

        private static double NoteSize(LayoutLine line) => DominantSize(line) is > 0 and var size ? size : line.Box.Height;

        private static void Emit(PipelineContext context, Paragraph? note)
        {
            if (note is null)
            {
                return;
            }

            var block = new LayoutBlock(LayoutBlockKind.Paragraph, new PageRange(note.FirstPage, note.LastPage));
            foreach (LayoutLine line in note.Lines)
            {
                block.Lines.Add(line);
            }

            foreach (Inline inline in note.Inlines.Complete())
            {
                block.Inlines.Add(inline);
            }

            context.Blocks.Add(block);
        }
    }

    private static bool ContinuesParagraph(
        PipelineContext context,
        Paragraph paragraph,
        LayoutLine line,
        double size,
        LayoutPage page,
        double columnLeft,
        double columnWidth,
        double previousColumnLeft)
    {
        LayoutLine previous = paragraph.LastLine;
        if (Math.Abs(paragraph.LastSize - size) > SizeTolerance)
        {
            return false;
        }

        if (InTableDocument(previous) && InTableDocument(line) && EndsTableDocumentParagraph(previous, line, size))
        {
            return false;
        }

        double tolerance = context.Options.Lists.IndentTolerance;

        if (page.Number != paragraph.LastPage)
        {
            if (page.Number != paragraph.LastPage + 1 || EndsSentence(previous.Text))
            {
                return false;
            }

            double reference = paragraph.Lines.Count > 1 ? previous.Box.Left : previousColumnLeft;
            return StartsLowercase(line.Text) || Math.Abs(line.Box.Left - reference) <= tolerance;
        }

        double leading = Math.Max(
            context.BodyStyle?.Leading ?? 0,
            MinLeadingInFontSizes * paragraph.LastSize);
        double gap = line.Baseline - previous.Baseline;
        if (gap <= 0 || gap > context.Options.Layout.ParagraphGapFactor * leading)
        {
            return false;
        }

        double indentDelta = line.Box.Left - previous.Box.Left;
        if (indentDelta > tolerance)
        {
            return false;
        }

        // The first line may be indented; text after „Art. 5.” on the designation line may start far to the right (FR-045).
        if (indentDelta < -tolerance
            && !(paragraph.Lines.Count == 1
                && (paragraph.FollowsHeadingOnItsLine || -indentDelta <= MaxFirstLineOutdentInFontSizes * size)))
        {
            return false;
        }

        // On multi-column pages reading order records the column of each line; otherwise the page text block is the column.
        double? annotatedLeft = LayoutAnnotations.GetNumber(previous, LayoutAnnotations.ColumnLeft);
        double? annotatedRight = LayoutAnnotations.GetNumber(previous, LayoutAnnotations.ColumnRight);
        if (annotatedLeft is double left && annotatedRight is double right)
        {
            columnLeft = left;
            columnWidth = right - left;
        }

        if (EndsWithPeriod(previous.Text)
            && previous.Box.Right - columnLeft < context.Options.Layout.ShortLineRatio * columnWidth)
        {
            return false;
        }

        return true;
    }

    private static double DominantSize(LayoutLine line)
    {
        var chars = new SortedDictionary<double, int>();
        foreach (LayoutWord word in line.Words)
        {
            foreach (LayoutGlyph glyph in word.Glyphs)
            {
                double rounded = Math.Round(glyph.PointSize * 2, MidpointRounding.AwayFromZero) / 2;
                chars[rounded] = chars.GetValueOrDefault(rounded) + glyph.Text.Length;
            }
        }

        return chars.Count == 0
            ? 0
            : chars.OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key).First().Key;
    }

    private static string StripClosers(string text)
    {
        string trimmed = text.TrimEnd();
        int end = trimmed.Length;
        while (end > 0 && trimmed[end - 1] is ')' or ']' or '"' or '”' or '»' or '\'' or '’')
        {
            end--;
        }

        return trimmed[..end];
    }

    private static bool InTableDocument(LayoutLine line) => line.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex);

    /// <summary>
    /// R9 (spec 002, FR-085, FR-086): in table-document content a line ends its paragraph when the first word of the next
    /// line — a one-letter word together with the word after it — would have fit before the column's right edge (the
    /// break was intended, the text is ragged-right), or when the line is bold as a whole and the next one is not, or
    /// the other way round.
    /// </summary>
    private static bool EndsTableDocumentParagraph(LayoutLine previous, LayoutLine line, double size)
    {
        if (AllBold(previous) != AllBold(line))
        {
            return true;
        }

        if (LayoutAnnotations.GetNumber(previous, LayoutAnnotations.ColumnRight) is not double right || line.Words.Count == 0)
        {
            return false;
        }

        List<double> gaps = previous.Words.Zip(previous.Words.Skip(1), (a, b) => b.Box.Left - a.Box.Right).Where(g => g > 0).Order().ToList();
        double space = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0.25 * size;
        LayoutWord first = line.Words[0];
        double width = first.Text.Length == 1 && line.Words.Count > 1 ? line.Words[1].Box.Right - first.Box.Left : first.Box.Width;
        return previous.Box.Right + space + width <= right;
    }

    private static bool AllBold(LayoutLine line) => line.Words.Count > 0 && line.Words.All(w => w.Style.HasFlag(TextStyle.Bold));

    private static bool EndsWithPeriod(string text)
    {
        string core = StripClosers(text);
        return core.Length > 0 && core[^1] == '.';
    }

    private static bool EndsSentence(string text)
    {
        string core = StripClosers(text);
        return core.Length > 0 && core[^1] is '.' or '!' or '?' or ':' or ';' or '…';
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

    private sealed class Paragraph(LayoutLine first, int page, double size, InlineAccumulator inlines)
    {
        public List<LayoutLine> Lines { get; } = [first];

        public InlineAccumulator Inlines { get; } = inlines;

        public int FirstPage { get; } = page;

        public int LastPage { get; set; } = page;

        public LayoutLine LastLine { get; set; } = first;

        public double LastSize { get; set; } = size;

        /// <summary>The first line is the remainder of a heading line („Art. 5. Treść…”).</summary>
        public bool FollowsHeadingOnItsLine { get; set; }
    }

    private sealed class ParagraphBuilder(string[] exceptions)
    {
        public static Paragraph Start(LayoutLine line, int page, double size, bool leadingPageBreak = false)
        {
            var inlines = new InlineAccumulator();
            if (leadingPageBreak)
            {
                inlines.AddPageBreak(page);
            }

            foreach (LayoutWord word in line.Words)
            {
                inlines.Add(word, glue: false);
            }

            return new Paragraph(line, page, size, inlines);
        }

        public void AppendLine(Paragraph paragraph, LayoutLine line, int page)
        {
            LayoutLine previous = paragraph.LastLine;
            HyphenJoin join = Hyphenation.Decide(previous.Text, line.Text, exceptions);
            bool glue = join != HyphenJoin.None;
            if (join == HyphenJoin.Remove)
            {
                paragraph.Inlines.TrimLastHyphen();
            }

            bool newPage = page != paragraph.LastPage;
            bool breakAfterFirstWord = newPage && glue;
            if (newPage && !glue)
            {
                paragraph.Inlines.AddPageBreak(page);
            }

            for (int i = 0; i < line.Words.Count; i++)
            {
                LayoutWord word = line.Words[i];
                paragraph.Inlines.Add(word, glue: i == 0 && glue);
                if (i == 0 && breakAfterFirstWord)
                {
                    paragraph.Inlines.AddPageBreak(page);
                }
            }

            paragraph.Lines.Add(line);
            paragraph.LastPage = page;
            paragraph.LastLine = line;
        }
    }

    /// <summary>
    /// Builds one top-level list from the lines annotated by list detection: item lines open items under their parent
    /// (consecutive child items share one nested list), continuation lines extend the owner's text, and common-part
    /// lines form a paragraph placed among the owner's children after its nested list (FR-052 – FR-054). An item or
    /// common part starting on a later page than the previous list line begins with a <see cref="PageBreak"/>.
    /// </summary>
    private sealed class ListAssembler(ParagraphBuilder builder)
    {
        private readonly Dictionary<string, ItemNode> _items = new(StringComparer.Ordinal);
        private readonly List<ItemNode> _top = [];
        private int _lastPage;
        private ItemNode? _commonOwner;
        private Paragraph? _common;

        public bool TryAdd(LayoutLine line, int page)
        {
            bool newPage = _lastPage != 0 && page != _lastPage;
            if (line.Role == LineRole.ListItem)
            {
                if (!AddItem(line, page, newPage))
                {
                    return false;
                }
            }
            else if (!line.Annotations.TryGetValue(LayoutAnnotations.ListOwner, out string? ownerId)
                || !_items.TryGetValue(ownerId, out ItemNode? owner))
            {
                return false;
            }
            else if (line.Annotations.TryGetValue(LayoutAnnotations.ListCommonPart, out string? common) && common == "1")
            {
                if (_common is not null && ReferenceEquals(_commonOwner, owner))
                {
                    builder.AppendLine(_common, line, page);
                }
                else
                {
                    _common = ParagraphBuilder.Start(line, page, 0, newPage);
                    _commonOwner = owner;
                    owner.Children.Add(_common);
                }
            }
            else
            {
                builder.AppendLine(owner.Text, line, page);
            }

            _lastPage = page;
            return true;
        }

        private bool AddItem(LayoutLine line, int page, bool newPage)
        {
            if (line.Words.Count < 2
                || !line.Annotations.TryGetValue(LayoutAnnotations.ListItemId, out string? id)
                || !Enum.TryParse(Annotation(line, LayoutAnnotations.ListKind), out ListLabelKind kind))
            {
                return false;
            }

            LayoutLine content = LineSlicer.Slice(line, line.Words.Skip(1).ToList());
            string label = Annotation(line, LayoutAnnotations.ListLabel) ?? line.Words[0].Text;
            var item = new ItemNode(label, kind, ParagraphBuilder.Start(content, page, 0, newPage));

            string parentId = Annotation(line, LayoutAnnotations.ListParent) ?? string.Empty;
            if (_items.TryGetValue(parentId, out ItemNode? parent))
            {
                if (parent.Children.Count > 0 && parent.Children[^1] is List<ItemNode> siblings)
                {
                    siblings.Add(item);
                }
                else
                {
                    parent.Children.Add(new List<ItemNode> { item });
                }
            }
            else
            {
                _top.Add(item);
            }

            _items[id] = item;
            _common = null;
            _commonOwner = null;
            return true;
        }

        public ListBlock Build() => BuildList(_top);

        private static string? Annotation(LayoutLine line, string key) =>
            line.Annotations.TryGetValue(key, out string? value) ? value : null;

        private static ListBlock BuildList(List<ItemNode> items)
        {
            (ListItem Item, PageRange Pages)[] built = items.Select(BuildItem).ToArray();
            return new ListBlock(
                new PageRange(built.Min(b => b.Pages.First), built.Max(b => b.Pages.Last)),
                built.Select(b => b.Item).ToArray());
        }

        private static (ListItem Item, PageRange Pages) BuildItem(ItemNode node)
        {
            var children = new List<ContentBlock>();
            foreach (object child in node.Children)
            {
                children.Add(child switch
                {
                    List<ItemNode> nested => BuildList(nested),
                    Paragraph paragraph => new ParagraphBlock(
                        new PageRange(paragraph.FirstPage, paragraph.LastPage),
                        paragraph.Inlines.Complete().ToArray()),
                    _ => throw new InvalidOperationException("Nieznany element listy."),
                });
            }

            int last = children.Select(c => c.Pages.Last).Append(node.Text.LastPage).Max();
            return (
                new ListItem(node.Label, node.Kind, node.Text.Inlines.Complete().ToArray(), children),
                new PageRange(node.Text.FirstPage, last));
        }

        private sealed class ItemNode(string label, ListLabelKind kind, Paragraph text)
        {
            public string Label { get; } = label;

            public ListLabelKind Kind { get; } = kind;

            public Paragraph Text { get; } = text;

            /// <summary>Nested item lists (<c>List&lt;ItemNode&gt;</c>) and common-part paragraphs in source order.</summary>
            public List<object> Children { get; } = [];
        }
    }

    /// <summary>
    /// Builds text runs; a separating space belongs to the run before it and never touches a page break. A footnote
    /// reference is glued to the preceding word; the space after it opens the next run.
    /// </summary>
    private sealed class InlineAccumulator
    {
        private readonly List<Inline> _inlines = [];
        private readonly StringBuilder _text = new();
        private TextStyle _style;
        private bool _hasRun;
        private bool _spaceAfterReference;

        public void Add(LayoutWord word, bool glue)
        {
            if (word.FootnoteId is int id)
            {
                FlushRun();
                _inlines.Add(new FootnoteRef(id));
                _spaceAfterReference = true;
                return;
            }

            if (_spaceAfterReference && !_hasRun)
            {
                _spaceAfterReference = false;
                _text.Clear().Append(glue ? string.Empty : " ").Append(word.Text);
                _style = word.Style;
                _hasRun = true;
                return;
            }

            AddWord(word.Text, word.Style, glue);
        }

        public void AddWord(string word, TextStyle style, bool glue)
        {
            if (!_hasRun)
            {
                _text.Clear().Append(word);
                _style = style;
                _hasRun = true;
                return;
            }

            if (!glue)
            {
                _text.Append(' ');
            }

            if (style == _style)
            {
                _text.Append(word);
                return;
            }

            FlushRun();
            _text.Append(word);
            _style = style;
            _hasRun = true;
        }

        public void AddPageBreak(int page)
        {
            FlushRun();
            _spaceAfterReference = false;
            _inlines.Add(new PageBreak(page));
        }

        public void TrimLastHyphen()
        {
            if (_hasRun && _text.Length > 0 && _text[^1] == '-')
            {
                _text.Length--;
            }
        }

        public List<Inline> Complete()
        {
            FlushRun();
            return _inlines;
        }

        private void FlushRun()
        {
            if (_hasRun && _text.Length > 0)
            {
                _inlines.Add(new TextRun(_text.ToString(), _style));
            }

            _text.Clear();
            _hasRun = false;
        }
    }
}
