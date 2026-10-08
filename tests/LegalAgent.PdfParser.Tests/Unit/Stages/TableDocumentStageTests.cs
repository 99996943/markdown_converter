using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// Spec 002, FR-080 – FR-084: table-documents — one multi-page table with a full grid and two columns, section names on
/// the left, their content on the right. Geometry follows the reference promotion terms (research.md „Pomiary”).
/// </summary>
public sealed class TableDocumentStageTests
{
    /// <summary>Three table pages: named rows of 48 words, a bullet, a row continued from page 1 to page 2.</summary>
    private static TableSheet ThreePages() => new TableSheet()
        .Page().Row("Organizator promocji", 48).Row("Uczestnik promocji", 48, bullet: true)
        .Page().Row(null, 24).Row("Ważne pojęcia", 48)
        .Page(header: false).Row("Korzyści promocji", 56).Row("Dodatkowe informacje", 48);

    private static PipelineContext Run(TableSheet sheet, Action<PdfParserOptions>? configure = null)
    {
        PipelineContext context = sheet.Context(configure);
        new TableDocumentStage().Execute(context);
        return context;
    }

    // ---------------------------------------------------------------- region (R2, R3)

    [Fact]
    public void TwoColumnBorderedTableOverMostPages_IsATableDocument()
    {
        PipelineContext context = Run(ThreePages());

        TableDocumentRegion region = Assert.Single(context.TableDocuments);
        Assert.Equal((0, 1, 3), (region.Index, region.FirstPage, region.LastPage));
        Assert.Equal(TableSheet.FrameTop, region.Top, 0.5);
        Assert.Equal(181, region.Divider, 0.5);
    }

    [Fact]
    public void LinesInsideTheFrame_AreAnnotatedWithTheTableDocument_LinesAboveItAreNot()
    {
        PipelineContext context = Run(ThreePages().Page().Above("Tekst nad ramką").Row("Ostatnia sekcja", 48));

        List<LayoutLine> lines = context.Pages.SelectMany(p => p.Lines).ToList();
        Assert.All(lines.Where(l => l.Text != "Tekst nad ramką"), l => Assert.Equal("0", l.Annotations[LayoutAnnotations.TableDocumentIndex]));
        Assert.DoesNotContain(LayoutAnnotations.TableDocumentIndex, lines.Single(l => l.Text == "Tekst nad ramką").Annotations.Keys);
    }

    [Fact]
    public void LinkUnderlinesAndShadedLinksInsideACell_DoNotSplitRows()
    {
        // Every named row has 48 words; split at the underline its first part would have 24 (FR-080 e: median < 40).
        PipelineContext context = Run(new TableSheet()
            .Page().Row("Organizator promocji", 48, underline: true).Row("Uczestnik promocji", 48, underline: true, bullet: true)
            .Page().Row(null, 24).Row("Ważne pojęcia", 48, underline: true));

        Assert.Single(context.TableDocuments);
    }

    [Fact]
    public void PagesWithADifferentColumnDivider_EndTheRegion()
    {
        TableSheet sheet = new TableSheet().Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48)
            .Page().Row(null, 24).Row("Ważne pojęcia", 48);
        sheet.Divider = 200;
        sheet.Page().Row("Korzyści promocji", 48);

        PipelineContext context = Run(sheet);

        TableDocumentRegion region = Assert.Single(context.TableDocuments);
        Assert.Equal((1, 2), (region.FirstPage, region.LastPage));
    }

    private static readonly Dictionary<string, (Func<TableSheet> Sheet, Action<PdfParserOptions>? Configure)> Variants = new(StringComparer.Ordinal)
    {
        ["three columns"] = (() => { var s = new TableSheet { ExtraVerticals = [400] }; return s.Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48).Page().Row(null, 24).Row("Ważne pojęcia", 48); }, null),
        ["left column of 40%"] = (() => new TableSheet { Divider = 249 }.Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48).Page().Row(null, 24).Row("Ważne pojęcia", 48), null),
        ["one page"] = (() => new TableSheet().Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48).Row("Ważne pojęcia", 48), null),
        ["two of six pages"] = (() => new TableSheet().PlainPage().PlainPage().Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48).Page().Row(null, 24).Row("Ważne pojęcia", 48).PlainPage().PlainPage(), null),
        ["short cells"] = (() => new TableSheet().Page().Row("Bank", 12, bullet: true).Row("Klient", 16).Row("Rachunek", 20).Page().Row(null, 8).Row("Karta", 16), null),
        ["no list, paragraphs or page break in a cell"] = (() => new TableSheet().Page().Row("Organizator promocji", 48).Row("Uczestnik promocji", 48).Page().Row("Ważne pojęcia", 48).Row("Korzyści promocji", 48), null),
        ["step scheme in between"] = (() => new TableSheet().Page().Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48).Page().Row(null, 24).MarkLastLineAsStepScheme().Row("Ważne pojęcia", 48).Page().Row("Korzyści promocji", 48), null),
        ["detection disabled"] = (ThreePages, o => o.Tables.DetectTableDocuments = false),
    };

    public static TheoryData<string> NotATableDocument() => new(Variants.Keys);

    [Theory]
    [MemberData(nameof(NotATableDocument))]
    public void OtherTables_AreNotTableDocuments(string variant)
    {
        (Func<TableSheet> sheet, Action<PdfParserOptions>? configure) = Variants[variant];
        PipelineContext context = Run(sheet(), configure);

        Assert.True(context.TableDocuments.Count == 0, variant);
        Assert.All(context.Pages.SelectMany(p => p.Lines), l => Assert.DoesNotContain(LayoutAnnotations.TableDocumentIndex, l.Annotations.Keys));
    }
}
