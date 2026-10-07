using System.Globalization;
using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// T072 — block assembly builds <see cref="ListBlock"/>/<see cref="ListItem"/> trees from the roles and annotations set by
/// list detection (items, continuations, nested lists, common parts), and document build places them in the model.
/// </summary>
public sealed class BlockAssemblyListTests
{
    private static LayoutLine Item(string text, double x, double top, int id, int? parent = null, ListLabelKind kind = ListLabelKind.ArabicParen)
    {
        LayoutLine line = LayoutFactory.Line(text, x, top);
        line.Role = LineRole.ListItem;
        line.Annotations[LayoutAnnotations.ListLabel] = line.Words[0].Text;
        line.Annotations[LayoutAnnotations.ListKind] = kind.ToString();
        line.Annotations[LayoutAnnotations.ListItemId] = id.ToString(CultureInfo.InvariantCulture);
        line.Annotations[LayoutAnnotations.ListParent] = parent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        return line;
    }

    private static LayoutLine Cont(string text, double x, double top, int owner, bool common = false)
    {
        LayoutLine line = LayoutFactory.Line(text, x, top);
        line.Role = LineRole.ListContinuation;
        line.Annotations[LayoutAnnotations.ListOwner] = owner.ToString(CultureInfo.InvariantCulture);
        if (common)
        {
            line.Annotations[LayoutAnnotations.ListCommonPart] = "1";
        }

        return line;
    }

    private static PipelineContext Assemble(params LayoutPage[] pages)
    {
        PipelineContext context = LayoutFactory.Context(pages);
        context.BodyStyle = new BodyStyle(10, 20);
        new BlockAssemblyStage().Execute(context);
        return context;
    }

    private static string Flat(IEnumerable<Inline> inlines) => string.Concat(inlines.Select(i => i switch
    {
        TextRun run => run.Text,
        PageBreak pb => $" [p{pb.PageNumber}] ",
        FootnoteRef r => $"[^{r.FootnoteNumber}]",
        _ => throw new InvalidOperationException("Unexpected inline " + i),
    }));

    private static ListBlock SingleList(PipelineContext context)
    {
        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal(LayoutBlockKind.List, block.Kind);
        return Assert.IsType<ListBlock>(block.List);
    }

    [Fact]
    public void Items_WithContinuations_FormOneListWithoutLabelsInTheText()
    {
        PipelineContext context = Assemble(LayoutFactory.Page(1,
        [
            Item("1) pracownik oznacza osobe zatrudniona", 71, 100, 1),
            Cont("na podstawie umowy o prace;", 97, 120, 1),
            Item("2) urzednik oznacza osobe mianowana.", 71, 140, 2),
        ]));

        ListBlock list = SingleList(context);
        Assert.Equal(new PageRange(1, 1), list.Pages);
        Assert.Equal(["1)", "2)"], list.Items.Select(i => i.Label));
        Assert.All(list.Items, i => Assert.Equal(ListLabelKind.ArabicParen, i.LabelKind));
        Assert.Equal("pracownik oznacza osobe zatrudniona na podstawie umowy o prace;", Flat(list.Items[0].Inlines));
        Assert.Equal("urzednik oznacza osobe mianowana.", Flat(list.Items[1].Inlines));
        Assert.All(list.Items, i => Assert.Empty(i.Children));
    }

    [Fact]
    public void NestedItemsAndCommonPart_BecomeChildrenOfTheParentItemInOrder()
    {
        PipelineContext context = Assemble(LayoutFactory.Page(1,
        [
            Item("3) na pisemny wniosek:", 71, 100, 1),
            Item("a) Szefa Kancelarii,", 97, 120, 2, parent: 1, kind: ListLabelKind.LetterParen),
            Item("b) wojewody", 97, 140, 3, parent: 1, kind: ListLabelKind.LetterParen),
            Cont("– po zasiegnieciu opinii", 97, 160, 1, common: true),
            Cont("Szefa Sluzby Cywilnej;", 71, 180, 1, common: true),
            Item("4) z wlasnej inicjatywy.", 71, 200, 4),
        ]));

        ListBlock list = SingleList(context);
        Assert.Equal(["3)", "4)"], list.Items.Select(i => i.Label));
        ListItem p3 = list.Items[0];
        Assert.Equal("na pisemny wniosek:", Flat(p3.Inlines));
        Assert.Equal(2, p3.Children.Count);
        ListBlock letters = Assert.IsType<ListBlock>(p3.Children[0]);
        Assert.Equal(["a)", "b)"], letters.Items.Select(i => i.Label));
        Assert.Equal("wojewody", Flat(letters.Items[1].Inlines));
        ParagraphBlock common = Assert.IsType<ParagraphBlock>(p3.Children[1]);
        Assert.Equal("– po zasiegnieciu opinii Szefa Sluzby Cywilnej;", Flat(common.Inlines));
    }

    [Fact]
    public void ListBetweenParagraphs_IsItsOwnBlock()
    {
        PipelineContext context = Assemble(LayoutFactory.Page(1,
        [
            LayoutFactory.Line("Bank oferuje:", 71, 100),
            Item("• karte debetowa,", 71, 120, 1, kind: ListLabelKind.Bullet),
            Item("• rachunek.", 71, 140, 2, kind: ListLabelKind.Bullet),
            LayoutFactory.Line("Pozostale uslugi opisuje taryfa.", 71, 160),
        ]));

        Assert.Equal([LayoutBlockKind.Paragraph, LayoutBlockKind.List, LayoutBlockKind.Paragraph], context.Blocks.Select(b => b.Kind));
        ListBlock list = context.Blocks[1].List!;
        Assert.Equal(["karte debetowa,", "rachunek."], list.Items.Select(i => Flat(i.Inlines)));
        Assert.Equal("Pozostale uslugi opisuje taryfa.", Flat(context.Blocks[2].Inlines));
    }

    [Fact]
    public void ContinuationOnNextPage_GetsAnInlinePageBreak()
    {
        PipelineContext context = Assemble(
            LayoutFactory.Page(1, [Item("1) zlozenia reklamacji dotyczacej", 71, 720, 1)]),
            LayoutFactory.Page(2, [Cont("uslugi swiadczonej przez dostawce.", 97, 100, 1)]));

        ListBlock list = SingleList(context);
        Assert.Equal(new PageRange(1, 2), list.Pages);
        Assert.Equal("zlozenia reklamacji dotyczacej [p2] uslugi swiadczonej przez dostawce.", Flat(list.Items[0].Inlines));
    }

    [Fact]
    public void ItemStartingOnNextPage_BeginsWithAPageBreak()
    {
        PipelineContext context = Assemble(
            LayoutFactory.Page(1, [Item("1) pierwszy punkt,", 71, 720, 1)]),
            LayoutFactory.Page(2, [Item("2) drugi punkt.", 71, 100, 2)]));

        ListBlock list = SingleList(context);
        Assert.Equal("pierwszy punkt,", Flat(list.Items[0].Inlines));
        Assert.Equal(" [p2] drugi punkt.", Flat(list.Items[1].Inlines));
    }

    [Fact]
    public void HyphenatedWordInsideItem_IsJoined()
    {
        PipelineContext context = Assemble(LayoutFactory.Page(1,
        [
            Item("1) obowiazki przedsiebior-", 71, 100, 1),
            Cont("cy wobec konsumenta.", 97, 120, 1),
        ]));

        Assert.Equal("obowiazki przedsiebiorcy wobec konsumenta.", Flat(SingleList(context).Items[0].Inlines));
    }

    [Fact]
    public void ArtifactAndFootnoteLines_DoNotSplitTheList()
    {
        LayoutLine footer = LayoutFactory.Line("Strona 1 z 2", 280, 810);
        footer.Role = LineRole.Artifact;
        LayoutLine footnote = LayoutFactory.Line("1) Przypis.", 71, 790);
        footnote.Role = LineRole.Footnote;

        PipelineContext context = Assemble(
            LayoutFactory.Page(1, [Item("1) pierwszy punkt,", 71, 700, 1), footnote, footer]),
            LayoutFactory.Page(2, [Item("2) drugi punkt.", 71, 100, 2)]));

        Assert.Equal(2, SingleList(context).Items.Count);
    }

    [Fact]
    public void DocumentBuild_PlacesTheListAndNumbersFootnoteReferencesInsideItems()
    {
        var context = new PipelineContext(
            new PdfParserOptions(),
            new SourceInfo("ustawa.pdf", 1, null, 0, new string('0', 64)),
            new ReportBuilder());
        context.Pages.Add(new LayoutPage(1, 595, 842));
        var draft = new FootnoteDraft(7, "1)", 1);
        draft.Inlines.Add(new TextRun("Przypis."));
        context.Footnotes.Add(draft);

        var nested = new ListBlock(new PageRange(1, 1), [new ListItem("a)", ListLabelKind.LetterParen, [new TextRun("lit"), new FootnoteRef(7)], [])]);
        var list = new ListBlock(new PageRange(1, 1), [new ListItem("1)", ListLabelKind.ArabicParen, [new TextRun("pkt")], [nested])]);
        context.Blocks.Add(new LayoutBlock(LayoutBlockKind.List, new PageRange(1, 1)) { List = list });

        new DocumentBuildStage().Execute(context);

        ListBlock built = Assert.IsType<ListBlock>(Assert.Single(context.Document!.Preamble));
        ListBlock builtNested = Assert.IsType<ListBlock>(Assert.Single(built.Items[0].Children));
        Assert.Equal(new FootnoteRef(1), builtNested.Items[0].Inlines[1]);
        Footnote footnote = Assert.Single(context.Document.PreambleFootnotes);
        Assert.Equal(1, footnote.Number);
        Assert.Equal(1, context.Report.ListCount);
    }

    private static LayoutLine Note(string text, double top)
    {
        LayoutLine line = LayoutFactory.Line(text, 480, top, height: 8);
        line.Role = LineRole.SideNote;
        return line;
    }

    [Fact]
    public void SideNoteLines_FormAParagraphAfterTheBlockTheyStandBeside_FR034()
    {
        PipelineContext context = Assemble(
            LayoutFactory.Page(1,
            [
                LayoutFactory.Line("W rozumieniu ustawy pracownik oznacza osobe zatrudniona na podstawie", 71, 100),
                Note("Nowe brzmienie pkt 1", 104),
                LayoutFactory.Line("umowy o prace zgodnie z zasadami okreslonymi w ustawie oraz przepisach", 71, 120),
                Note("wejdzie w zycie", 114),
                LayoutFactory.Line("wykonawczych do ustawy", 71, 140),
            ]),
            LayoutFactory.Page(2, [LayoutFactory.Line("wydanych na jej podstawie.", 71, 100)]));

        Assert.Equal([LayoutBlockKind.Paragraph, LayoutBlockKind.Paragraph], context.Blocks.Select(b => b.Kind));
        Assert.Equal(
            "W rozumieniu ustawy pracownik oznacza osobe zatrudniona na podstawie umowy o prace zgodnie z zasadami "
                + "okreslonymi w ustawie oraz przepisach wykonawczych do ustawy [p2] wydanych na jej podstawie.",
            Flat(context.Blocks[0].Inlines));
        Assert.Equal("Nowe brzmienie pkt 1 wejdzie w zycie", Flat(context.Blocks[1].Inlines));
        Assert.Equal(new PageRange(1, 1), context.Blocks[1].Pages);
    }

    [Fact]
    public void SideNoteLines_DoNotSplitAList_FR034()
    {
        PipelineContext context = Assemble(LayoutFactory.Page(1,
        [
            Item("1) pierwszy punkt,", 71, 100, 1),
            Note("Dodany pkt 2", 104),
            Item("2) drugi punkt.", 71, 120, 2),
        ]));

        Assert.Equal([LayoutBlockKind.List, LayoutBlockKind.Paragraph], context.Blocks.Select(b => b.Kind));
        Assert.Equal(2, context.Blocks[0].List!.Items.Count);
        Assert.Equal("Dodany pkt 2", Flat(context.Blocks[1].Inlines));
    }
}
