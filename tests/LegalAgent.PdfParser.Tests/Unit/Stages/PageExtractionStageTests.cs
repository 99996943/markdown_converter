using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;
using LegalAgent.PdfParser.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class PageExtractionStageTests
{
    private static string TextOf(LayoutPage page) => string.Concat(page.Glyphs.Select(g => g.Text));

    [Fact]
    public void Order_IsPageExtraction()
    {
        Assert.Equal(StageOrder.PageExtraction, new PageExtractionStage().Order);
    }

    [Fact]
    public void Execute_GlyphsCarryEffectivePointSizeNotFontSize()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().ScaledText(50, 100, "AB", 12).Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        LayoutPage page = Assert.Single(h.Context.Pages);
        Assert.All(page.Glyphs, g => Assert.Equal(12, g.PointSize, 0.01));
    }

    [Fact]
    public void Execute_ConvertsYToTopDown()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Hello", 11).Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        LayoutPage page = Assert.Single(h.Context.Pages);
        Assert.Equal(842, page.Height, 0.5);
        LayoutGlyph g = page.Glyphs[0];
        Assert.Equal(100, g.Baseline, 0.5);
        Assert.True(g.Box.Top < 100 && g.Box.Bottom >= 99.5, $"Box {g.Box}");
        Assert.True(g.Box.Top > 80, $"Box {g.Box}");
        Assert.Equal(50, g.Box.Left, 1.5);
    }

    [Fact]
    public void Execute_DetectsBoldAndItalicFaces()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Aa", bold: true)
            .Text(50, 130, "Bb", italic: true)
            .Text(50, 160, "Cc")
            .Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        IList<LayoutGlyph> glyphs = h.Context.Pages[0].Glyphs;
        Assert.All(glyphs.Take(2), g => Assert.True(g.IsBold && !g.IsItalic));
        Assert.All(glyphs.Skip(2).Take(2), g => Assert.True(g.IsItalic && !g.IsBold));
        Assert.All(glyphs.Skip(4), g => Assert.True(!g.IsItalic && !g.IsBold));
    }

    [Fact]
    public void Execute_DropsRotatedTextAndCountsIt()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Body")
            .RotatedText(20, 700, "WXYZ", 90)
            .RotatedText(300, 700, "QR", 45)
            .Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal("Body", TextOf(h.Context.Pages[0]));
        Assert.Equal(6, h.Context.Report.DroppedTextCount);
    }

    [Fact]
    public void Execute_KeepsRotatedTextWhenDropRotatedTextDisabled()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").RotatedText(20, 700, "WXYZ", 90).Build();
        using StageHarness h = StageHarness.Open(pdf, o => o.Normalization.DropRotatedText = false);

        h.Run(new PageExtractionStage());

        Assert.Equal(8, h.Context.Pages[0].Glyphs.Count);
        Assert.Equal(0, h.Context.Report.DroppedTextCount);
    }

    [Fact]
    public void Execute_DropsInvisibleText()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").InvisibleText(50, 100, "OCRLAYER").Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal("Body", TextOf(h.Context.Pages[0]));
        Assert.Equal(8, h.Context.Report.DroppedTextCount);
    }

    [Fact]
    public void Execute_KeepsInvisibleTextWhenDropInvisibleTextDisabled()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").InvisibleText(50, 130, "OCR").Build();
        using StageHarness h = StageHarness.Open(pdf, o => o.Normalization.DropInvisibleText = false);

        h.Run(new PageExtractionStage());

        Assert.Equal(7, h.Context.Pages[0].Glyphs.Count);
    }

    [Fact]
    public void Execute_DropsTextOutsideThePage()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").Text(700, 100, "Out").Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal("Body", TextOf(h.Context.Pages[0]));
        Assert.Equal(3, h.Context.Report.DroppedTextCount);
    }

    [Fact]
    public void Execute_DropsWhiteTextOnPageWithoutFilledShapes()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").WhiteText(50, 200, "Hidden").Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal("Body", TextOf(h.Context.Pages[0]));
        Assert.Equal(6, h.Context.Report.DroppedTextCount);
    }

    [Fact]
    public void Execute_KeepsWhiteTextOnPageWithFilledBackgroundShape()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .FilledRect(40, 80, 300, 40)
            .WhiteText(50, 100, "Banner")
            .Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal("Banner", TextOf(h.Context.Pages[0]));
    }

    [Fact]
    public void Execute_ExtractsHorizontalAndVerticalRulings()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page()
            .Text(50, 60, "Table")
            .HLine(50, 300, 100)
            .VLine(120, 100, 200)
            .Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        IList<Segment> rulings = h.Context.Pages[0].Rulings;
        Segment horizontal = Assert.Single(rulings, s => s.IsHorizontal);
        Segment vertical = Assert.Single(rulings, s => s.IsVertical);
        Assert.Equal(100, horizontal.Y1, 0.5);
        Assert.Equal(50, Math.Min(horizontal.X1, horizontal.X2), 0.5);
        Assert.Equal(300, Math.Max(horizontal.X1, horizontal.X2), 0.5);
        Assert.Equal(120, vertical.X1, 0.5);
        Assert.Equal(100, Math.Min(vertical.Y1, vertical.Y2), 0.5);
        Assert.Equal(200, Math.Max(vertical.Y1, vertical.Y2), 0.5);
    }

    [Fact]
    public void Execute_SetsHasImages()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").Image(50, 200, 50, 50).Page().Text(50, 100, "Plain").Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.True(h.Context.Pages[0].HasImages);
        Assert.False(h.Context.Pages[1].HasImages);
    }

    [Fact]
    public void Execute_ImageOnlyPageIsSkippedAsNoTextLayer()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").Page().Image(50, 100, 400, 600).Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Null(h.Context.Pages[0].Skipped);
        Assert.Equal(SkipReason.NoTextLayer, h.Context.Pages[1].Skipped);
        ConversionReport report = h.Context.Report.Build(2, TimeSpan.Zero);
        SkippedPage skipped = Assert.Single(report.SkippedPages);
        Assert.Equal(2, skipped.PageNumber);
        Assert.Equal(SkipReason.NoTextLayer, skipped.Reason);
        Assert.Contains(report.Warnings, w => w.Code == "PDF001_NoTextLayer" && w.PageNumber == 2);
    }

    [Fact]
    public void Execute_CompletelyBlankPageIsNotSkippedAndNotReported()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").BlankPage().Build();
        using StageHarness h = StageHarness.Open(pdf);

        h.Run(new PageExtractionStage());

        Assert.Equal(2, h.Context.Pages.Count);
        Assert.Null(h.Context.Pages[1].Skipped);
        ConversionReport report = h.Context.Report.Build(2, TimeSpan.Zero);
        Assert.Empty(report.SkippedPages);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Execute_DocumentWithoutAnyText_ThrowsPdfNoText()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Image(50, 100, 400, 600).Page().BlankPage().Build();
        using StageHarness h = StageHarness.Open(pdf);

        Assert.Throws<PdfNoTextException>(() => h.Run(new PageExtractionStage()));
    }

    [Fact]
    public void Execute_AlreadyCancelled_ThrowsBeforeProcessingPages()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "Body").Build();
        using var cts = new CancellationTokenSource();
        using StageHarness h = StageHarness.Open(pdf, cancellationToken: cts.Token);
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => h.Run(new PageExtractionStage()));
        Assert.Empty(h.Context.Pages);
    }

    [Fact]
    public void Execute_PageReadErrorWithoutPartialResult_ThrowsPageReadException()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "One").Page().Text(50, 100, "Two").Build();
        using StageHarness h = StageHarness.Open(pdf);

        PdfPageReadException ex = Assert.Throws<PdfPageReadException>(() => h.Run(new FailingOnPageStage(2)));

        Assert.Equal(2, ex.PageNumber);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Execute_PageReadErrorWithPartialResult_SkipsPageAndReportsIt()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "One").Page().Text(50, 100, "Two").Build();
        using StageHarness h = StageHarness.Open(pdf, o => o.AllowPartialResult = true);

        h.Run(new FailingOnPageStage(2));

        Assert.Equal("One", TextOf(h.Context.Pages[0]));
        Assert.Equal(SkipReason.PageReadError, h.Context.Pages[1].Skipped);
        SkippedPage skipped = Assert.Single(h.Context.Report.Build(2, TimeSpan.Zero).SkippedPages);
        Assert.Equal(2, skipped.PageNumber);
        Assert.Equal(SkipReason.PageReadError, skipped.Reason);
    }

    [Fact]
    public void Execute_AllPagesUnreadable_ThrowsEvenWithPartialResult()
    {
        byte[] pdf = new SyntheticPdfBuilder().Page().Text(50, 100, "One").Page().Text(50, 100, "Two").Build();
        using StageHarness h = StageHarness.Open(pdf, o => o.AllowPartialResult = true);

        PdfPageReadException ex = Assert.Throws<PdfPageReadException>(() => h.Run(new FailingOnPageStage(1, 2)));

        Assert.Equal(1, ex.PageNumber);
    }

    [Fact]
    public void Execute_WithoutSourceDocument_ThrowsInvalidOperation()
    {
        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo(null, 1, null, 1, new string('0', 64)),
            new ReportBuilder());

        Assert.Throws<InvalidOperationException>(() => new PageExtractionStage().Execute(context));
    }

    private sealed class FailingOnPageStage(params int[] failing) : PageExtractionStage
    {
        internal override Page GetPage(PdfDocument document, int pageNumber) =>
            failing.Contains(pageNumber)
                ? throw new InvalidOperationException("boom")
                : base.GetPage(document, pageNumber);
    }
}

public sealed class FontStyleDetectorTests
{
    [Theory]
    [InlineData("ABCDEF+Arial-BoldMT", true)]
    [InlineData("ABCDEF+Calibri-Black", true)]
    [InlineData("Roboto-Heavy", true)]
    [InlineData("OpenSans-Semibold", true)]
    [InlineData("Lato-Demi", true)]
    [InlineData("Times,B", true)]
    [InlineData("Garamond-B", true)]
    [InlineData("ABCDEF+Arial", false)]
    [InlineData("ABCDEF+TimesNewRomanPSMT", false)]
    [InlineData("", false)]
    public void IsBold_UsesFontNameTokensAfterRemovingSubsetPrefix(string fontName, bool expected)
    {
        Assert.Equal(expected, FontStyleDetector.IsBold(fontName, pdfPigIsBold: false, renderedWithStroke: false));
    }

    [Fact]
    public void IsBold_TrustsPdfPigFlagAndFakeBoldRenderMode()
    {
        Assert.True(FontStyleDetector.IsBold("Plain", pdfPigIsBold: true, renderedWithStroke: false));
        Assert.True(FontStyleDetector.IsBold("Plain", pdfPigIsBold: false, renderedWithStroke: true));
    }

    [Fact]
    public void IsBold_SubsetPrefixItselfDoesNotCount()
    {
        // "BLACKB+" would match the Black token if the prefix were not stripped.
        Assert.False(FontStyleDetector.IsBold("BLACKB+Arial", pdfPigIsBold: false, renderedWithStroke: false));
    }

    [Theory]
    [InlineData("ABCDEF+Arial-ItalicMT", true)]
    [InlineData("Helvetica-Oblique", true)]
    [InlineData("Times,It", true)]
    [InlineData("Garamond-It", true)]
    [InlineData("Wit", false)]
    [InlineData("ABCDEF+Arial", false)]
    public void IsItalic_UsesFontNameTokens(string fontName, bool expected)
    {
        Assert.Equal(expected, FontStyleDetector.IsItalic(fontName, pdfPigIsItalic: false));
    }

    [Fact]
    public void IsItalic_TrustsPdfPigFlag()
    {
        Assert.True(FontStyleDetector.IsItalic("Plain", pdfPigIsItalic: true));
    }
}
