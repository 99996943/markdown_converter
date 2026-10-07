using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Builds layout-model objects directly (without a PDF) for unit tests of stages that run after line assembly.
/// Coordinates are PDF points, Y grows downwards; an A4 page is 595 × 842.
/// </summary>
internal static class LayoutFactory
{
    public const double PageWidth = 595;
    public const double PageHeight = 842;
    public const double CharWidth = 5;
    public const double LineHeight = 10;

    /// <summary>A line whose words are laid out left to right with one character width between words.</summary>
    public static LayoutLine Line(string text, double left, double top, double height = LineHeight)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words = new List<LayoutWord>(parts.Length);
        double x = left;
        foreach (string part in parts)
        {
            var box = new Rect(x, top, x + (part.Length * CharWidth), top + height);
            words.Add(new LayoutWord([], box, part, TextStyle.None));
            x = box.Right + CharWidth;
        }

        Rect lineBox = words.Count == 0
            ? new Rect(left, top, left, top + height)
            : words.Skip(1).Aggregate(words[0].Box, (acc, w) => acc.Union(w.Box));
        var line = new LayoutLine(words, lineBox, top + (height * 0.8));
        line.Segments.Add(new LineSegment(words, lineBox));
        return line;
    }

    /// <summary>A line that ends at <paramref name="right"/> (right-aligned).</summary>
    public static LayoutLine RightAligned(string text, double right, double top) =>
        Line(text, right - (TextWidth(text)), top);

    public static double TextWidth(string text) => text.Replace(" ", "", StringComparison.Ordinal).Length * CharWidth
        + (Math.Max(0, text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length - 1) * CharWidth);

    public static LayoutPage Page(int number, IEnumerable<LayoutLine> lines, double width = PageWidth, double height = PageHeight)
    {
        var page = new LayoutPage(number, width, height);
        foreach (LayoutLine line in lines.OrderBy(l => l.Box.Top).ThenBy(l => l.Box.Left))
        {
            page.Lines.Add(line);
        }

        return page;
    }

    public static PipelineContext Context(IEnumerable<LayoutPage> pages, Action<PdfParserOptions>? configure = null)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        List<LayoutPage> list = pages.ToList();
        var context = new PipelineContext(
            options,
            new SourceInfo("test", list.Count, null, 0, "00"),
            new ReportBuilder(),
            TestContext.Current.CancellationToken);
        foreach (LayoutPage page in list)
        {
            context.Pages.Add(page);
        }

        return context;
    }

    /// <summary>Document of <paramref name="pageCount"/> pages; <paramref name="linesForPage"/> receives the 1-based page number.</summary>
    public static PipelineContext Document(
        int pageCount,
        Func<int, IEnumerable<LayoutLine>> linesForPage,
        Action<PdfParserOptions>? configure = null) =>
        Context(Enumerable.Range(1, pageCount).Select(n => Page(n, linesForPage(n))), configure);

    public static IEnumerable<string> AllLineTexts(PipelineContext context) =>
        context.Pages.SelectMany(p => p.Lines).Select(l => l.Text);
}
