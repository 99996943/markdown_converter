using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>T056 — typographic headings, legal units and heading levels (FR-040 – FR-047, FR-043a).</summary>
public sealed class HeadingDetectionStageTests
{
    private const double X = 50;
    private const double Body = 10;
    private const double Leading = 14;

    // 90 characters → 450 pt at 10 pt (PageSketch letters advance 0.5 em): the text column is 50–500.
    private const string BodyText = "Treść zwykłego akapitu, która jest wystarczająco długa, aby nie była nagłówkiem dokumentu.";

    /// <summary>Writes a sequence of rows top-down; <c>null</c> text inserts an extra blank gap of one leading.</summary>
    private sealed class Flow(PageSketch sketch, double start = 80)
    {
        private double _baseline = start;

        public PageSketch Sketch { get; } = sketch;

        public Flow Text(string text, double size = Body, bool bold = false, double x = X)
        {
            _baseline += Math.Max(Leading, size * 1.4);
            Sketch.Line(text, x, _baseline, size, bold);
            return this;
        }

        public Flow Paragraph(int lines = 2)
        {
            for (int i = 0; i < lines; i++)
            {
                Text(BodyText);
            }

            return this;
        }

        public Flow Gap()
        {
            _baseline += Leading * 1.5;
            return this;
        }

        public Flow Mixed(params (string Text, bool Bold, bool Italic)[] parts)
        {
            _baseline += Leading;
            Sketch.Mixed(X, _baseline, Body, parts);
            return this;
        }
    }

    private static Flow Page(int number = 1) => new(new PageSketch(number));

    private static PipelineContext Run(Action<PdfParserOptions>? configure, params Flow[] pages)
    {
        PipelineContext context = PageSketch.Assemble(configure, pages.Select(p => p.Sketch).ToArray());
        new HeadingDetectionStage().Execute(context);
        return context;
    }

    private static PipelineContext Run(params Flow[] pages) => Run(null, pages);

    private static List<HeadingInfo> Headings(PipelineContext context) =>
        context.Pages.SelectMany(p => p.Lines).Where(l => l.Heading is not null).Select(l => l.Heading!).ToList();

    private static HeadingInfo Heading(PipelineContext context, string text) =>
        Assert.Single(Headings(context), h => h.Text == text);

    private static LayoutLine LineStarting(PipelineContext context, string prefix) =>
        context.Pages.SelectMany(p => p.Lines).First(l => l.Text.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public void Order_IsHeadingDetection()
    {
        Assert.Equal(StageOrder.HeadingDetection, new HeadingDetectionStage().Order);
    }

    // (a) FR-040
    [Fact]
    public void BodyStyleIncludesWeight_BoldBodyTextIsNotAHeading()
    {
        Flow page = Page();
        for (int i = 0; i < 6; i++)
        {
            page.Text(BodyText, bold: true);
        }

        page.Gap().Text("Ważna informacja", bold: true).Text(BodyText, bold: true)
            .Gap().Text("Postanowienia ogólne", size: 14, bold: true).Text(BodyText, bold: true);

        PipelineContext context = Run(page);

        HeadingInfo heading = Assert.Single(Headings(context));
        Assert.Equal("Postanowienia ogólne", heading.Text);
    }

    // (b) FR-041
    [Fact]
    public void LargerStandaloneLine_IsATypographicHeading()
    {
        PipelineContext context = Run(Page().Paragraph().Gap().Text("Postanowienia ogólne", size: 14).Paragraph());

        HeadingInfo heading = Heading(context, "Postanowienia ogólne");
        Assert.Equal(SectionKind.Typographic, heading.Kind);
        Assert.Null(heading.Designation);
        Assert.Equal(LineRole.Heading, LineStarting(context, "Postanowienia").Role);
    }

    [Fact]
    public void LargerLine_LongerThanMaxLength_IsNotAHeading()
    {
        string longText = string.Concat(Enumerable.Repeat("Bardzo długi wiersz ", 7)); // 140 characters
        PipelineContext context = Run(Page().Paragraph().Gap().Text(longText, size: 14).Paragraph());

        Assert.Empty(Headings(context));
    }

    [Theory]
    [InlineData("Postanowienia ogólne,")]
    [InlineData("Postanowienia ogólne;")]
    public void LargerLine_EndingWithCommaOrSemicolon_IsNotAHeading(string text)
    {
        PipelineContext context = Run(Page().Paragraph().Gap().Text(text, size: 14).Paragraph());

        Assert.Empty(Headings(context));
    }

    [Fact]
    public void LargerLine_WithoutGapBefore_IsNotAHeading()
    {
        // Baseline distance equals the body leading: not separated (gap must exceed 1.3 × leading).
        Flow page = Page().Paragraph(3);
        page.Sketch.Line("Postanowienia ogólne", X, 80 + (4 * Leading), 11);
        page.Sketch.Line(BodyText, X, 80 + (5 * Leading), Body);

        PipelineContext context = Run(page);

        Assert.Empty(Headings(context));
    }

    [Fact]
    public void AllCapsStandaloneLine_IsAHeading()
    {
        PipelineContext context = Run(Page().Paragraph().Gap().Text("POSTANOWIENIA OGÓLNE").Paragraph());

        Assert.Equal("POSTANOWIENIA OGÓLNE", Assert.Single(Headings(context)).Text);
    }

    [Fact]
    public void CentredStandaloneLine_IsAHeading()
    {
        // „Przepisy końcowe” is 80 pt wide; the column is 50–500 (centre 275).
        PipelineContext context = Run(Page().Paragraph().Gap().Text("Przepisy końcowe", x: 235).Paragraph());

        Assert.Equal("Przepisy końcowe", Assert.Single(Headings(context)).Text);
    }

    // (c) FR-042
    [Fact]
    public void SizeClasses_MapToLevels_BoldOnlyIsOneBelowLowestEnlargedClass()
    {
        PipelineContext context = Run(Page()
            .Text("Informacje o dokumencie", size: 18).Paragraph()
            .Gap().Text("Zakres stosowania", size: 14).Paragraph()
            .Gap().Text("Wyjątki szczegółowe", bold: true).Paragraph()
            .Gap().Text("Zakres wyłączeń", size: 14.3).Paragraph());

        Assert.Equal(1, Heading(context, "Informacje o dokumencie").Level);
        Assert.Equal(2, Heading(context, "Zakres stosowania").Level);
        Assert.Equal(2, Heading(context, "Zakres wyłączeń").Level); // 14.3 pt is in the 14 pt class (0.5 pt tolerance)
        Assert.Equal(3, Heading(context, "Wyjątki szczegółowe").Level);
    }

    [Fact]
    public void BoldOnlyHeadings_WithoutEnlargedClasses_AreLevelTwo()
    {
        PipelineContext context = Run(Page().Paragraph().Gap().Text("Wyjątki szczegółowe", bold: true).Paragraph());

        Assert.Equal(2, Heading(context, "Wyjątki szczegółowe").Level);
    }

    [Fact]
    public void MaxTypographicDepth_CapsLevels()
    {
        PipelineContext context = Run(
            o => o.Headings.MaxTypographicDepth = 2,
            Page()
                .Text("Informacje o dokumencie", size: 18).Paragraph()
                .Gap().Text("Zakres stosowania", size: 14).Paragraph()
                .Gap().Text("Wyjątki szczegółowe", bold: true).Paragraph());

        Assert.Equal(2, Heading(context, "Wyjątki szczegółowe").Level);
    }

    [Fact]
    public void HeadingsDisabled_DetectsNothing()
    {
        PipelineContext context = Run(
            o => o.Headings.Enabled = false,
            Page().Gap().Text("Postanowienia ogólne", size: 14).Text("Art. 1. Ustawa określa zasady."));

        Assert.Empty(Headings(context));
    }

    // (d) FR-043
    [Fact]
    public void LegalUnits_GetLevelsOnlyForTypesPresent_ArticlesOneBelowDeepest()
    {
        PipelineContext context = Run(Page()
            .Text("DZIAŁ I", bold: true)
            .Text("Rozdział 1", bold: true)
            .Text("Art. 1. Ustawa określa zasady.")
            .Text("Art. 2. Ustawę stosuje się do banków."));

        HeadingInfo division = Heading(context, "DZIAŁ I");
        Assert.Equal((2, SectionKind.Division, "DZIAŁ I", "I"), (division.Level, division.Kind, division.Designation, division.Number));
        Assert.Equal(3, Heading(context, "Rozdział 1").Level);
        HeadingInfo article = Heading(context, "Art. 1.");
        Assert.Equal((4, SectionKind.Article, "Art. 1", "1"), (article.Level, article.Kind, article.Designation, article.Number));
    }

    [Fact]
    public void ChaptersAndArticles_AreLevelsTwoAndThree()
    {
        PipelineContext context = Run(Page()
            .Text("Rozdział 1", bold: true)
            .Text("Art. 1. Ustawa określa zasady.")
            .Text("Rozdział 2", bold: true)
            .Text("Art. 2. Ustawę stosuje się do banków."));

        Assert.Equal(2, Heading(context, "Rozdział 2").Level);
        Assert.Equal(3, Heading(context, "Art. 2.").Level);
    }

    [Theory]
    [InlineData("Art. 1. Ustawa określa zasady.", "Art. 1.", SectionKind.Article)]
    [InlineData("§ 1. Regulamin określa zasady.", "§ 1.", SectionKind.Paragraph)]
    public void UnitsWithoutStructuralParents_AreLevelTwo(string line, string heading, SectionKind kind)
    {
        PipelineContext context = Run(Page().Text(line).Text(BodyText));

        HeadingInfo info = Heading(context, heading);
        Assert.Equal((2, kind), (info.Level, info.Kind));
    }

    [Fact]
    public void LargestHeadingBeforeAnyLegalUnit_IsTheDocumentTitle()
    {
        PipelineContext context = Run(Page()
            .Text("USTAWA", size: 16, bold: true, x: 250)
            .Text("z dnia 1 stycznia 2026 r.", x: 215)
            .Gap().Text("Rozdział 1", bold: true)
            .Text("Art. 1. Ustawa określa zasady."));

        HeadingInfo title = Heading(context, "USTAWA");
        Assert.Equal((1, SectionKind.DocumentTitle), (title.Level, title.Kind));
        Assert.Equal(2, Heading(context, "Rozdział 1").Level);
        Assert.Equal(3, Heading(context, "Art. 1.").Level);
    }

    [Fact]
    public void ActTypeLine_IsTheTitle_AndTheJournalMastheadAboveItStaysPlainText()
    {
        // Dz. U. 2026 poz. 1298, page 1: a large-font masthead above a body-size, bold „OBWIESZCZENIE” title block.
        static double Width(string text, double size) => text.Sum(c => c == ' ' ? 0.26 * size : 0.5 * size);
        double centre = X + (Width(BodyText, Body) / 2);
        double Centred(string text, double size = Body) => centre - (Width(text, size) / 2);

        var sketch = new PageSketch();
        sketch.Line("DZIENNIK USTAW", Centred("DZIENNIK USTAW", 30), 60, 30);
        sketch.Line("RZECZYPOSPOLITEJ POLSKIEJ", Centred("RZECZYPOSPOLITEJ POLSKIEJ", 18), 95, 18);
        sketch.Line("Warszawa, dnia 6 października 2026 r.", Centred("Warszawa, dnia 6 października 2026 r.", 14), 130, 14);
        sketch.Line("Poz. 1298", Centred("Poz. 1298", 14), 155, 14);
        sketch.Line("OBWIESZCZENIE", Centred("OBWIESZCZENIE"), 200, Body, bold: true);
        sketch.Line("MINISTRA SPRAW ZAGRANICZNYCH", Centred("MINISTRA SPRAW ZAGRANICZNYCH"), 214, Body);
        sketch.Line("z dnia 15 września 2026 r.", Centred("z dnia 15 września 2026 r."), 230, Body);
        sketch.Line("w sprawie opłat konsularnych", Centred("w sprawie opłat konsularnych"), 252, Body, bold: true);
        for (int i = 0; i < 6; i++)
        {
            sketch.Line(BodyText, X, 280 + (i * Leading), Body);
        }

        PipelineContext context = Run(new Flow(sketch));

        HeadingInfo title = Assert.Single(Headings(context));
        Assert.Equal(SectionKind.DocumentTitle, title.Kind);
        Assert.Equal("OBWIESZCZENIE MINISTRA SPRAW ZAGRANICZNYCH z dnia 15 września 2026 r. w sprawie opłat konsularnych", title.Text);
        Assert.All(
            ["DZIENNIK USTAW", "RZECZYPOSPOLITEJ POLSKIEJ", "Warszawa, dnia", "Poz. 1298"],
            prefix => Assert.Equal(LineRole.Unknown, LineStarting(context, prefix).Role));
    }

    [Fact]
    public void MultiLineTitleInTheSameLargeFont_IsOneTitle()
    {
        // mBank terms, page 1: a four-line, left-aligned 32 pt title.
        PipelineContext context = Run(Page()
            .Text("Regulamin podstawowego", size: 32, bold: true)
            .Text("rachunku płatniczego", size: 32, bold: true)
            .Text("w ramach bankowości", size: 32, bold: true)
            .Text("detalicznej Banku S.A.", size: 32, bold: true)
            .Gap().Paragraph(4));

        HeadingInfo title = Assert.Single(Headings(context));
        Assert.Equal(SectionKind.DocumentTitle, title.Kind);
        Assert.Equal("Regulamin podstawowego rachunku płatniczego w ramach bankowości detalicznej Banku S.A.", title.Text);
    }

    // (e) FR-044
    [Fact]
    public void DesignationLine_FollowedByShortTitleInSameStyle_IsMergedIntoOneHeading()
    {
        PipelineContext context = Run(Page()
            .Text("Rozdział 3", bold: true)
            .Text("Ochrona konsumenta", bold: true)
            .Text("Art. 1. Ustawa określa zasady."));

        HeadingInfo chapter = Heading(context, "Rozdział 3. Ochrona konsumenta");
        Assert.Equal(("Rozdział 3", "3", "Ochrona konsumenta"), (chapter.Designation, chapter.Number, chapter.Title));
        LayoutLine titleLine = LineStarting(context, "Ochrona");
        Assert.Equal(LineRole.Heading, titleLine.Role);
        Assert.Null(titleLine.Heading);
    }

    [Fact]
    public void RegularDesignation_FollowedByBoldTitle_IsMerged_FR044()
    {
        // ISAP consolidated texts: „Rozdział 1” in the body font, the chapter title in bold.
        PipelineContext context = Run(Page()
            .Text("Rozdział 1")
            .Text("Przepisy ogólne", bold: true)
            .Text("Art. 1. Ustawa określa zasady."));

        Assert.Equal("Przepisy ogólne", Heading(context, "Rozdział 1. Przepisy ogólne").Title);
    }

    [Fact]
    public void DivisionWithUppercaseTitle_IsMerged()
    {
        PipelineContext context = Run(Page()
            .Text("DZIAŁ II", bold: true)
            .Text("PRZEPISY OGÓLNE", bold: true)
            .Text("Art. 1. Ustawa określa zasady."));

        Assert.Equal("PRZEPISY OGÓLNE", Heading(context, "DZIAŁ II. PRZEPISY OGÓLNE").Title);
    }

    [Fact]
    public void DesignationAndTitleOnTheSameLine_FormOneHeading()
    {
        PipelineContext context = Run(Page().Text("DZIAŁ III PRZEPISY KOŃCOWE", bold: true).Text("Art. 1. Ustawa określa zasady."));

        Assert.Equal("PRZEPISY KOŃCOWE", Heading(context, "DZIAŁ III. PRZEPISY KOŃCOWE").Title);
    }

    [Fact]
    public void DesignationLine_FollowedByBodyText_IsNotMerged()
    {
        PipelineContext context = Run(Page().Text("Rozdział 3", bold: true).Text(BodyText).Text(BodyText));

        HeadingInfo chapter = Heading(context, "Rozdział 3");
        Assert.Null(chapter.Title);
        Assert.NotEqual(LineRole.Heading, LineStarting(context, "Treść").Role);
    }

    // (f) FR-045
    [Theory]
    [InlineData("Art. 5. Ustawa określa zasady.", "Art. 5.", "Ustawa określa zasady.")]
    [InlineData("§ 2. 1. Czynności podlegające opłatom.", "§ 2.", "1. Czynności podlegające opłatom.")]
    public void UnitWithContentOnTheSameLine_IsSplitIntoHeadingAndContentLine(string line, string heading, string content)
    {
        PipelineContext context = Run(Page().Text(line).Text(BodyText));

        IList<LayoutLine> lines = context.Pages[0].Lines;
        int index = lines.IndexOf(lines.Single(l => l.Heading?.Text == heading));
        LayoutLine headingLine = lines[index];
        LayoutLine contentLine = lines[index + 1];

        Assert.Equal(heading, headingLine.Text);
        Assert.Equal(content, contentLine.Text);
        Assert.Equal(LineRole.Unknown, contentLine.Role);
        Assert.True(contentLine.Box.Left > headingLine.Box.Left);
        Assert.Equal(headingLine.Baseline, contentLine.Baseline);
    }

    // (g) FR-046
    [Fact]
    public void BoldWordInsideALine_IsNotAHeading()
    {
        PipelineContext context = Run(Page().Paragraph().Gap()
            .Mixed(("Bank pobiera ", false, false), ("opłatę", true, false), (" za prowadzenie rachunku.", false, false))
            .Gap().Paragraph());

        Assert.Empty(Headings(context));
    }

    // (h) FR-047
    [Fact]
    public void TableAndFootnoteLines_AreNeverPromoted()
    {
        PipelineContext context = PageSketch.Assemble(null, Page().Paragraph().Gap()
            .Text("Postanowienia ogólne", size: 14).Text("Art. 3. Treść przypisu o artykule.").Sketch);
        LineStarting(context, "Postanowienia").Role = LineRole.Table;
        LineStarting(context, "Art. 3.").Role = LineRole.Footnote;

        new HeadingDetectionStage().Execute(context);

        Assert.Empty(Headings(context));
        Assert.Equal(LineRole.Table, LineStarting(context, "Postanowienia").Role);
    }

    // (i) US2 scenario 5
    [Fact]
    public void UniformTypography_OnlyLegalUnitsBecomeHeadings()
    {
        PipelineContext context = Run(Page()
            .Text("Uwagi wstępne")
            .Paragraph()
            .Text("Art. 1. Ustawa określa zasady, o których mowa w art. 5 ustawy.")
            .Paragraph()
            .Gap().Text("Uwagi")
            .Text("Art. 2. Ustawę stosuje się do banków."));

        Assert.Equal(["Art. 1.", "Art. 2."], Headings(context).Select(h => h.Text));
    }

    // (j) FR-043a
    [Fact]
    public void TypographicHeadingsInALegalDocument_FollowTheOpenSection()
    {
        PipelineContext context = Run(Page()
            .Text("Preambuła", bold: true).Paragraph()
            .Gap().Text("Rozdział 1", bold: true).Paragraph()
            .Gap().Text("Przepisy szczególne", bold: true).Paragraph()
            .Gap().Text("Art. 1. Ustawa określa zasady.")
            .Text("Art. 2. Ustawę stosuje się do banków.")
            .Gap().Text("Załącznik nr 1", bold: true).Paragraph());

        Assert.Equal(
            [("Preambuła", 2), ("Rozdział 1", 2), ("Przepisy szczególne", 3), ("Art. 1.", 3), ("Art. 2.", 3), ("Załącznik nr 1", 2)],
            Headings(context).Select(h => (h.Text, h.Level)));
        Assert.Equal(SectionKind.Typographic, Heading(context, "Załącznik nr 1").Kind);
    }

    [Fact]
    public void Levels_NeverJumpByMoreThanOne()
    {
        PipelineContext context = Run(Page()
            .Text("Informacje o dokumencie", size: 18).Paragraph()
            .Gap().Text("Wyjątki szczegółowe", bold: true).Paragraph());

        // Without a 14 pt class in between, the bold heading follows its level-1 parent directly.
        Assert.Equal(2, Heading(context, "Wyjątki szczegółowe").Level);
    }

    private static double Centred(string text, double size = Body) => 270 - (text.Length * 0.5 * size / 2);

    [Fact]
    public void ActTitleBlockInsideTheDocument_IsOneHeading_FR043()
    {
        // A consolidated text published as an annex of an announcement: the act's own title block follows the
        // announcement and must not become three separate headings.
        PipelineContext context = Run(Page()
            .Text("OBWIESZCZENIE MARSZAŁKA SEJMU", 14, bold: true, x: Centred("OBWIESZCZENIE MARSZAŁKA SEJMU", 14))
            .Paragraph(2)
            .Gap()
            .Text("USTAWA", bold: true, x: Centred("USTAWA"))
            .Text("z dnia 29 sierpnia 1997 r.", x: Centred("z dnia 29 sierpnia 1997 r."))
            .Text("Prawo bankowe", bold: true, x: Centred("Prawo bankowe"))
            .Gap()
            .Text("Art. 1. Ustawa określa zasady prowadzenia działalności bankowej.")
            .Paragraph(2));

        HeadingInfo act = Heading(context, "USTAWA z dnia 29 sierpnia 1997 r. Prawo bankowe");
        Assert.Equal(SectionKind.Typographic, act.Kind);
        Assert.DoesNotContain(Headings(context), h => h.Text == "Prawo bankowe");
    }

    [Fact]
    public void FootnoteMarkerBetweenTitleLines_DoesNotCutTheTitleBlock()
    {
        var sketch = new PageSketch();
        sketch.Line(BodyText, X, 60);
        sketch.Line("USTAWA", Centred("USTAWA"), 110, bold: true);
        sketch.Line("z dnia 30 maja 2014 r.", Centred("z dnia 30 maja 2014 r."), 126);
        sketch.Line("1)", 346, 135, size: 6.5);
        sketch.Line("o prawach konsumenta", Centred("o prawach konsumenta"), 145, bold: true);
        sketch.Line("Art. 1. Ustawa określa prawa przysługujące konsumentowi.", X, 175);
        sketch.Line(BodyText, X, 189);

        PipelineContext context = PageSketch.Assemble(null, sketch);
        new HeadingDetectionStage().Execute(context);

        Assert.Contains(context.Pages[0].Lines, l => l.Text == "1)");
        Assert.Contains(Headings(context), h => h.Text == "USTAWA z dnia 30 maja 2014 r. o prawach konsumenta");
    }

    [Fact]
    public void ArticleInAmendmentNotation_IsAUnitWithTheBracketInItsHeading_FR043()
    {
        PipelineContext context = Run(Page()
            .Text("[Art. 31. 1. Dyrektor generalny urzędu upowszechnia informację o wyniku naboru.]")
            .Text("<Art. 31. 1. Dyrektor generalny urzędu zamieszcza ogłoszenie o wyniku naboru.>"));

        List<HeadingInfo> articles = Headings(context).Where(h => h.Kind == SectionKind.Article).ToList();
        Assert.Equal(["[Art. 31.", "<Art. 31."], articles.Select(h => h.Text));
        Assert.All(articles, h => Assert.Equal("Art. 31", h.Designation));
    }

    [Fact]
    public void UnitsInsideAQuotation_AreNotHeadings_UntilTheQuotationCloses()
    {
        // An announcement quoting amending articles of another act (Dz. U. 2019 poz. 1781): the quotation opened by
        // „Art. 109. runs over pages and quotes further articles and paragraphs; the act itself follows after ”.
        PipelineContext context = Run(
            Page()
                .Text("„Art. 109. W ustawie z dnia 17 listopada 1964 r. dodaje się pkt 4 w brzmieniu:")
                .Text("„4) o roszczenia wynikające z naruszenia przepisów o ochronie danych.”.")
                .Gap()
                .Text("Art. 110. W ustawie z dnia 17 czerwca 1966 r. wprowadza się następujące zmiany:"),
            Page(2)
                .Text("Art. 111. W ustawie z dnia 26 czerwca 1974 r. dodaje się art. 22a w brzmieniu:")
                .Text("„Art. 22a. § 1. Pracodawca może wprowadzić szczególny nadzór nad terenem zakładu.")
                .Gap()
                .Text("§ 2. Monitoring nie obejmuje pomieszczeń sanitarnych.”.”;")
                .Gap()
                .Text("Art. 1. Ustawa określa „zasady” ochrony danych osobowych.")
                .Gap()
                .Text("Art. 2. Ustawę stosuje się do przetwarzania danych."));

        Assert.Equal(["Art. 1.", "Art. 2."], Headings(context).Select(h => h.Text));
    }

    // ---------------------------------------------------------------- spec 002: table-documents (FR-086, FR-087)

    /// <summary>Runs the stage with a table-document starting on <paramref name="page"/> at <paramref name="top"/>.</summary>
    private static PipelineContext RunWithTableDocument(int page, double top, Action<PdfParserOptions>? configure, params Flow[] pages)
    {
        PipelineContext context = PageSketch.Assemble(configure, pages.Select(p => p.Sketch).ToArray());
        context.TableDocuments.Add(new TableDocumentRegion(0, page, pages.Length, top, 181, 186, 529, 1, null, 0));
        new HeadingDetectionStage().Execute(context);
        return context;
    }

    [Fact]
    public void FromTheStartOfATableDocument_OnlyLegalUnitsAreHeadings()
    {
        PipelineContext context = RunWithTableDocument(
            2,
            300,
            null,
            Page().Text("Regulamin promocji", size: 16, bold: true).Paragraph().Gap().Text("Przed tabelą", bold: true).Paragraph(),
            Page(2).Gap().Text("Nagłówek nad ramką", bold: true).Paragraph()
                .Gap().Gap().Gap().Gap().Gap().Gap().Gap().Gap()
                .Text("Nie możesz uczestniczyć w promocji, jeśli:", bold: true).Paragraph()
                .Gap().Text("MOJE OŚWIADCZENIA", size: 14, bold: true).Paragraph()
                .Gap().Text("Art. 5. Treść artykułu za tabelą."));

        Assert.Equal(["Regulamin promocji", "Przed tabelą", "Nagłówek nad ramką", "Art. 5."], Headings(context).Select(h => h.Text));
    }

    [Fact]
    public void UnitsInATableDocument_AreOneLevelBelowTheSectionNames()
    {
        PipelineContext context = RunWithTableDocument(
            2,
            100,
            null,
            Page().Text("Regulamin promocji", size: 16, bold: true).Paragraph(),
            Page(2).Gap().Gap().Gap().Gap().Gap().Gap().Gap().Gap()
                .Text("§ 1.", bold: true).Gap().Text("1. Organizatorem promocji jest Bank.").Paragraph()
                .Gap().Text("§ 2.", bold: true).Gap().Text("1. Promocja trwa do końca roku.").Paragraph());

        Assert.Equal([("Regulamin promocji", 1), ("§ 1.", 3), ("§ 2.", 3)], Headings(context).Select(h => (h.Text, h.Level)));
    }

    [Fact]
    public void WithoutATableDocument_TheSameLinesAreHeadings()
    {
        PipelineContext context = Run(
            Page().Text("Regulamin promocji", size: 16, bold: true).Paragraph(),
            Page(2).Gap().Text("Nie możesz uczestniczyć w promocji, jeśli:", bold: true).Paragraph()
                .Gap().Text("MOJE OŚWIADCZENIA", size: 14, bold: true).Paragraph());

        Assert.Equal(["Regulamin promocji", "Nie możesz uczestniczyć w promocji, jeśli:", "MOJE OŚWIADCZENIA"], Headings(context).Select(h => h.Text));
    }

    // ---------------------------------------------------------------- spec 002: validity line (FR-093)

    [Theory]
    [InlineData("Obowiązuje od 01.09.2026 r. do 30.11.2026 r.")]
    [InlineData("obowiązuje od 1 października 2026 r.")]
    [InlineData("OBOWIĄZUJE OD 01.09.2026 R.")]
    public void ValidityLineUnderTheTitle_IsNeitherAHeadingNorPartOfTheTitle(string validity)
    {
        PipelineContext context = Run(
            Page().Text("Regulamin promocji", size: 16, bold: true).Gap().Text(validity, size: 12).Paragraph()
                .Gap().Text("Postanowienia ogólne", size: 14, bold: true).Paragraph());

        Assert.Equal(["Regulamin promocji", "Postanowienia ogólne"], Headings(context).Select(h => h.Text));
        Assert.Equal(LineRole.Unknown, LineStarting(context, validity[..5]).Role);
    }

    [Fact]
    public void ValidityLineAsParagraphDisabled_KeepsTheBehaviourOfFeature001()
    {
        PipelineContext context = Run(
            o => o.Headings.ValidityLineAsParagraph = false,
            Page().Text("Regulamin promocji", size: 16, bold: true).Gap().Text("Obowiązuje od 01.09.2026 r.", size: 12).Paragraph());

        Assert.Contains("Obowiązuje od 01.09.2026 r.", Headings(context).Select(h => h.Text));
    }

    // ---------------------------------------------------------------- spec 002: image captions (FR-088)

    /// <summary>Title, an image at x 300–500 / y 150–350 and a bold „bank.example” at (<paramref name="x"/>, <paramref name="baseline"/>).</summary>
    private static PipelineContext RunWithImage(double x, double baseline, Action<PdfParserOptions>? configure = null)
    {
        var sketch = new PageSketch()
            .Line("Regulamin promocji", X, 100, 16, bold: true)
            .Line("bank.example", x, baseline, Body, bold: true)
            .Line(BodyText, X, 460, Body)
            .Line(BodyText, X, 474, Body);
        sketch.Page.ImageAreas.Add(new Rect(300, 150, 500, 350));
        PipelineContext context = PageSketch.Assemble(configure, sketch);
        new HeadingDetectionStage().Execute(context);
        return context;
    }

    [Theory]
    [InlineData(400, 375)] // 1.7 line heights under the image
    [InlineData(400, 340)] // on the image
    [InlineData(285, 375)] // starts within the image widened by 10%
    public void ShortLineUnderOrOnAnImage_IsACaptionNotAHeading(double x, double baseline)
    {
        PipelineContext context = RunWithImage(x, baseline);

        Assert.Equal(["Regulamin promocji"], Headings(context).Select(h => h.Text));
    }

    [Theory]
    [InlineData(400, 408)] // 5 line heights under the image
    [InlineData(X, 375)] // outside the image's width
    public void LineFarFromTheImage_IsStillAHeading(double x, double baseline)
    {
        PipelineContext context = RunWithImage(x, baseline);

        Assert.Contains("bank.example", Headings(context).Select(h => h.Text));
    }

    [Fact]
    public void ImageCaptionsDisabled_KeepTheBehaviourOfFeature001()
    {
        PipelineContext context = RunWithImage(400, 375, o => o.Headings.DetectImageCaptions = false);

        Assert.Contains("bank.example", Headings(context).Select(h => h.Text));
    }

    // ---------------------------------------------------------------- spec 006, T067e–T067g (real mBank regulations)

    /// <summary>
    /// T067e — mBank regulation of payment services, page 1: a 9 pt high decorative strip across the top of the page
    /// (0–595 × 6–15) above a five-line 32 pt title. The title lines are not captions of the strip.
    /// </summary>
    [Fact]
    public void TitleUnderAThinStripAcrossThePage_IsOneTitle_NotCaptions()
    {
        var sketch = new PageSketch()
            .Line("Regulamin usług płatniczych", X, 87, 32, bold: true)
            .Line("dla osób fizycznych", X, 126, 32, bold: true)
            .Line("i klientów Private Banking", X, 165, 32, bold: true)
            .Line("w ramach bankowości", X, 204, 32, bold: true)
            .Line("detalicznej mBanku S.A.", X, 243, 32, bold: true)
            .Line("obowiązuje od 26 sierpnia 2026 r.", X, 284, 16, bold: true);
        for (int i = 0; i < 6; i++)
        {
            sketch.Line(BodyText, X, 340 + (i * Leading), Body);
        }

        sketch.Page.ImageAreas.Add(new Rect(0, 6, 595, 15));
        PipelineContext context = PageSketch.Assemble(null, sketch);
        new HeadingDetectionStage().Execute(context);

        HeadingInfo title = Assert.Single(Headings(context));
        Assert.Equal(SectionKind.DocumentTitle, title.Kind);
        Assert.Equal(
            "Regulamin usług płatniczych dla osób fizycznych i klientów Private Banking w ramach bankowości detalicznej mBanku S.A.",
            title.Text);
    }

    /// <summary>
    /// T067f — mBank regulation of customer service, page 17: a bold chapter heading wrapped after a comma onto a second,
    /// indented line, between bold one-line chapter headings.
    /// </summary>
    [Fact]
    public void BoldHeadingWrappedAfterAComma_IsOneHeadingAtTheLevelOfTheOtherChapters()
    {
        PipelineContext context = Run(Page()
            .Text("13. Jak dbamy o bezpieczeństwo naszych usług i Twoich pieniędzy?", size: 11, bold: true)
            .Paragraph(3)
            .Gap().Text("14. Jak będziemy Cię obsługiwać, gdy władze ogłoszą stan nadzwyczajny,", size: 11, bold: true)
            .Text("stan zagrożenia epidemicznego lub stan epidemii?", size: 11, bold: true, x: X + 25)
            .Paragraph(3)
            .Gap().Text("15. Co musisz zrobić, jeśli zmienią się Twoje dane?", size: 11, bold: true)
            .Paragraph(2));

        HeadingInfo chapter = Heading(
            context,
            "14. Jak będziemy Cię obsługiwać, gdy władze ogłoszą stan nadzwyczajny, stan zagrożenia epidemicznego lub stan epidemii?");
        Assert.Equal(Heading(context, "13. Jak dbamy o bezpieczeństwo naszych usług i Twoich pieniędzy?").Level, chapter.Level);
        Assert.Equal(LineRole.Heading, LineStarting(context, "stan zagrożenia").Role);
    }

    [Fact]
    public void BoldLineEndingWithACommaFollowedByBodyText_IsNotAHeading()
    {
        PipelineContext context = Run(Page()
            .Paragraph(2)
            .Gap().Text("Pamiętaj, że w każdej chwili możesz to zmienić,", bold: true)
            .Paragraph(2));

        Assert.Empty(Headings(context));
    }

    /// <summary>
    /// T067g — mBank regulation of complaints, pages 2–3: „Spis treści” in 13 pt bold, the chapters in 12 pt bold over an
    /// 11 pt body. The table of contents has no subsections, so the chapters do not stand below it.
    /// </summary>
    [Fact]
    public void TableOfContentsHeadingInALargerFont_IsNotTheParentOfTheChapters()
    {
        PipelineContext context = Run(
            Page(1)
                .Text("Regulamin przyjmowania", size: 32, bold: true)
                .Text("i rozpatrywania reklamacji", size: 32, bold: true)
                .Gap().Paragraph(2),
            Page(2)
                .Text("Spis treści", size: 12, bold: true)
                .Text("1. Co znajdziesz w regulaminie? ........................ 3")
                .Text("2. Jak możesz złożyć reklamację? ...................... 4"),
            Page(3)
                .Text("1. Co znajdziesz w regulaminie?", size: 11, bold: true)
                .Paragraph(3)
                .Gap().Text("2. Jak możesz złożyć reklamację?", size: 11, bold: true)
                .Paragraph(3));

        Assert.Equal(2, Heading(context, "Spis treści").Level);
        Assert.Equal(2, Heading(context, "1. Co znajdziesz w regulaminie?").Level);
        Assert.Equal(2, Heading(context, "2. Jak możesz złożyć reklamację?").Level);
    }
}
