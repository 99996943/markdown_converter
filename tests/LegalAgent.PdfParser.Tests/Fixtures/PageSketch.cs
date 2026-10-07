using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Hand-made page of glyphs for stage tests that need exact geometry. Letters advance <c>0.5 * size</c>,
/// spaces <c>0.26 * size</c> (explicit space glyphs). Y values are baselines measured from the top.
/// </summary>
internal sealed class PageSketch
{
    public PageSketch(int number = 1, double width = 595, double height = 842)
    {
        Page = new LayoutPage(number, width, height);
    }

    public LayoutPage Page { get; }

    public PageSketch Line(string text, double x, double baseline, double size = 11, bool bold = false, bool italic = false) =>
        Mixed(x, baseline, size, (text, bold, italic));

    public PageSketch Mixed(double x, double baseline, double size, params (string Text, bool Bold, bool Italic)[] parts)
    {
        double cursor = x;
        foreach ((string text, bool bold, bool italic) in parts)
        {
            foreach (char c in text)
            {
                if (c == ' ')
                {
                    double w = 0.26 * size;
                    Page.Glyphs.Add(new LayoutGlyph(" ", new Rect(cursor, baseline - size, cursor + w, baseline + (0.2 * size)), baseline, size, false, false));
                    cursor += w;
                    continue;
                }

                double advance = 0.5 * size;
                Page.Glyphs.Add(new LayoutGlyph(
                    c.ToString(),
                    new Rect(cursor, baseline - (0.8 * size), cursor + advance, baseline + (0.2 * size)),
                    baseline,
                    size,
                    bold,
                    italic));
                cursor += advance;
            }
        }

        return this;
    }

    /// <summary>Marks the page as skipped (no glyphs).</summary>
    public PageSketch Skipped(SkipReason reason)
    {
        Page.Skipped = reason;
        return this;
    }

    /// <summary>Creates a context holding the sketched pages and runs <see cref="LineAssemblyStage"/> on it.</summary>
    public static PipelineContext Assemble(Action<PdfParserOptions>? configure, params PageSketch[] pages)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        var context = new PipelineContext(
            options,
            new SourceInfo(null, pages.Length, null, 0, new string('0', 64)),
            new ReportBuilder());
        foreach (PageSketch p in pages)
        {
            context.Pages.Add(p.Page);
        }

        new LineAssemblyStage().Execute(context);
        return context;
    }
}
