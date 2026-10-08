using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// Line assembly merges glyphs of both columns that share a baseline into one multi-segment line; reading order
/// must split such lines at the gutter, and paragraph assembly must measure the column, not the page (FR-031, FR-032).
/// </summary>
public sealed class TwoColumnFlowTests
{
    private const string L1 = "Pierwszy akapit lewej kolumny ma zdanie.";
    private const string L2 = "i ciagnie sie dalej w tej samej kolumnie";
    private const string L3 = "az do konca tego akapitu w kolumnie.";
    private const string R1 = "Drugi akapit prawej kolumny zaczyna sie.";
    private const string R2 = "i rowniez ciagnie sie w prawej kolumnie";
    private const string R3 = "konczac sie tutaj na trzeciej linii.";

    private static PipelineContext TwoColumnPage()
    {
        PipelineContext context = PageSketch.Assemble(null, new PageSketch()
            .Line(L1, 50, 100).Line(R1, 320, 100)
            .Line(L2, 50, 114).Line(R2, 320, 114)
            .Line(L3, 50, 128).Line(R3, 320, 128));
        return context;
    }

    [Fact]
    public void LineAssembly_MergesSideBySideColumnLines_IntoMultiSegmentLines()
    {
        PipelineContext context = TwoColumnPage();

        LayoutLine first = context.Pages[0].Lines[0];
        Assert.Equal(2, first.Segments.Count);
    }

    [Fact]
    public void ReadingOrder_SplitsMergedLinesAtTheGutter()
    {
        PipelineContext context = TwoColumnPage();

        new ReadingOrderStage().Execute(context);

        Assert.Equal([L1, L2, L3, R1, R2, R3], context.Pages[0].Lines.Select(l => l.Text));
        Assert.All(context.Pages[0].Lines, l => Assert.Single(l.Segments));
    }

    [Fact]
    public void BlockAssembly_MeasuresShortLinesAgainstTheColumn()
    {
        PipelineContext context = TwoColumnPage();

        new ReadingOrderStage().Execute(context);
        new BlockAssemblyStage().Execute(context);

        string[] paragraphs = context.Blocks
            .Select(b => string.Concat(b.Inlines.OfType<TextRun>().Select(r => r.Text)))
            .ToArray();
        Assert.Equal([$"{L1} {L2} {L3}", $"{R1} {R2} {R3}"], paragraphs);
    }
}
