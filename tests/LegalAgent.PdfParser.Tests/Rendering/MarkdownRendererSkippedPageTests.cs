using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

/// <summary>T085 — skipped pages are marked where they occur (FR-071, FR-009a), also without page markers.</summary>
public sealed class MarkdownRendererSkippedPageTests
{
    private static readonly SourceInfo Source = new("skan.pdf", 4, null, 0, new string('0', 64));

    private static ParagraphBlock P(int page, string text) => new(new PageRange(page, page), [new TextRun(text)]);

    private static SkippedPageBlock Skipped(int page, SkipReason reason) => new(new PageRange(page, page), page, reason);

    private static string Render(LegalDocument document, bool pageMarkers) =>
        new MarkdownRenderer().Render(document, new RenderingOptions { PageMarkers = pageMarkers });

    [Fact]
    public void SkippedPages_AreMarkedAtTheirPosition_WithReason()
    {
        var document = new LegalDocument(
            Source,
            null,
            [P(1, "Pierwsza strona."), Skipped(2, SkipReason.NoTextLayer), Skipped(3, SkipReason.PageReadError), P(4, "Czwarta strona.")],
            [],
            []);

        Assert.Equal(
            "<!-- page: 1 -->\nPierwsza strona.\n\n<!-- page 2 skipped: no-text-layer -->\n\n"
                + "<!-- page 3 skipped: read-error -->\n\n<!-- page: 4 -->\nCzwarta strona.\n",
            Render(document, pageMarkers: true));
    }

    [Fact]
    public void SkippedPageMarkers_AreWrittenEvenWithoutPageMarkers()
    {
        var document = new LegalDocument(Source, null, [P(1, "Tekst."), Skipped(2, SkipReason.NoTextLayer)], [], []);

        Assert.Equal("Tekst.\n\n<!-- page 2 skipped: no-text-layer -->\n", Render(document, pageMarkers: false));
    }

    [Fact]
    public void SkippedPageInsideASection_IsRenderedAmongItsBlocks()
    {
        var section = new Section(
            2,
            SectionKind.Article,
            "Art. 1",
            "1",
            null,
            "Art. 1.",
            ["Art. 1."],
            new PageRange(1, 3),
            [P(1, "Treść."), Skipped(2, SkipReason.PageReadError), P(3, "Dalej.")],
            [],
            []);

        string md = Render(new LegalDocument(Source, null, [], [], [section]), pageMarkers: false);

        Assert.Equal("## Art. 1.\n\nTreść.\n\n<!-- page 2 skipped: read-error -->\n\nDalej.\n", md);
    }
}
