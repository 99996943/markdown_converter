using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>T076 — table rendering (contracts/markdown-output.md).</summary>
public sealed class MarkdownRendererTableTests
{
    private static readonly SourceInfo Source = new("test.pdf", 5, null, 100, new string('0', 64));

    private static TableCell C(string text, int span = 1) => new([new TextRun(text)], span);

    private static TableRow R(params string[] cells) => new(cells.Select(c => C(c)).ToList());

    private static TableBlock Table(int first, int last, TableRow? header, int columns, bool fallback, params TableRow[] rows) =>
        new(new PageRange(first, last), header, rows, columns, fallback);

    private static ParagraphBlock Para(int page, string text) =>
        new(new PageRange(page, page), [new TextRun(text)]);

    private static string Render(bool pageMarkers, params ContentBlock[] blocks)
    {
        var doc = new LegalDocument(Source, null, blocks, [], []);
        return new MarkdownRenderer().Render(doc, new RenderingOptions { PageMarkers = pageMarkers });
    }

    [Fact]
    public void Render_GfmTableWithHeader()
    {
        string md = Render(false, Table(1, 1, R("Usługa", "Opłata"), 2, false, R("Rachunek", "0 zł"), R("Karta", "5 zł")));

        Assert.Equal("| Usługa | Opłata |\n| --- | --- |\n| Rachunek | 0 zł |\n| Karta | 5 zł |\n", md);
    }

    [Fact]
    public void Render_TableWithoutHeaderUsesFirstRowAsHeader()
    {
        string md = Render(false, Table(1, 1, null, 2, false, R("A", "B"), R("C", "D")));

        Assert.Equal("| A | B |\n| --- | --- |\n| C | D |\n", md);
    }

    [Fact]
    public void Render_PipeInCellIsEscaped()
    {
        string md = Render(false, Table(1, 1, R("X", "Y"), 2, false, R("a|b", "c")));

        Assert.Equal("| X | Y |\n| --- | --- |\n| a\\|b | c |\n", md);
    }

    [Fact]
    public void Render_EmphasisAndFootnoteRefInCell()
    {
        var cell = new TableCell([new TextRun("zmiana", TextStyle.Bold), new TextRun(" opłaty"), new FootnoteRef(2)]);
        var row = new TableRow([cell, C("x")]);
        string md = Render(false, Table(1, 1, R("H1", "H2"), 2, false, row));

        Assert.Equal("| H1 | H2 |\n| --- | --- |\n| **zmiana** opłaty[^2] | x |\n", md);
    }

    [Fact]
    public void Render_ColumnSpanAddsEmptyCells()
    {
        var section = new TableRow([C("I. Czynności", 3)]);
        string md = Render(false, Table(1, 1, R("A", "B", "C"), 3, false, section, R("1", "2", "3")));

        Assert.Equal("| A | B | C |\n| --- | --- | --- |\n| I. Czynności |  |  |\n| 1 | 2 | 3 |\n", md);
    }

    [Fact]
    public void Render_ShortRowIsPadded()
    {
        string md = Render(false, Table(1, 1, R("A", "B", "C"), 3, false, R("1")));

        Assert.Equal("| A | B | C |\n| --- | --- | --- |\n| 1 |  |  |\n", md);
    }

    [Fact]
    public void Render_TableSpanningPagesHasNoInnerMarkerAndNextMarkerFollows()
    {
        string md = Render(
            true,
            Para(4, "Przed"),
            Table(4, 5, R("A", "B"), 2, false, R("1", "2")),
            Para(5, "Po"));

        Assert.Equal("<!-- page: 4 -->\nPrzed\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n<!-- page: 5 -->\nPo\n", md);
    }

    [Fact]
    public void Render_MarkerBeforeTableStartingOnNewPage()
    {
        string md = Render(true, Para(1, "Przed"), Table(2, 2, R("A", "B"), 2, false, R("1", "2")));

        Assert.Equal("<!-- page: 1 -->\nPrzed\n\n<!-- page: 2 -->\n| A | B |\n| --- | --- |\n| 1 | 2 |\n", md);
    }

    [Fact]
    public void Render_FallbackRowsAreSeparateParagraphs()
    {
        string md = Render(false, Table(1, 1, R("Prowadzenie rachunku", "0 zł", "miesięcznie"), 3, true, R("Karta", "", "5 zł")));

        Assert.Equal("Prowadzenie rachunku \\| 0 zł \\| miesięcznie\n\nKarta \\| 5 zł\n", md);
        Assert.DoesNotContain("\n|", "\n" + md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_FallbackFirstCellGetsLineStartEscaping()
    {
        string md = Render(false, Table(1, 1, null, 2, true, R("2024. r.", "x")));

        Assert.Equal("2024\\. r. \\| x\n", md);
    }

    [Fact]
    public void Render_FallbackMarkerOnlyBeforeFirstRow()
    {
        string md = Render(true, Table(2, 2, null, 2, true, R("a", "b"), R("c", "d")));

        Assert.Equal("<!-- page: 2 -->\na \\| b\n\nc \\| d\n", md);
    }

    [Fact]
    public void Render_PageMarkersDisabledWritesNone()
    {
        string md = Render(false, Para(1, "P"), Table(2, 3, R("A", "B"), 2, false, R("1", "2")), Para(3, "Q"));

        Assert.DoesNotContain("<!--", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_AmountsAreUnchanged()
    {
        string md = Render(false, Table(1, 1, R("Kwota", "Stawka", "Opis"), 3, false, R("0,00 zł", "1,5%", "min. 10 zł")));

        Assert.Equal("| Kwota | Stawka | Opis |\n| --- | --- | --- |\n| 0,00 zł | 1,5% | min. 10 zł |\n", md);
    }
}
