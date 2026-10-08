using LegalAgent.PdfParser.Layout;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Stages;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Unit.Stages;

/// <summary>
/// T067 — list detection (FR-050 – FR-054). Geometry follows the two layouts seen in the corpus: ISAP acts (ustęp with a
/// first-line indent at 97 pt and continuation at the margin 71 pt; points at the margin with a hanging continuation at
/// 97 pt; letters at 97 pt continued at 120 pt; a common part „– …” at the label position) and bank regulations (hanging
/// indent aligned to the item text). Lines are 20 pt apart, body leading is 20 pt.
/// </summary>
public sealed class ListDetectionStageTests
{
    private const double Margin = 71;
    private const double Indent = 97;
    private const double Deep = 120;

    private static PipelineContext Run(Action<PdfParserOptions>? configure, params LayoutPage[] pages)
    {
        PipelineContext context = LayoutFactory.Context(pages, configure);
        context.BodyStyle = new BodyStyle(10, 20);
        new ListDetectionStage().Execute(context);
        return context;
    }

    private static PipelineContext Run(params LayoutPage[] pages) => Run(null, pages);

    /// <summary>One page with lines placed 20 pt apart from the top, each given as (text, left).</summary>
    private static LayoutPage Page(int number, params (string Text, double Left)[] lines) =>
        LayoutFactory.Page(number, lines.Select((l, i) => LayoutFactory.Line(l.Text, l.Left, 100 + (20 * i))));

    private static LayoutPage Page(params (string Text, double Left)[] lines) => Page(1, lines);

    private static LayoutLine Find(PipelineContext context, string startsWith) =>
        context.Pages.SelectMany(p => p.Lines).Single(l => l.Text.StartsWith(startsWith, StringComparison.Ordinal));

    private static string Id(LayoutLine line) => line.Annotations[LayoutAnnotations.ListItemId];

    private static string Parent(LayoutLine line) => line.Annotations[LayoutAnnotations.ListParent];

    private static string Owner(LayoutLine line) => line.Annotations[LayoutAnnotations.ListOwner];

    private static bool IsCommonPart(LayoutLine line) =>
        line.Annotations.TryGetValue(LayoutAnnotations.ListCommonPart, out string? value) && value == "1";

    private static void AssertItem(LayoutLine line, string label, ListLabelKind kind)
    {
        Assert.Equal(LineRole.ListItem, line.Role);
        Assert.Equal(label, line.Annotations[LayoutAnnotations.ListLabel]);
        Assert.Equal(kind.ToString(), line.Annotations[LayoutAnnotations.ListKind]);
    }

    private static void AssertContinues(LayoutLine line, LayoutLine item)
    {
        Assert.Equal(LineRole.ListContinuation, line.Role);
        Assert.Equal(Id(item), Owner(line));
        Assert.False(IsCommonPart(line));
    }

    private static LayoutLine Bold(LayoutLine line)
    {
        var bold = new LayoutLine(line.Words.Select(w => w with { Style = TextStyle.Bold }).ToList(), line.Box, line.Baseline);
        bold.Segments.Add(new LineSegment(bold.Words, bold.Box));
        return bold;
    }

    [Fact]
    public void Order_IsListDetection()
    {
        Assert.Equal(StageOrder.ListDetection, new ListDetectionStage().Order);
    }

    [Fact]
    public void BulletLines_AreSiblingItemsOfOneTopLevelList()
    {
        PipelineContext context = Run(Page(
            ("Bank oferuje:", Margin),
            ("• karte debetowa,", Margin),
            ("• rachunek oszczednosciowy.", Margin)));

        LayoutLine first = Find(context, "• karte");
        LayoutLine second = Find(context, "• rachunek");
        AssertItem(first, "•", ListLabelKind.Bullet);
        AssertItem(second, "•", ListLabelKind.Bullet);
        Assert.Equal(string.Empty, Parent(first));
        Assert.Equal(string.Empty, Parent(second));
        Assert.NotEqual(Id(first), Id(second));
        Assert.Equal(LineRole.Unknown, Find(context, "Bank oferuje").Role);
    }

    /// <summary>Check marks („✓” from a symbol font) are bullets: mBank schemes list conditions with them, also under „•”.</summary>
    [Fact]
    public void CheckMarkLines_AreBulletItems_NestedUnderABulletByIndent()
    {
        PipelineContext context = Run(Page(
            ("• Złożenie wniosku nie jest zawarciem umowy, które wynikają:", Margin),
            ("✓ z przepisów prawa lub", Indent),
            ("✓ ze względów ostrożnościowych związanych", Indent),
            ("z podejrzeniami działalności przestępczej", Indent + 12),
            ("✔ z innych przyczyn.", Indent)));

        LayoutLine parent = Find(context, "• Złożenie");
        LayoutLine first = Find(context, "✓ z przepisów");
        LayoutLine second = Find(context, "✓ ze względów");
        AssertItem(first, "✓", ListLabelKind.Bullet);
        AssertItem(second, "✓", ListLabelKind.Bullet);
        AssertItem(Find(context, "✔ z innych"), "✔", ListLabelKind.Bullet);
        Assert.Equal(Id(parent), Parent(first));
        Assert.Equal(Id(parent), Parent(second));
        Assert.Equal(Id(second), Owner(Find(context, "z podejrzeniami")));
    }

    /// <summary>Gives every word of <paramref name="line"/> one glyph set in the matching font (R10).</summary>
    private static LayoutLine InFonts(LayoutLine line, params string[] fonts)
    {
        List<LayoutWord> words = line.Words
            .Select((w, i) => w with
            {
                Glyphs = [new LayoutGlyph(w.Text[..1], w.Box, w.Box.Bottom, 10, false, false, FontName: fonts[Math.Min(i, fonts.Length - 1)])],
            })
            .ToList();
        var result = new LayoutLine(words, line.Box, line.Baseline);
        result.Segments.Add(new LineSegment(words, result.Box));
        return result;
    }

    private static PipelineContext RunWithFonts(params (string Text, double Left, string[] Fonts)[] lines) =>
        Run(LayoutFactory.Page(1, lines.Select((l, i) => InFonts(LayoutFactory.Line(l.Text, l.Left, 100 + (20 * i)), l.Fonts))));

    [Fact]
    public void SubBulletO_InAnotherFontFamily_IsABulletItem_NestedUnderABullet_R10()
    {
        PipelineContext context = RunWithFonts(
            ("• Bank oferuje:", Margin, ["Verdana"]),
            ("o rachunek oszczednosciowy", Indent, ["CourierNewPSMT", "Verdana"]),
            ("o rachunek walutowy", Indent, ["ABCDEF+CourierNewPSMT", "ABCDEF+Verdana-Bold"]));

        LayoutLine parent = Find(context, "• Bank");
        LayoutLine first = Find(context, "o rachunek oszczednosciowy");
        LayoutLine second = Find(context, "o rachunek walutowy");
        AssertItem(first, "o", ListLabelKind.Bullet);
        AssertItem(second, "o", ListLabelKind.Bullet);
        Assert.Equal(Id(parent), Parent(first));
        Assert.Equal(Id(parent), Parent(second));
    }

    [Fact]
    public void WordO_InTheFontOfTheText_StaysAnOrdinaryWord_R10()
    {
        PipelineContext context = RunWithFonts(
            ("o rachunek oszczednosciowy", Margin, ["Verdana"]),
            ("o rachunek walutowy", Margin, ["ABCDEF+Verdana", "ABCDEF+Verdana-Bold"]));

        Assert.All(context.Pages.SelectMany(p => p.Lines), l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Fact]
    public void LoneO_IsNotABullet_R10()
    {
        PipelineContext context = RunWithFonts(
            ("• Bank oferuje:", Margin, ["Verdana"]),
            ("o", Indent, ["CourierNewPSMT"]));

        Assert.NotEqual(LineRole.ListItem, Find(context, "o").Role);
    }

    [Fact]
    public void HangingContinuation_BelongsToTheItem_FR053()
    {
        PipelineContext context = Run(Page(
            ("1) pracownik sluzby cywilnej oznacza osobe zatrudniona na podstawie umowy", Margin),
            ("o prace zgodnie z zasadami okreslonymi w ustawie;", Indent),
            ("2) urzednik sluzby cywilnej oznacza osobe mianowana;", Margin)));

        LayoutLine item1 = Find(context, "1) pracownik");
        AssertItem(item1, "1)", ListLabelKind.ArabicParen);
        AssertContinues(Find(context, "o prace"), item1);
        AssertItem(Find(context, "2) urzednik"), "2)", ListLabelKind.ArabicParen);
    }

    [Fact]
    public void ContinuationAlignedToItemText_BelongsToTheItem_FR053()
    {
        PipelineContext context = Run(Page(
            ("2) Zachecamy abys zapoznal sie z innymi dokumentami.", 57.6),
            ("Znajdziesz w nich informacje.", 72.6)));

        AssertContinues(Find(context, "Znajdziesz"), Find(context, "2) Zachecamy"));
    }

    [Fact]
    public void ContinuationOnNextPage_StaysInTheSameItem_FR053()
    {
        LayoutPage page1 = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("Odznake nadaje Prezes Rady Ministrow:", Margin, 700),
            LayoutFactory.Line("1) z wlasnej inicjatywy na podstawie przeslanek", Margin, 720),
        ]);
        LayoutPage page2 = Page(2,
            ("okreslonych w regulaminie,", Indent),
            ("2) na pisemny wniosek.", Margin));

        PipelineContext context = Run(page1, page2);

        LayoutLine item1 = Find(context, "1) z wlasnej");
        AssertContinues(Find(context, "okreslonych"), item1);
        AssertItem(Find(context, "2) na pisemny"), "2)", ListLabelKind.ArabicParen);
    }

    [Fact]
    public void LabelledLines_NestByIndentation_FR052()
    {
        PipelineContext context = Run(Page(
            ("1) wzor:", Margin),
            ("a) wniosku,", Indent),
            ("b) odznaki,", Indent),
            ("2) sposob noszenia odznaki.", Margin)));

        LayoutLine p1 = Find(context, "1) wzor");
        LayoutLine a = Find(context, "a) wniosku");
        LayoutLine b = Find(context, "b) odznaki");
        LayoutLine p2 = Find(context, "2) sposob");
        AssertItem(a, "a)", ListLabelKind.LetterParen);
        Assert.Equal(string.Empty, Parent(p1));
        Assert.Equal(Id(p1), Parent(a));
        Assert.Equal(Id(p1), Parent(b));
        Assert.Equal(string.Empty, Parent(p2));
    }

    [Fact]
    public void LabelsWithinIndentTolerance_AreTheSameLevel()
    {
        PipelineContext context = Run(Page(
            ("1) z wlasnej inicjatywy lub", 72.4),
            ("2) na wniosek Szefa Sluzby Cywilnej.", 71.0)));

        Assert.Equal(string.Empty, Parent(Find(context, "2) na wniosek")));
    }

    [Fact]
    public void ArticleUstep_IsSplitFromTheArticleLineAndContinuesAtTheMargin_FR051()
    {
        PipelineContext context = Run(Page(
            ("Art. 7. 1. Limit mianowan urzednikow w sluzbie cywilnej na dany rok", Indent),
            ("budzetowy okresla ustawa budzetowa.", Margin),
            ("2. Rada Ministrow ustala corocznie trzyletni plan limitu mianowan", Indent),
            ("urzednikow w sluzbie cywilnej.", Margin)));

        LayoutPage page = context.Pages[0];
        Assert.Equal("Art. 7.", page.Lines[0].Text);
        Assert.Equal(LineRole.Unknown, page.Lines[0].Role);

        LayoutLine ust1 = page.Lines[1];
        Assert.StartsWith("1. Limit", ust1.Text, StringComparison.Ordinal);
        AssertItem(ust1, "1.", ListLabelKind.ArabicDot);
        AssertContinues(Find(context, "budzetowy"), ust1);

        LayoutLine ust2 = Find(context, "2. Rada");
        AssertItem(ust2, "2.", ListLabelKind.ArabicDot);
        Assert.Equal(string.Empty, Parent(ust2));
        AssertContinues(Find(context, "urzednikow w sluzbie"), ust2);
    }

    [Fact]
    public void PointsInsideUstep_AreChildrenOfTheUstepDespiteSmallerIndent_FR052()
    {
        PipelineContext context = Run(Page(
            ("4. Dyrektor generalny urzedu:", Indent),
            ("1) zapewnia funkcjonowanie urzedu,", Margin),
            ("2) wykonuje zadania okreslone w ustawie.", Margin),
            ("5. Dyrektor generalny odpowiada przed kierownikiem.", Indent)));

        LayoutLine ust4 = Find(context, "4. Dyrektor");
        Assert.Equal(Id(ust4), Parent(Find(context, "1) zapewnia")));
        Assert.Equal(Id(ust4), Parent(Find(context, "2) wykonuje")));
        Assert.Equal(string.Empty, Parent(Find(context, "5. Dyrektor")));
    }

    [Fact]
    public void ArabicDotSequence_IsAList_FR051()
    {
        PipelineContext context = Run(Page(
            ("1. Co znajdziesz w regulaminie", Margin),
            ("2. Poznaj definicje zwrotow", Margin)));

        AssertItem(Find(context, "1. Co"), "1.", ListLabelKind.ArabicDot);
        AssertItem(Find(context, "2. Poznaj"), "2.", ListLabelKind.ArabicDot);
    }

    [Fact]
    public void LoneArabicDotOrYear_StaysParagraph_FR051()
    {
        PipelineContext context = Run(Page(
            ("2024 r. weszla w zycie nowa taryfa oplat.", Margin),
            ("1. stycznia bank zmienil oprocentowanie rachunku.", Margin),
            ("Pozostale warunki nie ulegaja zmianie.", Margin),
            ("3. kwietnia nastapila kolejna zmiana.", Margin)));

        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Fact]
    public void CommonPartAlignedWithTopLevelLabels_EndsTheList_FR054()
    {
        PipelineContext context = Run(Page(
            ("1) wzor odznaki,", Margin),
            ("2) sposob noszenia odznaki", Margin),
            ("– majac na uwadze koniecznosc ujednolicenia wnioskow oraz", Margin),
            ("potrzebe jednolitego sposobu noszenia odznaki.", Margin)));

        Assert.Equal(LineRole.Unknown, Find(context, "– majac").Role);
        Assert.Equal(LineRole.Unknown, Find(context, "potrzebe").Role);
    }

    [Fact]
    public void CommonPartOfNestedList_BelongsToTheParentItem_FR054()
    {
        PipelineContext context = Run(Page(
            ("3) na pisemny wniosek:", Margin),
            ("a) Szefa Kancelarii,", Indent),
            ("b) wojewody", Indent),
            ("– po zasiegnieciu opinii Szefa Sluzby Cywilnej;", Indent),
            ("4) z wlasnej inicjatywy.", Margin)));

        LayoutLine p3 = Find(context, "3) na pisemny");
        LayoutLine common = Find(context, "– po zasiegnieciu");
        Assert.Equal(LineRole.ListContinuation, common.Role);
        Assert.Equal(Id(p3), Owner(common));
        Assert.True(IsCommonPart(common));
        Assert.Equal(string.Empty, Parent(Find(context, "4) z wlasnej")));
    }

    [Fact]
    public void LineAlignedWithParentText_AfterNestedList_IsCommonPartOfTheParent_FR054()
    {
        PipelineContext context = Run(Page(
            ("2) Zachecamy abys zapoznal sie z dokumentami.", 57.6),
            ("a) zasadach obslugi klientow,", 71.5),
            ("b) wysokosci oprocentowania.", 71.5),
            ("Aktualne regulaminy zamieszczamy na stronie.", 72.1)));

        LayoutLine common = Find(context, "Aktualne");
        Assert.Equal(LineRole.ListContinuation, common.Role);
        Assert.Equal(Id(Find(context, "2) Zachecamy")), Owner(common));
        Assert.True(IsCommonPart(common));
    }

    [Fact]
    public void DashLinesBelowALetter_AreTiretItems_FR052()
    {
        PipelineContext context = Run(Page(
            ("a) w przypadku:", Indent),
            ("– pierwszym,", Deep),
            ("– drugim,", Deep),
            ("b) w pozostalych przypadkach.", Indent)));

        LayoutLine a = Find(context, "a) w przypadku");
        LayoutLine t1 = Find(context, "– pierwszym");
        AssertItem(t1, "–", ListLabelKind.Dash);
        Assert.Equal(Id(a), Parent(t1));
        Assert.Equal(Id(a), Parent(Find(context, "– drugim")));
        Assert.Equal(string.Empty, Parent(Find(context, "b) w pozostalych")));
    }

    [Fact]
    public void HeadingStyledNumberedLines_AreLeftForHeadingDetection()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            Bold(LayoutFactory.Line("1. Wprowadzenie", Margin, 100)),
            LayoutFactory.Line("Tresc pierwszego rozdzialu regulaminu.", Margin, 120),
            Bold(LayoutFactory.Line("2. Definicje", Margin, 160)),
            LayoutFactory.Line("Tresc drugiego rozdzialu regulaminu.", Margin, 180),
        ]);

        PipelineContext context = Run(page);

        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Fact]
    public void IndentedLineAfterLargeGap_IsNotAContinuation()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("1) pierwszy punkt,", Margin, 100),
            LayoutFactory.Line("2) drugi punkt.", Margin, 120),
            LayoutFactory.Line("Nowy akapit z wcieciem pierwszej linii.", Indent, 180),
        ]);

        PipelineContext context = Run(page);

        Assert.Equal(LineRole.Unknown, Find(context, "Nowy akapit").Role);
    }

    [Fact]
    public void LinesOwnedByOtherStages_AreUntouchedAndEndTheList()
    {
        LayoutPage page = Page(
            ("1) pierwszy punkt,", Margin),
            ("Usluga Oplata", Indent),
            ("dalszy tekst po tabeli", Indent));
        page.Lines[1].Role = LineRole.Table;

        PipelineContext context = Run(page);

        Assert.Equal(LineRole.Table, page.Lines[1].Role);
        Assert.Equal(LineRole.Unknown, Find(context, "dalszy tekst").Role);
    }

    [Fact]
    public void Disabled_DetectsNothing()
    {
        PipelineContext context = Run(
            o => o.Lists.Enabled = false,
            Page(("1) pierwszy punkt,", Margin), ("2) drugi punkt.", Margin)));

        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.Unknown, l.Role));
    }

    [Fact]
    public void SideNoteLines_DoNotBreakTheList_FR034()
    {
        LayoutLine note = LayoutFactory.Line("Nowe brzmienie pkt 1", 480, 110);
        note.Role = LineRole.SideNote;
        LayoutPage page = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("1) pierwszy punkt, ktory zawija sie", Margin, 100),
            note,
            LayoutFactory.Line("na druga linie,", Indent, 120),
            LayoutFactory.Line("2) drugi punkt.", Margin, 140),
        ]);

        PipelineContext context = Run(page);

        LayoutLine item1 = Find(context, "1) pierwszy");
        AssertContinues(Find(context, "na druga"), item1);
        Assert.Equal(string.Empty, Parent(Find(context, "2) drugi")));
        Assert.Equal(LineRole.SideNote, page.Lines[1].Role);
    }

    [Fact]
    public void BoldLabelledLineWithinTheListAtNormalLeading_IsAnItem()
    {
        LayoutPage page = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("W sluzbie cywilnej moze byc zatrudniona osoba, ktora:", Margin, 100),
            LayoutFactory.Line("[1) jest obywatelem polskim;]", Margin, 120),
            Bold(LayoutFactory.Line("<1) jest obywatelem Unii Europejskiej;>", Margin, 140)),
            LayoutFactory.Line("2) korzysta z pelni praw publicznych;", Margin, 160),
        ]);

        PipelineContext context = Run(page);

        AssertItem(Find(context, "[1)"), "[1)", ListLabelKind.ArabicParen);
        AssertItem(Find(context, "<1)"), "<1)", ListLabelKind.ArabicParen);
        AssertItem(Find(context, "2) korzysta"), "2)", ListLabelKind.ArabicParen);
    }

    [Fact]
    public void RepealedAndFutureWordingOfOneUstep_KeepTheSequence_FR051()
    {
        PipelineContext context = Run(Page(
            ("1. Dyrektor generalny upowszechnia informacje.", Indent),
            ("[2. Osoba nieposiadajaca obywatelstwa moze zostac zatrudniona.]", Indent),
            ("<2. Osoba nieposiadajaca obywatelstwa moze zostac zatrudniona w sluzbie.", Indent),
            ("<2a. Wymogu nie stosuje sie do osob posiadajacych Karte Polaka.>", Indent),
            ("3. Prezes Rady Ministrow okresli rodzaje dokumentow.", Indent)));

        Assert.All(context.Pages[0].Lines, l => Assert.Equal(LineRole.ListItem, l.Role));
    }

    [Fact]
    public void BoldContinuationAtTheTopOfTheNextPage_ContinuesTheItem()
    {
        LayoutPage page1 = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("1. Dyrektor generalny upowszechnia informacje.", Indent, 680),
            Bold(LayoutFactory.Line("2b. Dyrektor generalny wskazuje stanowiska pracy, o ktore moga", Indent, 700)),
            Bold(LayoutFactory.Line("ubiegac sie wylacznie osoby", Margin, 720)),
        ]);
        LayoutPage page2 = LayoutFactory.Page(2,
        [
            Bold(LayoutFactory.Line("posiadajace obywatelstwo polskie.>", Margin, 100)),
            LayoutFactory.Line("3. Prezes Rady Ministrow okresli rodzaje dokumentow.", Indent, 120),
        ]);

        PipelineContext context = Run(page1, page2);

        LayoutLine item = Find(context, "2b.");
        AssertContinues(Find(context, "posiadajace"), item);
        Assert.Equal(LineRole.ListItem, Find(context, "3. Prezes").Role);
    }

    [Fact]
    public void BoldItemContinuingTheSequenceAtTheTopOfAPage_IsAnItem()
    {
        LayoutPage page1 = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("1) zapewnia ochrone systemu;", Margin, 700),
            LayoutFactory.Line("2) zapewnia integralnosc danych;", Margin, 720),
        ]);
        LayoutPage page2 = LayoutFactory.Page(2,
        [
            Bold(LayoutFactory.Line("3) zapewnia dostepnosc systemu;", Margin, 100)),
            LayoutFactory.Line("4) przeciwdziala uszkodzeniom.", Margin, 120),
        ]);

        PipelineContext context = Run(page1, page2);

        AssertItem(Find(context, "3) zapewnia"), "3)", ListLabelKind.ArabicParen);
        Assert.Equal(string.Empty, Parent(Find(context, "4) przeciw")));
    }

    [Fact]
    public void BoldPassage_PointsAndRunOnUstepsAreItemsNotHeadings()
    {
        // A passage printed in bold: a point introducing letters, and an ustęp whose sentence runs on.
        LayoutPage page = LayoutFactory.Page(1,
        [
            LayoutFactory.Line("1. Dostawca informuje konsumenta o oplatach.", Indent, 100),
            Bold(LayoutFactory.Line("1) zawarciem umowy:", Margin, 130)),
            Bold(LayoutFactory.Line("a) rachunku platniczego,", Indent, 150)),
            Bold(LayoutFactory.Line("2. Dostawca, o ktorym mowa w ust. 1, nie moze domagac sie", Indent, 180)),
            Bold(LayoutFactory.Line("zaspokojenia roszczenia z tytulu zawarcia umowy.", Margin, 200)),
        ]);

        PipelineContext context = Run(page);

        AssertItem(Find(context, "1) zawarciem"), "1)", ListLabelKind.ArabicParen);
        AssertItem(Find(context, "2. Dostawca"), "2.", ListLabelKind.ArabicDot);
        AssertContinues(Find(context, "zaspokojenia"), Find(context, "2. Dostawca"));
    }

    [Fact]
    public void LargerBoldNumberedHeadingWrappedToALowercaseLine_IsLeftForHeadingDetection()
    {
        // A section title of a bank regulation (12 pt bold over 11 pt body) wrapped onto a second line.
        var sketch = new PageSketch();
        sketch.Line("5. Jak mozesz zmienic typ rachunku?", 43, 70, 12, bold: true);
        double y = 100;
        foreach (string line in new[] { "4) Po zmianie typu rachunku otrzymasz od nas potwierdzenie zmiany", "5) Nie zmieniamy innych rachunkow platniczych prowadzonych dla Ciebie." })
        {
            sketch.Line(line, 57, y, 11);
            y += 15.4;
        }

        sketch.Line("6. Jakie limity dotyczace zlecen platniczych mozemy wprowadzic i jak moga", 43, y + 22, 12, bold: true);
        sketch.Line("sie zmienic?", 43, y + 38.7, 12, bold: true);
        sketch.Line("1) Mozemy wprowadzic limity, ktore dotycza:", 58, y + 60.4, 11);
        sketch.Line("a) wysokosci przelewow,", 72, y + 75.8, 11);
        for (int i = 0; i < 6; i++)
        {
            sketch.Line("Tresc zwyklego akapitu regulaminu w rozmiarze tekstu podstawowego dokumentu.", 72, y + 100 + (15.4 * i), 11);
        }

        sketch.Line("7. Jak mozesz skladac dyspozycje dotyczace rachunku?", 43, y + 220, 12, bold: true);

        PipelineContext context = PageSketch.Assemble(null, sketch);
        new ListDetectionStage().Execute(context);

        Assert.Equal(LineRole.Unknown, Find(context, "6. Jakie").Role);
    }
}
