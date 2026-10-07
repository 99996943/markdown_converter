using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

public sealed class BlockAssemblyStageTests
{
    // About 400 pt wide at 11 pt; defines the column width of a page.
    private const string Long1 = "Bank pobiera oplate za prowadzenie rachunku platniczego oraz karty debetowej";
    private const string Long2 = "zgodnie z tabela oplat i prowizji obowiazujaca w dniu wykonania transakcji";
    private const string Long3 = "oraz w sposob okreslony w regulaminie swiadczenia uslug bankowosci detalicznej";

    private static PipelineContext Run(Action<PdfParserOptions>? configure, params PageSketch[] pages)
    {
        PipelineContext context = PageSketch.Assemble(configure, pages);
        new BlockAssemblyStage().Execute(context);
        return context;
    }

    private static PipelineContext Run(params PageSketch[] pages) => Run(null, pages);

    private static string Flat(LayoutBlock block)
    {
        var sb = new System.Text.StringBuilder();
        foreach (Inline inline in block.Inlines)
        {
            switch (inline)
            {
                case TextRun run:
                    sb.Append(run.Text);
                    break;
                case PageBreak pb:
                    sb.Append(" [p").Append(pb.PageNumber).Append("] ");
                    break;
                default:
                    throw new InvalidOperationException("Unexpected inline " + inline);
            }
        }

        return sb.ToString();
    }

    private static string[] Paragraphs(PipelineContext context) => context.Blocks.Select(Flat).ToArray();

    private static LayoutLine FindLine(PipelineContext context, string startsWith) =>
        context.Pages.SelectMany(p => p.Lines).First(l => l.Text.StartsWith(startsWith, StringComparison.Ordinal));

    [Fact]
    public void Order_IsBlockAssembly()
    {
        Assert.Equal(StageOrder.BlockAssembly, new BlockAssemblyStage().Order);
    }

    [Fact]
    public void Execute_MergesLinesWithinOneAndHalfLeadingIntoOneParagraph()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line(Long2, 50, 114)
            .Line("koncowka akapitu.", 50, 128));

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal(LayoutBlockKind.Paragraph, block.Kind);
        Assert.Equal($"{Long1} {Long2} koncowka akapitu.", Flat(block));
        Assert.Equal(3, block.Lines.Count);
        Assert.Equal(new PageRange(1, 1), block.Pages);
    }

    [Fact]
    public void Execute_StartsNewParagraphWhenGapExceedsOneAndHalfLeading()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line(Long2, 50, 114)
            .Line(Long3, 50, 150));

        Assert.Equal([$"{Long1} {Long2}", Long3], Paragraphs(context));
    }

    [Fact]
    public void Execute_ParagraphGapFactorComesFromOptions()
    {
        PipelineContext context = Run(
            o => o.Layout.ParagraphGapFactor = 0.9,
            new PageSketch().Line(Long1, 50, 100).Line(Long2, 50, 116));

        Assert.Equal(2, context.Blocks.Count);
    }

    [Fact]
    public void Execute_StartsNewParagraphAfterShortLineEndingWithPeriod()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line("Koniec ustepu.", 50, 114)
            .Line(Long2, 50, 128));

        Assert.Equal([$"{Long1} Koniec ustepu.", Long2], Paragraphs(context));
    }

    [Fact]
    public void Execute_KeepsShortLineWithoutPeriodInTheParagraph()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line("krotka linia bez kropki", 50, 114)
            .Line(Long2, 50, 128));

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal($"{Long1} krotka linia bez kropki {Long2}", Flat(block));
    }

    [Fact]
    public void Execute_KeepsLongLineEndingWithPeriodInTheParagraph()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line(Long2 + ".", 50, 114)
            .Line(Long3, 50, 128));

        Assert.Single(context.Blocks);
    }

    [Fact]
    public void Execute_ShortLineRatioComesFromOptions()
    {
        PipelineContext context = Run(
            o => o.Layout.ShortLineRatio = 0.1,
            new PageSketch().Line(Long1, 50, 100).Line("Koniec ustepu.", 50, 114).Line(Long2, 50, 128));

        Assert.Single(context.Blocks);
    }

    [Fact]
    public void Execute_StartsNewParagraphWhenFontSizeChanges()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line("Duzy napis", 50, 116, size: 14));

        Assert.Equal(2, context.Blocks.Count);
    }

    [Fact]
    public void Execute_StartsNewParagraphWhenLineIsIndentedFurther()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 50, 100)
            .Line(Long2, 50, 114)
            .Line(Long3, 80, 128));

        Assert.Equal([$"{Long1} {Long2}", Long3], Paragraphs(context));
    }

    [Fact]
    public void Execute_FirstLineIndentDoesNotSplitTheParagraphButNextIndentedLineDoes()
    {
        PipelineContext context = Run(new PageSketch()
            .Line(Long1, 62, 100)
            .Line(Long2, 50, 114)
            .Line("koniec pierwszego.", 50, 128)
            .Line(Long3, 62, 142)
            .Line(Long2, 50, 156));

        Assert.Equal(
            [$"{Long1} {Long2} koniec pierwszego.", $"{Long3} {Long2}"],
            Paragraphs(context));
    }

    [Fact]
    public void Execute_ConsumesLinesInListOrderWithoutResortingByY()
    {
        PipelineContext context = PageSketch.Assemble(
            null,
            new PageSketch().Line(Long1, 50, 100).Line(Long2, 50, 114).Line(Long3, 50, 400));
        LayoutPage page = context.Pages[0];

        // Simulate the reading order stage moving the lower block in front of the upper one.
        List<LayoutLine> reordered = [page.Lines[2], page.Lines[0], page.Lines[1]];
        page.Lines.Clear();
        foreach (LayoutLine l in reordered)
        {
            page.Lines.Add(l);
        }

        new BlockAssemblyStage().Execute(context);

        Assert.Equal([Long3, $"{Long1} {Long2}"], Paragraphs(context));
    }

    [Fact]
    public void Execute_SkipsLinesWithArtifactRole()
    {
        PipelineContext context = PageSketch.Assemble(
            null,
            new PageSketch().Line("Dziennik Ustaw - 1 - Poz. 1234", 50, 40).Line(Long1, 50, 100).Line("Strona 1", 250, 800));
        FindLine(context, "Dziennik").Role = LineRole.Artifact;
        FindLine(context, "Strona").Role = LineRole.Artifact;

        new BlockAssemblyStage().Execute(context);

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal(Long1, Flat(block));
    }

    [Fact]
    public void Execute_LinesOfOtherRolesEndTheParagraphAndAreNotConsumed()
    {
        PipelineContext context = PageSketch.Assemble(
            null,
            new PageSketch().Line(Long1, 50, 100).Line("Rozdzial 1", 50, 114).Line(Long2, 50, 128));
        FindLine(context, "Rozdzial").Role = LineRole.Heading;

        new BlockAssemblyStage().Execute(context);

        Assert.Equal([Long1, Long2], Paragraphs(context));
    }

    [Fact]
    public void Execute_ContinuesParagraphAcrossPageWhenNextPageStartsLowercase()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line("w terminie do dnia zawarcia", 50, 714),
            new PageSketch(2).Line("umowy przez strony.", 50, 100));

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal($"{Long1} w terminie do dnia zawarcia [p2] umowy przez strony.", Flat(block));
        Assert.Equal(new PageRange(1, 2), block.Pages);
        Assert.Single(block.Inlines.OfType<PageBreak>());
    }

    [Fact]
    public void Execute_ContinuesAcrossPageWhenNextLineHasTheSameIndentAsBodyText()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line(Long2, 50, 714),
            new PageSketch(2).Line("Warszawa jest stolica.", 50, 100));

        Assert.Single(context.Blocks);
    }

    [Fact]
    public void Execute_DoesNotContinueAcrossPageWhenLastLineEndsTheSentence()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line(Long2 + ".", 50, 714),
            new PageSketch(2).Line("kolejne zdanie zaczyna sie malo.", 50, 100));

        Assert.Equal(2, context.Blocks.Count);
        Assert.Equal(new PageRange(2, 2), context.Blocks[1].Pages);
    }

    [Fact]
    public void Execute_DoesNotContinueAcrossPageWhenNextLineIsUppercaseAndIndented()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line(Long2, 50, 714),
            new PageSketch(2).Line("Nowy akapit z wcieciem", 80, 100).Line(Long3, 50, 114));

        Assert.Equal(2, context.Blocks.Count);
    }

    [Fact]
    public void Execute_DoesNotContinueAcrossNonConsecutivePages()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line(Long2, 50, 714),
            new PageSketch(2).Skipped(SkipReason.NoTextLayer),
            new PageSketch(3).Line("dalszy ciag maly.", 50, 100));

        Assert.Equal(2, context.Blocks.Count);
    }

    [Fact]
    public void Execute_ContinuationIgnoresArtifactsBetweenPages()
    {
        PipelineContext context = PageSketch.Assemble(
            null,
            new PageSketch(1).Line(Long1, 50, 700).Line("w terminie do dnia zawarcia", 50, 714).Line("Strona 1", 250, 800),
            new PageSketch(2).Line("Dziennik Ustaw - 2 - Poz. 1234", 50, 40).Line("umowy przez strony.", 50, 100));
        FindLine(context, "Strona").Role = LineRole.Artifact;
        FindLine(context, "Dziennik").Role = LineRole.Artifact;

        new BlockAssemblyStage().Execute(context);

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal($"{Long1} w terminie do dnia zawarcia [p2] umowy przez strony.", Flat(block));
    }

    [Fact]
    public void Execute_JoinsHyphenatedWordWithinParagraph()
    {
        PipelineContext context = Run(new PageSketch()
            .Line("Umowa z przedsiebior-", 50, 100)
            .Line("ca zawarta w formie pisemnej oraz opisanej w regulaminie uslug", 50, 114));

        Assert.Equal(
            "Umowa z przedsiebiorca zawarta w formie pisemnej oraz opisanej w regulaminie uslug",
            Flat(Assert.Single(context.Blocks)));
    }

    [Fact]
    public void Execute_KeepsHyphenOfExceptionsCapitalizedPrefixesAndAbbreviations()
    {
        PipelineContext context = Run(new PageSketch()
            .Line("Wyslij wiadomosc na adres e-", 50, 100)
            .Line("mail banku oraz do miasta Bielsko-", 50, 114)
            .Line("Biala a takze na flage biało-", 50, 128)
            .Line("czerwony w calym kraju zgodnie z PKB-", 50, 142)
            .Line("owskim wskaznikiem i regulacja.", 50, 156));

        Assert.Equal(
            "Wyslij wiadomosc na adres e-mail banku oraz do miasta Bielsko-Biala a takze na flage "
            + "biało-czerwony w calym kraju zgodnie z PKB-owskim wskaznikiem i regulacja.",
            Flat(Assert.Single(context.Blocks)));
    }

    [Fact]
    public void Execute_HyphenExceptionsComeFromOptions()
    {
        PipelineContext context = Run(
            o => o.Normalization.HyphenationExceptions.Add("on-line"),
            new PageSketch().Line("Sprzedaz on-", 50, 100).Line("line jest dostepna dla klientow banku w calym kraju", 50, 114));

        Assert.Equal("Sprzedaz on-line jest dostepna dla klientow banku w calym kraju", Flat(Assert.Single(context.Blocks)));
    }

    [Fact]
    public void Execute_HyphenatedWordAcrossPageGetsMarkerAfterTheCompletedWord()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line(Long1, 50, 700).Line("Umowa z przedsiebior-", 50, 714),
            new PageSketch(2).Line("ca zawarta w formie pisemnej.", 50, 100));

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal($"{Long1} Umowa z przedsiebiorca [p2] zawarta w formie pisemnej.", Flat(block));
    }

    [Fact]
    public void Execute_PreservesBoldAndItalicRunsAsTextStyle()
    {
        PipelineContext context = Run(new PageSketch()
            .Mixed(50, 100, 11, ("Art. 5.", true, false), (" Bank moze ", false, false), ("zmienic", false, true), (" oplaty.", false, false)));

        LayoutBlock block = Assert.Single(context.Blocks);
        TextRun[] runs = block.Inlines.OfType<TextRun>().ToArray();
        Assert.Equal(["Art. 5.", "Bank moze", "zmienic", "oplaty."], runs.Select(r => r.Text.Trim()).ToArray());
        Assert.Equal([TextStyle.Bold, TextStyle.None, TextStyle.Italic, TextStyle.None], runs.Select(r => r.Style).ToArray());
        Assert.Equal("Art. 5. Bank moze zmienic oplaty.", string.Concat(runs.Select(r => r.Text)));
    }

    [Fact]
    public void Execute_MergesAdjacentWordsOfTheSameStyleIntoOneRun()
    {
        PipelineContext context = Run(new PageSketch().Line("jeden dwa trzy", 50, 100));

        TextRun run = Assert.IsType<TextRun>(Assert.Single(Assert.Single(context.Blocks).Inlines));
        Assert.Equal("jeden dwa trzy", run.Text);
    }

    [Fact]
    public void Execute_PageWithoutLinesProducesNoBlocks()
    {
        PipelineContext context = Run(new PageSketch(1).Line(Long1, 50, 100), new PageSketch(2).Skipped(SkipReason.NoTextLayer));

        Assert.Single(context.Blocks);
    }

    [Fact]
    public void Execute_ParagraphsOnDifferentPagesCarryTheirOwnPageRange()
    {
        PipelineContext context = Run(
            new PageSketch(1).Line("Pierwszy akapit.", 50, 100),
            new PageSketch(2).Line("Drugi akapit.", 50, 100));

        Assert.Equal([new PageRange(1, 1), new PageRange(2, 2)], context.Blocks.Select(b => b.Pages).ToArray());
    }

    [Fact]
    public void Execute_AlreadyCancelled_Throws()
    {
        PipelineContext source = PageSketch.Assemble(null, new PageSketch().Line(Long1, 50, 100));
        using var cts = new CancellationTokenSource();
        var context = new PipelineContext(source.Options, source.Source, new ReportBuilder(), cts.Token);
        foreach (LayoutPage p in source.Pages)
        {
            context.Pages.Add(p);
        }

        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new BlockAssemblyStage().Execute(context));
    }

    // ---- T065 wiring: heading blocks, footnote lines and reference markers (FR-026, FR-045) ----

    private static PipelineContext RunPrepared(Action<PipelineContext> prepare, params PageSketch[] pages)
    {
        PipelineContext context = PageSketch.Assemble(null, pages);
        prepare(context);
        new BlockAssemblyStage().Execute(context);
        return context;
    }

    [Fact]
    public void Execute_HeadingLineBecomesAHeadingBlock_AndMergedTitleLinesAreSkipped()
    {
        PipelineContext context = RunPrepared(
            c =>
            {
                LayoutLine designation = FindLine(c, "Rozdzial 3");
                designation.Role = LineRole.Heading;
                designation.Heading = new HeadingInfo(2, SectionKind.Chapter, "Rozdzial 3", "3", "Ochrona", "Rozdzial 3. Ochrona");
                FindLine(c, "Ochrona").Role = LineRole.Heading;
            },
            new PageSketch()
                .Line(Long1, 50, 100)
                .Line("Rozdzial 3", 50, 130)
                .Line("Ochrona", 50, 144)
                .Line(Long2, 50, 170));

        Assert.Equal(
            [LayoutBlockKind.Paragraph, LayoutBlockKind.Heading, LayoutBlockKind.Paragraph],
            context.Blocks.Select(b => b.Kind));
        LayoutBlock heading = context.Blocks[1];
        Assert.Equal("Rozdzial 3. Ochrona", heading.Heading!.Text);
        Assert.Equal(2, heading.HeadingLevel);
        Assert.Equal(new PageRange(1, 1), heading.Pages);
    }

    [Fact]
    public void Execute_TextAfterArticleDesignation_ContinuesAtTheMargin_FR045()
    {
        PipelineContext context = RunPrepared(
            c =>
            {
                LayoutPage page = c.Pages[0];
                LayoutLine line = FindLine(c, "Art. 1.");
                LayoutLine designation = LineSlicer.Slice(line, line.Words.Take(2).ToList());
                designation.Role = LineRole.Heading;
                designation.Heading = new HeadingInfo(2, SectionKind.Article, "Art. 1", "1", null, "Art. 1.");
                int index = page.Lines.IndexOf(line);
                page.Lines[index] = designation;
                page.Lines.Insert(index + 1, LineSlicer.Slice(line, line.Words.Skip(2).ToList()));
            },
            new PageSketch()
                .Line("Art. 1. W celu zapewnienia rzetelnego i bezstronnego", 97, 100)
                .Line("wykonywania zadan panstwa ustanawia sie sluzbe cywilna.", 50, 114));

        Assert.Equal(
            ["W celu zapewnienia rzetelnego i bezstronnego wykonywania zadan panstwa ustanawia sie sluzbe cywilna."],
            context.Blocks.Where(b => b.Kind == LayoutBlockKind.Paragraph).Select(Flat));
    }

    [Fact]
    public void Execute_FootnoteLinesAreSkipped_WithoutBreakingAParagraphContinuedOnTheNextPage()
    {
        PipelineContext context = RunPrepared(
            c => FindLine(c, "1) Tresc przypisu").Role = LineRole.Footnote,
            new PageSketch(1).Line(Long1, 50, 700).Line("1) Tresc przypisu na dole strony.", 50, 780, size: 8),
            new PageSketch(2).Line("ciag dalszy akapitu na drugiej stronie.", 50, 100));

        Assert.Equal([$"{Long1} [p2] ciag dalszy akapitu na drugiej stronie."], Paragraphs(context));
    }

    [Fact]
    public void Execute_FootnoteReferenceWord_BecomesAGluedFootnoteRef()
    {
        PipelineContext context = RunPrepared(
            c =>
            {
                LayoutPage page = c.Pages[0];
                LayoutLine line = page.Lines[0];
                List<LayoutWord> words = [.. line.Words];
                words[^1] = words[^1] with { FootnoteId = 7 };
                page.Lines[0] = new LayoutLine(words, line.Box, line.Baseline);
            },
            new PageSketch().Line("Ustawa wdraza dyrektywe 1)", 50, 100));

        LayoutBlock block = Assert.Single(context.Blocks);
        Assert.Equal(
            [new TextRun("Ustawa wdraza dyrektywe"), new FootnoteRef(7)],
            block.Inlines.ToArray());
    }
}
