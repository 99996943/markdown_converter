using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>T057 — footnote definitions and reference markers (FR-026).</summary>
public sealed class FootnoteDetectionStageTests
{
    private const double X = 50;
    private const double Body = 10;
    private const double Note = 8;
    private const string BodyText = "Treść zwykłego akapitu, która jest wystarczająco długa, aby nie była nagłówkiem dokumentu.";

    /// <summary>Body text with a raised, smaller reference marker glued to the end of <paramref name="text"/>.</summary>
    private static PageSketch WithReference(PageSketch sketch, string text, string marker, double baseline)
    {
        sketch.Line(text, X, baseline, Body);
        // PageSketch advances letters by 0.5 em and spaces by 0.26 em: the marker starts exactly where the text ends.
        double end = X + text.Sum(c => c == ' ' ? 0.26 * Body : 0.5 * Body);
        sketch.Line(marker, end, baseline - 4, 6);
        return sketch;
    }

    private static PageSketch BodyLines(PageSketch sketch, double from, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sketch.Line(BodyText, X, from + (i * 14), Body);
        }

        return sketch;
    }

    private static PipelineContext Run(Action<PdfParserOptions>? configure, params PageSketch[] pages)
    {
        PipelineContext context = PageSketch.Assemble(configure, pages);
        new FootnoteDetectionStage().Execute(context);
        return context;
    }

    private static PipelineContext Run(params PageSketch[] pages) => Run(null, pages);

    private static string Text(FootnoteDraft draft) => string.Concat(draft.Inlines.OfType<TextRun>().Select(r => r.Text));

    private static LayoutLine LineStarting(PipelineContext context, string prefix) =>
        context.Pages.SelectMany(p => p.Lines).First(l => l.Text.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public void Order_IsFootnoteDetection()
    {
        Assert.Equal(StageOrder.FootnoteDetection, new FootnoteDetectionStage().Order);
    }

    [Fact]
    public void SmallFontBlockBelowARule_IsAFootnote_AndTheGluedSuperscriptIsItsReference()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        WithReference(page, "Niniejsza ustawa wdraża dyrektywę", "1)", 180);
        page.Page.Rulings.Add(new Segment(X, 760, X + 120, 760));
        page.Line("1) Niniejsza ustawa wdraża dyrektywę Rady (UE) 2019/997.", X, 775, Note);

        PipelineContext context = Run(page);

        FootnoteDraft draft = Assert.Single(context.Footnotes);
        Assert.Equal(("1)", 1, false), (draft.Label, draft.Page, draft.IsOrphan));
        Assert.Equal("Niniejsza ustawa wdraża dyrektywę Rady (UE) 2019/997.", Text(draft));
        Assert.Equal(LineRole.Footnote, LineStarting(context, "1) Niniejsza").Role);

        LayoutLine body = LineStarting(context, "Niniejsza ustawa wdraża dyrektywę");
        Assert.NotEqual(LineRole.Footnote, body.Role);
        Assert.Equal("dyrektywę", body.Words[^2].Text);
        Assert.Equal("1)", body.Words[^1].Text);
        Assert.Equal(draft.Id, body.Words[^1].FootnoteId);
        Assert.Null(body.Words[^2].FootnoteId);
    }

    [Fact]
    public void SmallFontBlockAtBottomWithoutRule_IsAFootnote_WhenItsLabelMatchesAReference()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        WithReference(page, "Minister Spraw Zagranicznych", "1)", 180);
        page.Line("1) Minister kieruje działem administracji rządowej.", X, 775, Note);

        PipelineContext context = Run(page);

        Assert.Equal("Minister kieruje działem administracji rządowej.", Text(Assert.Single(context.Footnotes)));
    }

    [Fact]
    public void SmallFontTextInTheMiddleOfThePage_IsNotAFootnote()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 3);
        page.Line("1) Uwaga drobnym drukiem w treści dokumentu.", X, 150, Note);
        BodyLines(page, 170, 3);

        PipelineContext context = Run(page);

        Assert.Empty(context.Footnotes);
        Assert.NotEqual(LineRole.Footnote, LineStarting(context, "1) Uwaga").Role);
    }

    [Fact]
    public void FootnoteWithHangingIndentAndHyphenation_IsJoinedIntoOneDefinition()
    {
        // Dz. U. 2026 poz. 1298, page 1: the continuation line is indented under the text, not under the label.
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        WithReference(page, "MINISTRA SPRAW ZAGRANICZNYCH", "1)", 180);
        page.Page.Rulings.Add(new Segment(X, 750, X + 120, 750));
        page.Line("1) Minister Spraw Zagranicznych kieruje działem administracji rządowej, na podstawie roz-", X, 765, Note);
        page.Line("porządzenia Prezesa Rady Ministrów (Dz. U. poz. 993).", X + 8, 775, Note);

        PipelineContext context = Run(page);

        Assert.Equal(
            "Minister Spraw Zagranicznych kieruje działem administracji rządowej, na podstawie rozporządzenia Prezesa Rady Ministrów (Dz. U. poz. 993).",
            Text(Assert.Single(context.Footnotes)));
    }

    [Fact]
    public void TwoFootnotesOnOnePage_AreSeparateDefinitions()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        WithReference(page, "Pierwsze odwołanie", "1)", 180);
        WithReference(page, "Drugie odwołanie", "2)", 194);
        page.Page.Rulings.Add(new Segment(X, 750, X + 120, 750));
        page.Line("1) Pierwszy przypis.", X, 765, Note);
        page.Line("2) Drugi przypis.", X, 775, Note);

        PipelineContext context = Run(page);

        Assert.Equal(["1)", "2)"], context.Footnotes.Select(f => f.Label));
        Assert.Equal(["Pierwszy przypis.", "Drugi przypis."], context.Footnotes.Select(Text));
        Assert.Equal(context.Footnotes[1].Id, LineStarting(context, "Drugie").Words[^1].FootnoteId);
    }

    [Fact]
    public void FootnoteWithoutReference_IsOrphan_WithWarning()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        page.Page.Rulings.Add(new Segment(X, 760, X + 120, 760));
        page.Line("2) Przypis bez odnośnika w tekście.", X, 775, Note);

        PipelineContext context = Run(page);

        FootnoteDraft draft = Assert.Single(context.Footnotes);
        Assert.True(draft.IsOrphan);
        ConversionWarning warning = Assert.Single(context.Report.Build(1, TimeSpan.Zero).Warnings, w => w.Code == "FTN001_OrphanFootnote");
        Assert.Equal(1, warning.PageNumber);
    }

    [Fact]
    public void FootnoteContinuedOnTheNextPage_IsJoined()
    {
        PageSketch first = BodyLines(new PageSketch(1), 100, 5);
        WithReference(first, "Odwołanie do przypisu", "1)", 180);
        first.Page.Rulings.Add(new Segment(X, 760, X + 120, 760));
        first.Line("1) Przypis, który zaczyna się na pierwszej stronie i", X, 775, Note);

        PageSketch second = BodyLines(new PageSketch(2), 100, 5);
        second.Page.Rulings.Add(new Segment(X, 760, X + 120, 760));
        second.Line("kończy na drugiej stronie dokumentu.", X, 775, Note);

        PipelineContext context = Run(first, second);

        FootnoteDraft draft = Assert.Single(context.Footnotes);
        Assert.Equal(1, draft.Page);
        Assert.Equal("Przypis, który zaczyna się na pierwszej stronie i kończy na drugiej stronie dokumentu.", Text(draft));
        Assert.Equal(LineRole.Footnote, LineStarting(context, "kończy").Role);
    }

    [Fact]
    public void DetectionDisabled_LeavesEverythingUntouched()
    {
        PageSketch page = BodyLines(new PageSketch(), 100, 5);
        WithReference(page, "Odwołanie", "1)", 180);
        page.Page.Rulings.Add(new Segment(X, 760, X + 120, 760));
        page.Line("1) Przypis.", X, 775, Note);

        PipelineContext context = Run(o => o.Footnotes.Enabled = false, page);

        Assert.Empty(context.Footnotes);
        Assert.DoesNotContain(context.Pages[0].Lines, l => l.Role == LineRole.Footnote);
    }
}
