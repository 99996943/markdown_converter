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
}
