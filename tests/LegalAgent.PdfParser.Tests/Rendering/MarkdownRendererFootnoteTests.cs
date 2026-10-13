using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>Spec 012 T005 — the renderer writes <c>Identifier</c> of footnotes and references (contract 2.0.0).</summary>
public sealed class MarkdownRendererFootnoteTests
{
    private static readonly SourceInfo Source = new("regulamin.pdf", 1, null, 0, new string('0', 64));

    private static string Render(IReadOnlyList<ContentBlock> preamble, params Footnote[] footnotes) =>
        new MarkdownRenderer()
            .Render(new LegalDocument(Source, null, preamble, footnotes, []), new PdfParserOptions { Rendering = { PageMarkers = false } }.Rendering)
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static ParagraphBlock P(params Inline[] inlines) => new(new PageRange(1, 1), inlines);

    private static Footnote F(int number, string label, string identifier, string text) =>
        new(number, label, [new TextRun(text)], 1, false) { Identifier = identifier };

    [Fact]
    public void Render_WritesTheIdentifierOfReferenceAndDefinition()
    {
        string markdown = Render([P(new TextRun("TAK"), new FootnoteRef(3) { Identifier = "21" })], F(3, "21", "21", "Funkcja wkrótce."));

        Assert.Equal("TAK[^21]\n\n[^21]: Funkcja wkrótce.\n", markdown);
    }

    [Fact]
    public void Render_StarLabel_IsWrittenAsIs_WithoutEscapingOrEmphasis()
    {
        string markdown = Render([P(new TextRun("ustawy"), new FootnoteRef(1) { Identifier = "*)" })], F(1, "*)", "*)", "Ogłoszono w dzienniku."));

        Assert.Equal("ustawy[^*)]\n\n[^*)]: Ogłoszono w dzienniku.\n", markdown);
    }

    [Fact]
    public void Render_SuffixedIdentifier()
    {
        string markdown = Render([P(new TextRun("art. 5"), new FootnoteRef(9) { Identifier = "1)-2" })], F(9, "1)", "1)-2", "Druga seria."));

        Assert.Equal("art. 5[^1)-2]\n\n[^1)-2]: Druga seria.\n", markdown);
    }

    [Fact]
    public void Render_EmptyIdentifier_FallsBackToTheNumber()
    {
        string markdown = Render([P(new TextRun("Ustawa"), new FootnoteRef(4))], new Footnote(4, "1)", [new TextRun("Przypis.")], 1, false));

        Assert.Equal("Ustawa[^4]\n\n[^4]: Przypis.\n", markdown);
    }

    [Fact]
    public void Render_ReferenceInAGfmTableCell()
    {
        var table = new TableBlock(
            new PageRange(1, 1),
            new TableRow([new TableCell([new TextRun("czynność")]), new TableCell([new TextRun("serwis")])]),
            [new TableRow([new TableCell([new TextRun("wniosek")]), new TableCell([new TextRun("TAK"), new FootnoteRef(2) { Identifier = "21" }])])],
            2,
            false);

        string markdown = Render([table], F(2, "21", "21", "Funkcja wkrótce."));

        Assert.Contains("| wniosek | TAK[^21] |", markdown, StringComparison.Ordinal);
        Assert.EndsWith("[^21]: Funkcja wkrótce.\n", markdown, StringComparison.Ordinal);
    }
}
