using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// T080 — tables found by table detection are placed in the block flow at their first line (once, also when continued
/// on the next page) and reach the document model and the report.
/// </summary>
public sealed class BlockAssemblyTableTests
{
    private static TableBlock Table(int first, int last, bool fallback = false) => new(
        new PageRange(first, last),
        null,
        [new TableRow([new TableCell([new TextRun("Usluga")]), new TableCell([new TextRun("0 zl")])])],
        2,
        fallback);

    private static LayoutLine TableLine(string text, double top, int index = 0)
    {
        LayoutLine line = LayoutFactory.Line(text, 71, top);
        line.Role = LineRole.Table;
        line.Annotations[LayoutAnnotations.TableIndex] = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return line;
    }

    private static PipelineContext Context(params LayoutPage[] pages)
    {
        PipelineContext context = LayoutFactory.Context(pages);
        context.BodyStyle = new BodyStyle(10, 20);
        return context;
    }

    [Fact]
    public void TableLines_BecomeOneTableBlockBetweenParagraphs()
    {
        PipelineContext context = Context(LayoutFactory.Page(1,
        [
            LayoutFactory.Line("Tabela oplat:", 71, 100),
            TableLine("Usluga 0 zl", 120),
            TableLine("Przelew 1 zl", 140),
            LayoutFactory.Line("Tekst po tabeli.", 71, 180),
        ]));
        context.Tables.Add(new LayoutBlock(LayoutBlockKind.Table, new PageRange(1, 1)) { Table = Table(1, 1) });

        new BlockAssemblyStage().Execute(context);

        Assert.Equal([LayoutBlockKind.Paragraph, LayoutBlockKind.Table, LayoutBlockKind.Paragraph], context.Blocks.Select(b => b.Kind));
        Assert.Same(context.Tables[0], context.Blocks[1]);
    }

    [Fact]
    public void TableContinuedOnTheNextPage_IsEmittedOnce()
    {
        PipelineContext context = Context(
            LayoutFactory.Page(1, [TableLine("Usluga 0 zl", 700), TableLine("Przelew 1 zl", 720)]),
            LayoutFactory.Page(2, [TableLine("Karta 5 zl", 100), LayoutFactory.Line("Tekst po tabeli.", 71, 140)]));
        context.Tables.Add(new LayoutBlock(LayoutBlockKind.Table, new PageRange(1, 2)) { Table = Table(1, 2) });

        new BlockAssemblyStage().Execute(context);

        Assert.Equal([LayoutBlockKind.Table, LayoutBlockKind.Paragraph], context.Blocks.Select(b => b.Kind));
    }

    [Fact]
    public void DocumentBuild_PlacesTablesAndCountsThemInTheReport()
    {
        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo("taryfa.pdf", 1, null, 0, new string('0', 64)),
            new ReportBuilder());
        context.Pages.Add(new LayoutPage(1, 595, 842));
        context.Blocks.Add(new LayoutBlock(LayoutBlockKind.Table, new PageRange(1, 1)) { Table = Table(1, 1) });
        context.Blocks.Add(new LayoutBlock(LayoutBlockKind.Table, new PageRange(1, 1)) { Table = Table(1, 1, fallback: true) });

        new DocumentBuildStage().Execute(context);

        Assert.All(context.Document!.Preamble, b => Assert.IsType<TableBlock>(b));
        ConversionReport report = context.Report.Build(1, TimeSpan.Zero);
        Assert.Equal((2, 1), (report.TableCount, report.FallbackTableCount));
    }
}
