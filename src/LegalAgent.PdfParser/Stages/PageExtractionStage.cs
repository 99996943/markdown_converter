using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;

namespace LegalAgent.PdfParser.Stages;

/// <summary>
/// Reads glyphs, rulings and image presence from the PDF into <see cref="LayoutPage"/>s (research R6).
/// Y coordinates are converted to a top-down axis; rotated and invisible text is dropped and counted (FR-013).
/// </summary>
public class PageExtractionStage : IPipelineStage
{
    private const double WhiteThreshold = 0.99;
    private const double ThinRuleThickness = 1.5;
    private const double MinRuleLength = 3;

    /// <inheritdoc />
    public int Order => StageOrder.PageExtraction;

    /// <inheritdoc />
    public void Execute(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        PdfDocument document = context.SourceDocument
            ?? throw new InvalidOperationException("PipelineContext.SourceDocument must be set before page extraction.");

        context.Pages.Clear();
        int pageCount = document.NumberOfPages;
        int pagesWithText = 0;
        int readErrors = 0;
        int firstFailedPage = 0;
        Exception? firstFailure = null;

        for (int n = 1; n <= pageCount; n++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            LayoutPage layoutPage;
            try
            {
                Page page = GetPage(document, n);
                layoutPage = Extract(page, n, context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not PdfParserException)
            {
                if (!context.Options.AllowPartialResult)
                {
                    throw new PdfPageReadException(n, innerException: ex);
                }

                readErrors++;
                if (firstFailure is null)
                {
                    firstFailure = ex;
                    firstFailedPage = n;
                }

                layoutPage = new LayoutPage(n, 0, 0) { Skipped = SkipReason.PageReadError };
                context.Report.AddSkippedPage(n, SkipReason.PageReadError, ex.Message);
                context.Report.AddWarning("PDF002_PageReadError", n, $"Strona {n} została pominięta: {ex.Message}");
            }

            if (layoutPage.Glyphs.Count > 0)
            {
                pagesWithText++;
            }

            context.Pages.Add(layoutPage);
            context.ReportProgress(n);
        }

        if (pagesWithText == 0)
        {
            if (pageCount > 0 && readErrors == pageCount)
            {
                throw new PdfPageReadException(firstFailedPage, innerException: firstFailure);
            }

            throw new PdfNoTextException();
        }
    }

    /// <summary>Reads one page from the document; a seam for tests.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    internal virtual Page GetPage(PdfDocument document, int pageNumber) => document.GetPage(pageNumber);

    private static LayoutPage Extract(Page page, int number, PipelineContext context)
    {
        double height = page.Height;
        double width = page.Width;
        var layout = new LayoutPage(number, width, height);
        NormalizationOptions norm = context.Options.Normalization;

        bool hasFilledShapes = page.Paths.Any(p => p.IsFilled);
        int dropped = 0;

        foreach (Letter letter in page.Letters)
        {
            if (norm.DropRotatedText && letter.TextOrientation != TextOrientation.Horizontal)
            {
                dropped++;
                continue;
            }

            if (norm.DropInvisibleText && IsInvisible(letter, width, height, hasFilledShapes))
            {
                dropped++;
                continue;
            }

            PdfRectangle r = letter.BoundingBox;
            var box = new Rect(r.Left, height - r.Top, r.Right, height - r.Bottom);
            string fontName = letter.FontName ?? string.Empty;
            bool stroke = letter.RenderingMode == TextRenderingMode.FillThenStroke;
            bool bold = FontStyleDetector.IsBold(fontName, letter.FontDetails?.IsBold ?? false, stroke);
            bool italic = FontStyleDetector.IsItalic(fontName, letter.FontDetails?.IsItalic ?? false);

            layout.Glyphs.Add(new LayoutGlyph(
                letter.Value,
                box,
                height - letter.StartBaseLine.Y,
                letter.PointSize,
                bold,
                italic,
                letter.StartBaseLine.X,
                letter.EndBaseLine.X));
        }

        context.Report.AddDroppedText(dropped);
        layout.HasImages = page.NumberOfImages > 0;
        ExtractRulings(page, layout, height);

        if (layout.Glyphs.Count == 0 && layout.HasImages)
        {
            layout.Skipped = SkipReason.NoTextLayer;
            context.Report.AddSkippedPage(number, SkipReason.NoTextLayer, "Strona nie zawiera warstwy tekstowej (np. skan).");
            context.Report.AddWarning("PDF001_NoTextLayer", number, $"Strona {number} nie ma warstwy tekstowej i została pominięta.");
        }
        else if (layout.Glyphs.Count > 0 && layout.HasImages)
        {
            context.Report.AddWarning("IMG001_ImagesIgnored", number, $"Obrazy na stronie {number} zostały pominięte.");
        }

        return layout;
    }

    private static bool IsInvisible(Letter letter, double width, double height, bool hasFilledShapes)
    {
        if (letter.RenderingMode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip)
        {
            return true;
        }

        PdfRectangle r = letter.BoundingBox;
        double cx = (r.Left + r.Right) / 2;
        double cy = (r.Top + r.Bottom) / 2;
        if (cx < 0 || cx > width || cy < 0 || cy > height)
        {
            return true;
        }

        if (!hasFilledShapes && letter.FillColor is { } color)
        {
            (double red, double green, double blue) = color.ToRGBValues();
            return red >= WhiteThreshold && green >= WhiteThreshold && blue >= WhiteThreshold;
        }

        return false;
    }

    private static void ExtractRulings(Page page, LayoutPage layout, double height)
    {
        foreach (PdfPath path in page.Paths)
        {
            if (path.IsClipping)
            {
                continue;
            }

            foreach (PdfSubpath subpath in path)
            {
                PdfRectangle? bounds = subpath.GetBoundingRectangle();
                if (bounds is null)
                {
                    continue;
                }

                PdfRectangle b = bounds.Value;
                double w = b.Right - b.Left;
                double h = b.Top - b.Bottom;

                if (h <= ThinRuleThickness && w >= MinRuleLength)
                {
                    double y = height - ((b.Top + b.Bottom) / 2);
                    layout.Rulings.Add(new Segment(b.Left, y, b.Right, y));
                }
                else if (w <= ThinRuleThickness && h >= MinRuleLength)
                {
                    double x = (b.Left + b.Right) / 2;
                    layout.Rulings.Add(new Segment(x, height - b.Top, x, height - b.Bottom));
                }
                else if (path.IsStroked)
                {
                    foreach (PdfSubpath.Line line in subpath.Commands.OfType<PdfSubpath.Line>())
                    {
                        var segment = new Segment(line.From.X, height - line.From.Y, line.To.X, height - line.To.Y);
                        if (segment.IsHorizontal || segment.IsVertical)
                        {
                            layout.Rulings.Add(segment);
                        }
                    }
                }
            }
        }
    }
}
