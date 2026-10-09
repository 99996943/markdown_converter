using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
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

    // ---------------------------------------------------------------- column-name row (R4)

    [Fact]
    public void ColumnNameRow_OnTheFirstPageAndItsRepeats_IsAnArtifact()
    {
        PipelineContext context = Run(ThreePages());

        List<LayoutLine> header = context.Pages.SelectMany(p => p.Lines).Where(l => l.Text.StartsWith("Definicje", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, header.Count);
        Assert.All(header, l => Assert.Equal(LineRole.Artifact, l.Role));
        Assert.All(header, l => Assert.Equal("0", l.Annotations[LayoutAnnotations.TableDocumentIndex]));
        TableDocumentRegion region = Assert.Single(context.TableDocuments);
        Assert.Equal("Definicje | Wyjaśnienie", region.HeaderRowText);
        Assert.Equal(2, region.DroppedHeaderRows);
    }

    [Fact]
    public void FirstRowWithLongContent_IsNotAColumnNameRow()
    {
        PipelineContext context = Run(new TableSheet()
            .Page(header: false).Row("Organizator promocji", 48, bullet: true).Row("Uczestnik promocji", 48)
            .Page(header: false).Row(null, 24).Row("Ważne pojęcia", 48));

        TableDocumentRegion region = Assert.Single(context.TableDocuments);
        Assert.Null(region.HeaderRowText);
        Assert.Equal(0, region.DroppedHeaderRows);
        Assert.DoesNotContain(context.Pages.SelectMany(p => p.Lines), l => l.Role == LineRole.Artifact);
        Assert.Equal(["Organizator promocji", "Uczestnik promocji", "Ważne pojęcia"], HeadingTexts(context));
    }

    // ---------------------------------------------------------------- splitting at the divider (R5)

    [Fact]
    public void LineSpanningBothColumns_IsSplitAtTheDivider()
    {
        PipelineContext context = Run(ThreePages());

        List<LayoutLine> lines = Content(context).ToList();
        Assert.All(lines, l => Assert.True(l.Words.All(w => w.Box.CenterX < 181) || l.Words.All(w => w.Box.CenterX >= 181), l.Text));
        LayoutLine name = lines.Single(l => l.Text == "Organizator promocji");
        Assert.Contains(lines, l => l.Text.StartsWith("tekst", StringComparison.Ordinal) && l.Box.Top == name.Box.Top);
    }

    // ---------------------------------------------------------------- section names (R6)

    [Fact]
    public void SectionName_IsALevel2HeadingWithTheOriginalName()
    {
        PipelineContext context = Run(ThreePages());

        LayoutLine name = Content(context).Single(l => l.Text == "Organizator promocji");
        Assert.Equal(LineRole.Heading, name.Role);
        Assert.Equal(new HeadingInfo(2, SectionKind.TableDocumentSection, null, null, "Organizator promocji", "Organizator promocji"), name.Heading);
        Assert.Equal(["Organizator promocji", "Uczestnik promocji", "Ważne pojęcia", "Korzyści promocji", "Dodatkowe informacje"], HeadingTexts(context));
        Assert.Equal(5, context.TableDocuments[0].SectionCount);
    }

    [Fact]
    public void MultiLineName_IsOneHeadingJoinedWithSpaces()
    {
        PipelineContext context = Run(ThreePages().Page().Row("Jak możesz złożyć reklamację dotyczącą promocji?", 48));

        List<LayoutLine> name = context.Pages[3].Lines.Where(l => l.Role == LineRole.Heading).ToList();
        Assert.Equal(["Jak możesz", "złożyć reklamację", "dotyczącą promocji?"], name.Select(l => l.Text));
        Assert.Equal("Jak możesz złożyć reklamację dotyczącą promocji?", name[0].Heading?.Text);
        Assert.Equal("Jak możesz złożyć reklamację dotyczącą promocji?", name[0].Heading?.Title);
        Assert.All(name.Skip(1), l => Assert.Null(l.Heading));
    }

    [Fact]
    public void NameBrokenByAPageBoundary_IsOneHeading()
    {
        PipelineContext context = Run(new TableSheet()
            .Page().Row("Organizator promocji", 48, bullet: true).Row("Warunki/zasady", 8)
            .Page(header: false).Row("promocji", 48).Row("Ważne pojęcia", 48));

        Assert.Equal(["Organizator promocji", "Warunki/zasady promocji", "Ważne pojęcia"], HeadingTexts(context));
        LayoutLine rest = Content(context).Single(l => l.Text == "promocji");
        Assert.Equal(LineRole.Heading, rest.Role);
        Assert.Null(rest.Heading);
        Assert.Equal(3, context.TableDocuments[0].SectionCount);
    }

    [Fact]
    public void NameEndingFarAboveTheFrameBottom_IsNotJoinedWithTheNextPage()
    {
        PipelineContext context = Run(new TableSheet()
            .Page().Row("Organizator promocji", 48, bullet: true).Row("Warunki/zasady", 24)
            .Page(header: false).Row("promocji", 48).Row("Ważne pojęcia", 48));

        Assert.Equal(["Organizator promocji", "Warunki/zasady", "promocji", "Ważne pojęcia"], HeadingTexts(context));
    }

    [Fact]
    public void RowWithAnEmptyLeftCell_ContinuesTheSectionWithoutAHeading()
    {
        PipelineContext context = Run(ThreePages());

        List<LayoutLine> continuation = context.Pages[1].Lines
            .Where(l => l.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex) && l.Role != LineRole.Artifact)
            .Take(3)
            .ToList();
        Assert.Equal(3, continuation.Count);
        Assert.All(continuation, l => Assert.Equal(LineRole.Unknown, l.Role));
        Assert.All(continuation, l => Assert.Null(l.Heading));
    }

    [Fact]
    public void FirstDataRowWithAnEmptyLeftCell_IsLeadingContentWithoutAName()
    {
        PipelineContext context = Run(new TableSheet()
            .Page().Row(null, 48, bullet: true).Row("Organizator promocji", 48)
            .Page().Row(null, 24).Row("Ważne pojęcia", 48));

        Assert.Equal(["Organizator promocji", "Ważne pojęcia"], HeadingTexts(context));
        LayoutLine first = Content(context).First();
        Assert.Equal(LineRole.Unknown, first.Role);
        Assert.StartsWith("•", first.Text, StringComparison.Ordinal);
        Assert.Equal(2, context.TableDocuments[0].SectionCount);
    }

    [Fact]
    public void NameBelowContinuedContentWithoutARuling_StartsItsSectionAtItsOwnLine()
    {
        // A page starts with the continuation of the previous section and the next name stands lower, on the baseline of
        // its first content line, with no ruling between them: the content above the name continues the previous section.
        PipelineContext context = Run(new TableSheet()
            .Page().Row("Organizator promocji", 48, bullet: true)
            .Page().Row(null, 24, ruled: false).Row("Ważne pojęcia", 48).Row("Korzyści promocji", 48));

        Assert.Equal(["Organizator promocji", "Ważne pojęcia", "Korzyści promocji"], HeadingTexts(context));
        string kinds = string.Concat(context.Pages[1].Lines.Select(l =>
            l.Role == LineRole.Artifact ? 'H' : l.Heading is not null ? 'N' : 'C'));
        Assert.Equal("HCCCNCCCCCCNCCCCCC", kinds);
        Assert.Equal(3, context.TableDocuments[0].SectionCount);
    }

    // ---------------------------------------------------------------- order and content column (R7)

    [Fact]
    public void LinesAreOrderedRowByRow_NameBeforeContent_OutsideLinesStayInPlace()
    {
        PipelineContext context = Run(ThreePages().Page().Above("Tekst nad ramką")
            .Row("Uczestnik promocji tego", 16, nameOffset: 1).Row("Ostatnia sekcja", 16, nameOffset: 1));

        string kinds = string.Concat(context.Pages[3].Lines.Select(l =>
            l.Text == "Tekst nad ramką" ? 'O' : l.Role == LineRole.Artifact ? 'H' : l.Role == LineRole.Heading ? 'N' : 'C'));
        Assert.Equal("OHNNCCNCC", kinds);
    }

    [Fact]
    public void ContentLines_GetTheContentColumnOfTheWholeRegion()
    {
        PipelineContext context = Run(ThreePages());

        TableDocumentRegion region = context.TableDocuments[0];
        List<LayoutLine> content = Content(context).Where(l => l.Role != LineRole.Heading).ToList();
        Assert.Equal(TableSheet.TextX, region.ContentLeft, 0.5);
        Assert.Equal(content.Max(l => l.Box.Right), region.ContentRight, 0.5);
        Assert.All(content, l => Assert.Equal(region.ContentLeft, LayoutAnnotations.GetNumber(l, LayoutAnnotations.ColumnLeft)));
        Assert.All(content, l => Assert.Equal(region.ContentRight, LayoutAnnotations.GetNumber(l, LayoutAnnotations.ColumnRight)));
    }

    /// <summary>Non-artifact lines of the table-document in page order.</summary>
    private static IEnumerable<LayoutLine> Content(PipelineContext context) =>
        context.Pages.SelectMany(p => p.Lines).Where(l => l.Annotations.ContainsKey(LayoutAnnotations.TableDocumentIndex) && l.Role != LineRole.Artifact);

    private static List<string> HeadingTexts(PipelineContext context) =>
        context.Pages.SelectMany(p => p.Lines).Where(l => l.Heading is not null).Select(l => l.Heading!.Text).ToList();
}
