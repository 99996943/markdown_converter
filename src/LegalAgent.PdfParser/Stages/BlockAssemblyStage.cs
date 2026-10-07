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
/// reordered them); lines with role <see cref="LineRole.Artifact"/> are skipped and lines with any role other than
/// <see cref="LineRole.Unknown"/> or <see cref="LineRole.Body"/> end the current paragraph and are left to the
/// stage that owns them.
/// </summary>
public sealed class BlockAssemblyStage : IPipelineStage
{
    private const double SizeTolerance = 0.5;
    private const double MaxFirstLineOutdentInFontSizes = 4;
    private const double MinLeadingInFontSizes = 1.2;

    /// <inheritdoc />
    public int Order => StageOrder.BlockAssembly;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new ParagraphBuilder(context.Options.Normalization.HyphenationExceptions.ToArray());
        Paragraph? current = null;
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
                if (line.Role == LineRole.Artifact)
                {
                    continue;
                }

                if (line.Role is not (LineRole.Unknown or LineRole.Body))
                {
                    Finish(context, ref current);
                    continue;
                }

                double size = DominantSize(line);
                if (current is not null && ContinuesParagraph(context, current, line, size, page, columnLeft, columnWidth, previousColumnLeft))
                {
                    builder.AppendLine(current, line, page.Number);
                }
                else
                {
                    Finish(context, ref current);
                    current = ParagraphBuilder.Start(line, page.Number, size);
                }

                current.LastPage = page.Number;
                current.LastLine = line;
                current.LastSize = size;
            }

            previousColumnLeft = columnLeft;
        }

        Finish(context, ref current);
    }

    private static void Finish(PipelineContext context, ref Paragraph? paragraph)
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

        if (indentDelta < -tolerance
            && !(paragraph.Lines.Count == 1 && -indentDelta <= MaxFirstLineOutdentInFontSizes * size))
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
    }

    private sealed class ParagraphBuilder(string[] exceptions)
    {
        public static Paragraph Start(LayoutLine line, int page, double size)
        {
            var inlines = new InlineAccumulator();
            foreach (LayoutWord word in line.Words)
            {
                inlines.AddWord(word.Text, word.Style, glue: false);
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
                paragraph.Inlines.AddWord(word.Text, word.Style, glue: i == 0 && glue);
                if (i == 0 && breakAfterFirstWord)
                {
                    paragraph.Inlines.AddPageBreak(page);
                }
            }

            paragraph.Lines.Add(line);
        }
    }

    /// <summary>Builds text runs; a separating space belongs to the run before it and never touches a page break.</summary>
    private sealed class InlineAccumulator
    {
        private readonly List<Inline> _inlines = [];
        private readonly StringBuilder _text = new();
        private TextStyle _style;
        private bool _hasRun;

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
