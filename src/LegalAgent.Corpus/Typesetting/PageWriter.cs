using System.Globalization;
using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Pdf;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// Page and column flow of one typesetting pass (the generalised <c>Flow</c> of the test banking generator): body
/// lines in columns, footnotes at the bottom of the page where their marker stands, running header and footer drawn
/// when a page ends, and recording of the reference truth, element pages and words per block.
/// </summary>
internal sealed class PageWriter
{
    private const double FootnoteSeparatorGap = 8;
    private const double FootnoteAreaAboveFooter = 22;

    private readonly LayoutStyle _style;
    private readonly FrontMatter _front;
    private readonly int _totalPages;
    private readonly List<(int Number, List<SetLine> Lines)> _pageFootnotes = [];
    private readonly IReadOnlyDictionary<int, IReadOnlyList<Inline>> _footnotes;
    private readonly HashSet<int> _placedFootnotes = [];
    private double _footnoteHeight;
    private double _columnTop;
    private double _maxYOfPreviousColumns;
    private bool _pageHasContent;
    private bool _marginsOnPage = true;
    private Element? _current;

    public PageWriter(LayoutStyle style, FrontMatter front, IReadOnlyDictionary<int, IReadOnlyList<Inline>> footnotes, int totalPages)
    {
        _style = style;
        _front = front;
        _footnotes = footnotes;
        _totalPages = totalPages;
        Builder = new SyntheticPdfBuilder().Title(front.Title);
    }

    public SyntheticPdfBuilder Builder { get; }

    public DocumentTruth Truth { get; } = new();

    public Dictionary<string, PageSpan> ElementPages { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> BlockWords { get; } = new(StringComparer.Ordinal);

    public int Page { get; private set; }

    /// <summary>Baseline of the next body line.</summary>
    public double Y { get; set; }

    public int Column { get; private set; }

    public LayoutStyle Style => _style;

    /// <summary>Whether the last element drawn was a list item.</summary>
    public bool AfterListItem { get; set; }

    public double ColumnWidth => (_style.Right - _style.Left - (_style.ColumnGap * (Columns - 1))) / Columns;

    public double ColumnLeft => _style.Left + (Column * (ColumnWidth + _style.ColumnGap));

    /// <summary>Number of columns of the current page region (1 above the columns, e.g. the title).</summary>
    public int Columns { get; private set; } = 1;

    /// <summary>Lowest baseline allowed for body text on this page (footnotes reserve space below it).</summary>
    public double Limit => Math.Min(_style.Bottom, FootnoteTop(_footnoteHeight) - _style.Leading);

    public bool AtTopOfColumn => Y <= _columnTop + 0.01;

    /// <summary>Starts the first or a new page.</summary>
    public void NewPage(bool margins = true)
    {
        if (Page > 0)
        {
            EndPage();
        }

        Builder.Page(_style.PageWidth, _style.PageHeight);
        Page++;
        _marginsOnPage = margins;
        _footnoteHeight = 0;
        _pageFootnotes.Clear();
        _pageHasContent = false;
        Column = 0;
        _maxYOfPreviousColumns = 0;
        Y = _style.Top;
        _columnTop = Y;
    }

    /// <summary>Switches to <paramref name="columns"/> columns starting at the current position.</summary>
    public void StartColumns(int columns)
    {
        Columns = columns;
        Column = 0;
        _columnTop = Y;
        _maxYOfPreviousColumns = 0;
    }

    /// <summary>Continues in the next column, or on a new page when the last column is full.</summary>
    public void Break()
    {
        if (Column + 1 < Columns)
        {
            _maxYOfPreviousColumns = Math.Max(_maxYOfPreviousColumns, Y);
            Column++;
            Y = _columnTop;
            return;
        }

        int columns = Columns;
        NewPage();
        StartColumns(columns);
    }

    /// <summary>Breaks unless the remaining space holds <paramref name="height"/>.</summary>
    public void Need(double height)
    {
        if (!AtTopOfColumn && Y + height - _style.Leading > Limit)
        {
            Break();
        }
    }

    /// <summary>Sets the element whose lines are drawn next (page tracking, block words).</summary>
    public void Begin(Element element) => _current = element;

    /// <summary>
    /// Makes room for one line of <paramref name="leading"/> that carries the footnote markers of <paramref name="tokens"/>:
    /// breaks the column when the line (and its footnotes) do not fit. Returns the baseline to draw at.
    /// </summary>
    public double Place(IReadOnlyList<Token> tokens, double leading)
    {
        var numbers = tokens.Where(t => t.Footnote > 0 && !_placedFootnotes.Contains(t.Footnote)).Select(t => t.Footnote).Distinct().ToList();
        for (int attempt = 0; ; attempt++)
        {
            double extra = numbers.Sum(n => FootnoteBlockHeight(n));
            if (extra > 0 && _pageFootnotes.Count == 0)
            {
                extra += FootnoteSeparatorGap;
            }

            double limit = Math.Min(_style.Bottom, FootnoteTop(_footnoteHeight + extra) - _style.Leading);
            bool fits = Y <= limit && (extra == 0 || _maxYOfPreviousColumns <= limit);
            if (fits || (attempt > 0 && AtTopOfColumn && Column == 0))
            {
                foreach (int n in numbers)
                {
                    _pageFootnotes.Add((n, FootnoteLines(n)));
                    _placedFootnotes.Add(n);
                }

                _footnoteHeight += extra;
                double y = Y;
                Y += leading;
                _pageHasContent = true;
                Track();
                return y;
            }

            if (attempt > 3)
            {
                throw new InvalidOperationException("A line with footnotes does not fit on an empty page.");
            }

            Break();
        }
    }

    /// <summary>Draws a set line at (<paramref name="x"/>, <paramref name="y"/>) and records its words.</summary>
    public void Draw(SetLine line, double x, double y, double size, bool record = true)
    {
        double space = TextMeasure.Space(size);
        int i = 0;
        IReadOnlyList<Token> tokens = line.Tokens;
        while (i < tokens.Count)
        {
            int j = i + 1;
            while (j < tokens.Count && tokens[j].Style == tokens[i].Style)
            {
                j++;
            }

            var run = new System.Text.StringBuilder();
            double width = 0;
            for (int k = i; k < j; k++)
            {
                if (k > i && !tokens[k].Glued)
                {
                    run.Append(' ');
                    width += space;
                }

                run.Append(tokens[k].Text);
                width += TextMeasure.Width(tokens[k].Text, size, tokens[k].Style);
            }

            InlineStyle style = tokens[i].Style;
            Builder.Text(x, y, run.ToString(), size, bold: style == InlineStyle.Bold, italic: style == InlineStyle.Italic);
            x += width;
            if (j < tokens.Count && !tokens[j].Glued)
            {
                x += space;
            }

            i = j;
        }

        if (record)
        {
            Words(line.Text);
        }
    }

    /// <summary>Draws plain text (labels, cover lines) and records its words.</summary>
    public void Text(double x, double y, string text, double size, bool bold = false, bool italic = false, bool record = true)
    {
        Builder.Text(x, y, text, size, bold, italic);
        if (record)
        {
            Words(text);
        }
    }

    /// <summary>Records printed words for the truth and the current block.</summary>
    public void Words(string text)
    {
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Truth.Words.Add(word);
            if (_current?.BlockId is { } block)
            {
                BlockWords[block] = BlockWords.GetValueOrDefault(block) + 1;
            }
        }
    }

    /// <summary>Marks the current element as printed on the current page.</summary>
    public void Track()
    {
        if (_current is { Id.Length: > 0 } e)
        {
            ElementPages[e.Id] = ElementPages.TryGetValue(e.Id, out PageSpan span) ? span with { Last = Page } : new PageSpan(Page, Page);
        }
    }

    /// <summary>Ends the last page.</summary>
    public void Finish() => EndPage();

    private void EndPage()
    {
        DrawFootnotes();
        if (_marginsOnPage)
        {
            string number = Page.ToString(CultureInfo.InvariantCulture);
            string total = _totalPages.ToString(CultureInfo.InvariantCulture);
            string Fill(string format) => format
                .Replace("{tytul}", _front.Title, StringComparison.Ordinal)
                .Replace("{bank}", _front.Bank, StringComparison.Ordinal)
                .Replace("{oznaczenie}", _front.Designation, StringComparison.Ordinal)
                .Replace("{n}", number, StringComparison.Ordinal)
                .Replace("{N}", total, StringComparison.Ordinal);

            if (_style.HeaderFormat is { } header && Page > 1)
            {
                Margin(_style.Left, _style.HeaderBaseline, Fill(header));
            }

            if (_style.FooterLeftFormat is { } left)
            {
                Margin(_style.Left, _style.FooterBaseline, Fill(left));
            }

            if (_style.FooterRightFormat is { } right)
            {
                string text = Fill(right);
                Margin(_style.Right - TextMeasure.Width(text, _style.MarginSize), _style.FooterBaseline, text);
            }
        }

        _ = _pageHasContent;
    }

    private void Margin(double x, double y, string text)
    {
        Builder.Text(x, y, text, _style.MarginSize);
        Truth.Artifacts.Add(text);
    }

    private void DrawFootnotes()
    {
        if (_pageFootnotes.Count == 0)
        {
            return;
        }

        double y = FootnoteTop(_footnoteHeight) + FootnoteSeparatorGap + _style.FootnoteSize;
        Builder.HLine(_style.Left, _style.Left + 120, y - _style.FootnoteSize - 3, 0.5);
        Element? saved = _current;
        _current = null;
        foreach ((int _, List<SetLine> lines) in _pageFootnotes)
        {
            foreach (SetLine line in lines)
            {
                Draw(line, _style.Left, y, _style.FootnoteSize);
                y += _style.FootnoteLeading;
            }
        }

        _current = saved;
    }

    private double FootnoteTop(double height) => _style.FooterBaseline - FootnoteAreaAboveFooter - height;

    private double FootnoteBlockHeight(int number) => FootnoteLines(number).Count * _style.FootnoteLeading;

    private List<SetLine> FootnoteLines(int number)
    {
        if (!_footnotes.TryGetValue(number, out IReadOnlyList<Inline>? text))
        {
            throw new InvalidOperationException("Missing footnote " + number.ToString(CultureInfo.InvariantCulture));
        }

        var runs = new List<Inline> { new(Inline.Superscript(number.ToString(CultureInfo.InvariantCulture)) + " ") };
        runs.AddRange(text);
        return TextMeasure.Wrap(TextMeasure.Tokenize(runs), _style.Right - _style.Left, _style.FootnoteSize);
    }
}
