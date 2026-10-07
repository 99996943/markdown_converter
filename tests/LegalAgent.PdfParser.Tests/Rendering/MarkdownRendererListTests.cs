using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>T068 — list rendering (contracts/markdown-output.md).</summary>
public sealed class MarkdownRendererListTests
{
    private static readonly SourceInfo Source = new("test.pdf", 5, null, 100, new string('0', 64));

    private static ListItem Item(string label, ListLabelKind kind, string text, params ContentBlock[] children) =>
        new(label, kind, [new TextRun(text)], children);

    private static ListItem Num(string label, string text, params ContentBlock[] children) =>
        Item(label, ListLabelKind.ArabicParen, text, children);

    private static ListItem Letter(string label, string text, params ContentBlock[] children) =>
        Item(label, ListLabelKind.LetterParen, text, children);

    private static ListBlock List(int first, int last, params ListItem[] items) =>
        new(new PageRange(first, last), items);

    private static ListBlock List(params ListItem[] items) => List(1, 1, items);

    private static ParagraphBlock Para(int page, string text) =>
        new(new PageRange(page, page), [new TextRun(text)]);

    private static LegalDocument Doc(params ContentBlock[] blocks) =>
        new(Source, null, blocks, [], []);

    private static string Render(LegalDocument doc, bool pageMarkers = false)
    {
        var options = new RenderingOptions { PageMarkers = pageMarkers };
        return new MarkdownRenderer().Render(doc, options);
    }

    [Fact]
    public void Render_ArabicParenItemsEscapeTheLabel()
    {
        string md = Render(Doc(List(Num("1)", "definicja"), Num("2)", "druga"))));

        Assert.Equal("- 1\\) definicja\n- 2\\) druga\n", md);
    }

    [Fact]
    public void Render_ArabicDotLabelIsEscaped()
    {
        string md = Render(Doc(List(Item("2.", ListLabelKind.ArabicDot, "Treść"))));

        Assert.Equal("- 2\\. Treść\n", md);
    }

    [Fact]
    public void Render_NestedListIsIndentedByTwoSpacesPerLevelWithoutBlankLines()
    {
        ListBlock nested = List(Letter("a)", "lit. a"), Letter("b)", "lit. b"));
        string md = Render(Doc(List(Num("1)", "punkt", nested), Num("2)", "drugi"))));

        Assert.Equal("- 1\\) punkt\n  - a\\) lit. a\n  - b\\) lit. b\n- 2\\) drugi\n", md);
    }

    [Fact]
    public void Render_DeeplyNestedListIndentsFourSpaces()
    {
        ListBlock deep = List(Item("-", ListLabelKind.Dash, "tiret"));
        ListBlock mid = List(Letter("a)", "lit.", deep));
        string md = Render(Doc(List(Num("1)", "punkt", mid))));

        Assert.Equal("- 1\\) punkt\n  - a\\) lit.\n    - \\- tiret\n", md);
    }

    [Fact]
    public void Render_BulletCharacterIsDropped()
    {
        string md = Render(Doc(List(Item("•", ListLabelKind.Bullet, "karta debetowa"))));

        Assert.Equal("- karta debetowa\n", md);
    }

    [Fact]
    public void Render_EnDashLabelIsKeptLiterally()
    {
        string md = Render(Doc(List(Item("–", ListLabelKind.Dash, "w przypadku …"))));

        Assert.Equal("- – w przypadku …\n", md);
    }

    [Fact]
    public void Render_HyphenMinusDashLabelIsEscaped()
    {
        string md = Render(Doc(List(Item("-", ListLabelKind.Dash, "tekst"))));

        Assert.Equal("- \\- tekst\n", md);
    }

    [Fact]
    public void Render_InlinePageBreakInsideItemIsWrittenBetweenWords()
    {
        ListItem item = new("1)", ListLabelKind.ArabicParen,
            [new TextRun("przed "), new PageBreak(2), new TextRun("po")], []);
        string md = Render(Doc(List(1, 2, item)), pageMarkers: true);

        Assert.Equal("<!-- page: 1 -->\n- 1\\) przed <!-- page: 2 --> po\n", md);
    }

    [Fact]
    public void Render_ItemStartingOnNewPageGetsMarkerLineBeforeIt()
    {
        ListItem second = new("2)", ListLabelKind.ArabicParen, [new PageBreak(2), new TextRun("drugi")], []);
        string md = Render(Doc(List(1, 2, Num("1)", "pierwszy"), second)), pageMarkers: true);

        Assert.Equal("<!-- page: 1 -->\n- 1\\) pierwszy\n<!-- page: 2 -->\n- 2\\) drugi\n", md);
    }

    [Fact]
    public void Render_NestedItemStartingOnNewPageGetsIndentedMarkerLine()
    {
        ListItem nestedSecond = new("b)", ListLabelKind.LetterParen, [new PageBreak(2), new TextRun("lit. b")], []);
        ListBlock nested = List(1, 2, Letter("a)", "lit. a"), nestedSecond);
        string md = Render(Doc(List(1, 2, Num("1)", "punkt", nested))), pageMarkers: true);

        Assert.Equal("<!-- page: 1 -->\n- 1\\) punkt\n  - a\\) lit. a\n  <!-- page: 2 -->\n  - b\\) lit. b\n", md);
    }

    [Fact]
    public void Render_LeadingPageBreakOnCurrentPageWritesNothing()
    {
        ListItem first = new("1)", ListLabelKind.ArabicParen, [new PageBreak(1), new TextRun("pierwszy")], []);
        string md = Render(Doc(List(first)), pageMarkers: true);

        Assert.Equal("<!-- page: 1 -->\n- 1\\) pierwszy\n", md);
    }

    [Fact]
    public void Render_FollowingBlockDoesNotRepeatMarkerOfPageReachedInsideTheList()
    {
        ListItem second = new("2)", ListLabelKind.ArabicParen, [new PageBreak(2), new TextRun("drugi")], []);
        string md = Render(Doc(List(1, 2, Num("1)", "pierwszy"), second), Para(2, "Dalej.")), pageMarkers: true);

        Assert.Equal("<!-- page: 1 -->\n- 1\\) pierwszy\n<!-- page: 2 -->\n- 2\\) drugi\n\nDalej.\n", md);
    }

    [Fact]
    public void Render_CommonPartParagraphIsIndentedAndSurroundedByBlankLines()
    {
        ListBlock nested = List(Letter("a)", "lit. a"), Letter("b)", "lit. b"));
        ListItem first = Num("1)", "w tym:", nested, Para(1, "część wspólna"));
        string md = Render(Doc(List(first, Num("2)", "inne"))));

        Assert.Equal(
            "- 1\\) w tym:\n  - a\\) lit. a\n  - b\\) lit. b\n\n  część wspólna\n\n- 2\\) inne\n",
            md);
    }

    [Fact]
    public void Render_CommonPartParagraphStartingOnNewPageGetsIndentedMarker()
    {
        ListBlock nested = List(Letter("a)", "lit. a"));
        var common = new ParagraphBlock(new PageRange(2, 2), [new PageBreak(2), new TextRun("część wspólna")]);
        string md = Render(Doc(List(1, 2, Num("1)", "w tym:", nested, common))), pageMarkers: true);

        Assert.Equal(
            "<!-- page: 1 -->\n- 1\\) w tym:\n  - a\\) lit. a\n\n  <!-- page: 2 -->\n  część wspólna\n",
            md);
    }

    [Fact]
    public void Render_ListBetweenParagraphsIsSeparatedByBlankLines()
    {
        string md = Render(Doc(Para(1, "Przed."), List(Num("1)", "a"), Num("2)", "b")), Para(1, "Po.")));

        Assert.Equal("Przed.\n\n- 1\\) a\n- 2\\) b\n\nPo.\n", md);
    }

    [Fact]
    public void Render_EmphasisAndFootnoteReferenceInsideItem()
    {
        ListItem item = new("1)", ListLabelKind.ArabicParen,
            [new TextRun("kredyt", TextStyle.Bold), new TextRun(" konsumencki"), new FootnoteRef(3)], []);
        string md = Render(Doc(List(item)));

        Assert.Equal("- 1\\) **kredyt** konsumencki[^3]\n", md);
    }

    [Fact]
    public void Render_ItemTextIsEscapedLikeParagraphText()
    {
        string md = Render(Doc(List(Num("1)", "- nie lista, a_b"))));

        Assert.Equal("- 1\\) \\- nie lista, a\\_b\n", md);
    }

    [Fact]
    public void Render_BiggerNestedSampleKeepsInvariants()
    {
        ListBlock inner = List(Letter("a)", "lit. a"), Letter("b)", "lit. b"));
        ListItem withCommon = Num("1)", "w tym:", inner, Para(1, "część wspólna"));
        ListItem withNested = Num("2)", "kolejny", List(Item("–", ListLabelKind.Dash, "tiret", List(Letter("c)", "głęboko")))));
        string md = Render(Doc(Para(1, "Wstęp:"), List(withCommon, withNested, Num("3)", "koniec")), Para(1, "Zakończenie.")));

        Assert.DoesNotContain("\n\n\n", md, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', md);
        Assert.EndsWith("\n", md, StringComparison.Ordinal);
        Assert.False(md.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.DoesNotMatch(new Regex(@"[ \t]+\n", RegexOptions.None, TimeSpan.FromSeconds(1)), md);
    }

    [Fact]
    public void Render_PageMarkersDisabledEmitsNoMarkersInLists()
    {
        ListItem second = new("2)", ListLabelKind.ArabicParen,
            [new PageBreak(2), new TextRun("drugi "), new PageBreak(3), new TextRun("koniec")], []);
        string md = Render(Doc(List(1, 3, Num("1)", "pierwszy"), second)));

        Assert.Equal("- 1\\) pierwszy\n- 2\\) drugi koniec\n", md);
    }
}
