using System.Globalization;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Writer;

namespace LegalAgent.Corpus.Pdf;

/// <summary>
/// Fluent builder of synthetic PDF documents. Text uses embedded Noto Sans TrueType
/// fonts (Regular, Bold, Italic, Mono) so Polish diacritics are supported. Vertical coordinates are
/// given as the distance from the top edge of the page (Y grows downwards).
/// </summary>
public sealed class SyntheticPdfBuilder
{
    // 1x1 pixel PNG used by Image().
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const double HeaderBaselineFromTop = 40;
    private const double FooterBaselineFromBottom = 40;
    private const double MarginFontSize = 9;

    private readonly List<PageSpec> _pages = [];
    private string? _headerTemplate;
    private string? _footerTemplate;
    private string? _title;

    /// <summary>Starts a new page; subsequent drawing calls apply to it.</summary>
    public SyntheticPdfBuilder Page(double width = 595, double height = 842)
    {
        _pages.Add(new PageSpec(width, height));
        return this;
    }

    /// <summary>Adds a page without any content.</summary>
    public SyntheticPdfBuilder BlankPage(double width = 595, double height = 842) => Page(width, height);

    /// <summary>
    /// Writes horizontal text whose baseline is <paramref name="yFromTop"/> from the top edge; <paramref name="mono"/>
    /// selects the monospace face (like the Courier New „o” of second-level bullets in word processors).
    /// </summary>
    public SyntheticPdfBuilder Text(double x, double yFromTop, string text, double size = 11, bool bold = false, bool italic = false, bool mono = false)
    {
        Current.Draw.Add((ctx, page) =>
        {
            page.AddText(text, size, new PdfPoint(x, page.PageSize.Height - yFromTop), mono ? ctx.Mono() : ctx.Font(bold, italic));
        });
        return this;
    }

    /// <summary>Writes text rotated counter-clockwise by <paramref name="degrees"/> around its start point.</summary>
    public SyntheticPdfBuilder RotatedText(double x, double yFromTop, string text, double degrees, double size = 11)
    {
        Current.Draw.Add((ctx, page) =>
        {
            double rad = degrees * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double originY = page.PageSize.Height - yFromTop;

            page.CurrentStream.Operations.Add(Push.Value);
            page.CurrentStream.Operations.Add(new ModifyCurrentTransformationMatrix([cos, sin, -sin, cos, x, originY]));
            page.AddText(text, size, new PdfPoint(0, 0), ctx.Font(false, false));
            page.CurrentStream.Operations.Add(Pop.Value);
        });
        return this;
    }

    /// <summary>
    /// Writes text whose font size is 1 pt and whose effective size (<c>PointSize</c>) comes from a scaling
    /// transformation matrix, as ISAP-generated PDFs do.
    /// </summary>
    public SyntheticPdfBuilder ScaledText(double x, double yFromTop, string text, double scale)
    {
        Current.Draw.Add((ctx, page) =>
        {
            double originY = page.PageSize.Height - yFromTop;
            page.CurrentStream.Operations.Add(Push.Value);
            page.CurrentStream.Operations.Add(new ModifyCurrentTransformationMatrix([scale, 0, 0, scale, x, originY]));
            page.AddText(text, 1, new PdfPoint(0, 0), ctx.Font(false, false));
            page.CurrentStream.Operations.Add(Pop.Value);
        });
        return this;
    }

    /// <summary>Writes white text (fill colour 255,255,255).</summary>
    public SyntheticPdfBuilder WhiteText(double x, double yFromTop, string text, double size = 11)
    {
        Current.Draw.Add((ctx, page) =>
        {
            page.SetTextAndFillColor(255, 255, 255);
            page.AddText(text, size, new PdfPoint(x, page.PageSize.Height - yFromTop), ctx.Font(false, false));
            page.SetTextAndFillColor(0, 0, 0);
        });
        return this;
    }

    /// <summary>
    /// Draws a filled rectangle with its top-left corner at (<paramref name="x"/>, <paramref name="yFromTop"/>);
    /// <paramref name="gray"/> is the fill level from 0 (black) to 255 (white), e.g. 217 for a light gray cell.
    /// </summary>
    public SyntheticPdfBuilder FilledRect(double x, double yFromTop, double width, double height, byte gray = 0)
    {
        Current.Draw.Add((_, page) =>
        {
            page.SetTextAndFillColor(gray, gray, gray);
            page.DrawRectangle(new PdfPoint(x, page.PageSize.Height - yFromTop - height), width, height, 1, true);
            page.SetTextAndFillColor(0, 0, 0);
        });
        return this;
    }

    /// <summary>Writes text with the invisible rendering mode (e.g. an OCR layer).</summary>
    public SyntheticPdfBuilder InvisibleText(double x, double yFromTop, string text, double size = 11)
    {
        Current.Draw.Add((ctx, page) =>
        {
            page.SetTextRenderingMode(TextRenderingMode.Neither);
            page.AddText(text, size, new PdfPoint(x, page.PageSize.Height - yFromTop), ctx.Font(false, false));
            page.SetTextRenderingMode(TextRenderingMode.Fill);
        });
        return this;
    }

    /// <summary>Draws a horizontal line from <paramref name="x1"/> to <paramref name="x2"/> at <paramref name="yFromTop"/>.</summary>
    public SyntheticPdfBuilder HLine(double x1, double x2, double yFromTop, double lineWidth = 0.5)
    {
        Current.Draw.Add((_, page) =>
        {
            double y = page.PageSize.Height - yFromTop;
            page.DrawLine(new PdfPoint(x1, y), new PdfPoint(x2, y), lineWidth);
        });
        return this;
    }

    /// <summary>Draws a vertical line at <paramref name="x"/> from <paramref name="y1FromTop"/> to <paramref name="y2FromTop"/>.</summary>
    public SyntheticPdfBuilder VLine(double x, double y1FromTop, double y2FromTop, double lineWidth = 0.5)
    {
        Current.Draw.Add((_, page) =>
        {
            double h = page.PageSize.Height;
            page.DrawLine(new PdfPoint(x, h - y1FromTop), new PdfPoint(x, h - y2FromTop), lineWidth);
        });
        return this;
    }

    /// <summary>Embeds a small PNG image whose top-left corner is at (<paramref name="x"/>, <paramref name="yFromTop"/>).</summary>
    public SyntheticPdfBuilder Image(double x, double yFromTop, double width, double height)
    {
        Current.Draw.Add((_, page) =>
        {
            double bottom = page.PageSize.Height - yFromTop - height;
            page.AddPng(Convert.FromBase64String(PngBase64), new PdfRectangle(x, bottom, x + width, bottom + height));
        });
        return this;
    }

    /// <summary>
    /// Adds the same running header to every page; <c>{n}</c> is replaced with the 1-based page number.
    /// The header sits in the top margin zone.
    /// </summary>
    public SyntheticPdfBuilder RunningHeader(string template)
    {
        _headerTemplate = template;
        return this;
    }

    /// <summary>Adds a page-number footer to every page; <c>{n}</c> is replaced with the 1-based page number.</summary>
    public SyntheticPdfBuilder PageNumberFooter(string format)
    {
        _footerTemplate = format;
        return this;
    }

    /// <summary>Sets the document title stored in the PDF /Info dictionary.</summary>
    public SyntheticPdfBuilder Title(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>Builds the PDF document.</summary>
    public byte[] Build()
    {
        using var builder = new PdfDocumentBuilder();
        if (_title is not null)
        {
            builder.DocumentInformation.Title = _title;
        }

        var ctx = new BuildContext(builder);

        for (int i = 0; i < _pages.Count; i++)
        {
            PageSpec spec = _pages[i];
            PdfPageBuilder page = builder.AddPage(spec.Width, spec.Height);
            string number = (i + 1).ToString(CultureInfo.InvariantCulture);

            if (_headerTemplate is not null)
            {
                string header = _headerTemplate.Replace("{n}", number, StringComparison.Ordinal);
                page.AddText(header, MarginFontSize, new PdfPoint(50, spec.Height - HeaderBaselineFromTop), ctx.Font(false, false));
            }

            foreach (Action<BuildContext, PdfPageBuilder> draw in spec.Draw)
            {
                draw(ctx, page);
            }

            if (_footerTemplate is not null)
            {
                string footer = _footerTemplate.Replace("{n}", number, StringComparison.Ordinal);
                page.AddText(footer, MarginFontSize, new PdfPoint(250, FooterBaselineFromBottom), ctx.Font(false, false));
            }
        }

        return builder.Build();
    }

    /// <summary>Advance width in points of <paramref name="text"/> set in the given face, for laying out wrapped text.</summary>
    public static double TextWidth(string text, double size = 11, bool bold = false, bool italic = false, bool mono = false)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        lock (Measuring.Gate)
        {
            PdfDocumentBuilder.AddedFont font = mono ? Measuring.Context.Mono() : Measuring.Context.Font(bold, italic);
            IReadOnlyList<UglyToad.PdfPig.Content.Letter> letters = Measuring.Page.MeasureText(text, size, new PdfPoint(0, 0), font);
            return letters[^1].EndBaseLine.X - letters[0].StartBaseLine.X;
        }
    }

    private PageSpec Current =>
        _pages.Count > 0 ? _pages[^1] : throw new InvalidOperationException("Call Page() before drawing.");

    private sealed class PageSpec(double width, double height)
    {
        public double Width { get; } = width;
        public double Height { get; } = height;
        public List<Action<BuildContext, PdfPageBuilder>> Draw { get; } = [];
    }

    /// <summary>A never-built document whose fonts and page are used only to measure text.</summary>
    private static class Measuring
    {
        private static readonly PdfDocumentBuilder Builder = new();

        public static object Gate { get; } = new();

        public static BuildContext Context { get; } = new(Builder);

        public static PdfPageBuilder Page { get; } = Builder.AddPage(595, 842);
    }

    private sealed class BuildContext(PdfDocumentBuilder builder)
    {
        private readonly Dictionary<string, PdfDocumentBuilder.AddedFont> _fonts = new(StringComparer.Ordinal);

        public PdfDocumentBuilder.AddedFont Font(bool bold, bool italic) =>
            // There is no bold-italic face in the fixtures: bold wins.
            Load(bold ? "NotoSans-Bold.ttf" : italic ? "NotoSans-Italic.ttf" : "NotoSans-Regular.ttf");

        public PdfDocumentBuilder.AddedFont Mono() => Load("NotoSansMono-Regular.ttf");

        private PdfDocumentBuilder.AddedFont Load(string name)
        {
            if (!_fonts.TryGetValue(name, out PdfDocumentBuilder.AddedFont? font))
            {
                font = builder.AddTrueTypeFont(FontBytes(name));
                _fonts[name] = font;
            }

            return font;
        }

        private static byte[] FontBytes(string name)
        {
            using Stream stream = typeof(SyntheticPdfBuilder).Assembly.GetManifestResourceStream("LegalAgent.Corpus.Fonts." + name)
                ?? throw new InvalidOperationException("Missing embedded font: " + name);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
