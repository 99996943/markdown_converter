using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class TextNormalizationStageTests
{
    private const double Body = 11;
    private const double BaselineY = 100;

    private static LayoutGlyph G(string text, double x, double size = Body, double baseline = BaselineY) =>
        new(text, new Rect(x, baseline - size, x + (size / 2), baseline + (size * 0.2)), baseline, size, false, false);

    /// <summary>One glyph per character of <paramref name="text"/>, 5 pt apart, all at body size on the same baseline.</summary>
    private static List<LayoutGlyph> Glyphs(string text) =>
        text.Select((c, i) => G(c.ToString(), 50 + (i * 5))).ToList();

    private static LayoutPage Run(IEnumerable<LayoutGlyph> glyphs)
    {
        var page = new LayoutPage(1, 595, 842);
        foreach (LayoutGlyph g in glyphs)
        {
            page.Glyphs.Add(g);
        }

        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo(null, 1, null, 0, new string('0', 64)),
            new ReportBuilder());
        context.Pages.Add(page);
        new TextNormalizationStage().Execute(context);
        return page;
    }

    private static string TextOf(LayoutPage page) => string.Concat(page.Glyphs.Select(g => g.Text));

    [Fact]
    public void Order_IsTextNormalization()
    {
        Assert.Equal(StageOrder.TextNormalization, new TextNormalizationStage().Order);
    }

    [Theory]
    [InlineData("ﬀ", "ff")]
    [InlineData("ﬁ", "fi")]
    [InlineData("ﬂ", "fl")]
    [InlineData("ﬃ", "ffi")]
    [InlineData("ﬄ", "ffl")]
    [InlineData("ﬅ", "st")]
    [InlineData("ﬆ", "st")]
    public void Execute_ExpandsLigatures(string ligature, string expected)
    {
        LayoutPage page = Run([G("o", 50), G(ligature, 56), G("x", 62)]);

        Assert.Equal("o" + expected + "x", TextOf(page));
    }

    [Fact]
    public void Ligatures_Expand_HandlesWholeStrings()
    {
        Assert.Equal("official affluent", Ligatures.Expand("oﬃcial aﬀluent"));
        Assert.Equal("plain", Ligatures.Expand("plain"));
    }

    [Fact]
    public void Execute_ComposesBaseLetterWithSeparateCombiningMarkGlyph()
    {
        var a = G("a", 50);
        var ogonek = new LayoutGlyph("̨", new Rect(50, 95, 54, 108), 100, Body, false, false);

        LayoutPage page = Run([G("k", 40), a, ogonek, G("t", 60)]);

        Assert.Equal("kąt", TextOf(page));
        Assert.Equal(3, page.Glyphs.Count);
        Assert.Equal(108, page.Glyphs[1].Box.Bottom, 0.01);
    }

    [Fact]
    public void Execute_NormalizesDecomposedTextToNfc()
    {
        LayoutPage page = Run([G("é", 50)]);

        Assert.Equal("é", TextOf(page));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(" ")]
    [InlineData(" ")]
    public void Execute_ReplacesSpecialSpacesWithSpace(string special)
    {
        LayoutPage page = Run([G("a", 50), G(special, 55), G("b", 60)]);

        Assert.Equal("a b", TextOf(page));
    }

    [Fact]
    public void Execute_PreservesSuperscriptDigitCharacters()
    {
        LayoutPage page = Run(Glyphs("Art. 5¹ ²³"));

        Assert.Equal("Art. 5¹ ²³", TextOf(page));
    }

    [Fact]
    public void Execute_ConvertsRaisedSmallDigitsAfterArticleNumberToSuperscripts()
    {
        List<LayoutGlyph> glyphs = Glyphs("Art. 12");
        glyphs.Add(G("1", 85, size: 7, baseline: BaselineY - 4));
        glyphs.Add(G("2", 89, size: 7, baseline: BaselineY - 4));
        glyphs.Add(G(".", 93));

        LayoutPage page = Run(glyphs);

        Assert.Equal("Art. 12¹².", TextOf(page));
    }

    [Fact]
    public void Execute_ConvertsRaisedSmallDigitAfterParagraphSign()
    {
        List<LayoutGlyph> glyphs = Glyphs("§ 5");
        glyphs.Add(G("3", 75, size: 7, baseline: BaselineY - 4));

        LayoutPage page = Run(glyphs);

        Assert.Equal("§ 5³", TextOf(page));
    }

    [Fact]
    public void Execute_ConvertsRaisedDigitAfterNumberWithLetterSuffix()
    {
        List<LayoutGlyph> glyphs = Glyphs("Art. 12a");
        glyphs.Add(G("4", 90, size: 7, baseline: BaselineY - 4));

        LayoutPage page = Run(glyphs);

        Assert.Equal("Art. 12a⁴", TextOf(page));
    }

    [Fact]
    public void Execute_LeavesRaisedSmallDigitsAfterOrdinaryWordsAlone()
    {
        List<LayoutGlyph> glyphs = Glyphs("tekst");
        glyphs.Add(G("1", 80, size: 7, baseline: BaselineY - 4));

        LayoutPage page = Run(glyphs);

        Assert.Equal("tekst1", TextOf(page));
    }

    [Fact]
    public void Execute_LeavesFullSizeDigitsAfterArticleNumberAlone()
    {
        LayoutPage page = Run(Glyphs("Art. 123"));

        Assert.Equal("Art. 123", TextOf(page));
    }

    [Fact]
    public void Execute_LeavesSmallButNotRaisedDigitsAlone()
    {
        List<LayoutGlyph> glyphs = Glyphs("Art. 12");
        glyphs.Add(G("1", 85, size: 7));

        LayoutPage page = Run(glyphs);

        Assert.Equal("Art. 121", TextOf(page));
    }

    [Fact]
    public void Execute_AlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo(null, 1, null, 0, new string('0', 64)),
            new ReportBuilder(),
            cts.Token);
        context.Pages.Add(new LayoutPage(1, 595, 842));
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new TextNormalizationStage().Execute(context));
    }
}
