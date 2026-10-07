using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class DocumentBuildStageTests
{
    private static readonly SourceInfo Source = new("regulamin.pdf", 4, "Tytul z metadanych", 1234, new string('a', 64));

    private static LayoutBlock Paragraph(int first, int last, params Inline[] inlines)
    {
        var block = new LayoutBlock(LayoutBlockKind.Paragraph, new PageRange(first, last));
        foreach (Inline inline in inlines)
        {
            block.Inlines.Add(inline);
        }

        return block;
    }

    private static PipelineContext Context(IEnumerable<LayoutBlock> blocks, params (int Number, SkipReason? Skipped)[] pages)
    {
        var context = new PipelineContext(new PdfParserOptions(), Source, new ReportBuilder());
        foreach ((int number, SkipReason? skipped) in pages)
        {
            context.Pages.Add(new LayoutPage(number, 595, 842) { Skipped = skipped });
        }

        foreach (LayoutBlock b in blocks)
        {
            context.Blocks.Add(b);
        }

        return context;
    }

    [Fact]
    public void Order_IsDocumentBuild()
    {
        Assert.Equal(StageOrder.DocumentBuild, new DocumentBuildStage().Order);
    }

    [Fact]
    public void Execute_PutsAllParagraphsIntoThePreambleWithPageRangesAndSource()
    {
        PipelineContext context = Context(
            [Paragraph(1, 1, new TextRun("Pierwszy.")), Paragraph(1, 2, new TextRun("Drugi"), new PageBreak(2), new TextRun("dalej."))],
            (1, null),
            (2, null));

        new DocumentBuildStage().Execute(context);

        LegalDocument document = context.Document!;
        Assert.Same(Source, document.Source);
        Assert.Empty(document.Sections);
        Assert.Empty(document.PreambleFootnotes);
        Assert.Equal(2, document.Preamble.Count);

        ParagraphBlock first = Assert.IsType<ParagraphBlock>(document.Preamble[0]);
        Assert.Equal(new PageRange(1, 1), first.Pages);
        Assert.Equal(new Inline[] { new TextRun("Pierwszy.") }, first.Inlines.ToArray());

        ParagraphBlock second = Assert.IsType<ParagraphBlock>(document.Preamble[1]);
        Assert.Equal(new PageRange(1, 2), second.Pages);
        Assert.Equal(new Inline[] { new TextRun("Drugi"), new PageBreak(2), new TextRun("dalej.") }, second.Inlines.ToArray());
    }

    [Fact]
    public void Execute_TitleIsNullUntilHeadingDetectionExists()
    {
        PipelineContext context = Context([Paragraph(1, 1, new TextRun("Tekst."))], (1, null));

        new DocumentBuildStage().Execute(context);

        Assert.Null(context.Document!.Title);
    }

    [Fact]
    public void Execute_InsertsSkippedPageBlocksInPageOrder()
    {
        PipelineContext context = Context(
            [Paragraph(2, 2, new TextRun("Dwa.")), Paragraph(4, 4, new TextRun("Cztery."))],
            (1, SkipReason.NoTextLayer),
            (2, null),
            (3, SkipReason.PageReadError),
            (4, null));

        new DocumentBuildStage().Execute(context);

        IReadOnlyList<ContentBlock> blocks = context.Document!.Preamble;
        Assert.Equal(4, blocks.Count);
        Assert.Equal(new SkippedPageBlock(new PageRange(1, 1), 1, SkipReason.NoTextLayer), blocks[0]);
        Assert.IsType<ParagraphBlock>(blocks[1]);
        Assert.Equal(new SkippedPageBlock(new PageRange(3, 3), 3, SkipReason.PageReadError), blocks[2]);
        Assert.IsType<ParagraphBlock>(blocks[3]);
    }

    [Fact]
    public void Execute_AppendsTrailingSkippedPages()
    {
        PipelineContext context = Context(
            [Paragraph(1, 1, new TextRun("Jeden."))],
            (1, null),
            (2, SkipReason.NoTextLayer));

        new DocumentBuildStage().Execute(context);

        Assert.IsType<SkippedPageBlock>(context.Document!.Preamble[^1]);
    }

    [Fact]
    public void Execute_RejectsBlockKindsNotYetSupported()
    {
        PipelineContext context = Context(
            [new LayoutBlock(LayoutBlockKind.Table, new PageRange(1, 1))],
            (1, null));

        Assert.Throws<InvalidOperationException>(() => new DocumentBuildStage().Execute(context));
    }

    // ---- T058: section tree, paths, page ranges and footnote placement (FR-002, FR-026, FR-043, Clarifications Q1/Q5) ----

    private static LayoutBlock Head(int page, int level, SectionKind kind, string text, string? designation = null, string? number = null, string? title = null) =>
        new(LayoutBlockKind.Heading, new PageRange(page, page))
        {
            HeadingLevel = level,
            Heading = new HeadingInfo(level, kind, designation, number, title, text),
        };

    private static LayoutBlock Chapter(int page, int n, string title) =>
        Head(page, 2, SectionKind.Chapter, $"Rozdział {n}. {title}", $"Rozdział {n}", n.ToString(System.Globalization.CultureInfo.InvariantCulture), title);

    private static LayoutBlock Article(int page, string n, int level = 3) =>
        Head(page, level, SectionKind.Article, $"Art. {n}.", $"Art. {n}", n);

    private static FootnoteDraft Draft(int id, string label, int page, string text, bool orphan = false)
    {
        var draft = new FootnoteDraft(id, label, page) { IsOrphan = orphan };
        draft.Inlines.Add(new TextRun(text));
        return draft;
    }

    private static PipelineContext Built(IEnumerable<LayoutBlock> blocks, IEnumerable<FootnoteDraft>? footnotes = null, int pages = 4)
    {
        PipelineContext context = Context(blocks, Enumerable.Range(1, pages).Select(n => (n, (SkipReason?)null)).ToArray());
        foreach (FootnoteDraft draft in footnotes ?? [])
        {
            context.Footnotes.Add(draft);
        }

        new DocumentBuildStage().Execute(context);
        return context;
    }

    private static string Plain(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(i => i switch
    {
        TextRun run => run.Text,
        FootnoteRef r => $"[^{r.FootnoteNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)}]",
        _ => string.Empty,
    }));

    [Fact]
    public void Execute_BuildsTheSectionTreeFromHeadingLevels()
    {
        PipelineContext context = Built(
        [
            Paragraph(1, 1, new TextRun("Wstęp.")),
            Chapter(1, 1, "Przepisy ogólne"),
            Article(1, "1"), Paragraph(1, 1, new TextRun("Treść art. 1.")),
            Article(2, "2"), Paragraph(2, 3, new TextRun("Treść art. 2.")),
            Chapter(3, 2, "Przepisy końcowe"),
            Article(4, "3"), Paragraph(4, 4, new TextRun("Treść art. 3.")),
        ]);

        LegalDocument document = context.Document!;
        Assert.Equal("Wstęp.", Plain(((ParagraphBlock)Assert.Single(document.Preamble)).Inlines));
        Assert.Equal(["Rozdział 1. Przepisy ogólne", "Rozdział 2. Przepisy końcowe"], document.Sections.Select(s => s.HeadingText));

        Section first = document.Sections[0];
        Assert.Equal((2, SectionKind.Chapter, "Rozdział 1", "1", "Przepisy ogólne"), (first.Level, first.Kind, first.Designation, first.Number, first.Title));
        Assert.Empty(first.Blocks);
        Assert.Equal(["Art. 1.", "Art. 2."], first.Children.Select(c => c.HeadingText));
        Assert.Equal(new PageRange(1, 3), first.Pages);

        Section art2 = first.Children[1];
        Assert.Equal((3, SectionKind.Article, "Art. 2", "2"), (art2.Level, art2.Kind, art2.Designation, art2.Number));
        Assert.Equal("Treść art. 2.", Plain(((ParagraphBlock)Assert.Single(art2.Blocks)).Inlines));
        Assert.Empty(art2.Children);
        Assert.Equal(new PageRange(2, 3), art2.Pages);
        Assert.Equal(["Rozdział 1. Przepisy ogólne", "Art. 2."], art2.Path);

        Assert.Equal(["Rozdział 2. Przepisy końcowe", "Art. 3."], document.Sections[1].Children[0].Path);
        Assert.Equal(new PageRange(3, 4), document.Sections[1].Pages);
    }

    [Fact]
    public void Execute_DocumentTitleBecomesTheTitle_NotASection()
    {
        PipelineContext context = Built(
        [
            Head(1, 1, SectionKind.DocumentTitle, "USTAWA o usługach płatniczych"),
            Paragraph(1, 1, new TextRun("z dnia 1 stycznia 2026 r.")),
            Article(1, "1", level: 2), Paragraph(1, 1, new TextRun("Treść.")),
        ]);

        LegalDocument document = context.Document!;
        Assert.Equal("USTAWA o usługach płatniczych", document.Title);
        Assert.Equal("z dnia 1 stycznia 2026 r.", Plain(((ParagraphBlock)Assert.Single(document.Preamble)).Inlines));
        Assert.Equal("Art. 1.", Assert.Single(document.Sections).HeadingText);
        Assert.Equal(["Art. 1."], document.Sections[0].Path);
    }

    [Fact]
    public void Execute_FootnotesAreNumberedByFirstReference_AndPlacedInTheSmallestReferencingSection()
    {
        // Draft 0 („1)”) is first referenced in Art. 2, draft 1 („2)”) already in Art. 1 → global numbers 2 and 1.
        PipelineContext context = Built(
            [
                Chapter(1, 1, "Przepisy ogólne"),
                Article(1, "1"), Paragraph(1, 1, new TextRun("Pierwszy"), new FootnoteRef(1), new TextRun(" artykuł.")),
                Article(2, "2"), Paragraph(2, 2, new TextRun("Drugi"), new FootnoteRef(0), new TextRun(" artykuł.")),
                Article(3, "3"), Paragraph(3, 3, new TextRun("Trzeci"), new FootnoteRef(0), new TextRun(" artykuł.")),
            ],
            [Draft(0, "1)", 2, "Przypis o dyrektywie."), Draft(1, "2)", 1, "Przypis o ministrze.")]);

        Section chapter = context.Document!.Sections[0];
        Section art1 = chapter.Children[0];
        Section art2 = chapter.Children[1];
        Section art3 = chapter.Children[2];

        Assert.Equal("Pierwszy[^1] artykuł.", Plain(((ParagraphBlock)art1.Blocks[0]).Inlines));
        Assert.Equal("Drugi[^2] artykuł.", Plain(((ParagraphBlock)art2.Blocks[0]).Inlines));
        Assert.Equal("Trzeci[^2] artykuł.", Plain(((ParagraphBlock)art3.Blocks[0]).Inlines));

        Footnote note1 = Assert.Single(art1.Footnotes);
        Assert.Equal((1, "2)", "Przypis o ministrze.", 1, false), (note1.Number, note1.OriginalLabel, Plain(note1.Inlines), note1.Page, note1.IsOrphan));
        Footnote note2 = Assert.Single(art2.Footnotes);
        Assert.Equal((2, "1)"), (note2.Number, note2.OriginalLabel));
        Assert.Empty(art3.Footnotes); // a repeated reference does not duplicate the definition
        Assert.Empty(chapter.Footnotes);
        Assert.Equal(2, context.Report.Build(4, TimeSpan.Zero).FootnoteCount);
    }

    [Fact]
    public void Execute_ReferenceBeforeTheFirstSection_PutsTheDefinitionIntoThePreambleFootnotes()
    {
        PipelineContext context = Built(
            [
                Head(1, 1, SectionKind.DocumentTitle, "OBWIESZCZENIE MINISTRA"),
                Paragraph(1, 1, new TextRun("Minister"), new FootnoteRef(0), new TextRun(" ogłasza.")),
                Article(2, "1", level: 2), Paragraph(2, 2, new TextRun("Treść.")),
            ],
            [Draft(0, "1)", 1, "Minister kieruje działem.")]);

        LegalDocument document = context.Document!;
        Footnote note = Assert.Single(document.PreambleFootnotes);
        Assert.Equal((1, "Minister kieruje działem."), (note.Number, Plain(note.Inlines)));
        Assert.Equal("Minister[^1] ogłasza.", Plain(((ParagraphBlock)document.Preamble[0]).Inlines));
        Assert.Empty(document.Sections[0].Footnotes);
    }

    [Fact]
    public void Execute_OrphanFootnote_GoesToTheSectionOpenOnItsPage_NumberedAfterReferencedOnes()
    {
        PipelineContext context = Built(
            [
                Article(1, "1", level: 2), Paragraph(1, 1, new TextRun("Pierwszy"), new FootnoteRef(0), new TextRun(".")),
                Article(2, "2", level: 2), Paragraph(2, 3, new TextRun("Drugi.")),
            ],
            [Draft(1, "*", 3, "Przypis bez odnośnika.", orphan: true), Draft(0, "1)", 1, "Zwykły przypis.")]);

        LegalDocument document = context.Document!;
        Assert.Equal(1, Assert.Single(document.Sections[0].Footnotes).Number);
        Footnote orphan = Assert.Single(document.Sections[1].Footnotes);
        Assert.Equal((2, "*", true), (orphan.Number, orphan.OriginalLabel, orphan.IsOrphan));
    }

    [Fact]
    public void Execute_ReportsHeadingCountsPerLevel()
    {
        PipelineContext context = Built(
        [
            Chapter(1, 1, "Przepisy ogólne"), Article(1, "1"), Article(1, "2"),
            Chapter(2, 2, "Przepisy końcowe"), Article(2, "3"),
        ]);

        IReadOnlyDictionary<int, int> counts = context.Report.Build(4, TimeSpan.Zero).HeadingCounts;
        Assert.Equal([(2, 2), (3, 3)], counts.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)));
    }
}
