using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

public sealed class MarkdownRendererParagraphTests
{
    private static readonly SourceInfo Source = new("test.pdf", 5, null, 100, new string('0', 64));

    private static ParagraphBlock Para(int first, int last, params Inline[] inlines) =>
        new(new PageRange(first, last), inlines);

    private static ParagraphBlock Para(int page, string text) => Para(page, page, new TextRun(text));

    private static LegalDocument Doc(params ContentBlock[] blocks) =>
        new(Source, null, blocks, [], []);

    private static string Render(LegalDocument doc, Action<RenderingOptions>? configure = null)
    {
        var options = new RenderingOptions();
        configure?.Invoke(options);
        var renderer = new MarkdownRenderer();
        return renderer.Render(doc, options);
    }

    [Fact]
    public void Render_SeparatesParagraphsWithExactlyOneBlankLineAndEndsWithSingleLf()
    {
        string md = Render(Doc(Para(1, "Pierwszy akapit."), Para(1, "Drugi akapit.")), o => o.PageMarkers = false);

        Assert.Equal("Pierwszy akapit.\n\nDrugi akapit.\n", md);
    }

    [Fact]
    public void Render_UsesLfOnly()
    {
        string md = Render(Doc(Para(1, "A."), Para(1, "B.")));

        Assert.DoesNotContain('\r', md);
    }

    [Fact]
    public void Render_EmptyDocumentIsEmptyString()
    {
        Assert.Equal(string.Empty, Render(Doc()));
    }

    [Fact]
    public void Render_UsesDefaultOptionsWhenNoneAreGiven()
    {
        string md = new MarkdownRenderer().Render(Doc(Para(1, "Tekst.")));

        Assert.Equal("<!-- page: 1 -->\nTekst.\n", md);
    }

    [Fact]
    public void Render_UsesConstructorOptionsAsDefaults()
    {
        var renderer = new MarkdownRenderer(new RenderingOptions { PageMarkers = false });

        Assert.Equal("Tekst.\n", renderer.Render(Doc(Para(1, "Tekst."))));
    }

    [Theory]
    [InlineData(TextStyle.Bold, "**tekst**")]
    [InlineData(TextStyle.Italic, "*tekst*")]
    [InlineData(TextStyle.Bold | TextStyle.Italic, "***tekst***")]
    public void Render_WrapsStyledRunsInEmphasisMarkers(TextStyle style, string expected)
    {
        string md = Render(Doc(Para(1, 1, new TextRun("tekst", style))), o => o.PageMarkers = false);

        Assert.Equal(expected + "\n", md);
    }

    [Fact]
    public void Render_MovesWhitespaceOutsideEmphasisMarkers()
    {
        ParagraphBlock p = Para(1, 1, new TextRun("Art. 5. ", TextStyle.Bold), new TextRun("Bank"));
        string md = Render(Doc(p), o => o.PageMarkers = false);

        Assert.Equal("**Art. 5.** Bank\n", md);
    }

    [Fact]
    public void Render_DoesNotDoubleSpacesAroundStyledRun()
    {
        ParagraphBlock p = Para(1, 1, new TextRun("a "), new TextRun(" b ", TextStyle.Bold), new TextRun("c"));
        string md = Render(Doc(p), o => o.PageMarkers = false);

        Assert.Equal("a **b** c\n", md);
    }

    [Fact]
    public void Render_WhitespaceOnlyStyledRunGetsNoMarkers()
    {
        ParagraphBlock p = Para(1, 1, new TextRun("a"), new TextRun(" ", TextStyle.Bold), new TextRun("b"));
        string md = Render(Doc(p), o => o.PageMarkers = false);

        Assert.Equal("a b\n", md);
    }

    [Fact]
    public void Render_MergesAdjacentRunsOfTheSameStyleBeforeWrapping()
    {
        ParagraphBlock p = Para(1, 1, new TextRun("raz ", TextStyle.Bold), new TextRun("dwa", TextStyle.Bold));
        string md = Render(Doc(p), o => o.PageMarkers = false);

        Assert.Equal("**raz dwa**\n", md);
    }

    [Fact]
    public void Render_EmphasisInlineFalseEmitsPlainText()
    {
        ParagraphBlock p = Para(1, 1, new TextRun("Art. 5. ", TextStyle.Bold), new TextRun("Bank", TextStyle.Italic));
        string md = Render(Doc(p), o =>
        {
            o.PageMarkers = false;
            o.EmphasisInline = false;
        });

        Assert.Equal("Art. 5. Bank\n", md);
    }

    [Theory]
    [InlineData("2024. r. wchodzi w zycie", "2024\\. r. wchodzi w zycie")]
    [InlineData("# nie naglowek", "\\# nie naglowek")]
    [InlineData("- nie lista", "\\- nie lista")]
    [InlineData("a * b _ c [d] <e>", "a \\* b \\_ c \\[d\\] \\<e\\>")]
    public void Render_EscapesMarkdownSpecialCharacters(string text, string expected)
    {
        string md = Render(Doc(Para(1, text)), o => o.PageMarkers = false);

        Assert.Equal(expected + "\n", md);
    }

    [Fact]
    public void Render_EmitsPageMarkerBeforeFirstBlockOfEachPageOnItsOwnLine()
    {
        string md = Render(Doc(Para(1, "Pierwszy."), Para(1, "Drugi na tej samej stronie."), Para(2, "Trzeci.")));

        Assert.Equal(
            "<!-- page: 1 -->\nPierwszy.\n\nDrugi na tej samej stronie.\n\n<!-- page: 2 -->\nTrzeci.\n",
            md);
    }

    [Fact]
    public void Render_EmitsInlineMarkerBetweenWordsWhereAParagraphCrossesAPage()
    {
        ParagraphBlock crossing = Para(1, 2, new TextRun("do dnia zawarcia"), new PageBreak(2), new TextRun("umowy przez strony."));
        string md = Render(Doc(crossing, Para(2, "Nastepny.")));

        Assert.Equal(
            "<!-- page: 1 -->\ndo dnia zawarcia <!-- page: 2 --> umowy przez strony.\n\nNastepny.\n",
            md);
    }

    [Fact]
    public void Render_PageMarkersFalseEmitsNoMarkersAndKeepsWordsSeparated()
    {
        ParagraphBlock crossing = Para(1, 2, new TextRun("do dnia zawarcia"), new PageBreak(2), new TextRun("umowy przez strony."));
        string md = Render(Doc(crossing, Para(3, "Trzeci.")), o => o.PageMarkers = false);

        Assert.Equal("do dnia zawarcia umowy przez strony.\n\nTrzeci.\n", md);
        Assert.DoesNotContain("<!--", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RendersTitleAndFootnoteReferences()
    {
        var doc = new LegalDocument(
            Source,
            "Regulamin rachunku",
            [Para(1, 1, new TextRun("Ustawa"), new FootnoteRef(1), new TextRun(" wchodzi w zycie."))],
            [new Footnote(1, "1)", [new TextRun("Wdraza dyrektywe.")], 1, false)],
            []);

        string md = Render(doc, o => o.PageMarkers = false);

        Assert.Equal("# Regulamin rachunku\n\nUstawa[^1] wchodzi w zycie.\n\n[^1]: Wdraza dyrektywe.\n", md);
    }

    [Fact]
    public void Render_RendersSectionHeadingsRecursivelyWithFootnotesAfterTheSectionContent()
    {
        var child = new Section(
            3, SectionKind.Article, "Art. 1.", "1", null, "Art. 1.", ["Rozdzial 1. Ogolne", "Art. 1."],
            new PageRange(1, 1), [Para(1, "Tresc artykulu.")],
            [new Footnote(1, "1)", [new TextRun("Przypis.")], 1, false)], []);
        var chapter = new Section(
            2, SectionKind.Chapter, "Rozdzial 1", "1", "Ogolne", "Rozdzial 1. Ogolne", ["Rozdzial 1. Ogolne"],
            new PageRange(1, 1), [], [], [child]);
        var doc = new LegalDocument(Source, null, [], [], [chapter]);

        string md = Render(doc, o => o.PageMarkers = false);

        Assert.Equal("## Rozdzial 1. Ogolne\n\n### Art. 1.\n\nTresc artykulu.\n\n[^1]: Przypis.\n", md);
    }

    [Fact]
    public void Render_SkippedPageBlocksAreAlwaysEmittedAsComments()
    {
        var doc = Doc(
            Para(1, "Tekst."),
            new SkippedPageBlock(new PageRange(2, 2), 2, SkipReason.NoTextLayer),
            new SkippedPageBlock(new PageRange(3, 3), 3, SkipReason.PageReadError),
            Para(4, "Dalej."));

        string md = Render(doc, o => o.PageMarkers = false);

        Assert.Equal(
            "Tekst.\n\n<!-- page 2 skipped: no-text-layer -->\n\n<!-- page 3 skipped: read-error -->\n\nDalej.\n",
            md);
    }

    [Fact]
    public void Render_SatisfiesInvariantsOneTwoAndFive()
    {
        var doc = Doc(
            Para(1, "Pierwszy akapit, ktory jest dlugi."),
            Para(1, 2, new TextRun("Akapit "), new TextRun("pogrubiony ", TextStyle.Bold), new TextRun("przechodzi"), new PageBreak(2), new TextRun("na kolejna strone.")),
            Para(2, "Trzeci."),
            Para(3, "Czwarty."));

        string withMarkers = Render(doc);
        string without = Render(doc, o => o.PageMarkers = false);

        // Invariant 1: no two consecutive blank lines.
        Assert.DoesNotContain("\n\n\n", withMarkers, StringComparison.Ordinal);
        // Invariant 2: no line ends with a space.
        Assert.All(withMarkers.Split('\n'), line => Assert.False(line.EndsWith(' '), $"Line ends with space: '{line}'"));
        // Invariant 5: stripping the comments yields the output without markers.
        string stripped = Regex.Replace(withMarkers, @"^<!-- page: \d+ -->\n", string.Empty, RegexOptions.Multiline);
        stripped = Regex.Replace(stripped, @" ?<!-- page: \d+ --> ?", " ");
        Assert.Equal(without, stripped);
        Assert.EndsWith("\n", withMarkers, StringComparison.Ordinal);
        Assert.False(withMarkers.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_IsDeterministic()
    {
        LegalDocument doc = Doc(Para(1, "A."), Para(2, "B."));

        Assert.Equal(Render(doc), Render(doc));
    }
}
