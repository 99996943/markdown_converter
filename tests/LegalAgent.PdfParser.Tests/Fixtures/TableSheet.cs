using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Builds layout pages of a table-document (spec 002) for stage tests: a frame of rulings at x 54 / divider / 541 with
/// horizontal rulings in two pieces (as in the reference document), an optional column-name row and rows whose name
/// shares the baseline of the first content line. Content is made of five-letter words, eight per line, 15 pt apart.
/// </summary>
internal sealed class TableSheet
{
    public const double FrameLeft = 54;
    public const double FrameRight = 541;
    public const double FrameTop = 72;
    public const double NameX = 60;
    public const double TextX = 186;
    public const double Leading = 15;
    public const double LineHeight = 10;
    private const int WordsPerLine = 8;

    private readonly List<LayoutPage> _pages = [];
    private readonly List<double> _edges = [];
    private readonly List<LayoutLine> _lines = [];
    private readonly List<Segment> _extra = [];
    private readonly List<Rect> _filled = [];
    private double _y;
    private bool _open;

    /// <summary>X of the ruling between the columns on the pages started from now on.</summary>
    public double Divider { get; set; } = 181;

    /// <summary>Additional vertical rulings (e.g. a third column) on the pages started from now on.</summary>
    public double[] ExtraVerticals { get; set; } = [];

    /// <summary>Starts a table page; <paramref name="header"/> adds the bold „Definicje | Wyjaśnienie” row.</summary>
    public TableSheet Page(bool header = true)
    {
        EndPage();
        _open = true;
        _edges.Add(FrameTop);
        _y = FrameTop + 8;
        if (header)
        {
            _lines.Add(Merged(_y, [("Definicje", 65, TextStyle.Bold), ("Wyjaśnienie", TextX, TextStyle.Bold)]));
            AddEdge(_y + LineHeight + 5);
        }

        return this;
    }

    /// <summary>
    /// Adds a row: <paramref name="name"/> (null = empty left cell, a continuation) split into lines of at most two words,
    /// and <paramref name="words"/> words of content. <paramref name="bullet"/> starts the content with „•”;
    /// <paramref name="paragraphs"/> leaves a paragraph gap in the middle; <paramref name="underline"/> draws a short
    /// link underline (and a shaded link box) inside the right cell in the middle of the row.
    /// </summary>
    public TableSheet Row(string? name, int words, bool bullet = false, bool paragraphs = false, bool underline = false, bool boldContent = false, double nameOffset = 0)
    {
        if (!_open)
        {
            Page();
        }

        var nameLines = new Queue<string>();
        if (name is not null)
        {
            string[] parts = name.Split(' ');
            for (int i = 0; i < parts.Length; i += 2)
            {
                nameLines.Enqueue(string.Join(' ', parts.Skip(i).Take(2)));
            }
        }

        int lineCount = (words + WordsPerLine - 1) / WordsPerLine;
        int remaining = words;
        for (int i = 0; i < Math.Max(lineCount, nameLines.Count); i++)
        {
            if (paragraphs && i == lineCount / 2 && i > 0)
            {
                _y += Leading;
            }

            if (underline && i == lineCount / 2 && i > 0)
            {
                _extra.Add(new Segment(220, _y - 2, 400, _y - 2));
                _filled.Add(new Rect(218, _y - 13, 402, _y - 1));
            }

            var parts = new List<(string Text, double Left, TextStyle Style)>();
            if (nameLines.Count > 0)
            {
                parts.Add((nameLines.Dequeue(), NameX, TextStyle.Bold));
            }

            if (i < lineCount)
            {
                int n = Math.Min(WordsPerLine, remaining);
                remaining -= n;
                string text = string.Join(' ', Enumerable.Repeat("tekst", n));
                if (bullet && i == 0)
                {
                    parts.Add(("•", TextX + 4, TextStyle.None));
                    parts.Add((text, TextX + 22, boldContent ? TextStyle.Bold : TextStyle.None));
                }
                else
                {
                    parts.Add((text, TextX + (bullet ? 22 : 0), boldContent ? TextStyle.Bold : TextStyle.None));
                }
            }

            if (parts.Count > 0)
            {
                if (nameOffset != 0 && parts[0].Left == NameX && parts.Count > 1)
                {
                    _lines.Add(Merged(_y + nameOffset, [parts[0]]));
                    _lines.Add(Merged(_y, parts.Skip(1).ToList()));
                }
                else
                {
                    _lines.Add(Merged(_y, parts));
                }
            }

            _y += Leading;
        }

        AddEdge(_y - Leading + LineHeight + 4);
        return this;
    }

    /// <summary>A line of text above the frame of the current page (outside the table).</summary>
    public TableSheet Above(string text)
    {
        _lines.Add(LayoutFactory.Line(text, FrameLeft, 40));
        return this;
    }

    /// <summary>Marks the last line added as belonging to a step scheme (FR-067).</summary>
    public TableSheet MarkLastLineAsStepScheme()
    {
        _lines[^1].Annotations[LayoutAnnotations.StepIndex] = "0";
        return this;
    }

    /// <summary>A page of plain text without rulings.</summary>
    public TableSheet PlainPage(int lines = 10)
    {
        EndPage();
        var page = new LayoutPage(_pages.Count + 1, LayoutFactory.PageWidth, LayoutFactory.PageHeight);
        for (int i = 0; i < lines; i++)
        {
            page.Lines.Add(LayoutFactory.Line("Zwykły tekst regulaminu na stronie bez tabeli.", 72, 100 + (i * Leading)));
        }

        _pages.Add(page);
        return this;
    }

    /// <summary>Closes the current page: rulings of the frame and the row edges.</summary>
    public TableSheet EndPage()
    {
        if (!_open)
        {
            return this;
        }

        var page = new LayoutPage(_pages.Count + 1, LayoutFactory.PageWidth, LayoutFactory.PageHeight);
        foreach (LayoutLine line in _lines.OrderBy(l => l.Box.Top).ThenBy(l => l.Box.Left))
        {
            page.Lines.Add(line);
        }

        double bottom = _edges[^1];
        foreach (double edge in _edges)
        {
            page.Rulings.Add(new Segment(FrameLeft + 1, edge, Divider, edge));
            page.Rulings.Add(new Segment(Divider, edge, FrameRight, edge));
        }

        foreach (double x in new[] { FrameLeft, Divider, FrameRight }.Concat(ExtraVerticals))
        {
            page.Rulings.Add(new Segment(x, FrameTop, x, bottom));
        }

        foreach (Segment segment in _extra)
        {
            page.Rulings.Add(segment);
        }

        foreach (Rect area in _filled)
        {
            page.FilledAreas.Add(area);
        }

        _pages.Add(page);
        _lines.Clear();
        _edges.Clear();
        _extra.Clear();
        _filled.Clear();
        _open = false;
        return this;
    }

    public IReadOnlyList<LayoutPage> Pages()
    {
        EndPage();
        return _pages;
    }

    public PipelineContext Context(Action<PdfParserOptions>? configure = null) => LayoutFactory.Context(Pages(), configure);

    private void AddEdge(double edge)
    {
        _edges.Add(edge);
        _y = edge + 8;
    }

    /// <summary>One visual line made of parts on one baseline (line assembly joins the cells of both columns).</summary>
    private static LayoutLine Merged(double top, IReadOnlyList<(string Text, double Left, TextStyle Style)> parts)
    {
        var words = new List<LayoutWord>();
        var segments = new List<LineSegment>();
        foreach ((string text, double left, TextStyle style) in parts)
        {
            double x = left;
            var segmentWords = new List<LayoutWord>();
            foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var box = new Rect(x, top, x + (part.Length * LayoutFactory.CharWidth), top + LineHeight);
                segmentWords.Add(new LayoutWord([], box, part, style));
                x = box.Right + LayoutFactory.CharWidth;
            }

            words.AddRange(segmentWords);
            segments.Add(new LineSegment(segmentWords, segmentWords.Skip(1).Aggregate(segmentWords[0].Box, (acc, w) => acc.Union(w.Box))));
        }

        Rect lineBox = words.Skip(1).Aggregate(words[0].Box, (acc, w) => acc.Union(w.Box));
        var line = new LayoutLine(words, lineBox, top + (LineHeight * 0.8));
        foreach (LineSegment segment in segments)
        {
            line.Segments.Add(segment);
        }

        return line;
    }
}
