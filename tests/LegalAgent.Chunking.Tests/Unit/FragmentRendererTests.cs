using LegalAgent.Chunking.Splitting;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T006: chunk content rendered by the parser's renderer (research R1, FR-204, FR-230, FR-233).</summary>
public sealed class FragmentRendererTests
{
    private static readonly Section Paragraph2 = Section(
        3,
        SectionKind.Paragraph,
        "§ 2",
        "§ 2.",
        2,
        3,
        [
            Para(2, 3, Text("Bank pobiera opłaty"), Ref(1), Text(" określone w taryfie,"), Break(3), Text("z zastrzeżeniem ust. 2.")),
            List(3, 3, Item("1)", ListLabelKind.ArabicParen, "przelew krajowy,", List(3, 3, Item("a)", ListLabelKind.LetterParen, "w oddziale,"))), Item("2)", ListLabelKind.ArabicParen, "przelew zagraniczny.")),
            Table(3, 3, Row("Czynność", "Stawka"), Row("Przelew", "5,00 zł"), Row("Wypłata | gotówki", "0,00 zł")),
            FallbackTable(3, 3, Row("Poz.", "Opis"), Row("1", "Opłata za kartę")),
            new SkippedPageBlock(new PageRange(3, 3), 3, SkipReason.NoTextLayer),
        ],
        [Footnote(1, "Taryfa obowiązuje od 1 lipca.", 2)]);

    private static readonly LegalDocument Document = Document(
        "Regulamin rachunku",
        [Para("Postanowienia wstępne regulaminu.")],
        Section(
            2,
            SectionKind.Chapter,
            "Rozdział 1",
            "Rozdział 1 Opłaty",
            1,
            3,
            [Para("Tekst rozdziału przed paragrafami.")],
            children: [Paragraph(1, 1, 1, Para("Postanowienie pierwsze.")), Paragraph2]));

    [Fact]
    public void Render_SectionMatchesItsPartOfTheWholeDocumentWithoutPageMarkers()
    {
        string whole = new MarkdownRenderer().Render(Document, new RenderingOptions { PageMarkers = false });
        Section unit = Document.Sections[0].Children[1];

        string fragment = Renderer().Render(unit, unit.Blocks, unit.Footnotes);

        string expected = whole[whole.IndexOf("### § 2.", StringComparison.Ordinal)..].TrimEnd('\n')
            .Replace("\n\n<!-- page 3 skipped: no-text-layer -->", string.Empty, StringComparison.Ordinal);
        Assert.Equal(expected, fragment);
        Assert.StartsWith("### § 2.\n\nBank pobiera opłaty[^1] określone w taryfie, z zastrzeżeniem ust. 2.\n\n- 1\\) przelew krajowy,\n  - a\\) w oddziale,\n- 2\\) przelew zagraniczny.\n\n| Czynność | Stawka |", fragment, StringComparison.Ordinal);
        Assert.EndsWith("[^1]: Taryfa obowiązuje od 1 lipca.", fragment, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NeverWritesPageMarkersTitleOrTrailingLineFeed()
    {
        Section unit = Document.Sections[0].Children[1];

        string fragment = Renderer(new RenderingOptions { PageMarkers = true }).Render(unit, unit.Blocks, unit.Footnotes);

        Assert.DoesNotContain("<!--", fragment, StringComparison.Ordinal);
        Assert.DoesNotContain("Regulamin rachunku", fragment, StringComparison.Ordinal);
        Assert.False(fragment.EndsWith('\n'));
    }

    [Fact]
    public void Render_PreambleHasNoHeading()
    {
        string fragment = Renderer().Render(null, Document.Preamble, []);

        Assert.Equal("Postanowienia wstępne regulaminu.", fragment);
    }

    [Fact]
    public void Render_OwnContentOfAParentSectionHasNoChildren()
    {
        Section chapter = Document.Sections[0];

        string fragment = Renderer().Render(chapter, chapter.Blocks, chapter.Footnotes);

        Assert.Equal("## Rozdział 1 Opłaty\n\nTekst rozdziału przed paragrafami.", fragment);
    }

    [Fact]
    public void Render_ListOfNestedItemsStartsAtColumnZero()
    {
        Section unit = Paragraph(5, 4, 4);
        ListBlock rest = List(4, 4, Item("b)", ListLabelKind.LetterParen, "w bankomacie,", List(4, 4, Item("-", ListLabelKind.Dash, "własnym,"))), Item("c)", ListLabelKind.LetterParen, "przez internet;"));
        ListBlock next = List(4, 4, Item("3)", ListLabelKind.ArabicParen, "wypłata gotówki."));

        string fragment = Renderer().Render(unit, [rest, next], []);

        Assert.Equal("### § 5.\n\n- b\\) w bankomacie,\n  - \\- własnym,\n- c\\) przez internet;\n\n- 3\\) wypłata gotówki.", fragment);
    }

    private static FragmentRenderer Renderer(RenderingOptions? options = null) =>
        new(new MarkdownRenderer(), options ?? new RenderingOptions());
}
