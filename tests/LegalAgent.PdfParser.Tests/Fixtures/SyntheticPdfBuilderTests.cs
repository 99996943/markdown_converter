using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace LegalAgent.PdfParser.Tests.Fixtures;

public sealed class SyntheticPdfBuilderTests
{
    private static string TextOf(Page page) =>
        string.Concat(page.Letters.Select(l => l.Value));

    [Fact]
    public void Build_ProducesPdfThatPdfPigCanOpen()
    {
        byte[] bytes = new SyntheticPdfBuilder()
            .Page().Text(50, 100, "Hello")
            .Page().Text(50, 100, "World")
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Equal(2, doc.NumberOfPages);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5), StringComparison.Ordinal);
    }

    [Fact]
    public void Title_IsStoredInDocumentInformation()
    {
        byte[] bytes = new SyntheticPdfBuilder().Title("Regulamin testowy").Page().Text(50, 100, "x").Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Equal("Regulamin testowy", doc.Information.Title);
    }

    [Fact]
    public void ScaledText_HasUnitFontSizeAndScaledPointSize()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().ScaledText(50, 100, "AB", 12).Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        var letter = doc.GetPage(1).Letters[0];
        Assert.Equal(12, letter.PointSize, 0.01);
        Assert.Equal(1, letter.FontSize, 0.01);
    }

    [Fact]
    public void WhiteText_HasWhiteFillColor()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().WhiteText(50, 100, "AB").Text(50, 200, "CD").Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        var letters = doc.GetPage(1).Letters;
        Assert.Equal(1.0, (double)letters[0].FillColor!.ToRGBValues().r, 0.01);
        Assert.Equal(0.0, (double)letters[^1].FillColor!.ToRGBValues().r, 0.01);
    }

    [Fact]
    public void FilledRect_ProducesFilledPath()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().FilledRect(50, 100, 200, 30).Text(60, 120, "x").Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Contains(doc.GetPage(1).Paths, p => p.IsFilled);
    }

    [Fact]
    public void Page_DefaultsToA4()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, "x").Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        Page page = doc.GetPage(1);
        Assert.Equal(595, page.Width, 0.5);
        Assert.Equal(842, page.Height, 0.5);
    }

    [Fact]
    public void Text_PolishCharactersRoundTrip()
    {
        const string text = "zażółć gęślą jaźń";
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, text).Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Contains(text.Replace(" ", string.Empty, StringComparison.Ordinal),
            TextOf(doc.GetPage(1)).Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_PointSizeAndPositionMatchRequest()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(72, 100, "Abc", size: 14).Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        Letter first = doc.GetPage(1).Letters[0];
        Assert.Equal(14, first.PointSize, 0.01);
        Assert.Equal(72, first.StartBaseLine.X, 0.5);
        // yFromTop is the baseline distance from the top edge of the page.
        Assert.Equal(842 - 100, first.StartBaseLine.Y, 0.5);
    }

    [Fact]
    public void Text_BoldAndItalicSelectMatchingFonts()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Plain")
            .Text(50, 120, "Strong", bold: true)
            .Text(50, 140, "Kursywa", italic: true)
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        IReadOnlyList<Letter> letters = doc.GetPage(1).Letters;
        string FontOf(string value) => letters.First(l => l.Value == value).FontName ?? string.Empty;

        Assert.Contains("Regular", FontOf("P"), StringComparison.Ordinal);
        Assert.Contains("Bold", FontOf("S"), StringComparison.Ordinal);
        Assert.Contains("Italic", FontOf("K"), StringComparison.Ordinal);
    }

    [Fact]
    public void HLineAndVLine_AppearAsPagePaths()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page()
            .HLine(50, 300, 200)
            .VLine(100, 150, 400)
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        var paths = doc.GetPage(1).Paths;
        Assert.Equal(2, paths.Count);

        var segments = paths.SelectMany(p => p.SelectMany(sp => sp.Commands)).ToList();
        Assert.Equal(2, segments.Count(c => c is PdfSubpath.Line));
    }

    [Fact]
    public void RotatedText_HasNonHorizontalOrientation()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Horizontal")
            .RotatedText(300, 400, "Vertical", 90)
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        IReadOnlyList<Letter> letters = doc.GetPage(1).Letters;
        Assert.Contains(letters, l => l.TextOrientation == TextOrientation.Horizontal);
        Assert.Contains(letters, l => l.TextOrientation != TextOrientation.Horizontal);
    }

    [Fact]
    public void InvisibleText_UsesInvisibleRenderingMode()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page()
            .Text(50, 100, "Visible")
            .InvisibleText(50, 200, "Hidden")
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        Page page = doc.GetPage(1);
        string all = TextOf(page);
        Assert.Contains("Hidden", all, StringComparison.Ordinal);
        Assert.Contains("Visible", all, StringComparison.Ordinal);
        Assert.Contains(page.Letters, l => l.RenderingMode == TextRenderingMode.Neither);
    }

    [Fact]
    public void Image_AddsOneImageToPage()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Image(50, 100, 80, 60).Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Single(doc.GetPage(1).GetImages());
    }

    [Fact]
    public void BlankPage_HasNoLetters()
    {
        byte[] bytes = new SyntheticPdfBuilder().Page().Text(50, 100, "a").BlankPage().Build();
        using PdfDocument doc = PdfDocument.Open(bytes);
        Assert.Equal(2, doc.NumberOfPages);
        Assert.Empty(doc.GetPage(2).Letters);
    }

    [Fact]
    public void RunningHeaderAndFooter_AreAppliedToAllPagesWithPageNumber()
    {
        byte[] bytes = new SyntheticPdfBuilder()
            .Page().Text(50, 200, "Body one")
            .Page().Text(50, 200, "Body two")
            .Page().Text(50, 200, "Body three")
            .RunningHeader("Dziennik Ustaw – {n} – Poz. 1234")
            .PageNumberFooter("Strona {n} z 3")
            .Build();

        using PdfDocument doc = PdfDocument.Open(bytes);
        for (int n = 1; n <= 3; n++)
        {
            string text = TextOf(doc.GetPage(n)).Replace(" ", string.Empty, StringComparison.Ordinal);
            string num = n.ToString(CultureInfo.InvariantCulture);
            Assert.Contains($"DziennikUstaw–{num}–Poz.1234", text, StringComparison.Ordinal);
            Assert.Contains($"Strona{num}z3", text, StringComparison.Ordinal);
        }

        // Header sits in the top 8% zone, footer in the bottom 8% zone.
        Page p1 = doc.GetPage(1);
        Letter header = p1.Letters.First(l => l.Value == "D");
        Letter footer = p1.Letters.First(l => l.Value == "S");
        Assert.True(842 - header.StartBaseLine.Y < 842 * 0.08);
        Assert.True(footer.StartBaseLine.Y < 842 * 0.08);
    }
}
