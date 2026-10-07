using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>T059 — headings, section order and footnotes in Markdown (contracts/markdown-output.md, invariants 3 and 4).</summary>
public sealed partial class MarkdownRendererSectionTests
{
    private static readonly SourceInfo Source = new("ustawa.pdf", 3, null, 0, new string('0', 64));

    private static ParagraphBlock P(int page, params Inline[] inlines) => new(new PageRange(page, page), inlines);

    private static Section S(
        int level,
        SectionKind kind,
        string text,
        int first,
        int last,
        IReadOnlyList<ContentBlock>? blocks = null,
        IReadOnlyList<Section>? children = null,
        IReadOnlyList<Footnote>? footnotes = null,
        IReadOnlyList<string>? path = null) =>
        new(level, kind, null, null, null, text, path ?? [text], new PageRange(first, last), blocks ?? [], footnotes ?? [], children ?? []);

    private static Footnote F(int number, string text, int page) => new(number, $"{number})", [new TextRun(text)], page, false);

    private static LegalDocument Ustawa() => new(
        Source,
        "USTAWA z dnia 1 stycznia 2026 r. o usługach testowych",
        [P(1, new TextRun("Preambuła"), new FootnoteRef(1), new TextRun("."))],
        [F(1, "Przypis do preambuły.", 1)],
        [
            S(2, SectionKind.Chapter, "Rozdział 1. Przepisy ogólne", 1, 2, children:
            [
                S(3, SectionKind.Article, "Art. 1.", 1, 1, blocks: [P(1, new TextRun("Treść art. 1"), new FootnoteRef(2), new TextRun("."))], footnotes: [F(2, "Przypis do art. 1.", 1)]),
                S(3, SectionKind.Article, "Art. 2.", 2, 2, blocks: [P(2, new TextRun("Treść art. 2"), new FootnoteRef(2), new TextRun("."))]),
            ]),
            S(2, SectionKind.Chapter, "Rozdział 2. Przepisy końcowe", 3, 3, children:
            [
                S(3, SectionKind.Article, "Art. 3.", 3, 3, blocks: [P(3, new TextRun("Treść art. 3."))]),
            ]),
        ]);

    private static string Render(LegalDocument document, bool pageMarkers = false) =>
        new MarkdownRenderer().Render(document, new PdfParserOptions { Rendering = { PageMarkers = pageMarkers } }.Rendering);

    [Fact]
    public void Render_WritesTitlePreambleSectionsAndFootnotesInContractOrder()
    {
        string expected = string.Join("\n\n",
            "# USTAWA z dnia 1 stycznia 2026 r. o usługach testowych",
            "Preambuła[^1].",
            "[^1]: Przypis do preambuły.",
            "## Rozdział 1. Przepisy ogólne",
            "### Art. 1.",
            "Treść art. 1[^2].",
            "[^2]: Przypis do art. 1.",
            "### Art. 2.",
            "Treść art. 2[^2].",
            "## Rozdział 2. Przepisy końcowe",
            "### Art. 3.",
            "Treść art. 3.") + "\n";

        Assert.Equal(expected, Render(Ustawa()));
    }

    [Fact]
    public void Render_HeadingLevelIsTheSectionLevel()
    {
        LegalDocument document = new(Source, null, [], [], [S(4, SectionKind.Article, "Art. 9.", 1, 1)]);

        Assert.Equal("#### Art. 9.\n", Render(document));
    }

    [Fact]
    public void Render_PageMarkerPrecedesASectionStartingOnANewPage()
    {
        string markdown = Render(Ustawa(), pageMarkers: true);

        Assert.Contains("<!-- page: 2 -->\n### Art. 2.", markdown, StringComparison.Ordinal);
        Assert.Contains("<!-- page: 3 -->\n## Rozdział 2. Przepisy końcowe", markdown, StringComparison.Ordinal);
    }

    // Invariant 3: every [^n] has exactly one definition [^n]: and numbering is continuous from 1.
    [Fact]
    public void Render_EveryFootnoteReferenceHasExactlyOneDefinition_NumberedFromOne()
    {
        string markdown = Render(Ustawa());

        int[] references = FootnoteReference().Matches(markdown).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
        int[] definitions = FootnoteDefinition().Matches(markdown).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        Assert.Equal(Enumerable.Range(1, references.Length), references);
        Assert.Equal(references, definitions.Order());
        Assert.Equal(definitions.Length, definitions.Distinct().Count());
    }

    // Invariant 4: a heading level grows by at most one relative to the previous heading.
    [Fact]
    public void Render_HeadingLevelsNeverJumpByMoreThanOne()
    {
        string markdown = Render(Ustawa());

        int[] levels = HeadingLine().Matches(markdown).Select(m => m.Groups[1].Value.Length).ToArray();
        Assert.All(levels.Zip(levels.Skip(1)), pair => Assert.True(pair.Second <= pair.First + 1, $"{pair.First} → {pair.Second}"));
    }

    [Fact]
    public void Render_EscapesMarkdownInHeadingText()
    {
        LegalDocument document = new(Source, null, [], [], [S(2, SectionKind.Typographic, "Opłaty *promocyjne* [2026]", 1, 1)]);

        Assert.Equal("## Opłaty \\*promocyjne\\* \\[2026\\]\n", Render(document));
    }

    [GeneratedRegex(@"\[\^(\d+)\](?!:)")]
    private static partial Regex FootnoteReference();

    [GeneratedRegex(@"^\[\^(\d+)\]:", RegexOptions.Multiline)]
    private static partial Regex FootnoteDefinition();

    [GeneratedRegex(@"^(#{1,6}) ", RegexOptions.Multiline)]
    private static partial Regex HeadingLine();
}
